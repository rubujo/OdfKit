"""Reconstruct publication objects and declared alternatives without adjudicating them.

An embedded mathematical definition and its frame's replacement image are kept as
separate original source units. A preferred MathML rendition is available for
review; this catalog neither waives mathematical obligations nor asserts that an
unrendered binary replacement has been visually compared.
"""

import argparse
import base64
import collections
import csv
import hashlib
import importlib.util
import json
import re
import sys
import urllib.parse
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path, PurePosixPath

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parent.parent
TARGET = ROOT / "docs/odf-publication-objects.json"
MATH = "{http://www.w3.org/1998/Math/MathML}"
OFFICE = "{urn:oasis:names:tc:opendocument:xmlns:office:1.0}"
DRAW = "{urn:oasis:names:tc:opendocument:xmlns:drawing:1.0}"
MANIFEST = "{urn:oasis:names:tc:opendocument:xmlns:manifest:1.0}"
FRAME_CONTENT = {DRAW + name for name in ("object", "object-ole", "image", "text-box", "applet", "plugin", "floating-frame")}
FRAME_CONTENT.add("{urn:oasis:names:tc:opendocument:xmlns:table:1.0}table")
FRAME_BASES = {"1.2": ["ODF-1.2/schema/B02539", "ODF-1.2/schema/B02541"],
               "1.3": ["ODF-1.3/schema/B02570", "ODF-1.3/schema/B02572"],
               "1.4": ["ODF-1.4/schema/B02592", "ODF-1.4/schema/B02594"]}


def sha(value):
    return hashlib.sha256(value).hexdigest()


def file_hash(path):
    return sha(path.read_bytes().replace(b"\r\n", b"\n"))


def source_rows(root=ROOT):
    rows = {}
    for version in ("1.0", "1.1", "1.2", "1.3", "1.4"):
        with (root / f"docs/odf-obligation-inventory/odf-{version}.csv").open(encoding="utf-8", newline="") as stream:
            rows.update((row["id"], row) for row in csv.DictReader(stream))
    return rows


def local_reference(href):
    if href is None:
        return None
    parsed = urllib.parse.urlsplit(href)
    if parsed.scheme or parsed.netloc or parsed.query or parsed.fragment:
        return None
    path = urllib.parse.unquote(parsed.path)
    if path.startswith("./"):
        path = path[2:]
    parts = PurePosixPath(path)
    if not path or parts.is_absolute() or ".." in parts.parts or "\\" in path:
        return None
    return str(parts)


def locate(body, locator):
    prefix = "/office:document-content/office:body/office:text"
    if not locator.startswith(prefix + "/"):
        raise ValueError("Unknown publication object locator root")
    current = body
    for component in locator[len(prefix) + 1:].split("/"):
        match = re.fullmatch(r"(?P<name>[\w-]+)\[(?P<index>[1-9][0-9]*)\]", component)
        if match is None:
            raise ValueError("Invalid source object locator")
        matches = [child for child in current if child.tag.rsplit("}", 1)[-1] == match["name"]]
        current = matches[int(match["index"]) - 1]
    return current


def mathml_evidence(content):
    root = ET.fromstring(content)
    if root.tag != MATH + "math":
        return None
    annotations = []
    presentation = []
    foreign = set()
    for node in root.iter():
        if not node.tag.startswith(MATH):
            foreign.add(node.tag)
        if node.tag in (MATH + "annotation", MATH + "annotation-xml"):
            annotations.append({"kind": node.tag.rsplit("}", 1)[-1], "encoding": node.get("encoding"),
                "textSha256": sha("".join(node.itertext()).encode()), "xmlSha256": sha(ET.tostring(node))})
        presentation.append([node.tag, sorted(node.attrib.items()), node.text or "", node.tail or "", len(node)])
    return {"nodeCount": len(presentation), "treeSha256": sha(json.dumps(presentation, ensure_ascii=False,
        separators=(",", ":")).encode()), "annotations": annotations, "foreignElements": sorted(foreign)}


def frame_pair(frame, primary, alternate, container_version):
    if frame.tag != DRAW + "frame" or container_version not in FRAME_BASES:
        return False
    content = [child for child in frame if child.tag in FRAME_CONTENT]
    return len(content) == 2 and content[0] is primary and content[1] is alternate and (
        primary.tag == DRAW + "object" and alternate.tag == DRAW + "image")


def build(source_dir):
    spec = importlib.util.spec_from_file_location("object_inventory", ROOT / "eng/Build-OdfObligationInventory.py")
    inventory = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(inventory)
    locks = json.loads((ROOT / "docs/odf-obligation-inventory/sources.json").read_text(encoding="utf-8"))["sources"]
    rows, records = source_rows(), []
    for source in locks:
        if not source["file"].endswith(".odt"):
            continue
        path = source_dir / source["file"]
        if sha(path.read_bytes()) != source["sha256"]:
            raise ValueError("Locked object source package changed")
        _, _, objects = inventory.odt_blocks(path)
        if not objects:
            continue
        with zipfile.ZipFile(path) as package:
            root = ET.fromstring(package.read("content.xml"))
            body = root.find(".//" + OFFICE + "text")
            manifest = ET.fromstring(package.read("META-INF/manifest.xml"))
            media = {node.get(MANIFEST + "full-path"): node.get(MANIFEST + "media-type") for node in manifest}
            container_version = root.get(OFFICE + "version")
            frames = collections.defaultdict(list)
            for index, obj in enumerate(objects, 1):
                unit = f"ODF-{source['version']}/{source['part']}/O{index:05d}"
                row = rows[unit]
                if obj["sha256"] != row["blockSha256"] or obj["locator"] != row["locator"]:
                    raise ValueError("Publication object extraction changed original source identity")
                node = locate(body, obj["locator"])
                if node.tag != DRAW + obj["kind"]:
                    raise ValueError("Source object namespace differs from its inventory kind")
                record = {"unitId": unit, "sourceObjectSha256": obj["sha256"], "sourceSha256": source["sha256"],
                    "kind": obj["kind"], "locator": obj["locator"], "sourceHref": obj["href"],
                    "sourceContainerVersion": container_version, "payload": None, "mathml": None,
                    "declaredPrimaryUnitId": None, "frameSelectionBasis": [],
                    "visualEquivalence": "not-assessed", "semanticAdjudication": "pending"}
                entry = local_reference(obj["href"])
                reference = entry
                if obj["kind"] == "object" and entry and entry.rstrip("/") + "/content.xml" in package.namelist():
                    reference = entry.rstrip("/") + "/content.xml"
                payload = package.read(reference) if reference in package.namelist() else None
                if payload is None:
                    binary = node.find(OFFICE + "binary-data")
                    if binary is not None and binary.text:
                        payload = base64.b64decode("".join(binary.text.split()), validate=True)
                        reference = "inline:office:binary-data"
                    else:
                        math = node.find(MATH + "math")
                        if math is not None:
                            payload = ET.tostring(math)
                            reference = "inline:math:math"
                if payload is not None:
                    record["payload"] = {"reference": reference, "sha256": sha(payload), "byteCount": len(payload),
                        "mediaType": media.get(reference), "signatureHex": payload[:8].hex()}
                    if obj["kind"] == "object" and (reference.endswith("/content.xml") or reference == "inline:math:math"):
                        record["mathml"] = mathml_evidence(payload)
                records.append(record)
                frames[obj["locator"].rsplit("/", 1)[0]].append((node, record))
            for locator, members in frames.items():
                if len(members) != 2:
                    continue
                (primary, first), (alternate, second) = members
                frame = locate(body, locator)
                if first["mathml"] is not None and frame_pair(frame, primary, alternate, container_version):
                    second["declaredPrimaryUnitId"] = first["unitId"]
                    second["frameSelectionBasis"] = FRAME_BASES[container_version]
    return {"schemaVersion": 1, "fullConformanceClaim": False, "semanticClassificationComplete": False,
        "objectAdjudicationComplete": False, "builderSha256": file_hash(Path(__file__)),
        "inventoryBuilderSha256": file_hash(ROOT / "eng/Build-OdfObligationInventory.py"),
        "frameSelectionBasis": {version: [{"blockId": block, "blockSha256": rows[block + "/S001"]["blockSha256"]}
            for block in blocks] for version, blocks in FRAME_BASES.items()}, "objects": records}


def check_contracts(root=ROOT):
    document = json.loads((root / "docs/odf-publication-objects.json").read_text(encoding="utf-8"))
    if document["schemaVersion"] != 1 or any(document[key] is not False for key in (
            "fullConformanceClaim", "semanticClassificationComplete", "objectAdjudicationComplete")):
        raise ValueError("Object reconstruction cannot close semantic or implementation adjudication")
    if document["builderSha256"] != file_hash(Path(__file__)) or document["inventoryBuilderSha256"] != file_hash(
            root / "eng/Build-OdfObligationInventory.py"):
        raise ValueError("Publication object extraction code changed")
    rows = source_rows(root)
    locks = {(source["version"], source["part"]): source for source in json.loads((root /
        "docs/odf-obligation-inventory/sources.json").read_text(encoding="utf-8"))["sources"]}
    for version, blocks in FRAME_BASES.items():
        expected = [{"blockId": block, "blockSha256": rows[block + "/S001"]["blockSha256"]} for block in blocks]
        if document["frameSelectionBasis"][version] != expected:
            raise ValueError("Frame rendition selection source changed")
    records = {record["unitId"]: record for record in document["objects"]}
    if len(records) != len(document["objects"]) or set(records) != {unit for unit in rows if "/O" in unit}:
        raise ValueError("Object reconstruction lost, duplicated, or invented a source unit")
    for unit, record in records.items():
        row = rows[unit]
        if record["sourceObjectSha256"] != row["blockSha256"] or record["locator"] != row["locator"] or (
                record["sourceSha256"] != locks[row["version"], row["part"]]["sha256"]) or (
                record["visualEquivalence"] != "not-assessed") or record["semanticAdjudication"] != "pending":
            raise ValueError("Object provenance or pending adjudication changed")
        if record["kind"] not in ("image", "object", "object-ole") or not re.search(
                rf"/{re.escape(record['kind'])}\[[0-9]+\]$", record["locator"]):
            raise ValueError("Object kind differs from its original locator")
        payload = record["payload"]
        if payload is not None and (not re.fullmatch(r"[0-9a-f]{64}", payload["sha256"]) or payload["byteCount"] < 0):
            raise ValueError("Missing object payload fingerprint")
        mathml = record["mathml"]
        if mathml is not None:
            if record["kind"] != "object" or payload is None or mathml["nodeCount"] < 1 or not re.fullmatch(
                    r"[0-9a-f]{64}", mathml["treeSha256"]):
                raise ValueError("Invalid embedded mathematical representation")
            for annotation in mathml["annotations"]:
                if any(not re.fullmatch(r"[0-9a-f]{64}", annotation[key]) for key in ("textSha256", "xmlSha256")):
                    raise ValueError("Missing mathematical annotation fingerprint")
        primary_id = record["declaredPrimaryUnitId"]
        if primary_id:
            primary = records.get(primary_id)
            if primary is None or primary["mathml"] is None or primary["kind"] != "object" or record["kind"] != "image" or (
                    primary_id.rsplit("/", 1)[0] != unit.rsplit("/", 1)[0]) or (
                    primary["locator"].rsplit("/", 1)[0] != record["locator"].rsplit("/", 1)[0]) or (
                    primary["sourceContainerVersion"] != record["sourceContainerVersion"]) or (
                    record["frameSelectionBasis"] != FRAME_BASES.get(record["sourceContainerVersion"])):
                raise ValueError("Unqualified or cross-source mathematical rendition alias")
        elif record["frameSelectionBasis"]:
            raise ValueError("Rendition source basis has no primary object")
    return records


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-dir", type=Path, default=ROOT / "artifacts/odf-conformance-sources")
    parser.add_argument("--verify", action="store_true")
    parser.add_argument("--check-contracts", action="store_true")
    options = parser.parse_args()
    if options.check_contracts:
        records = check_contracts()
    else:
        document = build(options.source_dir)
        value = json.dumps(document, ensure_ascii=False, indent=2) + "\n"
        if options.verify:
            if TARGET.read_text(encoding="utf-8") != value:
                raise ValueError("Publication object catalog differs from locked sources")
        else:
            TARGET.write_text(value, encoding="utf-8", newline="")
        records = {record["unitId"]: record for record in document["objects"]}
    print(json.dumps({"objectCount": len(records), "parsedMathmlObjects": sum(record["mathml"] is not None for record in records.values()),
        "declaredMathmlAlternatives": sum(record["declaredPrimaryUnitId"] is not None for record in records.values()), "fullConformanceClaim": False}))


if __name__ == "__main__":
    main()
