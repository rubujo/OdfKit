"""Project locked ODF 1.1 errata redlines without changing original source IDs.

Only the specifically reviewed editorial styles are deletions. Ordinary strike
formatting is retained. The effective prose stays in ignored artifacts; tracked
evidence contains source, policy, block and deletion-run hashes, not a new claim
of normative adjudication.
"""

import argparse
from collections import Counter
import hashlib
import importlib.util
import json
import csv
from pathlib import Path
import sys
import re
import xml.etree.ElementTree as ET
import zipfile

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parent.parent
TEXT = "{urn:oasis:names:tc:opendocument:xmlns:text:1.0}"
STYLE = "{urn:oasis:names:tc:opendocument:xmlns:style:1.0}"
FO = "{urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0}"
OFFICE = "{urn:oasis:names:tc:opendocument:xmlns:office:1.0}"
ISOLATED_FLOWS = {TEXT + "note-body", "{urn:oasis:names:tc:opendocument:xmlns:drawing:1.0}text-box",
                  "{urn:oasis:names:tc:opendocument:xmlns:table:1.0}table-cell"}
EXCLUDED = {TEXT + name for name in ("table-of-content", "alphabetical-index", "illustration-index", "table-index",
                                    "tracked-changes", "sequence-decls", "user-field-decls", "variable-decls")}


def sha(value):
    return hashlib.sha256(value).hexdigest()


def clean(value):
    return " ".join(value.split())


def style_snapshot(node):
    properties = node.find(STYLE + "text-properties")
    return {"family": node.get(STYLE + "family"), "name": node.get(STYLE + "name"),
            "parent": node.get(STYLE + "parent-style-name", ""),
            "textProperties": dict(properties.attrib) if properties is not None else {}}


def style_hash(snapshot):
    return sha(json.dumps(snapshot, sort_keys=True, ensure_ascii=False).encode("utf-8"))


class Projector:
    def __init__(self, styles, approved):
        self.styles = styles
        self.approved = set(approved)
        self.cache = {}
        for key in self.approved:
            if key not in styles:
                raise ValueError("Reviewed editorial style is missing")

    def resolve(self, key, active=None):
        if key in self.cache:
            return self.cache[key]
        active = set() if active is None else active
        if key in active:
            raise ValueError("Cyclic source style inheritance")
        if key not in self.styles:
            raise ValueError("Missing source style parent")
        active.add(key)
        style = self.styles[key]
        properties, editorial = {}, set()
        if style["parent"]:
            inherited, parent_editorial = self.resolve((key[0], style["parent"]), active)
            properties.update(inherited)
            editorial.update(parent_editorial)
        properties.update(style["textProperties"])
        if key in self.approved:
            editorial.add(key[0] + ":" + key[1])
        active.remove(key)
        self.cache[key] = properties, editorial
        return properties, editorial

    def context(self, node, inherited):
        properties, editorial = inherited
        if node.tag in ISOLATED_FLOWS:
            properties, editorial = {}, set()
        else:
            properties, editorial = dict(properties), set(editorial)
        if node.tag in (TEXT + "p", TEXT + "h", TEXT + "span", TEXT + "a"):
            family = "paragraph" if node.tag in (TEXT + "p", TEXT + "h") else "text"
            name = node.get(TEXT + "style-name")
            if name:
                own, own_editorial = self.resolve((family, name))
                properties.update(own)
                editorial.update(own_editorial)
        return properties, editorial

    def inline_runs(self, node, inherited):
        context = self.context(node, inherited)
        deleted = bool(context[1]) and context[0].get(STYLE + "text-line-through-style", "none") != "none"
        result = []

        def append(text, current=context, removed=deleted):
            if text:
                result.append({"text": text, "deleted": removed, "styles": sorted(current[1]) if removed else []})

        append(node.text)
        for child in node:
            if child.tag in (TEXT + "p", TEXT + "h"):
                pass
            elif child.tag == TEXT + "s":
                append(" " * int(child.get(TEXT + "c", "1")))
            elif child.tag in (TEXT + "tab", TEXT + "line-break"):
                append(" ")
            else:
                result += self.inline_runs(child, context)
            # A child's style never applies to its tail.
            append(child.tail)
        return result

    def blocks(self, body):
        result = {}

        def walk(node, address, inherited):
            if node.tag in EXCLUDED:
                return
            if node.tag in (TEXT + "p", TEXT + "h"):
                runs = self.inline_runs(node, inherited)
                raw = clean("".join(run["text"] for run in runs))
                if raw:
                    effective = clean("".join(run["text"] for run in runs if not run["deleted"]))
                    position, deletions = 0, []
                    for run in runs:
                        end = position + len(run["text"])
                        if run["deleted"]:
                            deletions.append({"rawCharacterStart": position, "rawCharacterEnd": end,
                                "textSha256": sha(run["text"].encode("utf-8")), "editorialStyles": run["styles"]})
                        position = end
                    result[address] = {"rawText": raw, "effectiveText": effective, "rawCharacterLength": position,
                                       "deletionRuns": deletions}
            context = self.context(node, inherited)
            counts = Counter()
            for child in node:
                local = child.tag.split("}")[-1]
                counts[local] += 1
                walk(child, f"{address}/{local}[{counts[local]}]", context)

        walk(body, "/office:document-content/office:body/office:text", ({}, set()))
        return result


def build(source_dir):
    policy_path = ROOT / "docs/odf-effective-text-policy.json"
    policy = json.loads(policy_path.read_text(encoding="utf-8"))
    validate_policy(policy)
    sources = json.loads((ROOT / "docs/odf-obligation-inventory/sources.json").read_text(encoding="utf-8"))["sources"]
    source = next(s for s in sources if s["version"] == "1.1" and s["part"] == "spec")
    path = source_dir / source["file"]
    if policy["sourceSha256"] != source["sha256"] or sha(path.read_bytes()) != source["sha256"]:
        raise ValueError("Effective-text source differs from locked authoritative errata edition")
    with zipfile.ZipFile(path) as package:
        content = ET.fromstring(package.read("content.xml"))
        styles_root = ET.fromstring(package.read("styles.xml"))
    styles = {}
    for root in (styles_root, content):
        for node in root.iter(STYLE + "style"):
            snapshot = style_snapshot(node)
            key = snapshot["family"], snapshot["name"]
            if key in styles:
                raise ValueError("Ambiguous source style definition")
            styles[key] = snapshot
    approved = set()
    for style in policy["editorialDeletionStyles"]:
        key = style["family"], style["name"]
        if key in approved or key not in styles or style_hash(styles[key]) != style["styleSha256"]:
            raise ValueError("Editorial deletion style differs from reviewed source")
        approved.add(key)
    projector = Projector(styles, approved)
    body = content.find(".//" + OFFICE + "text")
    if body is None:
        raise ValueError("Missing authoritative text body")
    projected = projector.blocks(body)
    spec = importlib.util.spec_from_file_location("inventory", ROOT / "eng/Build-OdfObligationInventory.py")
    inventory = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(inventory)
    blocks, _, _ = inventory.odt_blocks(path)
    if len(projected) != len(blocks):
        raise ValueError("Effective projection lost source-addressed blocks")
    changed, prose = [], []
    for index, block in enumerate(blocks, 1):
        projection = projected[block["locator"]]
        if projection["rawText"] != block["text"]:
            raise ValueError("Effective projection changed original source text")
        if not projection["deletionRuns"]:
            continue
        identifier = f"ODF-1.1/spec/B{index:05d}"
        changed.append({"blockId": identifier, "locator": block["locator"],
            "sourceBlockSha256": sha(block["text"].encode("utf-8")),
            "effectiveBlockSha256": sha(projection["effectiveText"].encode("utf-8")),
            "effectiveTextEmpty": not projection["effectiveText"], "rawCharacterLength": projection["rawCharacterLength"],
            "deletionRuns": projection["deletionRuns"]})
        prose.append({"blockId": identifier, **projection})
    metadata = {"schemaVersion": 1, "version": "1.1", "part": "spec", "sourceSha256": source["sha256"],
        "projectorSha256": sha(Path(__file__).read_bytes().replace(b"\r\n", b"\n")),
        "policySha256": sha(policy_path.read_bytes().replace(b"\r\n", b"\n")),
        "sourceBlockCount": len(blocks), "changedBlockCount": len(changed),
        "originalSourceIdsPreserved": True, "fullNormativeAdjudication": False, "fullConformanceClaim": False,
        "opaqueObjectAdjudication": "pending", "blocks": changed}
    return metadata, prose


def validate_policy(policy):
    if policy.get("schemaVersion") != 1 or policy.get("ordinaryStrikethroughPolicy") != "preserve" or (
            policy.get("originalInventoryPolicy") != "preserve-original-identifiers-and-hashes") or (
            policy.get("opaqueObjectPolicy") != "requires-separate-adjudication"):
        raise ValueError("Unreviewed effective-text policy")
    styles = set()
    for style in policy["editorialDeletionStyles"]:
        key = style["family"], style["name"]
        if key in styles or style["family"] not in ("paragraph", "text") or not re.fullmatch(r"[0-9a-f]{64}", style["styleSha256"]):
            raise ValueError("Duplicate or unsupported editorial style policy")
        styles.add(key)
    if not styles:
        raise ValueError("Missing reviewed editorial styles")


def check_contracts(root=ROOT):
    metadata = json.loads((root / "docs/odf-effective-text/odf-1.1-spec.json").read_text(encoding="utf-8"))
    policy_bytes = (root / "docs/odf-effective-text-policy.json").read_bytes().replace(b"\r\n", b"\n")
    policy = json.loads(policy_bytes)
    validate_policy(policy)
    sources = json.loads((root / "docs/odf-obligation-inventory/sources.json").read_text(encoding="utf-8"))["sources"]
    source = next(s for s in sources if s["version"] == "1.1" and s["part"] == "spec")
    if metadata["sourceSha256"] != source["sha256"] or policy["sourceSha256"] != source["sha256"]:
        raise ValueError("Effective-text authoritative source changed")
    if metadata["projectorSha256"] != sha(Path(__file__).read_bytes().replace(b"\r\n", b"\n")) or metadata["policySha256"] != sha(policy_bytes):
        raise ValueError("Effective-text projector or policy changed")
    if metadata["originalSourceIdsPreserved"] is not True or metadata["fullNormativeAdjudication"] is not False or (
            metadata["fullConformanceClaim"] is not False) or metadata["opaqueObjectAdjudication"] != "pending":
        raise ValueError("Projection cannot close original source or object adjudication")
    with (root / "docs/odf-obligation-inventory/odf-1.1.csv").open(encoding="utf-8", newline="") as stream:
        rows = {row["id"].rsplit("/S", 1)[0]: row for row in csv.DictReader(stream) if row["part"] == "spec" and row["sentenceSha256"]}
    if rows[policy["basis"]["blockId"]]["blockSha256"] != policy["basis"]["textSha256"]:
        raise ValueError("Editorial context evidence changed")
    if metadata["sourceBlockCount"] != source["blockCount"] or metadata["changedBlockCount"] != len(metadata["blocks"]):
        raise ValueError("Effective-text block counts changed")
    approved = {style["family"] + ":" + style["name"] for style in policy["editorialDeletionStyles"]}
    seen = set()
    for block in metadata["blocks"]:
        identifier = block["blockId"]
        if identifier in seen or identifier not in rows or block["sourceBlockSha256"] != rows[identifier]["blockSha256"] or block["locator"] != rows[identifier]["locator"]:
            raise ValueError("Original block identity or source text changed")
        seen.add(identifier)
        if not re.fullmatch(r"[0-9a-f]{64}", block["effectiveBlockSha256"]) or (
                block["effectiveTextEmpty"] != (block["effectiveBlockSha256"] == sha(b""))):
            raise ValueError("Invalid effective-text hash or empty state")
        position = 0
        if not block["deletionRuns"]:
            raise ValueError("Affected block has no editorial deletion evidence")
        for run in block["deletionRuns"]:
            if not position <= run["rawCharacterStart"] < run["rawCharacterEnd"] <= block["rawCharacterLength"] or (
                    not run["editorialStyles"] or not set(run["editorialStyles"]) <= approved) or (
                    not re.fullmatch(r"[0-9a-f]{64}", run["textSha256"])):
                raise ValueError("Invalid or unreviewed editorial deletion run")
            position = run["rawCharacterEnd"]
    return metadata


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-dir", type=Path, default=ROOT / "artifacts/odf-conformance-sources")
    parser.add_argument("--verify", action="store_true")
    parser.add_argument("--check-contracts", action="store_true")
    options = parser.parse_args()
    if options.check_contracts:
        metadata = check_contracts()
        print(json.dumps({"changedBlockCount": metadata["changedBlockCount"], "originalSourceIdsPreserved": True}))
        return
    metadata, prose = build(options.source_dir)
    target = ROOT / "docs/odf-effective-text/odf-1.1-spec.json"
    value = json.dumps(metadata, ensure_ascii=False, indent=2) + "\n"
    if options.verify:
        if not target.exists() or target.read_text(encoding="utf-8") != value:
            raise ValueError("Effective text evidence differs from locked source projection")
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(value, encoding="utf-8", newline="")
        (options.source_dir / "effective-1.1-spec.json").write_text(json.dumps(prose, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="")
    print(json.dumps({key: metadata[key] for key in ("sourceBlockCount", "changedBlockCount", "originalSourceIdsPreserved", "fullConformanceClaim")}))


if __name__ == "__main__":
    main()
