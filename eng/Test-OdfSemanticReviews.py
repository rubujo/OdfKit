"""Check explicit group semantic reviews without treating them as conformance proof."""

import argparse
import csv
import hashlib
import importlib.util
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.dont_write_bytecode = True
KINDS = {"source-heading", "definition", "permission", "requirement", "recommendation",
         "conditional-permission-and-requirement", "historical-context", "informative-context"}
SCOPES = {"validator", "consumer", "producer", "source-context", "outside-sdk"}


def object_review_hash(records, units):
    return hashlib.sha256(json.dumps([records[unit] for unit in sorted(units)], ensure_ascii=False,
        sort_keys=True, separators=(",", ":")).encode("utf-8")).hexdigest()


def validate_object_reviews(document, records):
    reviewed = set()
    for review in document["reviews"]:
        units = {unit for rule in review["rules"] for unit in rule["units"] if "/O" in unit}
        if not units:
            continue
        if not units <= records.keys():
            raise ValueError("Reviewed opaque object lacks reconstructed source evidence")
        if review.get("objectEvidenceSha256") != object_review_hash(records, units):
            raise ValueError("Reviewed object payload, archive, or rendition evidence changed")
        reviewed.update(units)
    return len(reviewed)


def validate_pdf_grammar_reviews(document, evidence, fingerprint):
    pages = {page["page"]: page for page in evidence["pages"]}
    reviewed = set()
    for review in document["reviews"]:
        selected = review.get("pdfGrammarPages", [])
        if not selected and "sourcePdfGrammarEvidenceSha256" not in review:
            continue
        if (not selected or any(type(page) is not int or page not in pages for page in selected)
                or len(selected) != len(set(selected)) or review.get("sourcePdfGrammarEvidenceSha256") != fingerprint):
            raise ValueError("Missing or changed reviewed PDF grammar evidence")
        blocks = {unit.rsplit("/S", 1)[0] for rule in review["rules"] for unit in rule["units"]}
        if blocks != {pages[page]["sourceBlockId"] for page in selected}:
            raise ValueError("PDF grammar review covers different source pages")
        reviewed.update(selected)
    return len(reviewed)


def validate_effective_reviews(document, records):
    reviewed = set()
    for review in document["reviews"]:
        blocks = {unit.rsplit("/S", 1)[0] for rule in review["rules"] for unit in rule["units"] if "/S" in unit}
        affected = blocks & records.keys()
        expected = object_review_hash(records, affected) if affected else None
        if review.get("effectiveTextEvidenceSha256") != expected:
            raise ValueError("Reviewed editorial deletion or effective source text changed")
        reviewed.update(affected)
    return len(reviewed)


def validate_inline_reviews(document, records):
    reviewed = set()
    for review in document["reviews"]:
        blocks = {unit.rsplit("/S", 1)[0] for rule in review["rules"] for unit in rule["units"] if "/S" in unit}
        affected = blocks & records.keys()
        if affected:
            if review.get("sourceInlineEvidenceSha256") != object_review_hash(records, affected):
                raise ValueError("Reviewed inline typography or superscript source evidence changed")
            reviewed.update(affected)
    return len(reviewed)


def validate_table_reviews(document, source_rows):
    reviewed = set()
    for review in document["reviews"]:
        for rule in review["rules"]:
            if "tableCells" not in rule:
                continue
            cells, row_indices, seen = rule["tableCells"], set(), set()
            tables = {}
            for cell in cells:
                unit = cell["unitId"]
                row = source_rows.get(unit)
                if (unit in seen or row is None or cell["locator"] != row["locator"]
                        or cell["sourceUnitSha256"] != row["sentenceSha256"]):
                    raise ValueError("Reviewed table cell location or source changed")
                geometry = re.fullmatch(r"(.+/table\[\d+\](?:/table-header-rows\[\d+\])?)/table-row\[(\d+)\]/table-cell\[(\d+)\]/p\[\d+\]", cell["locator"])
                if geometry is None:
                    raise ValueError("Unknown reviewed table geometry")
                version = unit.split("/")[0]
                if version in tables and tables[version] != geometry[1]:
                    raise ValueError("One semantic row cannot span different source tables")
                tables[version] = geometry[1]
                row_indices.add(("/table-header-rows[" in geometry[1], geometry[2]))
                seen.add(unit)
            if not cells or seen != set(rule["units"]) or len(row_indices) != 1:
                raise ValueError("Incomplete or mixed semantic table row")
            if reviewed & seen:
                raise ValueError("Duplicate reviewed table cell")
            reviewed.update(seen)
    return len(reviewed)


def validate_scope_evidence(document, root=ROOT):
    for review in document["reviews"]:
        for rule in review["rules"]:
            if rule["exemption"] != "outside-sdk":
                continue
            expected = {}
            for reference in rule["scopeEvidence"]:
                path = (root / reference).resolve()
                if not path.is_relative_to(root.resolve()) or not path.is_file():
                    raise ValueError("Missing or unsafe SDK scope evidence")
                expected[reference] = hashlib.sha256(path.read_bytes().replace(b"\r\n", b"\n")).hexdigest()
            if not expected or rule.get("scopeEvidenceSha256") != expected:
                raise ValueError("SDK scope evidence changed after exemption review")


def validate_source_archives(document, sources):
    locks = {f"ODF-{source['version']}/{source['part']}": source["sha256"] for source in sources}
    for review in document["reviews"]:
        keys = {"/".join(unit.split("/")[:2]) for rule in review["rules"] for unit in rule["units"]}
        if not keys <= locks.keys() or review.get("sourceArchiveSha256") != {key: locks[key] for key in sorted(keys)}:
            raise ValueError("Reviewed source archive changed, including styles and hidden fields")


def validate_source_context_markers(document, rows, sources, root=ROOT):
    locks = {f"ODF-{source['version']}/{source['part']}": source["sha256"] for source in sources}
    blocks = {unit.rsplit("/S", 1)[0]: row for unit, row in rows.items() if "/S" in unit}
    checked = set()
    for review in document["reviews"]:
        reference = review.get("sourceContextEvidence")
        if reference is None or reference in checked:
            continue
        path = (root / reference).resolve()
        if not path.is_relative_to(root.resolve()) or not path.is_file():
            raise ValueError("Missing or unsafe source context evidence")
        if review.get("sourceContextEvidenceSha256") != hashlib.sha256(path.read_bytes().replace(b"\r\n", b"\n")).hexdigest():
            raise ValueError("Source context evidence changed after review")
        regions = json.loads(path.read_text(encoding="utf-8")).get("informativeContexts", [])
        if not regions:
            raise ValueError("Missing explicit informative source bounds")
        for region in regions:
            first, after = region["first"]["id"], region["afterLast"]["id"]
            publication = first.rsplit("/", 1)[0]
            if publication != after.rsplit("/", 1)[0] or not first < after:
                raise ValueError("Source context bounds cross publications or are reversed")
            label = region["first"].get("label")
            if label is not None and (not isinstance(label, str) or
                    re.fullmatch(r"Note(?: [1-9][0-9]*)?:", label) is None or
                    region["first"].get("kind") != "paragraph"):
                raise ValueError("Unknown source context label or non-paragraph Note")
            if label is not None and "title" in region["first"] and not region["first"]["title"].startswith(label):
                raise ValueError("Note label does not match source context title")
            if label is not None and (not re.fullmatch(r".+/B[0-9]+", first) or
                    after != publication + f"/B{int(first.rsplit('/B', 1)[1]) + 1:05d}"):
                raise ValueError("A Note context must end at the next source block")
            archive = region.get("sourceArchiveSha256", review.get("sourceArchiveSha256", {}).get(publication))
            if archive != locks.get(publication) or publication not in locks:
                raise ValueError("Source context archive is not the locked publication")
            for key in ("first", "afterLast"):
                marker = region[key]
                row = blocks.get(marker["id"])
                if row is None or marker.get("textSha256") != row["blockSha256"] or (
                        "title" in marker and hashlib.sha256(marker["title"].encode("utf-8")).hexdigest() != row["blockSha256"]):
                    raise ValueError("Source context marker text differs from the inventory")
                kind = marker.get("kind", marker.get("sourceNodeKind"))
                source_kind = "heading" if re.search(r"/h\[[0-9]+\]$", row["locator"]) else "paragraph"
                if (kind is not None and kind != source_kind) or ("locator" in marker and marker["locator"] != row["locator"]):
                    raise ValueError("Source context marker kind or location differs from the inventory")
        checked.add(reference)
    return len(checked)


def reference_blocks(records):
    grouped = {}
    for record in records:
        if record["sourceBlockId"]:
            grouped.setdefault(record["sourceBlockId"], []).append(record)
    return {block: {"sourceBlockId": block, "references": references} for block, references in grouped.items()}


def validate_reference_reviews(document, records):
    blocks = reference_blocks(records)
    reviewed = set()
    for review in document["reviews"]:
        affected = {unit.rsplit("/S", 1)[0] for rule in review["rules"] for unit in rule["units"] if "/S" in unit} & blocks.keys()
        if affected:
            if review.get("sourceReferenceEvidenceSha256") != object_review_hash(blocks, affected):
                raise ValueError("Reviewed hidden reference targets or peer fields changed")
            reviewed.update(affected)
    return len(reviewed)


def validate(document, groups, members, root=ROOT):
    if document.get("schemaVersion") != 1 or document.get("fullConformanceClaim") is not False:
        raise ValueError("Semantic review must not assert full conformance")
    seen = set()
    for review in document["reviews"]:
        identifier = review["groupId"]
        if identifier in seen or identifier not in groups:
            raise ValueError("Duplicate or unknown review group")
        seen.add(identifier)
        group = groups[identifier]
        if review["memberSha256"] != group["memberSha256"]:
            raise ValueError("Reviewed group source membership changed")
        if review["versions"] != group["versions"] or not review["versionAssessment"].strip():
            raise ValueError("Missing version assessment")
        if review["implementationAdjudication"] != "pending":
            raise ValueError("Semantic classification cannot close implementation adjudication")
        source_regions = None
        if "sourceContextEvidence" in review:
            reference = (root / review["sourceContextEvidence"]).resolve()
            if not reference.is_relative_to(root.resolve()) or not reference.is_file():
                raise ValueError("Missing or unsafe source context evidence")
            if review.get("sourceContextEvidenceSha256") != hashlib.sha256(reference.read_bytes().replace(b"\r\n", b"\n")).hexdigest():
                raise ValueError("Source context evidence changed after review")
            source_regions = json.loads(reference.read_text(encoding="utf-8")).get("informativeContexts", [])
            if not source_regions:
                raise ValueError("Missing explicit informative source bounds")
        covered = set()
        for rule in review["rules"]:
            if rule["kind"] not in KINDS or not rule["sdkResponsibilities"] or not set(rule["sdkResponsibilities"]) <= SCOPES:
                raise ValueError("Unknown semantic role or SDK responsibility")
            if not rule["assessment"].strip() or not rule["conditions"].strip():
                raise ValueError("Missing semantic assessment or applicability conditions")
            if rule["exemption"] not in ("none", "non-obligation", "outside-sdk"):
                raise ValueError("Unknown exemption")
            if "source-context" in rule["sdkResponsibilities"] and (rule["kind"] not in ("source-heading", "historical-context", "informative-context")
                    or rule["exemption"] != "non-obligation"):
                raise ValueError("Normative semantics cannot be hidden in source context")
            if rule["exemption"] == "non-obligation" and (rule["kind"] not in ("source-heading", "historical-context", "informative-context")
                    or rule["sdkResponsibilities"] != ["source-context"]):
                raise ValueError("Normative semantics cannot be waived as source context")
            if rule["exemption"] == "outside-sdk" and (rule["sdkResponsibilities"] != ["outside-sdk"]
                    or not rule.get("scopeEvidence")):
                raise ValueError("SDK exclusion requires explicit scope evidence")
            if "outside-sdk" in rule["sdkResponsibilities"] and rule["exemption"] != "outside-sdk":
                raise ValueError("SDK exclusion must carry an exemption assessment")
            for reference in rule.get("scopeEvidence", []):
                path = (root / reference).resolve()
                if not path.is_relative_to(root.resolve()) or not path.is_file():
                    raise ValueError("Missing or unsafe scope evidence")
            if not rule["units"]:
                raise ValueError("Empty semantic rule")
            for unit in rule["units"]:
                if source_regions is not None and rule["kind"] == "informative-context":
                    block = unit.rsplit("/S", 1)[0]
                    if not any(region["first"]["id"].rsplit("/", 1)[0] == block.rsplit("/", 1)[0]
                               and (unit in region.get("objectUnitIds", []) if "/O" in unit else
                                    region["first"]["id"] <= block < region["afterLast"]["id"]) for region in source_regions):
                        raise ValueError("Informative waiver lies outside its source appendix")
                if unit in covered or unit not in members[identifier]:
                    raise ValueError("Duplicate or foreign semantic member")
                covered.add(unit)
        if covered != members[identifier]:
            raise ValueError("Semantic review does not cover the whole group")
    total = sum(group["requiresFollowUp"] for group in groups.values())
    reviewed_pending = sum(groups[identifier]["requiresFollowUp"] for identifier in seen)
    return {"followUpGroups": total, "semanticReviewedGroups": len(seen),
            "semanticPendingGroups": total - reviewed_pending, "implementationPendingGroups": total,
            "semanticClassificationComplete": reviewed_pending == total, "fullConformanceClaim": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--require-complete", action="store_true")
    options = parser.parse_args()
    groups = {group["id"]: group for group in json.loads(
        (ROOT / "docs/odf-obligation-classification/groups.json").read_text(encoding="utf-8"))["groups"]}
    members = {identifier: set() for identifier in groups}
    for version in ("1.0", "1.1", "1.2", "1.3", "1.4"):
        with (ROOT / f"docs/odf-obligation-classification/odf-{version}.csv").open(encoding="utf-8", newline="") as stream:
            for row in csv.DictReader(stream):
                members[row["groupId"]].add(row["id"])
    document = json.loads((ROOT / "docs/odf-semantic-reviews.json").read_text(encoding="utf-8"))
    result = validate(document, groups, members)
    validate_source_archives(document, json.loads(
        (ROOT / "docs/odf-obligation-inventory/sources.json").read_text(encoding="utf-8"))["sources"])
    validate_scope_evidence(document)
    batch_spec = importlib.util.spec_from_file_location("semantic_batches", ROOT / "eng/Build-OdfSemanticBatches.py")
    batches = importlib.util.module_from_spec(batch_spec)
    batch_spec.loader.exec_module(batches)
    batch_ids = batches.check_contracts(ROOT)
    object_spec = importlib.util.spec_from_file_location("publication_objects", ROOT / "eng/Build-OdfPublicationObjects.py")
    objects = importlib.util.module_from_spec(object_spec)
    object_spec.loader.exec_module(objects)
    object_count = validate_object_reviews(document, objects.check_contracts(ROOT))
    effective_records = {record["blockId"]: record for record in json.loads(
        (ROOT / "docs/odf-effective-text/odf-1.1-spec.json").read_text(encoding="utf-8"))["blocks"]}
    effective_count = validate_effective_reviews(document, effective_records)
    pdf_spec = importlib.util.spec_from_file_location("pdf_grammar", ROOT / "eng/Build-OdfPdfGrammarEvidence.py")
    pdf = importlib.util.module_from_spec(pdf_spec)
    pdf_spec.loader.exec_module(pdf)
    pdf_count = validate_pdf_grammar_reviews(document, pdf.check_contracts(ROOT), hashlib.sha256(
        (ROOT / "docs/odf-pdf-grammar-evidence.json").read_bytes().replace(b"\r\n", b"\n")).hexdigest())
    source_rows = objects.source_rows(ROOT)
    table_count = validate_table_reviews(document, source_rows)
    validate_source_context_markers(document, source_rows, json.loads(
        (ROOT / "docs/odf-obligation-inventory/sources.json").read_text(encoding="utf-8"))["sources"])
    formula_spec = importlib.util.spec_from_file_location("formula_roles", ROOT / "eng/Build-OdfFormulaSemanticBatches.py")
    formula = importlib.util.module_from_spec(formula_spec)
    formula_spec.loader.exec_module(formula)
    formula_ids = formula.check_contracts(ROOT)
    inline_records = {record["blockId"]: record for record in json.loads(
        (ROOT / formula.TARGET).read_text(encoding="utf-8"))["inlineEvidence"]}
    inline_count = validate_inline_reviews(document, inline_records)
    references_spec = importlib.util.spec_from_file_location("empty_refs", ROOT / "eng/Build-OdfEmptyReferences.py")
    references = importlib.util.module_from_spec(references_spec)
    references_spec.loader.exec_module(references)
    reference_count = validate_reference_reviews(document, references.check_contracts(ROOT))
    manual_ids = {review["groupId"] for review in document["reviews"]}
    if batch_ids & manual_ids:
        raise ValueError("Manual and family reviews must not double-count a group")
    reviewed = manual_ids | batch_ids
    reviewed_pending = sum(groups[identifier]["requiresFollowUp"] for identifier in reviewed)
    result.update(manualReviewedGroups=len(manual_ids), familyReviewedGroups=len(batch_ids),
                  semanticReviewedObjects=object_count,
                  semanticReviewedEffectiveBlocks=effective_count,
                  semanticReviewedPdfGrammarPages=pdf_count,
                  semanticReviewedTableUnits=table_count,
                  semanticReviewedInlineBlocks=inline_count,
                  semanticReviewedReferenceBlocks=reference_count,
                  formulaRoleClassifiedGroups=len(formula_ids),
                  reviewedFollowUpGroups=reviewed_pending,
                  familyReviewedFollowUpGroups=sum(groups[identifier]["requiresFollowUp"] for identifier in batch_ids),
                  semanticReviewedGroups=len(reviewed), semanticPendingGroups=result["followUpGroups"] - reviewed_pending,
                  semanticClassificationComplete=reviewed_pending == result["followUpGroups"])
    implementation_spec = importlib.util.spec_from_file_location("implementation_reviews", ROOT / "eng/Test-OdfImplementationReviews.py")
    implementation = importlib.util.module_from_spec(implementation_spec)
    implementation_spec.loader.exec_module(implementation)
    result.update(implementation.validate(json.loads((ROOT / implementation.TARGET).read_text(encoding="utf-8")),
                                         groups, members, implementation.load_family_sources(document, ROOT), ROOT))
    print(json.dumps(result))
    if options.require_complete and not result["semanticClassificationComplete"]:
        raise SystemExit("Semantic classification remains incomplete")


if __name__ == "__main__":
    main()
