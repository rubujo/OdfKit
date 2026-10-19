"""Classify complete standard-function documentation templates by SDK role.

This records the roles of definitions, constraints and dependencies. Numerical
interpretation, implementation adjudication and conformance remain pending.
Unknown structures, publication objects and host-dependent functions fail closed.
"""

import argparse
import hashlib
import importlib.util
import json
import re
import sys
import xml.etree.ElementTree as ET
import zipfile
from collections import Counter
from pathlib import Path

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parent.parent
TARGET = Path("docs/odf-formula-semantic-batches.json")
HOST_FUNCTIONS = {"CELL", "DDE", "DOLLAR", "HYPERLINK", "INFO"}
ORDER = {"Summary": 1, "Syntax": 2, "Returns": 3, "Constraints": 4, "Semantics": 5, "See also": 6}
TYPES = {"Number", "Integer", "NonNegativeInteger", "PositiveInteger", "Text", "Logical", "Any", "Reference", "Array", "Complex",
         "String", "BytePosition", "ByteLength", "Date", "Time", "DateTime", "Complex Number"}
SECTION = r"[0-9]+(?:\.[0-9]+)+"
DEPENDENCY = (rf'(?:[A-Z][A-Z0-9._]*(?: {SECTION})?|Infix [Oo]perator (?:"(?:=|<>|&|[-+*/^%])"|“(?:=|[-+*/^%])”|'
              rf'Ordered Comparison \("<", "<=", ">", ">="\)) {SECTION})')
POLICY = {
    "heading": {"kind": "source-heading", "sdkResponsibilities": ["source-context"], "exemption": "non-obligation"},
    "Summary": {"kind": "function-purpose", "sdkResponsibilities": ["consumer"], "exemption": "none"},
    "Syntax": {"kind": "function-signature", "sdkResponsibilities": ["validator", "producer", "consumer"], "exemption": "none"},
    "Returns": {"kind": "function-result-type", "sdkResponsibilities": ["consumer"], "exemption": "none"},
    "Constraints": {"kind": "compound-function-domain", "sdkResponsibilities": ["consumer"], "exemption": "none"},
    "Semantics": {"kind": "compound-function-behavior", "sdkResponsibilities": ["consumer"], "exemption": "none"},
    "See also": {"kind": "function-dependency", "sdkResponsibilities": ["consumer"], "exemption": "none"},
}
CONDITIONS = (
    "適用所宣稱的 evaluator group 或 SDK 已提供的內建函式；語法延伸與額外參數須依 "
    "conformance 條款分別裁決，不以此簽章直接拒絕所有 ODF 文件。Constraints: None "
    "僅表示本函式沒有另列限制，不豁免共同型別、轉型或錯誤規則。所有函式數值、條件分支、"
    "精度與相依條款仍須實作裁決；未實作不是豁免理由。"
)
TEXT = "{urn:oasis:names:tc:opendocument:xmlns:text:1.0}"
STYLE = "{urn:oasis:names:tc:opendocument:xmlns:style:1.0}"
OFFICE = "{urn:oasis:names:tc:opendocument:xmlns:office:1.0}"


def digest(value):
    return hashlib.sha256(value).hexdigest()


def module(name, root=ROOT):
    spec = importlib.util.spec_from_file_location(name.replace("-", "_"), root / "eng" / (name + ".py"))
    loaded = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(loaded)
    return loaded


def position_resolver(roots):
    styles, defaults = {}, {}
    for root in roots:
        for node in root.iter(STYLE + "style"):
            family, name = node.get(STYLE + "family"), node.get(STYLE + "name")
            properties = node.find(STYLE + "text-properties")
            styles[family, name] = {"name": name, "family": family, "parent": node.get(STYLE + "parent-style-name"),
                                    "position": properties.get(STYLE + "text-position") if properties is not None else None,
                                    "styleSha256": digest(ET.tostring(node))}
        for node in root.iter(STYLE + "default-style"):
            properties = node.find(STYLE + "text-properties")
            if properties is not None and properties.get(STYLE + "text-position") is not None:
                defaults[node.get(STYLE + "family")] = properties.get(STYLE + "text-position")

    def resolve(family, name, inherited=None, active=None):
        if not name:
            return inherited if inherited is not None else defaults.get(family), []
        key = family, name
        active = set() if active is None else set(active)
        if key in active or key not in styles:
            raise ValueError("Cyclic or missing formula typography style")
        active.add(key)
        style = styles[key]
        position, chain = resolve(family, style["parent"], inherited, active)
        return style["position"] if style["position"] is not None else position, chain + [style]

    return resolve


def positioned_text(node, resolve, inherited=None, address="."):
    family = "paragraph" if node.tag in (TEXT + "p", TEXT + "h") else "text"
    position, chain = resolve(family, node.get(TEXT + "style-name"), inherited)
    spans = []
    if position is not None and position.split()[0] not in {"0%", "0", "0.0%"}:
        if node.text:
            spans.append({"locator": address, "text": node.text, "position": position, "styleChain": chain})
    counts = Counter()
    for child in node:
        local = child.tag.split("}")[-1]
        counts[local] += 1
        spans += positioned_text(child, resolve, position, f"{address}/{local}[{counts[local]}]")
        if child.tail and position is not None and position.split()[0] not in {"0%", "0", "0.0%"}:
            spans.append({"locator": f"{address}/after-{local}[{counts[local]}]", "text": child.tail,
                          "position": position, "styleChain": chain})
    return spans


def inline_evidence(path, blocks, version):
    with zipfile.ZipFile(path) as archive:
        roots = [ET.fromstring(archive.read(entry)) for entry in ("styles.xml", "content.xml")]
    resolve = position_resolver(roots)
    nodes = {}

    def walk(node, address):
        nodes[address] = node
        counts = Counter()
        for child in node:
            local = child.tag.split("}")[-1]
            counts[local] += 1
            walk(child, f"{address}/{local}[{counts[local]}]")

    walk(roots[1].find(".//" + OFFICE + "text"), "/office:document-content/office:body/office:text")
    result = []
    for index, block in enumerate(blocks, 1):
        node = nodes[block["locator"]]
        spans = positioned_text(node, resolve)
        if spans:
            result.append({"blockId": f"ODF-{version}/formula/B{index:05d}", "locator": block["locator"],
                           "plainTextSha256": digest(block["text"].encode("utf-8")),
                           "paragraphXmlSha256": digest(ET.tostring(node)), "positionedText": spans,
                           "arithmeticInterpretation": "pending"})
    return result


def signature(function, text):
    if " or " in text:
        return all(signature(function, alternative) for alternative in text.split(" or "))
    match = re.fullmatch(re.escape(function) + r"\s*\((.*)\)", text)
    if not match:
        return False
    stack = []
    for character in match[1]:
        if character in "([{":
            stack.append(character)
        elif character in ")]}":
            if not stack or {")": "(", "]": "[", "}": "{"}[character] != stack.pop():
                return False
    # Only the published signature metalanguage is accepted, not prose or an
    # alternative function call accidentally swallowed by a broad expression.
    return not stack and bool(re.fullmatch(r"[\w\s.;,\[\]{}=+*/:'\"|\-]*", match[1]))


def classify_version(function, units):
    """Return a complete role assignment, or leave the entire version pending."""
    active, seen, bindings = None, set(), []
    for index, unit in enumerate(units):
        text, material = unit["text"], unit["material"]
        if material == "heading":
            if index != 0 or text != function or unit.get("sourceKind", "heading") != "heading":
                return None
            field, body = "heading", text
        else:
            if material != "paragraph" or index == 0:
                return None
            marker = re.match(r"^(Summary|Syntax|Returns|Constraints|Semantics):\s*(.+)$", text)
            see_also = re.fullmatch(r"See also\s+(.+)", text)
            if marker or see_also:
                field, body = (marker[1], marker[2]) if marker else ("See also", see_also[1])
                if field in seen or (active is not None and ORDER[field] <= ORDER[active]):
                    return None
                seen.add(field)
                active = field
                if field == "Syntax" and not signature(function, body):
                    return None
                if (field == "Returns" and body.rstrip(".") not in TYPES
                        and not re.fullmatch(r"Number (?:>=|≥) 1\.?", body)):
                    return None
                if field == "See also" and not re.fullmatch(rf"{DEPENDENCY}(?:(?:\s*[,;]\s*| (?:and )?){DEPENDENCY})*\.?", body):
                    return None
            else:
                if (active not in {"Summary", "Constraints", "Semantics"} or text == "See also"
                        or (re.match(r"^[A-Z][A-Za-z ]{0,30}:", text) and not text.startswith("Note: ")
                            and not (active == "Semantics" and (text in {
                                "Equivalent to:", "This is equivalent to:", "This is equal to:"}
                                or (function == "EASTERSUNDAY" and text == "The resulting date is: DATE(Year; month; day)."))))):
                    return None
                field, body = active, text
        bindings.append({"unitId": unit["id"], "sourceUnitSha256": digest(text.encode("utf-8")),
                         "field": field, "fieldStart": field == "heading" or bool(marker or see_also),
                         "markerText": text if field == "heading" or bool(marker or see_also) else None,
                         "predicateSha256": digest(body.encode("utf-8")),
                         "scopeSignals": sorted(set(word.lower() for word in re.findall(
                             r"\b(?:shall|must|should|may|can|if|unless|not|only|otherwise|except|implementation-defined|implementation-dependent)\b", body, re.I))),
                         "referencedSections": re.findall(r"\b[0-9]+(?:\.[0-9]+)+\b", body),
                         "unresolvedNamedReferences": [match["name"] for match in re.finditer(
                             rf"(?:^|[,;]\s*| and )(?P<name>[A-Z][A-Z0-9._]*)(?P<section> {SECTION})?(?=$|[,;.]| and )", body)
                             if not match["section"]] if field == "See also" else []})
    return bindings if {"Summary", "Syntax", "Returns", "Semantics"} <= seen else None


def compile_group(group, units):
    headings = group["context"].get("headings", [])
    if (group["context"]["part"] != "formula" or len(headings) != 3
            or headings[0] != "Standard Operators and Functions"
            or not re.fullmatch(r"[A-Z][A-Z0-9._]*", headings[-1]) or headings[-1] in HOST_FUNCTIONS):
        return None
    bindings = []
    for version in group["versions"]:
        if version not in {"1.2", "1.3", "1.4"}:
            return None
        selected = sorted((unit for unit in units if unit["version"] == version), key=lambda unit: unit["id"])
        compiled = classify_version(headings[-1], selected)
        if compiled is None:
            return None
        bindings.extend(compiled)
    if len(bindings) != len(units):
        return None
    return {"groupId": group["id"], "function": headings[-1], "memberSha256": group["memberSha256"],
            "versions": group["versions"], "crossVersionEquivalenceClaim": False,
            "semanticInterpretation": "pending", "implementationAdjudication": "pending", "bindings": bindings}


def build(source_dir, root=ROOT):
    families = module("Build-OdfSemanticBatches", root)
    inventory = module("Build-OdfObligationInventory", root)
    groups, rows, classified = families.load_inventory(root)
    manual = {review["groupId"] for review in json.loads((root / "docs/odf-semantic-reviews.json").read_text(encoding="utf-8"))["reviews"]}
    sources = json.loads((root / "docs/odf-obligation-inventory/sources.json").read_text(encoding="utf-8"))["sources"]
    contexts, locks, inline = {}, [], []
    for source in sources:
        if source["part"] != "formula" or source["version"] not in {"1.2", "1.3", "1.4"}:
            continue
        path = source_dir / source["file"]
        if digest(path.read_bytes()) != source["sha256"]:
            raise ValueError("Formula publication changed")
        blocks, _, _ = inventory.odt_blocks(path)
        inline += inline_evidence(path, blocks, source["version"])
        locks.append({key: source[key] for key in ("version", "part", "file", "sha256")})
        for index, block in enumerate(blocks, 1):
            contexts[f"ODF-{source['version']}/formula/B{index:05d}"] = block
    members = {}
    for identifier, row in rows.items():
        group_id = classified[identifier]["groupId"]
        group = groups[group_id]
        if group_id in manual or group["context"]["part"] != "formula":
            continue
        block = contexts.get(identifier.rsplit("/S", 1)[0])
        unit = {"id": identifier, "version": row["version"], "material": classified[identifier]["material"], "text": ""}
        if block:
            text = block["text"][int(row["start"]):int(row["end"])].strip()
            if digest(text.encode("utf-8")) != row["sentenceSha256"]:
                raise ValueError("Formula source sentence changed")
            unit.update(text=text, sourceKind=block["kind"])
        members.setdefault(group_id, []).append(unit)
    reviews = []
    for identifier, units in sorted(members.items()):
        review = compile_group(groups[identifier], units)
        if review:
            reviews.append(review)
    return {"schemaVersion": 1, "basis": "complete-standard-function-template-role-classification",
            "builderSha256": digest((root / "eng/Build-OdfFormulaSemanticBatches.py").read_bytes().replace(b"\r\n", b"\n")),
            "fullConformanceClaim": False, "semanticClassificationComplete": False,
            "numericalSemanticsReviewed": False, "conditions": CONDITIONS, "fieldPolicy": POLICY,
            "sources": locks, "inlineEvidence": inline, "groups": reviews}


def validate_contracts(document, groups, rows, classified, manual_ids):
    if (document.get("schemaVersion") != 1 or document.get("fieldPolicy") != POLICY
            or document.get("conditions") != CONDITIONS
            or any(document.get(flag) is not False for flag in (
                "fullConformanceClaim", "semanticClassificationComplete", "numericalSemanticsReviewed"))):
        raise ValueError("Role classification cannot claim semantic or implementation completion")
    expected = {}
    for identifier, classification in classified.items():
        expected.setdefault(classification["groupId"], set()).add(identifier)
    seen = set()
    for review in document["groups"]:
        identifier = review["groupId"]
        if identifier in seen or identifier in manual_ids or identifier not in groups:
            raise ValueError("Duplicate, foreign or manually reviewed function group")
        group = groups[identifier]
        if (review["memberSha256"] != group["memberSha256"] or review["versions"] != group["versions"]
                or review["semanticInterpretation"] != "pending" or review["implementationAdjudication"] != "pending"
                or review["crossVersionEquivalenceClaim"] is not False):
            raise ValueError("Function members or adjudication state changed")
        units, actual = [], set()
        for binding in review["bindings"]:
            unit = binding["unitId"]
            if unit in actual or unit not in expected[identifier] or binding["sourceUnitSha256"] != rows[unit]["sentenceSha256"]:
                raise ValueError("Formula binding changed or escaped its group")
            actual.add(unit)
            marker = binding["markerText"]
            if binding["fieldStart"]:
                if not isinstance(marker, str) or digest(marker.encode("utf-8")) != binding["sourceUnitSha256"]:
                    raise ValueError("Function template marker changed")
                text = marker
            else:
                if marker is not None or binding["predicateSha256"] != binding["sourceUnitSha256"]:
                    raise ValueError("Function continuation changed")
                text = "Continuation of the preceding definition."
            units.append({"id": unit, "text": text, "material": classified[unit]["material"], "version": rows[unit]["version"]})
        if actual != expected[identifier]:
            raise ValueError("Partial function group cannot be classified")
        compiled = compile_group(group, units)
        if compiled is None or compiled["function"] != review["function"]:
            raise ValueError("Invalid function template")
        source_bindings = {binding["unitId"]: binding for binding in review["bindings"]}
        for checked in compiled["bindings"]:
            original = source_bindings[checked["unitId"]]
            if checked["field"] != original["field"] or checked["fieldStart"] != original["fieldStart"]:
                raise ValueError("Function section responsibility changed")
            if checked["fieldStart"] and checked != original:
                raise ValueError("Function marker interpretation changed")
        seen.add(identifier)
    return seen


def validate_inline_evidence(document, rows):
    blocks = {identifier.rsplit("/S", 1)[0]: row["blockSha256"] for identifier, row in rows.items() if "/S" in identifier}
    records = {}
    for record in document["inlineEvidence"]:
        block = record["blockId"]
        if (block in records or blocks.get(block) != record["plainTextSha256"]
                or record["arithmeticInterpretation"] != "pending" or not record["positionedText"]
                or not re.fullmatch(r"[a-f0-9]{64}", record["paragraphXmlSha256"])):
            raise ValueError("Invalid inline formula source evidence")
        for span in record["positionedText"]:
            if not span["text"] or not span["position"] or span["position"].split()[0] in {"0%", "0", "0.0%"}:
                raise ValueError("Baseline or empty text cannot be presented as a positioned formula")
        records[block] = record
    return records


def check_contracts(root=ROOT):
    document = json.loads((root / TARGET).read_text(encoding="utf-8"))
    if document["builderSha256"] != digest((root / "eng/Build-OdfFormulaSemanticBatches.py").read_bytes().replace(b"\r\n", b"\n")):
        raise ValueError("Function classifier changed without rebuilding evidence")
    sources = json.loads((root / "docs/odf-obligation-inventory/sources.json").read_text(encoding="utf-8"))["sources"]
    expected_sources = [{key: source[key] for key in ("version", "part", "file", "sha256")} for source in sources
                        if source["part"] == "formula" and source["version"] in {"1.2", "1.3", "1.4"}]
    if document["sources"] != expected_sources:
        raise ValueError("Function publication locks changed")
    groups, rows, classified = module("Build-OdfSemanticBatches", root).load_inventory(root)
    validate_inline_evidence(document, rows)
    manual = {review["groupId"] for review in json.loads((root / "docs/odf-semantic-reviews.json").read_text(encoding="utf-8"))["reviews"]}
    return validate_contracts(document, groups, rows, classified, manual)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-dir", type=Path, default=ROOT / "artifacts/odf-conformance-sources")
    parser.add_argument("--verify", action="store_true")
    parser.add_argument("--check-contracts", action="store_true")
    options = parser.parse_args()
    if options.check_contracts:
        print(json.dumps({"formulaRoleClassifiedGroups": len(check_contracts()), "semanticInterpretation": "pending",
                          "fullConformanceClaim": False}))
        return
    result = build(options.source_dir)
    serialized = json.dumps(result, ensure_ascii=False, indent=2) + "\n"
    path = ROOT / TARGET
    if options.verify:
        if path.read_text(encoding="utf-8") != serialized:
            raise ValueError("Formula role classifications differ from locked publications")
    else:
        path.write_text(serialized, encoding="utf-8", newline="")
    print(json.dumps({"formulaRoleClassifiedGroups": len(result["groups"]), "semanticInterpretation": "pending",
                      "fullConformanceClaim": False}))


if __name__ == "__main__":
    main()
