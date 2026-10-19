"""Locate empty cached ODT reference fields by their actual named anchors.

An empty display is not proof that the underlying target is missing. This source
projection preserves peer fields and ambiguity without calculating chapter
numbers, applying errata, or asserting that the referenced obligations are met.
"""

import argparse
import collections
import hashlib
import importlib.util
import json
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parent.parent
TARGET = Path("docs/odf-empty-reference-evidence.json")
TEXT = "{urn:oasis:names:tc:opendocument:xmlns:text:1.0}"
OFFICE = "{urn:oasis:names:tc:opendocument:xmlns:office:1.0}"
REFS = {TEXT + "reference-ref": "reference", TEXT + "bookmark-ref": "bookmark", TEXT + "sequence-ref": "sequence"}
ANCHORS = {TEXT + "reference-mark": "reference", TEXT + "reference-mark-start": "reference",
           TEXT + "bookmark": "bookmark", TEXT + "bookmark-start": "bookmark", TEXT + "sequence": "sequence"}


def sha(value):
    return hashlib.sha256(value).hexdigest()


def file_hash(path):
    return sha(path.read_bytes().replace(b"\r\n", b"\n"))


def inventory_module(root=ROOT):
    spec = importlib.util.spec_from_file_location("reference_inventory", root / "eng/Build-OdfObligationInventory.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def resolution(reference, targets):
    if not targets:
        return "target-not-found"
    if len(targets) != 1:
        return "ambiguous-target"
    if reference["editorialReviewRequired"] or targets[0]["editorialReviewRequired"]:
        return "editorial-review-required"
    return "unique-target-addressed" if targets[0]["sourceBlockId"] else "target-outside-inventory-block"


def project(body, blocks, source_key, changed=None):
    changed = set() if changed is None else changed
    owners = {block["locator"]: {"sourceBlockId": f"{source_key}/B{index:05d}",
              "sourceBlockSha256": sha(block["text"].encode("utf-8")), "sourceBlockLocator": block["locator"],
              "sourceBlockKind": block["kind"]} for index, block in enumerate(blocks, 1)}
    references, anchors = [], collections.defaultdict(list)

    def walk(node, locator, inherited_owner=None):
        owner = owners.get(locator, inherited_owner)
        common = {"locator": locator, "sourceXmlSha256": sha(ET.tostring(node)),
                  "sourceBlockId": owner["sourceBlockId"] if owner else None,
                  "sourceBlockSha256": owner["sourceBlockSha256"] if owner else None,
                  "sourceBlockLocator": owner["sourceBlockLocator"] if owner else None,
                  "sourceBlockKind": owner["sourceBlockKind"] if owner else None,
                  "editorialReviewRequired": bool(owner and owner["sourceBlockId"] in changed)} if node.tag in REFS or node.tag in ANCHORS else None
        if node.tag in ANCHORS:
            family = ANCHORS[node.tag]
            name = node.get(TEXT + ("ref-name" if family == "sequence" else "name"))
            anchors[family, name].append({**common, "family": family, "name": name})
        if node.tag in REFS:
            cached = "".join(node.itertext())
            references.append({**common, "referenceId": f"{source_key}/R{len(references) + 1:05d}",
                               "family": REFS[node.tag], "name": node.get(TEXT + "ref-name"),
                               "format": node.get(TEXT + "reference-format"), "cachedText": cached,
                               "cachedTextEmpty": not cached.strip()})
        counts = collections.Counter()
        for child in node:
            local = child.tag.rsplit("}", 1)[-1]
            counts[local] += 1
            walk(child, f"{locator}/{local}[{counts[local]}]", owner)

    walk(body, "/office:document-content/office:body/office:text")
    affected = {reference["sourceBlockId"] for reference in references if reference["cachedTextEmpty"] and reference["sourceBlockId"]}
    selected = []
    for reference in references:
        if reference["cachedTextEmpty"] or reference["sourceBlockId"] in affected:
            targets = anchors.get((reference["family"], reference["name"]), []) if reference["name"] else []
            selected.append({**reference, "targets": targets, "resolution": resolution(reference, targets)})
    return len(references), selected


def build(source_dir, root=ROOT):
    inventory = inventory_module(root)
    sources = json.loads((root / "docs/odf-obligation-inventory/sources.json").read_text(encoding="utf-8"))["sources"]
    changed = {block["blockId"] for block in json.loads(
        (root / "docs/odf-effective-text/odf-1.1-spec.json").read_text(encoding="utf-8"))["blocks"]}
    locked, selected, count = [], [], 0
    for source in sources:
        if not source["file"].endswith(".odt"):
            continue
        source_path = source_dir / source["file"]
        if sha(source_path.read_bytes()) != source["sha256"]:
            raise ValueError("Published reference source changed")
        with zipfile.ZipFile(source_path) as archive:
            document = ET.fromstring(archive.read("content.xml"))
        body = document.find(".//" + OFFICE + "text")
        if body is None:
            raise ValueError("Missing publication text body")
        blocks, _, _ = inventory.odt_blocks(source_path)
        total, records = project(body, blocks, f"ODF-{source['version']}/{source['part']}", changed)
        count += total
        selected += records
        locked.append({key: source[key] for key in ("version", "part", "file", "sha256")})
    return {"schemaVersion": 1, "fullConformanceClaim": False, "semanticClassificationComplete": False,
            "renderedChapterNumbers": "not-computed", "nonemptyReferenceConsistency": "pending",
            "builderSha256": file_hash(root / "eng/Build-OdfEmptyReferences.py"),
            "inventoryBuilderSha256": file_hash(root / "eng/Build-OdfObligationInventory.py"),
            "effectiveTextEvidenceSha256": file_hash(root / "docs/odf-effective-text/odf-1.1-spec.json"),
            "sources": locked, "referenceFieldCount": count,
            "emptyReferenceFieldCount": sum(record["cachedTextEmpty"] for record in selected), "references": selected}


def validate(document, source_blocks):
    if (document.get("schemaVersion") != 1 or document.get("fullConformanceClaim") is not False
            or document.get("semanticClassificationComplete") is not False
            or document.get("renderedChapterNumbers") != "not-computed"
            or document.get("nonemptyReferenceConsistency") != "pending"):
        raise ValueError("Reference projection cannot establish completed semantics")
    seen = set()
    for record in document["references"]:
        if record["referenceId"] in seen or record["cachedTextEmpty"] != (not record["cachedText"].strip()):
            raise ValueError("Duplicate reference or changed cached display")
        seen.add(record["referenceId"])
        for node in [record] + record["targets"]:
            block = node["sourceBlockId"]
            if block:
                expected = source_blocks.get(block)
                if (expected is None or node["sourceBlockSha256"] != expected["blockSha256"]
                        or node["sourceBlockLocator"] != expected["locator"]
                        or not node["locator"].startswith(expected["locator"] + "/")):
                    raise ValueError("Reference or target escaped its source block")
            if node["family"] != record["family"] or node["name"] != record["name"]:
                raise ValueError("Reference resolved to a different named-anchor family")
        if record["resolution"] != resolution(record, record["targets"]):
            raise ValueError("Reference ambiguity or editorial status was hidden")
    if document["emptyReferenceFieldCount"] != sum(record["cachedTextEmpty"] for record in document["references"]):
        raise ValueError("Empty reference inventory changed")
    return document["references"]


def check_contracts(root=ROOT):
    import csv
    document = json.loads((root / TARGET).read_text(encoding="utf-8"))
    for field, relative in (("builderSha256", "eng/Build-OdfEmptyReferences.py"),
                            ("inventoryBuilderSha256", "eng/Build-OdfObligationInventory.py"),
                            ("effectiveTextEvidenceSha256", "docs/odf-effective-text/odf-1.1-spec.json")):
        if document[field] != file_hash(root / relative):
            raise ValueError("Reference extraction or effective source evidence changed")
    sources = json.loads((root / "docs/odf-obligation-inventory/sources.json").read_text(encoding="utf-8"))["sources"]
    expected_sources = [{key: source[key] for key in ("version", "part", "file", "sha256")} for source in sources if source["file"].endswith(".odt")]
    if document["sources"] != expected_sources:
        raise ValueError("Reference publication locks changed")
    source_blocks = {}
    for version in ("1.0", "1.1", "1.2", "1.3", "1.4"):
        with (root / f"docs/odf-obligation-inventory/odf-{version}.csv").open(encoding="utf-8", newline="") as stream:
            for row in csv.DictReader(stream):
                if "/S" in row["id"]:
                    source_blocks[row["id"].rsplit("/S", 1)[0]] = row
    return validate(document, source_blocks)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-dir", type=Path, default=ROOT / "artifacts/odf-conformance-sources")
    parser.add_argument("--verify", action="store_true")
    parser.add_argument("--check-contracts", action="store_true")
    options = parser.parse_args()
    if options.check_contracts:
        records = check_contracts()
        print(json.dumps({"emptyReferenceFields": sum(record["cachedTextEmpty"] for record in records), "fullConformanceClaim": False}))
        return
    document = build(options.source_dir)
    serialized = json.dumps(document, ensure_ascii=False, indent=2) + "\n"
    output_path = ROOT / TARGET
    if options.verify:
        if output_path.read_text(encoding="utf-8") != serialized:
            raise ValueError("Empty reference evidence differs from fixed publications")
    else:
        output_path.write_text(serialized, encoding="utf-8", newline="")
    print(json.dumps({"referenceFields": document["referenceFieldCount"],
                      "emptyReferenceFields": document["emptyReferenceFieldCount"], "fullConformanceClaim": False}))


if __name__ == "__main__":
    main()
