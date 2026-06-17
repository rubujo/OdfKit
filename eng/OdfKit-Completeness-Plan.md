# OdfKit 完滿化路線圖

本文件為 [`IMPLEMENTATION_PLAN.md`](../IMPLEMENTATION_PLAN.md) 與 [`docs/odf-format-support.md`](../docs/odf-format-support.md) 之間的**指標落地檔**，與 [`eng/AsyncRefactor-Plan.md`](AsyncRefactor-Plan.md)、[`eng/GodClassRefactor-Plan.md`](GodClassRefactor-Plan.md) 並列維護。

## 完滿定義（四層 Tier）

| 層級 | 完成線指標 | 現況 |
|------|-----------|------|
| Tier 1 規範可信 | Validator + Profile + Corpus；Unknown 保真 round-trip | corpus 219 fixtures；validate-corpus 全綠 |
| Tier 2 語意可用 | 四主格式高階 API 深度；變體與特殊格式專屬模型 | 17 格式高階 API 多為 `usable` / `package-only` |
| Tier 3 互通可驗 | LibreOffice 實機驗收；OOXML 視覺 golden file | 自動化有、外部視覺驗收未完成 |
| Tier 4 產品就緒 | NuGet 發佈；統一開發者體驗 | 原始碼 repo 為主 |

明確非目標維持 [`docs/udx-non-goals.md`](../docs/udx-non-goals.md)：物理分頁引擎、樞紐重算引擎、SmartArt 佈局器、JSON Collaboration operations merge。

## Wave 1 — 規範可信度封頂（✅ 已完成）

| Phase | 狀態 | 產出 |
|-------|------|------|
| QC-3 Corpus 擴充 | ✅ | `tools/OdfCorpusGenerator`、219 fixtures（19 手工 + 200 bulk） |
| M-3 Content Model Facade | ✅ | `OdfElementContentModel*.cs`、`TypedDomParityTests` |
| DOC-1 指標文件 | ✅ | 本檔案、`docs/odf-format-support.md` 分級重寫 |

### QC-3 驗收

- `validate-corpus tests/fixtures/corpus/manifest.json` 全綠
- repo 內 fixtures ≥ 200（保留 19 個手工 fixture + bulk generated）
- `baselineMismatchCount` 僅允許 documented exception

### M-3 驗收

- `office:text`、`table:table`、`draw:page` content model facade 可 append / enumerate
- `TypedDomParityTests` 新增 content model user story
- typed-dom coverage guard 不退化

## Wave 2 — 高階語意 API 深度

| Phase | 產出 |
|-------|------|
| VAR-1 | `*TemplateDocument`、`MasterDocument`、`Flat*Document` |
| DEPTH-1/2 | ODT/ODS/ODP/ODG + ODC/ODF/ODI/ODB checklist 驅動 API |
| DEPTH-1-TC | ODT `text:tracked-changes` 完備化（接受／拒絕、表格層、LO 互通） |
| RDF-1 | 核心 `manifest.rdf` / `pkg:` ontology parity（不依賴外部 RDF 引擎） |
| RDF-2 | `OdfKit.Extensions.Rdf` + `dotNetRdf.Core` SPARQL 橋接（選用） |
| DX-1 | Presentation/Drawing Builder、`OdfFormulaBuilder`、cookbook |

目標：四主格式 High-level 欄位升為 `complete`。

### RDF 與協作邊界（Wave 2 起）

| 能力 | 位置 | 說明 |
|------|------|------|
| `manifest.rdf` triple CRUD | 核心 `OdfKit` | 已實作；RDF-1 深化 `pkg:` 語意與 corpus 對照 |
| SPARQL 查詢 | `OdfKit.Extensions.Rdf` | RDF-2；`OdfRdfMetadata` 橋接 dotNetRDF `IGraph` |
| ODT change tracking | `TextDocument` API | 已有基礎；DEPTH-1-TC 補測試與表格／互通 |
| JSON Collaboration ops | 不納入 Wave 2–3 | 見 udx-non-goals §4；Wave 4 選用 `Extensions.Collaboration` |
| RDF 語意 diff / merge | 不納入 | 研究級；ODF Toolkit Collaboration 主幹為 JSON，非 Jena merge |

## Wave 3 — 互通與視覺驗收

| Phase | 產出 |
|-------|------|
| X-2 | LibreOffice interop 矩陣 + `eng/Test-LibreOfficeInterop.ps1` |
| Q-3 | OOXML golden file 視覺 diff |
| REN-1 | 渲染 backend 部署指南 |

## Wave 4 — 產品化

| Phase | 產出 |
|-------|------|
| REL-1 | NuGet 穩定版、雙 TFM 相容矩陣 |
| COLLAB-1（選用） | ODT → JSON operations 匯出（對標 ODF Toolkit CLI） |
| COLLAB-2（選用） | JSON operations → ODT 單向 merge + golden file 對照 |
| QC-ongoing | 季度 OASIS RNG diff、本檔案季度檢視 |

## 高階 API 分級定義

| 分級 | 意義 |
|------|------|
| `complete` | 日常辦公自動化不需下沉 DOM；有 scenario 測試背書 |
| `usable` | 常用建立／編輯 API 可用，但仍有明確語意缺口 |
| `package-only` | 可建立、載入、保存、驗證；高階語意模型共用或尚未專屬化 |

詳細矩陣見 [`docs/odf-format-support.md`](../docs/odf-format-support.md)。

## 全程約束

- 協作者提取優先（[`eng/GodClassRefactor-Plan.md`](GodClassRefactor-Plan.md)）
- 公開 `*Async` 必須 `CancellationToken cancellationToken = default`（[`eng/AsyncRefactor-Plan.md`](AsyncRefactor-Plan.md)）
- 提交前 `pwsh eng/Format-Safe.ps1`；GPG 簽署；正體中文 Conventional Commits

## 完滿 Exit Criteria

1. Tier 1–4 指標表全綠
2. 四主格式 High-level = `complete`；其餘有明確 `usable` 或 `package-only`
3. 測試無回歸；corpus 與 interop 可選 CI 全綠
4. NuGet 穩定版 + cookbook 覆蓋主要場景
5. non-goals 邊界於 README 與 udx-non-goals 明確揭露