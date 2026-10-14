"""Build a clause inventory from pinned OASIS publications; never infer full conformance.

HTML parsing uses the standard library. Reading the 1.0/1.1 official PDF tables
of contents additionally requires pypdf. Publication bytes stay in artifacts.
"""

import argparse
import csv
import hashlib
import io
import json
import re
from collections import Counter
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import quote
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
MODAL = re.compile(r"\b(?:shall|must|required|should|may)\b", re.I)
NUMBER = re.compile(r"^((?:\d+|[A-Z])(?:\.\d+)*\.?)[\s]+(.+)$")


def clean(text):
    return " ".join(text.split())


def sha(data):
    return hashlib.sha256(data).hexdigest()


def schema_sha(data):
    # Git text checkout may use CRLF. Preserve every other byte, including BOM,
    # whitespace and XML content, so this lock survives core.autocrlf changes.
    return sha(data.replace(b"\r\n", b"\n"))


class Clauses(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.rows = []
        self.toc = []
        self.toc_anchors = set()
        self.link_anchor = None
        self.link_text = []
        self.heading = None
        self.text = []
        self.anchors = []
        self.body = []
        self.current = None
        self.ignored = 0

    def finish_body(self):
        if self.current is not None:
            prose = clean(" ".join(self.body))
            self.current["proseSha256"] = sha(prose.encode("utf-8"))
            self.current["modalCount"] = len(MODAL.findall(prose))
        self.body = []

    def handle_starttag(self, tag, attrs):
        attributes = dict(attrs)
        if self.heading is None and attributes.get("id") in self.toc_anchors:
            self.finish_body()
            self.current = None
        if tag == "a":
            href = dict(attrs).get("href", "")
            if href.startswith("#"):
                self.link_anchor = href[1:]
                self.link_text = []
        if tag in ("script", "style"):
            self.ignored += 1
        if re.fullmatch(r"h[1-6]", tag):
            self.finish_body()
            self.heading = tag
            self.text = []
            self.anchors = []
        if self.heading:
            for key, value in attrs:
                if key in ("id", "name") and value:
                    self.anchors.append(value)

    def handle_startendtag(self, tag, attrs):
        self.handle_starttag(tag, attrs)
        self.handle_endtag(tag)

    def handle_endtag(self, tag):
        if tag == "a" and self.link_anchor is not None:
            text = clean("".join(self.link_text))
            match = NUMBER.match(text)
            if text.startswith("Appendix"):
                match = re.match(r"Appendix\s*([A-Z](?:\.\d+)*)\.?\s*(.+)", text)
            if match:
                self.toc.append({"clause": match[1].rstrip("."), "title": match[2],
                                 "anchor": self.link_anchor, "proseSha256": "", "modalCount": ""})
                self.toc_anchors.add(self.link_anchor)
            self.link_anchor = None
        if tag in ("script", "style"):
            self.ignored -= 1
        if tag == self.heading:
            text = clean("".join(self.text))
            match = NUMBER.match(text)
            if text.startswith("Appendix"):
                match = re.match(r"Appendix\s*([A-Z](?:\.\d+)*)\.?\s*(.+)", text)
            if match:
                self.current = {
                    "clause": match[1].rstrip("."), "title": match[2],
                    "anchor": self.anchors[0] if self.anchors else "",
                    "proseSha256": "", "modalCount": 0,
                }
                self.rows.append(self.current)
            else:
                self.current = None
            self.heading = None

    def handle_data(self, data):
        if self.ignored:
            return
        if self.link_anchor is not None:
            self.link_text.append(data)
        if self.heading:
            self.text.append(data)
            if len(self.text) == 1 and re.fullmatch(r"\s*(?:\d+|[A-Z])(?:\.\d+)*\.?\s*", data):
                self.text.append(" ")
        elif self.current:
            self.body.append(data)


def read_html(text):
    parser = Clauses()
    parser.feed(text)
    parser.finish_body()
    by_number = {}
    for row in parser.rows:
        if row["clause"] in by_number:
            raise ValueError(f"Duplicate numbered heading: {row['clause']}")
        by_number[row["clause"]] = row
    # Some official HTML publications render appendix headings as <p>,
    # so use the local published TOC as well, without losing body hashes.
    ordered = {}
    for row in parser.toc:
        ordered.setdefault(row["clause"], by_number.get(row["clause"], row))
    for row in parser.rows:
        ordered.setdefault(row["clause"], row)
    return list(ordered.values())


def read_pdf(path):
    from pypdf import PdfReader

    reader = PdfReader(path)
    rows = []
    started = False
    pending = ""
    # Only the published TOC: stop at the numbered introduction, never use body
    # examples as headings. Page destinations refer to the printed page number.
    for page in reader.pages:
        text = page.extract_text()
        if "Table of Contents" in text:
            started = True
        if not started:
            continue
        if rows and not re.search(r"\.{3,}\s*\d+", text):
            break
        for line in text.splitlines():
            line = clean(line)
            line = re.sub(r"\.[. ]*\.", "...", line)
            if not line or line == "Table of Contents":
                continue
            candidate = clean(pending + " " + line)
            match = re.match(r"^((?:\d+|[A-Z])(?:\.\d+)*\.?)\s+(.+?)\.+\s*(\d+)\s*$", candidate)
            if match:
                rows.append({"clause": match[1].rstrip("."), "title": clean(match[2]),
                             "anchor": "page=" + match[3], "proseSha256": "", "modalCount": ""})
                pending = ""
            elif NUMBER.match(line):
                pending = line
            elif pending and not re.fullmatch(r"\d+", line):
                pending = candidate
    if len(rows) < 800:
        raise ValueError(f"Incomplete PDF table of contents: {path}: {len(rows)}")
    return rows


def publications():
    result = []
    for version in ("1.0", "1.1"):
        location = "v1.0/OpenDocument-v1.0-os.pdf" if version == "1.0" else "v1.1/OS/OpenDocument-v1.1.pdf"
        result.append((version, "spec", f"{version}-spec.pdf", "https://docs.oasis-open.org/office/" + location))
    for part, domain in ((0, "introduction"), (1, "schema"), (2, "formula"), (3, "packages")):
        suffix = "" if part == 0 else f"-part{part}"
        result.append(("1.2", domain, f"1.2-part{part}.html",
                       f"https://docs.oasis-open.org/office/v1.2/os/OpenDocument-v1.2-os{suffix}.html"))
    for version in ("1.3", "1.4"):
        for part in ("part1-introduction", "part2-packages", "part3-schema", "part4-formula"):
            result.append((version, part.split("-", 1)[1], f"{version}-{part}.html",
                           f"https://docs.oasis-open.org/office/OpenDocument/v{version}/os/{part}/OpenDocument-v{version}-os-{part}.html"))
    return result


def schema_names():
    result = {}
    locks = []
    for path in sorted((ROOT / "tools/OdfSchemaGenerator/schemas").glob("*.rng")):
        version = re.search(r"v(1\.[0-4])", path.name)[1]
        names = set()
        tree = ET.parse(path)
        for node in tree.iter():
            if node.tag in ("{http://relaxng.org/ns/structure/1.0}element", "{http://relaxng.org/ns/structure/1.0}attribute"):
                if "name" in node.attrib:
                    names.add(node.attrib["name"])
        result.setdefault(version, set()).update(names)
        locks.append({"path": path.relative_to(ROOT).as_posix(), "sha256": schema_sha(path.read_bytes())})
    return result, locks


def build(source_dir, audits):
    rows = []
    sources = []
    names, locks = schema_names()
    for version, part, filename, uri in publications():
        path = source_dir / filename
        if path.suffix == ".pdf":
            clauses = read_pdf(path)
        else:
            clauses = read_html(path.read_text(encoding="utf-8"))
            if not clauses:
                raise ValueError(f"No numbered headings: {path}")
        seen = set()
        nonnormative = []
        for clause in clauses:
            number = clause["clause"]
            if number in seen:
                raise ValueError(f"Duplicate clause: {version}/{part}/{number}")
            seen.add(number)
            if re.search(r"non[- ]?normative", clause["title"], re.I):
                nonnormative.append(number)
            informational = any(number == prefix or number.startswith(prefix + ".") for prefix in nonnormative)
            identifier = f"ODF-{version}/{part}/{number}"
            qnames = re.findall(r"\b[a-z][\w-]*:[\w-]+\b", clause["title"])
            linked = sorted(set(qnames) & names[version])
            associations = [audit["id"] for audit in audits if identifier in audit["clauses"]]
            evidence = "normative-schema" if linked else "none"
            status = "structure-only" if linked else "evidence-gap"
            scope = {"formula": "formula-engine", "packages": "package", "introduction": "conformance-definition"}.get(part, "document")
            if part == "formula":
                evidence = "openformula-safe-profile"
                status = "partial-formula-profile"
            if informational:
                scope, evidence, status = "informative", "not-applicable", "informative"
            if associations and not informational:
                evidence += ";reviewed-audit"
            rows.append({"id": identifier, "version": version, "part": part, **clause,
                         "source": uri + "#" + quote(clause["anchor"], safe="=_"),
                         "scope": scope, "status": status, "evidence": evidence,
                         "schemaNames": ";".join(linked), "audits": ";".join(associations)})
        sources.append({"version": version, "part": part, "uri": uri, "file": filename,
                        "sha256": sha(path.read_bytes()), "clauseCount": len(clauses),
                        "extraction": "published-pdf-toc" if path.suffix == ".pdf" else "numbered-html-headings"})
    return rows, sources, locks


def main():
    args = argparse.ArgumentParser(description=__doc__)
    args.add_argument("--source-dir", type=Path, default=ROOT / "artifacts/odf-conformance-sources")
    args.add_argument("--verify", action="store_true")
    options = args.parse_args()
    audit_path = ROOT / "docs/odf-conformance-audits.json"
    audits = json.loads(audit_path.read_text(encoding="utf-8-sig"))["requirements"]
    rows, sources, locks = build(options.source_dir, audits)
    target = ROOT / "docs/odf-conformance-matrix"
    outputs = {}
    for version in ("1.0", "1.1", "1.2", "1.3", "1.4"):
        stream = io.StringIO(newline="")
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]), lineterminator="\n")
        writer.writeheader()
        writer.writerows(row for row in rows if row["version"] == version)
        outputs[target / f"odf-{version}.csv"] = stream.getvalue()
    metadata = {"schemaVersion": 1, "fullConformanceClaim": False,
                "granularity": "published-numbered-clause; not atomized normative obligations",
                "sources": sources, "schemas": locks,
                "schemaHashEncoding": "bytes with CRLF normalized to LF; preserve BOM and other whitespace",
                "clauseCount": len(rows), "statusCounts": dict(sorted(Counter(row["status"] for row in rows).items())),
                "limitations": ["PDF rows are the published table of contents; normative paragraph counts/hashes are not available for 1.0/1.1.",
                                "A schema name association proves neither matcher equivalence nor semantic conformance.",
                                "All unreviewed prose obligations remain evidence gaps; modal-word counts are navigation hints, not obligation counts.",
                                "Formula evidence is the existing Safe Large manifest, which explicitly disclaims official Large conformance."]}
    outputs[target / "sources.json"] = json.dumps(metadata, ensure_ascii=False, indent=2) + "\n"
    for path, text in outputs.items():
        if options.verify:
            if not path.exists() or path.read_text(encoding="utf-8") != text:
                raise ValueError(f"Generated matrix differs: {path}")
        else:
            target.mkdir(parents=True, exist_ok=True)
            path.write_text(text, encoding="utf-8", newline="")
    print(f"{'Verified' if options.verify else 'Built'} {len(rows)} clauses from {len(sources)} pinned publications.")
    print(json.dumps(metadata["statusCounts"]))


if __name__ == "__main__":
    main()
