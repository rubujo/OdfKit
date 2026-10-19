"""Reconstruct the published ODF 1.0 PDF grammar without closing prose or errata review.

Full rebuilds require pypdf and pdfplumber. Offline contracts use only the standard
library. Gray shading alone is insufficient: expression grammars have the same
appearance but no normative RELAX NG line numbers.
"""

import argparse
import collections
import csv
import hashlib
import io
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parent.parent
POLICY = ROOT / "docs/odf-pdf-grammar-policy.json"
TARGET = ROOT / "docs/odf-pdf-grammar-evidence.json"
RNG = "{http://relaxng.org/ns/structure/1.0}"


def sha(value):
    return hashlib.sha256(value).hexdigest()


def text_file_hash(path):
    return sha(path.read_bytes().replace(b"\r\n", b"\n"))


def canonical_model(text):
    declarations, pending, index = [], [], 0
    for event, item in ET.iterparse(io.StringIO(text), events=("start-ns", "start")):
        if event == "start-ns":
            pending.append(item)
        else:
            if pending:
                declarations.append((index, sorted(pending)))
                pending = []
            index += 1
    root = ET.fromstring(text)
    if root.tag != RNG + "grammar":
        raise ValueError("Published document grammar has no RELAX NG root")
    nodes = []
    for node in root.iter():
        value = node.text or ""
        if node.tag not in {RNG + "param", RNG + "value", RNG + "name"} and not value.strip():
            value = ""
        if node.tail and node.tail.strip():
            raise ValueError("Non-XML expression grammar or prose entered the RELAX NG projection")
        nodes.append([node.tag, sorted(node.attrib.items()), value, len(node)])
    return nodes, declarations


def model_hash(model):
    return sha(json.dumps(model, ensure_ascii=False, separators=(",", ":")).encode("utf-8"))


def compare_models(projected, reference, approved):
    left, left_ns = canonical_model(projected)
    right, right_ns = canonical_model(reference)
    if len(left) != len(right) or left_ns != right_ns:
        raise ValueError("PDF grammar structure or scoped namespace declarations differ")
    permitted = {record["node"]: record for record in approved}
    used = set()
    for index, (actual, expected) in enumerate(zip(left, right)):
        if actual == expected:
            continue
        record = permitted.get(index)
        if record is None or actual[0] != RNG + "param" or actual[1] != [("name", "pattern")] or (
                actual[:2] != expected[:2]) or actual[3] != expected[3] or (
                actual[2].replace("\n", "") != expected[2]) or (
                sha(actual[2].encode()) != record["pdfTextSha256"]) or (
                sha(expected[2].encode()) != record["rngTextSha256"]):
            raise ValueError(f"Unreviewed PDF grammar difference at node {index}")
        actual[2] = actual[2].replace("\n", "")
        used.add(index)
    if used != set(permitted) or left != right:
        raise ValueError("Reviewed PDF pattern wrap inventory changed")
    return len(right), model_hash((right, right_ns))


def numbered_panels(rectangles, spans, policy):
    panels = []
    for rectangle in sorted(rectangles, key=lambda item: item["y0"]):
        if abs(rectangle["x0"] - policy["left"]) > .1 or abs(rectangle["x1"] - policy["right"]) > .2:
            continue
        if panels and rectangle["y0"] <= panels[-1]["y1"] + .2:
            panels[-1]["y1"] = max(panels[-1]["y1"], rectangle["y1"])
        else:
            panels.append(dict(rectangle))
    for panel in panels:
        panel["hasLineNumbers"] = any(policy["numberLeft"] <= span["x"] < policy["left"] and
            panel["y0"] - 1 <= span["y"] <= panel["y1"] and span["font"].endswith(policy["numberFontSuffix"]) and
            re.fullmatch(r"[0-9\s]+", span["text"]) for span in spans)
    return panels


def code_lines(panels, spans):
    lines = collections.defaultdict(list)
    for span in spans:
        if any(panel["hasLineNumbers"] and panel["x0"] - .1 <= span["x"] <= panel["x1"] and
               panel["y0"] - 1 <= span["y"] <= panel["y1"] for panel in panels):
            lines[span["y"]].append((span["x"], span["text"]))
    return ["".join(text for _, text in sorted(row)).strip() for _, row in sorted(lines.items(), reverse=True)]


def inventory_rows(root=ROOT):
    with (root / "docs/odf-obligation-inventory/odf-1.0.csv").open(encoding="utf-8", newline="") as stream:
        return {row["id"]: row for row in csv.DictReader(stream)}


def validate_policy(policy, root=ROOT):
    source = next(item for item in json.loads((root / "docs/odf-obligation-inventory/sources.json").read_text(
        encoding="utf-8"))["sources"] if item["version"] == "1.0" and item["part"] == "spec")
    if policy["schemaVersion"] != 1 or policy["profile"] != "published-1.0-document-grammar" or (
            policy["sourceSha256"] != source["sha256"]) or policy["source"] != source["uri"]:
        raise ValueError("Unreviewed published PDF source or grammar profile")
    reference = (root / policy["referenceRng"]).resolve()
    if not reference.is_relative_to(root.resolve()) or sha(reference.read_bytes()) != policy["referenceSha256"]:
        raise ValueError("Reference grammar changed")
    rows = inventory_rows(root)
    for basis in policy["basis"]:
        if rows[basis["unitId"]]["sentenceSha256"] != basis["sourceUnitSha256"]:
            raise ValueError("Normative PDF grammar identification basis changed")
    layout = policy["layout"]
    if layout["firstPage"] != 33 or layout["lastPage"] != 683 or layout["pageCount"] != 706 or (
            layout["left"] != 90.0) or layout["right"] != 522.1 or layout["numberLeft"] != 55 or (
            layout["numberFontSuffix"] != "+Thorndale") or layout["grayRgb"] != [0.85098] * 3:
        raise ValueError("Unreviewed PDF geometry or line-number policy")
    normalizations = policy["patternLineWraps"]
    if len({item["node"] for item in normalizations}) != len(normalizations):
        raise ValueError("Duplicate reviewed pattern normalization")
    if policy["commentTerminatorWrapCount"] != 2:
        raise ValueError("Unreviewed comment delimiter reconstruction")
    return source, reference, rows


def build(source_dir):
    import pdfplumber
    import pypdf

    policy = json.loads(POLICY.read_text(encoding="utf-8"))
    source, reference, rows = validate_policy(policy)
    path = source_dir / source["file"]
    if sha(path.read_bytes()) != policy["sourceSha256"]:
        raise ValueError("Authoritative PDF changed")
    reader = pypdf.PdfReader(path)
    layout = policy["layout"]
    if len(reader.pages) != layout["pageCount"]:
        raise ValueError("Authoritative PDF page count changed")
    pages, all_lines = [], []
    with pdfplumber.open(path) as document:
        for number in range(layout["firstPage"], layout["lastPage"] + 1):
            page = document.pages[number - 1]
            rectangles = [{key: item[key] for key in ("x0", "y0", "x1", "y1")} for item in page.rects
                if item["non_stroking_color"] and len(item["non_stroking_color"]) == 3 and
                all(abs(color - expected) < .001 for color, expected in zip(item["non_stroking_color"], layout["grayRgb"]))]
            spans = []

            def visit(text, current_matrix, text_matrix, font, size):
                if text.strip():
                    spans.append({"text": text, "x": text_matrix[4], "y": round(text_matrix[5], 1),
                        "font": str(font.get("/BaseFont", "")) if font else "", "size": size})

            raw = reader.pages[number - 1].extract_text(visitor_text=visit)
            raw_lines = raw.splitlines()
            body = " ".join(" ".join(line for line in raw_lines if not re.fullmatch(
                r"OpenDocument-v1\.0-os\.sxw 1 May 2005|Copyright .* All Rights Reserved\. Page \d+ of \d+", line)).split())
            block_id = f"ODF-1.0/spec/B{number - 30:05d}"
            if rows[block_id + "/S001"]["blockSha256"] != sha(body.encode()):
                raise ValueError("PDF layout projection changed original inventory text")
            panels = numbered_panels(rectangles, spans, layout)
            lines = code_lines(panels, spans)
            all_lines.extend(lines)
            pages.append({"page": number, "sourceBlockId": block_id, "sourceBlockSha256": sha(body.encode()),
                "physicalCodeLineCount": len(lines), "codeLineSha256": sha("\n".join(lines).encode()),
                "numberedPanelCount": sum(panel["hasLineNumbers"] for panel in panels),
                "unnumberedPanelCount": sum(not panel["hasLineNumbers"] for panel in panels)})
            page.close()
    text = "\n".join(all_lines)
    if text.count("--\n>") != policy["commentTerminatorWrapCount"]:
        raise ValueError("PDF comment delimiter wrap locations changed")
    text = text.replace("--\n>", "-->")
    count, digest = compare_models(text, reference.read_text(encoding="utf-8"), policy["patternLineWraps"])
    comments = [match.group(1) for match in re.finditer(r"<!--(.*?)-->", text, re.S)]
    return {"schemaVersion": 1, "fullConformanceClaim": False, "fullNormativeAdjudication": False,
        "semanticClassificationComplete": False, "sdkMatcherEquivalence": "not-assessed", "errataReconciliation": "pending",
        "manifestAndStrictSuffix": "outside-this-projection", "unnumberedSyntaxAndProse": "pending",
        "commentObligationAdjudication": "pending", "sourceSha256": policy["sourceSha256"],
        "referenceSha256": policy["referenceSha256"], "builderSha256": text_file_hash(Path(__file__)),
        "policySha256": text_file_hash(POLICY), "pypdfVersion": pypdf.__version__, "pdfplumberVersion": pdfplumber.__version__,
        "nodeCount": count, "referenceModelSha256": digest, "reconstructedXmlSha256": sha(text.encode()),
        "patternWrapCount": len(policy["patternLineWraps"]), "commentTerminatorWrapCount": policy["commentTerminatorWrapCount"],
        "comments": [{"textSha256": sha(comment.encode()), "scopeSignals": sorted(set(word.lower() for word in re.findall(
            r"\b(?:shall|must|should|may|if|unless)\b", comment, re.I)))} for comment in comments], "pages": pages}


def check_contracts(root=ROOT):
    policy_path = root / "docs/odf-pdf-grammar-policy.json"
    policy = json.loads(policy_path.read_text(encoding="utf-8"))
    _, reference, rows = validate_policy(policy, root)
    metadata = json.loads((root / "docs/odf-pdf-grammar-evidence.json").read_text(encoding="utf-8"))
    if metadata["schemaVersion"] != 1 or any(metadata[key] is not False for key in (
            "fullConformanceClaim", "fullNormativeAdjudication", "semanticClassificationComplete")) or any(
            metadata[key] != value for key, value in {"sdkMatcherEquivalence": "not-assessed", "errataReconciliation": "pending",
            "manifestAndStrictSuffix": "outside-this-projection", "unnumberedSyntaxAndProse": "pending",
            "commentObligationAdjudication": "pending"}.items()):
        raise ValueError("PDF grammar evidence cannot close SDK, prose, or effective-version review")
    if metadata["sourceSha256"] != policy["sourceSha256"] or metadata["referenceSha256"] != policy["referenceSha256"] or (
            metadata["builderSha256"] != text_file_hash(Path(__file__))) or metadata["policySha256"] != text_file_hash(policy_path):
        raise ValueError("PDF grammar source, builder, or policy changed")
    model = canonical_model(reference.read_text(encoding="utf-8"))
    if metadata["nodeCount"] != len(model[0]) or metadata["referenceModelSha256"] != model_hash(model) or (
            metadata["patternWrapCount"] != len(policy["patternLineWraps"])) or metadata["commentTerminatorWrapCount"] != policy["commentTerminatorWrapCount"]:
        raise ValueError("Reference grammar model or wrap inventory changed")
    if not re.fullmatch(r"[0-9a-f]{64}", metadata["reconstructedXmlSha256"]):
        raise ValueError("Missing reconstructed grammar fingerprint")
    for comment in metadata["comments"]:
        if not re.fullmatch(r"[0-9a-f]{64}", comment["textSha256"]) or not set(comment["scopeSignals"]) <= {
                "shall", "must", "should", "may", "if", "unless"}:
            raise ValueError("Invalid preserved grammar annotation evidence")
    pages = metadata["pages"]
    if [page["page"] for page in pages] != list(range(policy["layout"]["firstPage"], policy["layout"]["lastPage"] + 1)):
        raise ValueError("PDF reconstruction lost or duplicated a physical page")
    for page in pages:
        if page["sourceBlockId"] != f"ODF-1.0/spec/B{page['page'] - 30:05d}" or (
                rows[page["sourceBlockId"] + "/S001"]["blockSha256"] != page["sourceBlockSha256"]) or any(
                not isinstance(page[key], int) or page[key] < 0 for key in (
                    "physicalCodeLineCount", "numberedPanelCount", "unnumberedPanelCount")) or not re.fullmatch(
                r"[0-9a-f]{64}", page["codeLineSha256"]):
            raise ValueError("Invalid PDF page provenance or code partition")
    return metadata


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-dir", type=Path, default=ROOT / "artifacts/odf-conformance-sources")
    parser.add_argument("--verify", action="store_true")
    parser.add_argument("--check-contracts", action="store_true")
    options = parser.parse_args()
    if options.check_contracts:
        metadata = check_contracts()
    else:
        metadata = build(options.source_dir)
        content = json.dumps(metadata, ensure_ascii=False, indent=2) + "\n"
        if options.verify:
            if TARGET.read_text(encoding="utf-8") != content:
                raise ValueError("PDF grammar evidence differs from locked source reconstruction")
        else:
            TARGET.write_text(content, encoding="utf-8", newline="")
    print(json.dumps({"nodeCount": metadata["nodeCount"], "pageCount": len(metadata["pages"]), "fullConformanceClaim": False}))


if __name__ == "__main__":
    main()
