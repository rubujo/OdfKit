#!/usr/bin/env python3
"""以 Jing 獨立重驗元素定義固定資料。

OdfKit.Tests/TestData/OdfElementCases.jsonl.gz 的每個實例都記錄官方 RELAX NG schema 的判定
（接受或拒絕）。此腳本不使用 OdfKit，只用 Jing（見 eng/Install-Jing.ps1）重算全部判定，
讓元素差異測試的判定來源可獨立重現。
"""
import argparse
import gzip
import hashlib
import json
import subprocess
import sys
import tempfile
from collections import defaultdict
from pathlib import Path

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
SCHEMAS = {
    '1.1': 'OpenDocument-schema-v1.1.rng',
    '1.2': 'OpenDocument-v1.2-os-schema.rng',
    '1.3': 'OpenDocument-v1.3-schema.rng',
    '1.4': 'OpenDocument-v1.4-schema.rng',
}
FIXTURE = ROOT / 'OdfKit.Tests/TestData/OdfElementCases.jsonl.gz'
MANIFEST = ROOT / 'OdfKit.Tests/TestData/OdfElementCases.manifest.txt'


def load():
    with gzip.open(FIXTURE, 'rt', encoding='utf-8') as stream:
        lines = stream.read().split('\n')
    namespaces = json.loads(lines[0])['namespaces']
    return namespaces, [json.loads(line) for line in lines[1:] if line]


def document(namespaces, case):
    group, version, label, name, kind, expected, body = case
    if kind == 'base':
        return f'<?xml version="1.0" encoding="UTF-8"?><office:document-content{namespaces} office:version="{version}">{body}</office:document-content>'
    return (f'<?xml version="1.0" encoding="UTF-8"?><office:document{namespaces} office:mimetype="application/vnd.oasis.opendocument.{kind}" '
            f'office:version="{version}">{body}</office:document>')


def run_jing(jing, version, documents, chunk=300):
    schema = ROOT / 'tools/OdfSchemaGenerator/schemas' / SCHEMAS[version]
    invalid = set()
    with tempfile.TemporaryDirectory() as folder:
        folder = Path(folder)
        names = {}
        for index, (key, text) in enumerate(documents.items()):
            path = folder / f'd{index}.xml'
            path.write_text(text, encoding='utf-8')
            names[path.as_posix()] = key
        files = list(names)
        for start in range(0, len(files), chunk):
            arguments = folder / 'arguments.txt'
            arguments.write_text('\n'.join(['-jar', Path(jing).as_posix(), '-i', schema.as_posix()] + files[start:start + chunk]), encoding='utf-8')
            process = subprocess.run(['java', '@' + str(arguments)], capture_output=True, text=True, encoding='utf-8', errors='replace', check=False)
            for line in (process.stdout + process.stderr).splitlines():
                normalized = line.replace(chr(92), '/')
                for path, key in names.items():
                    if normalized.startswith(path + ':'):
                        invalid.add(key)
                        break
    return invalid


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--jing', default=str(ROOT / 'artifacts/odf-source-audit/jing/bin/jing.jar'))
    options = parser.parse_args()
    digest = hashlib.sha256(FIXTURE.read_bytes()).hexdigest()
    if MANIFEST.read_text(encoding='utf-8').splitlines()[0] != 'fixture-sha256 ' + digest:
        raise SystemExit('固定資料與清單記錄的雜湊不符')
    namespaces, cases = load()
    by_version = defaultdict(dict)
    expected = {}
    for index, case in enumerate(cases):
        by_version[case[1]][index] = document(namespaces, case)
        expected[index] = case[5]
    failures = []
    for version, documents in sorted(by_version.items()):
        rejected = run_jing(options.jing, version, documents)
        for key in documents:
            if (key not in rejected) != expected[key]:
                failures.append((version, cases[key][0], cases[key][2], cases[key][3]))
    print(json.dumps({'cases': len(cases), 'disagreements': len(failures)}))
    for item in failures[:20]:
        print(item)
    return 1 if failures else 0


if __name__ == '__main__':
    sys.exit(main())
