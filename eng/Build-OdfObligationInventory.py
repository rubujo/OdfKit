"""Extract source-addressable review units, without declaring them adjudicated.

Keep publication prose in ignored artifacts. Tracked removals, generated indexes,
PDF furniture and opaque objects are accounted for separately. Every unresolved
sentence remains an SDK evidence gap, including declarative text without modals.
"""

import argparse
import csv
import hashlib
import io
import json
import re
import sys
import zipfile
from collections import Counter
from pathlib import Path
import xml.etree.ElementTree as ET

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parent.parent
TEXT = "{urn:oasis:names:tc:opendocument:xmlns:text:1.0}"
OFFICE = "{urn:oasis:names:tc:opendocument:xmlns:office:1.0}"
DRAW = "{urn:oasis:names:tc:opendocument:xmlns:drawing:1.0}"
XLINK = "{http://www.w3.org/1999/xlink}"
MODAL = re.compile(r"\b(?:shall|must|required|should|recommended|may|optional)\b", re.I)
QNAME = re.compile(r"\b[a-z][\w-]*:[a-zA-Z_][\w.-]*\b")


def clean(text):
    return " ".join(text.split())


def sha(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def sentences(text):
    """Conservative boundaries: retain fragments, lists, formulas and table cells.

    Decimal points, QName punctuation, abbreviations and URLs are not boundaries.
    Offsets partition normalized text exactly; splitting cannot silently drop text.
    """
    cuts = [0]
    for match in re.finditer(r'[.!?]["\u201d\u2019)]*\s+(?=[A-Z<\u2022])', text):
        end = match.start() + 1
        prefix = text[:end]
        if re.search(r"(?:\b(?:e\.g|i\.e|etc|vs|Fig|Dr|Mr|Ms|No)|\b[A-Z])\.$", prefix, re.I):
            continue
        cuts.append(match.end())
    cuts.append(len(text))
    result = [(start, end, text[start:end].strip()) for start, end in zip(cuts, cuts[1:])
              if text[start:end].strip()]
    if clean(" ".join(unit[2] for unit in result)) != text:
        raise ValueError("Sentence splitter lost source text")
    return result


def inline_text(node):
    result = [node.text or ""]
    for child in node:
        if child.tag in (TEXT + "p", TEXT + "h"):
            pass  # nested notes and captions have their own addressed blocks
        elif child.tag == TEXT + "s":
            result.append(" " * int(child.get(TEXT + "c", "1")))
        elif child.tag in (TEXT + "tab", TEXT + "line-break"):
            result.append(" ")
        else:
            result.append(inline_text(child))
        result.append(child.tail or "")
    return "".join(result)


def odt_blocks(path):
    with zipfile.ZipFile(path) as package:
        xml = package.read("content.xml")
    root = ET.fromstring(xml)
    body = root.find(".//" + OFFICE + "text")
    if body is None:
        raise ValueError(f"Missing text body: {path}")
    blocks, exclusions, objects = [], [], []
    heading = "publication-frontmatter"
    heading_stack = {}
    started = False

    def walk(node, address):
        nonlocal heading, started
        if node.tag in (TEXT + "table-of-content", TEXT + "alphabetical-index",
                        TEXT + "illustration-index", TEXT + "table-index",
                        TEXT + "tracked-changes", TEXT + "sequence-decls",
                        TEXT + "user-field-decls", TEXT + "variable-decls"):
            exclusions.append({"locator": address, "kind": node.tag.split("}")[-1],
                               "sha256": hashlib.sha256(ET.tostring(node)).hexdigest()})
            return
        if node.tag == TEXT + "h":
            value = clean(inline_text(node))
            heading = value or heading
            level = max(1, int(node.get(TEXT + "outline-level", "1")))
            for depth in list(heading_stack):
                if depth >= level:
                    del heading_stack[depth]
            heading_stack[level] = heading
            started = True
        if node.tag in (TEXT + "p", TEXT + "h"):
            value = clean(inline_text(node))
            if value:
                bookmarks = [child.get(TEXT + "name") for child in node.iter()
                             if child.tag in (TEXT + "bookmark", TEXT + "bookmark-start")]
                blocks.append({"locator": address, "text": value, "heading": heading,
                               "headingPath": [heading_stack[depth] for depth in sorted(heading_stack)],
                               "style": node.get(TEXT + "style-name", ""),
                               "kind": "heading" if node.tag == TEXT + "h" else "paragraph",
                               "frontmatter": not started, "bookmarks": bookmarks})
        if node.tag in (DRAW + "image", DRAW + "object", DRAW + "object-ole"):
            objects.append({"locator": address, "kind": node.tag.split("}")[-1],
                            "headingPath": [heading_stack[depth] for depth in sorted(heading_stack)],
                            "href": node.get(XLINK + "href", ""),
                            "sha256": hashlib.sha256(ET.tostring(node)).hexdigest()})
        counts = Counter()
        for child in node:
            local = child.tag.split("}")[-1]
            counts[local] += 1
            walk(child, f"{address}/{local}[{counts[local]}]")

    walk(body, "/office:document-content/office:body/office:text")
    return blocks, exclusions, objects


def pdf_blocks(path):
    from pypdf import PdfReader

    reader = PdfReader(path)
    blocks, exclusions = [], []
    started = False
    for page_number, page in enumerate(reader.pages, 1):
        text = page.extract_text()
        if not started and re.match(r"\s*1 Introduction\s+1\.1 Introduction", text):
            started = True
        if not started:
            exclusions.append({"locator": f"page={page_number}", "kind": "frontmatter-or-toc",
                               "sha256": sha(text)})
            continue
        # Two specific repeated footer lines; never discard an arbitrary numeric
        # line, which may be a formula, a default value, or a schema line number.
        lines = text.splitlines()
        furniture = [line for line in lines if
                     re.match(r"^OpenDocument-v1\.0-os\.sxw 1 May 2005$", line) or
                     re.match(r"^Copyright .* All Rights Reserved\. Page \d+ of \d+$", line)]
        if furniture:
            exclusions.append({"locator": f"page={page_number}", "kind": "publication-footer",
                               "sha256": sha("\n".join(furniture))})
        value = clean(" ".join(line for line in lines if line not in furniture))
        if value:
            blocks.append({"locator": f"page={page_number}", "text": value,
                           "heading": "PDF physical page; paragraph reconstruction pending",
                           "kind": "pdf-page", "frontmatter": False, "bookmarks": []})
    if len(blocks) < 600:
        raise ValueError(f"Incomplete PDF body: {path}")
    return blocks, exclusions, []


def publications():
    locked = json.loads((ROOT / "docs/odf-conformance-matrix/sources.json").read_text(encoding="utf-8"))
    result = []
    for source in locked["sources"]:
        source = dict(source)
        if source["version"] == "1.1":
            source.update(file="1.1-spec-corrected.odt",
                          uri="https://docs.oasis-open.org/office/v1.1/errata01/os/OpenDocument-v1.1-errata01-os-complete.odt")
        elif source["file"].endswith(".html"):
            source["file"] = source["file"].replace(".html", ".odt")
            source["uri"] = source["uri"].replace(".html", ".odt")
        result.append(source)
    result.extend([
        {"version": "1.0", "part": "errata02", "file": "1.0-errata02.odt",
         "uri": "https://docs.oasis-open.org/office/v1.0/errata/os/OpenDocument-v1.0-errata-02-os.odt"},
        {"version": "1.1", "part": "errata01", "file": "1.1-errata01-authoritative.odt",
         "uri": "https://docs.oasis-open.org/office/v1.1/errata01/os/OpenDocument-v1.1-errata01-os.odt"}])
    return result


def build(source_dir):
    rows, sources, raw = [], [], []
    for source in publications():
        path = source_dir / source["file"]
        parser = pdf_blocks if path.suffix == ".pdf" else odt_blocks
        blocks, exclusions, objects = parser(path)
        key = f"ODF-{source['version']}/{source['part']}"
        for index, block in enumerate(blocks, 1):
            identifier = f"{key}/B{index:05d}"
            raw.append({"id": identifier, **block})
            for position, (start, end, sentence) in enumerate(sentences(block["text"]), 1):
                # Neither a clause audit nor an RNG name match proves this sentence.
                # Even explicit 'may' sentences can define constraints on optional
                # behavior. Only exact manual reviews can change these dispositions.
                status = "publication-context" if block["frontmatter"] else "evidence-gap"
                if block["kind"] == "heading":
                    status = "navigation"
                rows.append({"id": f"{identifier}/S{position:03d}",
                             "version": source["version"], "part": source["part"],
                             "heading": block["heading"], "locator": block["locator"],
                             "source": source["uri"], "blockSha256": sha(block["text"]),
                             "sentenceSha256": sha(sentence), "start": start, "end": end,
                             "modalTerms": ";".join(sorted(set(x.lower() for x in MODAL.findall(sentence)))),
                             "qnames": ";".join(sorted(set(QNAME.findall(sentence)))),
                             "status": status, "reviewState": "unreviewed",
                             "scope": "potential-sdk-obligation" if status == "evidence-gap" else "publication",
                             "decision": "NO-SENTENCE-PROOF" if status == "evidence-gap" else "SOURCE-CONTEXT"})
        for index, obj in enumerate(objects, 1):
            rows.append({"id": f"{key}/O{index:05d}", "version": source["version"],
                         "part": source["part"], "heading": "Opaque publication object",
                         "locator": obj["locator"], "source": source["uri"],
                         "blockSha256": obj["sha256"], "sentenceSha256": "", "start": "", "end": "",
                         "modalTerms": "", "qnames": "", "status": "evidence-gap",
                         "reviewState": "unreviewed-object", "scope": "potential-sdk-obligation",
                         "decision": "OBJECT-SEMANTICS-NOT-EXTRACTED"})
        sources.append({"version": source["version"], "part": source["part"], "file": source["file"],
                        "uri": source["uri"], "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                        "blockCount": len(blocks), "objectCount": len(objects), "exclusions": exclusions})
    return rows, sources, raw


def apply_reviews(rows, review_document):
    by_id = {row["id"]: row for row in rows}
    assigned = set()
    for review in review_document["decisions"]:
        if review["status"] not in ("partial", "validator-gap", "evidence-gap", "security-policy-limit",
                                    "product-profile-limit", "informative", "fulfilled", "out-of-sdk-scope",
                                    "normative-dependency"):
            raise ValueError(f"Unknown disposition: {review['id']}")
        if not review["requirement"] or not review["assessment"] or not review["units"]:
            raise ValueError(f"Incomplete decision: {review['id']}")
        if review["status"] == "fulfilled" and (not review["implementation"] or not review["tests"]):
            raise ValueError(f"Fulfilled decision has no evidence: {review['id']}")
        for unit in review["units"]:
            identifier = unit["id"]
            if identifier in assigned or identifier not in by_id:
                raise ValueError(f"Duplicate or missing reviewed unit: {identifier}")
            row = by_id[identifier]
            if row["sentenceSha256"] != unit["sentenceSha256"]:
                raise ValueError(f"Reviewed source sentence changed: {identifier}")
            assigned.add(identifier)
            row.update(status=review["status"], reviewState="reviewed",
                       scope=review["scope"], decision=review["id"])
    return len(assigned)


def check_contracts(target, reviews, require_complete=False):
    metadata = json.loads((target / "sources.json").read_text(encoding="utf-8"))
    if metadata["fullConformanceClaim"] or metadata["fullNormativeAdjudication"]:
        raise ValueError("Pending inventory must not imply full adjudication or conformance")
    if len(metadata["sources"]) != 16 or len({(x["version"], x["part"]) for x in metadata["sources"]}) != 16:
        raise ValueError("Missing or duplicate publication/errata source")
    sources = {(source["version"], source["part"]): source for source in metadata["sources"]}
    rows, identifiers = [], set()
    for version in ("1.0", "1.1", "1.2", "1.3", "1.4"):
        with (target / f"odf-{version}.csv").open(encoding="utf-8", newline="") as stream:
            for row in csv.DictReader(stream):
                source = sources.get((row["version"], row["part"]))
                if (row["id"] in identifiers or row["version"] != version or source is None or
                        row["source"] != source["uri"] or not re.fullmatch(r"[a-f0-9]{64}", row["blockSha256"])):
                    raise ValueError(f"Invalid source-addressable row: {row['id']}")
                if row["sentenceSha256"] and not re.fullmatch(r"[a-f0-9]{64}", row["sentenceSha256"]):
                    raise ValueError(f"Invalid sentence hash: {row['id']}")
                if row["reviewState"].startswith("unreviewed") and row["status"] not in (
                        "evidence-gap", "navigation", "publication-context"):
                    raise ValueError(f"Unreviewed sentence was waived: {row['id']}")
                identifiers.add(row["id"])
                rows.append(row)
    before = [(row["status"], row["scope"], row["decision"], row["reviewState"]) for row in rows]
    count = apply_reviews(rows, reviews)
    if before != [(row["status"], row["scope"], row["decision"], row["reviewState"]) for row in rows]:
        raise ValueError("CSV dispositions differ from the exact sentence decisions")
    if (len(rows) != metadata["rowCount"] or count != metadata["manualReviewedUnitCount"] or
            len(reviews["decisions"]) != metadata["decisionCount"] or
            dict(Counter(row["status"] for row in rows)) != metadata["statusCounts"] or
            dict(Counter(row["reviewState"] for row in rows)) != metadata["reviewCounts"]):
        raise ValueError("Inventory totals do not match rows")
    for review in reviews["decisions"]:
        for relative in review["implementation"] + review["tests"]:
            path = (ROOT / relative).resolve()
            if not path.is_relative_to(ROOT) or not path.is_file():
                raise ValueError(f"Missing decision evidence: {relative}")
    pending = sum(row["status"] == "evidence-gap" and row["reviewState"].startswith("unreviewed") for row in rows)
    if require_complete and pending:
        raise ValueError(f"Full adjudication incomplete: {pending} unresolved source units")
    print(f"Contracts passed: {len(rows)} candidates, {count} reviewed units, {pending} unresolved units.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-dir", type=Path, default=ROOT / "artifacts/odf-conformance-sources")
    parser.add_argument("--verify", action="store_true")
    parser.add_argument("--check-contracts", action="store_true")
    parser.add_argument("--require-complete", action="store_true")
    options = parser.parse_args()
    review_path = ROOT / "docs/odf-obligation-decisions.json"
    reviews = json.loads(review_path.read_text(encoding="utf-8"))
    target = ROOT / "docs/odf-obligation-inventory"
    if options.check_contracts:
        check_contracts(target, reviews, options.require_complete)
        return
    if options.require_complete:
        parser.error("--require-complete requires --check-contracts")
    rows, sources, raw = build(options.source_dir)
    reviewed_count = apply_reviews(rows, reviews)
    outputs = {}
    for version in ("1.0", "1.1", "1.2", "1.3", "1.4"):
        stream = io.StringIO(newline="")
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]), lineterminator="\n")
        writer.writeheader()
        writer.writerows(row for row in rows if row["version"] == version)
        outputs[target / f"odf-{version}.csv"] = stream.getvalue()
    metadata = {"schemaVersion": 1, "fullConformanceClaim": False,
                "fullNormativeAdjudication": False, "granularity": "source-addressable sentence candidates and opaque objects",
                "sources": sources, "rowCount": len(rows),
                "manualReviewedUnitCount": reviewed_count,
                "decisionCount": len(reviews["decisions"]),
                "statusCounts": dict(sorted(Counter(row["status"] for row in rows).items())),
                "reviewCounts": dict(sorted(Counter(row["reviewState"] for row in rows).items())),
                "limitations": ["Extraction is not atomic obligation adjudication. No modal filter or automatic out-of-scope waiver is applied.",
                                "PDF 1.0 page text does not reconstruct tables, schema panels, paragraph boundaries or diagrams.",
                                "1.0 base text and Errata 02 require an effective-text overlay; old statements cannot be certified separately.",
                                "Informative sections, examples, references and errata instructions still need exact semantic review.",
                                "Opaque object contents and normative referenced standards require separate evidence."]}
    outputs[target / "sources.json"] = json.dumps(metadata, ensure_ascii=False, indent=2) + "\n"
    for path, value in outputs.items():
        if options.verify:
            if not path.exists() or path.read_text(encoding="utf-8") != value:
                raise ValueError(f"Inventory differs: {path}")
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(value, encoding="utf-8", newline="")
    if not options.verify:
        raw_path = options.source_dir / "sentence-review-text.json"
        raw_path.write_text(json.dumps(raw, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"{'Verified' if options.verify else 'Built'} {len(rows)} candidates; full adjudication remains false.")
    print(json.dumps(metadata["reviewCounts"]))


if __name__ == "__main__":
    main()
