"""Partition every locked ODF review unit into a source-based work queue.

Source classification is not semantic adjudication, SDK exclusion or proof of
conformance. Only an exact existing sentence decision supplies a scope verdict.
"""

import argparse
import csv
import hashlib
import importlib.util
import io
import json
import re
import sys
from collections import Counter
from pathlib import Path

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location("inventory", ROOT / "eng/Build-OdfObligationInventory.py")
inventory = importlib.util.module_from_spec(spec)
spec.loader.exec_module(inventory)

# Exact official chapter titles, not matches against sentence vocabulary.
CHAPTERS = {
    "Introduction": "publication-and-conformance-context",
    "Conformance": "conformance-and-processor-requirements",
    "OpenDocument Documents, Consumers and Producers": "conformance-and-processor-requirements",
    "Packages, Package Consumers and Package Producers": "conformance-and-processor-requirements",
    "Document Structure": "document-structure-and-embedding",
    "Metadata": "metadata-and-settings",
    "Metadata Elements": "metadata-and-settings",
    "Text Content": "text-and-paragraphs",
    "Paragraph Elements Content": "text-and-paragraphs",
    "Text Fields": "text-fields-and-indexes",
    "Text Indices": "text-fields-and-indexes",
    "Text Indexes": "text-fields-and-indexes",
    "Tables": "tables-and-spreadsheets",
    "Graphic Content": "graphics-and-presentation-content",
    "Chart Content": "charts",
    "Database Front-end Document Content": "database-document-content",
    "Form Content": "forms-and-controls",
    "Common Content": "shared-document-content",
    "SMIL Animations": "animation-and-timing",
    "Styles": "styles-and-formatting",
    "Formatting Properties": "styles-and-formatting",
    "Formatting Elements": "styles-and-formatting",
    "Formatting Properties Elements": "styles-and-formatting",
    "Formatting Attributes": "styles-and-formatting",
    "General Attributes": "general-attributes",
    "Datatypes": "datatypes-and-schema",
    "Data Types and Schema Definitions": "datatypes-and-schema",
    "Schemas": "datatypes-and-schema",
    "Packages": "package-container-and-resources",
    "Manifest File": "package-manifest-and-encryption",
    "Digital Signatures File": "package-digital-signatures",
    "Metadata Manifest Files": "package-rdf-metadata",
    "OpenDocument Metadata Manifest Ontology": "package-rdf-metadata",
    "Expressions and Evaluators": "formula-processing-and-types",
    "Formula Processing Model": "formula-processing-and-types",
    "Types": "formula-processing-and-types",
    "Expression Syntax": "formula-syntax",
    "Standard Operators and Functions": "formula-functions-and-operators",
    "Other Capabilities": "formula-optional-capabilities",
    "Non-portable Features": "formula-nonportable-features",
    "Acknowledgments": "publication-and-conformance-context",
    "Changes from “Open Document Format for Office Applications (OpenDocument) v1.2”": "version-change-history-review",
    "Changes from “Open Document Format for Office Applications (OpenDocument) v1.3”": "version-change-history-review",
}
QUEUES = set(CHAPTERS.values()) | {
    "publication-navigation-and-frontmatter", "pdf-source-reconstruction",
    "effective-version-and-errata-reconciliation", "opaque-object-content-review",
    "unmapped-source-heading-review", "explicit-informative-context-review",
}
LABELS = {
    "animation-and-timing": "動畫與時序",
    "charts": "圖表",
    "conformance-and-processor-requirements": "合規類別與處理器要求",
    "database-document-content": "資料庫文件內容",
    "datatypes-and-schema": "資料型別與 schema",
    "document-structure-and-embedding": "文件結構與嵌入文件",
    "effective-version-and-errata-reconciliation": "有效版本與勘誤整合",
    "explicit-informative-context-review": "明示非規範性背景核對",
    "forms-and-controls": "表單與控制項",
    "formula-functions-and-operators": "公式函式與運算子",
    "formula-nonportable-features": "公式非可攜功能",
    "formula-optional-capabilities": "公式選用能力",
    "formula-processing-and-types": "公式處理模型與型別",
    "formula-syntax": "公式語法",
    "general-attributes": "一般屬性",
    "graphics-and-presentation-content": "圖形與簡報內容",
    "metadata-and-settings": "中繼資料與設定",
    "opaque-object-content-review": "非文字物件內容審閱",
    "package-container-and-resources": "封裝容器與資源",
    "package-digital-signatures": "封裝數位簽章",
    "package-manifest-and-encryption": "封裝 manifest 與加密",
    "package-rdf-metadata": "封裝 RDF 中繼資料",
    "pdf-source-reconstruction": "PDF 來源重建",
    "publication-and-conformance-context": "出版物與合規背景",
    "shared-document-content": "共用文件內容",
    "styles-and-formatting": "樣式與格式",
    "tables-and-spreadsheets": "表格與試算表",
    "text-and-paragraphs": "文字與段落",
    "text-fields-and-indexes": "文字欄位與索引",
    "version-change-history-review": "版本變更紀錄核對",
    "unmapped-source-heading-review": "尚未映射的來源標題",
    "publication-navigation-and-frontmatter": "導覽與出版前置內容",
}


def classify(row, context):
    headings = context.get("headingPath", [])
    if row["status"] in ("navigation", "publication-context"):
        queue = "publication-navigation-and-frontmatter"
    elif row["version"] == "1.0" and row["part"] == "spec":
        queue = "pdf-source-reconstruction"
    elif row["reviewState"] == "unreviewed-object":
        queue = "opaque-object-content-review"
    elif row["part"].startswith("errata"):
        queue = "effective-version-and-errata-reconciliation"
    else:
        queue = CHAPTERS.get(headings[0] if headings else "", "unmapped-source-heading-review")

    explicit_informative = any(re.search(r"\bnon[ -]?normative\b|\binformative\b", title, re.I)
                               for title in headings)
    if explicit_informative and queue == "unmapped-source-heading-review":
        queue = "explicit-informative-context-review"
    material = context.get("kind", "unknown-source-material")
    if material == "paragraph" and "/table-cell[" in row["locator"]:
        material = "table-cell"
    if material == "paragraph" and "/note-body[" in row["locator"]:
        material = "note"
    group_context = {"part": row["part"], "headings": headings, "kind": "source-heading"}
    if row["version"] == "1.0" and row["part"] == "spec":
        group_context.update(kind="pdf-page-reconstruction", version=row["version"], locator=row["locator"])
    group_context = json.dumps(group_context, ensure_ascii=False, separators=(",", ":"))
    return {
        "id": row["id"],
        "sourceUnitSha256": row["sentenceSha256"] or row["blockSha256"],
        "queue": queue,
        "material": material,
        "groupId": "ODFG-" + hashlib.sha256(group_context.encode("utf-8")).hexdigest()[:20],
        "groupContext": group_context,
        "sourceContext": "explicit-informative-heading" if explicit_informative else "requires-semantic-review",
        "semanticClassification": "existing-sentence-decision" if row["reviewState"] == "reviewed" else "pending",
        "sdkScope": row["scope"] if row["reviewState"] == "reviewed" else "undetermined",
        "decision": row["decision"] if row["reviewState"] == "reviewed" else "",
    }


def validate_partition(rows, classified):
    expected = {row["id"]: row for row in rows}
    if len(expected) != len(rows) or len(classified) != len(rows):
        raise ValueError("Classification does not cover every source unit exactly once")
    seen = set()
    for result in classified:
        identifier = result["id"]
        if identifier in seen or identifier not in expected:
            raise ValueError(f"Duplicate or unknown classification: {identifier}")
        seen.add(identifier)
        source = expected[identifier]
        if result["sourceUnitSha256"] != (source["sentenceSha256"] or source["blockSha256"]):
            raise ValueError(f"Classification source changed: {identifier}")
        if result["queue"] not in QUEUES or not result["material"]:
            raise ValueError(f"Missing classification: {identifier}")
        if result["groupId"] != "ODFG-" + hashlib.sha256(result["groupContext"].encode("utf-8")).hexdigest()[:20]:
            raise ValueError(f"Grouping identity changed: {identifier}")
        if json.loads(result["groupContext"])["part"] != source["part"]:
            raise ValueError(f"Grouping source part changed: {identifier}")
        if source["reviewState"] != "reviewed" and (
                result["semanticClassification"] != "pending" or result["sdkScope"] != "undetermined" or result["decision"]):
            raise ValueError(f"Structural classification asserted a semantic verdict: {identifier}")
        if source["reviewState"] == "reviewed" and (
                result["semanticClassification"] != "existing-sentence-decision" or
                result["sdkScope"] != source["scope"] or result["decision"] != source["decision"]):
            raise ValueError(f"Classification changed an existing sentence decision: {identifier}")


def outputs(rows, classified, sources):
    validate_partition(rows, classified)
    by_id = {row["id"]: row for row in rows}
    pending = [result for result in classified if by_id[result["id"]]["status"] == "evidence-gap"
               and by_id[result["id"]]["reviewState"].startswith("unreviewed")]
    target = ROOT / "docs/odf-obligation-classification"
    result_files = {}
    for version in ("1.0", "1.1", "1.2", "1.3", "1.4"):
        stream = io.StringIO(newline="")
        writer = csv.DictWriter(stream, fieldnames=list(classified[0]), lineterminator="\n")
        writer.writeheader()
        writer.writerows(result for result in classified if by_id[result["id"]]["version"] == version)
        result_files[target / f"odf-{version}.csv"] = stream.getvalue()
    counts = Counter(result["queue"] for result in pending)
    pending_ids = {result["id"] for result in pending}
    grouped = {}
    for result in classified:
        grouped.setdefault(result["groupId"], []).append(result)
    groups = []
    for identifier, members in sorted(grouped.items()):
        context = members[0]["groupContext"]
        if any(member["groupContext"] != context for member in members):
            raise ValueError(f"Grouping hash collision: {identifier}")
        open_decisions = any(by_id[member["id"]]["reviewState"] == "reviewed" and
                             by_id[member["id"]]["status"] not in ("fulfilled", "informative", "out-of-sdk-scope")
                             for member in members)
        groups.append({
            "id": identifier, "context": json.loads(context),
            "versions": sorted({by_id[member["id"]]["version"] for member in members}),
            "unitCount": len(members),
            "unresolvedUnitCount": sum(member["id"] in pending_ids for member in members),
            "queues": sorted({member["queue"] for member in members}),
            "decisions": sorted({member["decision"] for member in members if member["decision"]}),
            "groupReviewState": ("pending" if any(member["id"] in pending_ids for member in members)
                                 else "partial-review-needs-follow-up" if open_decisions
                                 else "existing-sentence-decisions-only" if any(member["decision"] for member in members)
                                 else "source-context-only"),
            "requiresFollowUp": open_decisions or any(member["id"] in pending_ids for member in members),
            "memberSha256": hashlib.sha256("\n".join(sorted(member["id"] + ":" + member["sourceUnitSha256"]
                                                            for member in members)).encode("utf-8")).hexdigest(),
            "crossVersionEquivalenceClaim": False,
        })
    pending_groups = [group for group in groups if group["unresolvedUnitCount"]]
    partial_only = [group for group in groups if group["groupReviewState"] == "partial-review-needs-follow-up"]
    result_files[target / "groups.json"] = json.dumps({"schemaVersion": 1, "basis": "source-heading-candidate-groups",
        "semanticEquivalenceEstablished": False, "groups": groups}, ensure_ascii=False, indent=2) + "\n"
    metadata = {
        "schemaVersion": 1,
        "basis": "official-source-structure-and-exact-existing-decisions",
        "sourcePartitionComplete": True,
        "semanticClassificationComplete": False,
        "fullNormativeAdjudication": False,
        "fullConformanceClaim": False,
        "classifierSha256": hashlib.sha256(Path(__file__).read_bytes().replace(b"\r\n", b"\n")).hexdigest(),
        "sourceExtractorSha256": hashlib.sha256((ROOT / "eng/Build-OdfObligationInventory.py").read_bytes().replace(b"\r\n", b"\n")).hexdigest(),
        "rowCount": len(rows),
        "unresolvedUnitCount": len(pending),
        "groupCount": len(groups),
        "unresolvedGroupCount": len(pending_groups),
        "reviewedPartialOnlyGroupCount": len(partial_only),
        "needsFollowUpGroupCount": sum(group["requiresFollowUp"] for group in groups),
        "crossVersionEquivalenceEstablished": False,
        "queueCounts": dict(sorted(Counter(result["queue"] for result in classified).items())),
        "unresolvedQueueCounts": dict(sorted(counts.items())),
        "semanticCounts": dict(sorted(Counter(result["semanticClassification"] for result in classified).items())),
        "materialCounts": dict(sorted(Counter(result["material"] for result in classified).items())),
        "sources": [{key: source[key] for key in ("version", "part", "sha256")} for source in sources],
        "limitations": [
            "Every unit is queued; source partition is not completed semantic classification.",
            "Chapter titles do not establish SDK exclusions, obligation strength or conformance.",
            "An explicitly informative heading is source context, not an automatic waiver.",
            "PDF reconstruction, opaque object interpretation and effective errata text are separate work queues.",
        ],
    }
    result_files[target / "summary.json"] = json.dumps(metadata, ensure_ascii=False, indent=2) + "\n"
    lines = ["# ODF 待裁決單位分類", "", "分類基礎：官方來源章節與精確句子裁決。", "",
             "全部來源單位已分派工作佇列；**語意分類、SDK 範圍裁決及完整合規裁決仍未完成**。",
             "不能用章節名稱、modal word 或來源相似度直接宣告豁免。", "",
             f"總來源單位：{len(rows):,}；其中待裁決：{len(pending):,}。", "",
             f"共建立 **{len(groups):,} 個候選工作群組**，其中 **{len(pending_groups):,} 組**含待裁決單位。",
             f"另有 **{len(partial_only):,} 組**雖已有句子裁決但仍需補證據，合計 **{sum(group['requiresFollowUp'] for group in groups):,} 組**需要後續處理。",
             "同一官方來源標題路徑可跨版本形成候選群組，須在組內確認共同要求與版本差異；",
             "**沒有因標題相同而認定規範語意相同**。PDF 先按實體頁分成來源重建批次。", "",
             "| 工作佇列 | 待裁決來源單位 |", "| --- | ---: |"]
    lines += [f"| {LABELS[queue]} | {count:,} |" for queue, count in sorted(counts.items(), key=lambda pair: (-pair[1], pair[0]))]
    lines += ["", "每個版本的 CSV 保留 unit ID、原始句子／物件 hash、來源種類、工作佇列及語意分類狀態。",
              "[groups.json](odf-obligation-classification/groups.json) 記錄各組的章節背景、版本、來源單位數、待裁決數及成員 hash；",
              "以群組完成共同規則、版本差異與例外裁決，共用實作與回歸，不替每句建立獨立修正工作。",
              "`sdkScope=undetermined` 表示尚未作出責任範圍裁決，不表示不屬 SDK。",
              "`sourceContext` 只記錄來源標題明示的 informative／non-normative 背景，不改寫原清單的裁決狀態。", "",
              "處理順序：先核對未映射來源、PDF 重建、物件與勘誤，確保分類單位可解釋；",
              "再按工作佇列逐項判定規範性、適用角色、SDK 責任與證據，分類完成後排定驗證缺口修正。", ""]
    result_files[ROOT / "docs/odf-obligation-classification.md"] = "\n".join(lines)
    return result_files, metadata


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-dir", type=Path, default=ROOT / "artifacts/odf-conformance-sources")
    parser.add_argument("--verify", action="store_true")
    parser.add_argument("--check-contracts", action="store_true")
    options = parser.parse_args()
    source_inventory = ROOT / "docs/odf-obligation-inventory"
    reviews = json.loads((ROOT / "docs/odf-obligation-decisions.json").read_text(encoding="utf-8"))
    inventory.check_contracts(source_inventory, reviews)
    if options.check_contracts:
        rows, classified = [], []
        for version in ("1.0", "1.1", "1.2", "1.3", "1.4"):
            with (source_inventory / f"odf-{version}.csv").open(encoding="utf-8", newline="") as stream:
                rows += list(csv.DictReader(stream))
            with (ROOT / f"docs/odf-obligation-classification/odf-{version}.csv").open(encoding="utf-8", newline="") as stream:
                classified += list(csv.DictReader(stream))
        metadata = json.loads((source_inventory / "sources.json").read_text(encoding="utf-8"))
        result_files, summary = outputs(rows, classified, metadata["sources"])
    else:
        locked = json.loads((source_inventory / "sources.json").read_text(encoding="utf-8"))
        for source in locked["sources"]:
            if hashlib.sha256((options.source_dir / source["file"]).read_bytes()).hexdigest() != source["sha256"]:
                raise ValueError(f"Classification source bytes changed before parsing: {source['file']}")
        rows, sources, raw = inventory.build(options.source_dir)
        inventory.apply_reviews(rows, reviews)
        if [(s["version"], s["part"], s["sha256"]) for s in sources] != [
                (s["version"], s["part"], s["sha256"]) for s in locked["sources"]]:
            raise ValueError("Classification source bytes differ from locked inventory")
        contexts = {block["id"]: block for block in raw}
        for source in sources:
            if source["file"].endswith(".odt"):
                _, _, objects = inventory.odt_blocks(options.source_dir / source["file"])
                for index, obj in enumerate(objects, 1):
                    contexts[f"ODF-{source['version']}/{source['part']}/O{index:05d}"] = obj
        classified = []
        for row in rows:
            identifier = row["id"].rsplit("/S", 1)[0]
            if identifier not in contexts:
                raise ValueError(f"Missing exact source context: {row['id']}")
            classified.append(classify(row, contexts[identifier]))
        result_files, summary = outputs(rows, classified, sources)
    for path, value in result_files.items():
        if options.verify or options.check_contracts:
            if not path.exists() or path.read_text(encoding="utf-8") != value:
                raise ValueError(f"Classification differs: {path}")
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(value, encoding="utf-8", newline="")
    print(json.dumps({"rowCount": summary["rowCount"], "unresolvedUnitCount": summary["unresolvedUnitCount"],
                      "groupCount": summary["groupCount"], "unresolvedGroupCount": summary["unresolvedGroupCount"],
                      "needsFollowUpGroupCount": summary["needsFollowUpGroupCount"],
                      "semanticClassificationComplete": False, "unresolvedQueueCounts": summary["unresolvedQueueCounts"]}))


if __name__ == "__main__":
    main()
