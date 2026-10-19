#!/usr/bin/env python3
"""以 Jing 獨立重驗屬性定義固定資料。

OdfKit.Tests/TestData/OdfAttributeCases.jsonl.gz 的每個正例都應被官方 RELAX NG schema 接受，
每個反例都應被拒絕。此腳本不使用 OdfKit，只用 Jing（見 eng/Install-Jing.ps1）核對固定資料本身，
讓差異測試的判定來源可獨立重現。
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
    '1.2': 'OpenDocument-v1.2-os-schema.rng',
    '1.3': 'OpenDocument-v1.3-schema.rng',
    '1.4': 'OpenDocument-v1.4-schema.rng',
}
FIXTURE = ROOT / 'OdfKit.Tests/TestData/OdfAttributeCases.jsonl.gz'
MANIFEST = ROOT / 'OdfKit.Tests/TestData/OdfAttributeCases.manifest.txt'


def escape(value):
    return value.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;').replace('"', '&quot;')


def load():
    with gzip.open(FIXTURE, 'rt', encoding='utf-8') as stream:
        lines = stream.read().split('\n')
    namespaces = json.loads(lines[0])['namespaces']
    return namespaces, [json.loads(line) for line in lines[1:] if line]


def document(namespaces, case, value):
    group, version, attribute, carrier, kind, valid, invalid, body = case
    if value != valid:
        body = body.replace(f' {attribute}="{escape(valid)}"', f' {attribute}="{escape(value)}"', 1)
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
    recorded = MANIFEST.read_text(encoding='utf-8').splitlines()[0]
    if recorded != 'fixture-sha256 ' + digest:
        raise SystemExit('固定資料與清單記錄的雜湊不符')
    namespaces, cases = load()
    by_version = defaultdict(dict)
    expected = {}
    for index, case in enumerate(cases):
        positive = f'{index}|pos'
        by_version[case[1]][positive] = document(namespaces, case, case[5])
        expected[positive] = True
        if case[6] is not None:
            negative = f'{index}|neg'
            by_version[case[1]][negative] = document(namespaces, case, case[6])
            expected[negative] = False
    failures = []
    for version, documents in sorted(by_version.items()):
        rejected = run_jing(options.jing, version, documents)
        for key in documents:
            if (key not in rejected) != expected[key]:
                failures.append((version, key, '正例遭拒絕' if expected[key] else '反例被接受'))
    print(json.dumps({'cases': len(cases), 'documents': sum(len(d) for d in by_version.values()), 'disagreements': len(failures)}))
    if failures:
        for item in failures[:20]:
            print(item)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
