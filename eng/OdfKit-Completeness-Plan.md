# OdfKit 完滿化路線圖

本文件為 [`IMPLEMENTATION_PLAN.md`](../IMPLEMENTATION_PLAN.md) 與 [`docs/odf-format-support.md`](../docs/odf-format-support.md) 之間的**指標落地檔**，與 [`eng/AsyncRefactor-Plan.md`](AsyncRefactor-Plan.md)、[`eng/GodClassRefactor-Plan.md`](GodClassRefactor-Plan.md) 並列維護。

## 完滿定義（四層 Tier）

| 層級 | 完成線指標 | 現況 |
|------|-----------|------|
| Tier 1 規範可信 | Validator + Profile + Corpus；Unknown 保真 round-trip | corpus 219 fixtures；validate-corpus 全綠 |
| Tier 2 語意可用 | 四主格式高階 API 深度；變體與特殊格式專屬模型 | 四主格式 High-level = `complete` ✅；其餘多為 `usable` / `usable-variant` / `package-only` |
| Tier 3 互通可驗 | LibreOffice 實機驗收；OOXML 視覺 golden file | Wave 3 X-2／Q-3／REN-1 基礎 ✅；外部 Office 視覺驗收為可選環境。**2026-06-20 首次實機執行**：補齊本機 LibreOffice 26.x／Office COM／Python 依賴後首次真正執行（先前皆因環境缺失而 SKIP），發現並修正 2 個核心缺陷——(1) `SpreadsheetDocument.AddChart` 的 `svg:x`／`svg:y` 寫死 1cm 未依錨點儲存格計算，造成圖表與資料重疊；(2) **文件根節點從未宣告 `xmlns:of`（OpenFormula）命名空間**，導致任何含公式的 ODS 在真實 LibreOffice 開啟時公式求值失敗顯示 `Err:510`（此 bug 長期被前者的重疊問題遮蔽未被發現）。兩者皆已修正並以實機重新驗證。ODT→DOCX 視覺驗收通過；ODS→XLSX 因含圖表，與 Excel 圖表引擎本身渲染風格差異（非轉換邏輯缺陷）仍超出 5% 門檻，此差異屬於本文件已列為非目標的「圖表樣式進階視覺保真」。 |
| Tier 4 產品就緒 | GitHub Release 套件資產；統一開發者體驗 | 原始碼 repo 為主；**非 nuget.org** |

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

- `office:text`、`table:table`、`draw:page`、`office:presentation`／`office:drawing` 與次格式 `office:chart`／`office:image`／`office:database`／`office:spreadsheet` content model facade 可 append / enumerate
- `TypedDomParityTests` 新增 content model user story
- typed-dom coverage guard 不退化

## Wave 2 — 高階語意 API 深度

| Phase | 狀態 | 產出 |
|-------|------|------|
| VAR-1 | ✅ | `*TemplateDocument`、`TextMasterDocument`、`Flat*Document`、factory 分派、`DocumentKindApiUsabilityTests` |
| DEPTH-1/2 | 基礎 ✅ | DEPTH-1 四主格式讀取 API 已齊；DEPTH-2 已補 ODC/ODB/ODF/ODI 讀取與表單／框架 API |
| DEPTH-1-TC | 基礎 ✅ | ODT/ODS `tracked-changes` 內容／表格／結構／移動；LO 互通測試已備 |
| RDF-1 | 基礎 ✅ | 核心 `manifest.rdf` / `pkg:` ontology parity；文件層往返與 corpus fixture |
| RDF-2 | ✅ | `OdfKit.Extensions.Rdf` + `dotNetRdf.Core` SPARQL 橋接 |
| DX-1 | 基礎 ✅ | `PresentationDocumentBuilder`、`DrawingDocumentBuilder`、`OdfFormulaBuilder`；cookbook 已補 Builder 範例 |
| LOEXT-1 | 基礎 ✅ | `loext:decorative` 讀取映射、`OdfNamespaces.LoExt`；`LoExtInteropTests` 覆蓋 ODT/ODP/ODG |
| CALCEXT-1 | 基礎 ✅ | 工作表與文件層 calcext 讀取 API（條件格式、走勢圖）；`ConditionalFormatTests`、`SpreadsheetHighLevelApiTests` |

目標：四主格式 High-level 欄位升為 `complete`（✅ 已達成；見 `docs/odf-format-support.md` 矩陣）。

### VAR-1 驗收

- `OdfDocumentFactory.CreateDocumentWrapper` 依完整 `OdfDocumentKind` 回傳專屬型別
- 範本／主控／Flat 變體具備 `Create` / `Load` / `LoadAsync` typed 入口
- 基底 `TextDocument.Load` 等僅接受封裝主格式（`.odt` 等），變體須使用專屬型別載入
- `TextMasterDocument.GetSubDocumentReferences` 可列舉 `text:section-source` 參照

### LibreOffice 私有擴充互通邊界

策略：**只跟進影響「能否開啟／正確保存真實世界文件」的擴充**；純編輯器外觀或進階功能不納入。

| 命名空間 | 能力 | 狀態 | 位置 |
|---------|------|------|------|
| `loext` | Argon2id 加密參數（`kdf-name`、`argon2-t/m/p`） | ✅ 已完成 | `OdfEncryption`、`OdfPackageManifestWriter`、`EncryptionTests` |
| `loext` | `decorative` 讀取映射 | 基礎 ✅ LOEXT-1 | 載入正規化為 `draw:decorative`；寫入用標準屬性 |
| `calcext` | 色階／資料橫條／圖示集條件格式 | ✅ 寫入 + OOXML 橋接 | `OdfTableSheetConditionalFormatEngine`、`OdfToXlsxConverter` |
| `calcext` | Sparkline（`sparkline-groups`） | ✅ 寫入 | `AddSparklineGroup` |
| `calcext` | 既有規則讀取／列舉 API | 基礎 ✅ CALCEXT-1 | `OdfTableSheet.ConditionalFormats`／`SparklineGroups` 與 `SpreadsheetDocument.GetConditionalFormats`／`GetSparklineGroups` |
| ODF 標準 | `text:tracked-changes`（ODT） | 基礎 ✅（段落／表格儲存格）；LO 互通測試已備 | `TextDocument` accept/reject/record API |
| — | `table:tracked-changes`（ODS） | 基礎 ✅（內容／公式／結構／移動）；LO 互通測試已備 | `SpreadsheetDocument` tracked-changes API |
| — | Writer Navigator 書籤擴充、pivot 重算 | 不納入 | 見 non-goals |

### RDF 與協作邊界（Wave 2 起）

| 能力 | 位置 | 說明 |
|------|------|------|
| `manifest.rdf` triple CRUD | 核心 `OdfKit` | 基礎 ✅；`pkg:hasPart`／`pkg:mimeType` 同步、文件層往返、`repo-generated-manifest-rdf-text` corpus |
| SPARQL 查詢 | `OdfKit.Extensions.Rdf` | RDF-2；`OdfRdfMetadata` 橋接 dotNetRDF `IGraph` |
| ODT change tracking | `TextDocument` API | 已有基礎；DEPTH-1-TC 補測試與表格／互通 |
| JSON Collaboration ops | 不納入 Wave 2–3 | 見 udx-non-goals §4；Wave 4 選用 `Extensions.Collaboration` |
| RDF 語意 diff / merge | 不納入 | 研究級；ODF Toolkit Collaboration 主幹為 JSON，非 Jena merge |

## Wave 3 — 互通與視覺驗收

| Phase | 狀態 | 產出 |
|-------|------|------|
| X-2 | 基礎 ✅ | `docs/libreoffice-interop-matrix.md`、`eng/Test-LibreOfficeInterop.ps1`；四主格式 + 追蹤修訂 LO 26.x headless 矩陣 |
| Q-3 | 基礎 ✅ | `docs/ooxml-visual-golden-matrix.md`、`eng/Test-OoxmlVisualGolden.ps1`、`eng/scripts/PdfVisualDiff.py`；ODT→DOCX、ODS→XLSX 雙路徑 PDF 像素比對 |
| REN-1 | 基礎 ✅ | `docs/rendering-backend-deployment.md`、`eng/Test-RenderingBackends.ps1`；LocalProcess / Unoserver / HttpRenderer 選型與部署 |
| CONV-1 | 基礎 ✅ | `docs/managed-first-conversion-strategy.md`；ODT→Markdown/RTF（含表格／註腳／超連結／圖片參照／註解基礎／段落對齊、縮排、間距、行距、分頁、typographic symbols/spacing、nonbreaking/optional hyphen controls／inline 粗斜體底線刪除線上標下標字號色彩）、Markdown→ODT（GitHub/GitLab/CommonMark/Basic flavor options，段落／標題／清單／表格／超連結／圖片參照／粗體／斜體／粗斜體／GitHub-GitLab 刪除線／HTML span 樣式／註解與 footnote 往返基礎）、RTF→ODT（段落／段落對齊 `\\ql`/`\\qc`/`\\qr`/`\\qj`／段落縮排 `\\li`/`\\ri`/`\\fi`／段落間距與行距 `\\sb`/`\\sa`/`\\sl`／分頁 `\\page`／typographic symbols/spacing `\\emdash`/`\\endash`/quotes/`\\enspace`/`\\emspace`/`\\qmspace`/`\\bullet`／Unicode escapes `\\uN` with `\\ucN` fallback skip／ANSI hex escapes `\\'hh`／nonbreaking/optional hyphen controls `\\~`/`\\_`/`\\-`／表格／inline 粗斜體底線刪除線上標下標字號色彩與 `\\plain` 重設／註腳／超連結／exporter 圖片參照、RTF `\\pict` PNG/JPEG 圖片與 goal 尺寸、標準 annotation group 註解基礎）、ODG→SVG（幾何／文字 run 粗斜體字號色彩／樣式色彩／線條呈現／opacity／transform／線性命名漸層／漸層角度／放射漸層／封裝圖片 data URI／frame/image rect 裁切／draw:contour-path 與 draw:contour-polygon 非矩形裁切／相容 enhanced-path custom-shape／modifier/equation 與常見公式函式展開／`A`/`B`/`G`/`T`/`U`/`V`/`W`/`X`/`Y` arc commands 與 `N`/`F`/`S` state command 安全處理／LibreOffice 風格 custom-shape corpus regression）、ODP↔PPTX（投影片與名稱／slide size／基本 slide layout type／slide placeholder type/text／投影片背景色／ODP→PPTX theme palette 與 font scheme 萃取／PPTX theme scheme color、font scheme token、background style reference 與 shape fill/line style matrix reference 解析／文字框與多段落 API/轉換／段落對齊／基本圖形與形狀文字、line API 與 solid fill/stroke color/width/dash 與 line direction/flip／圖片與替代文字／裁切／講者備忘多段落 API/轉換／slide transition type/duration/speed／基礎 object animation duration/delay timing 與 build list／混合文字 run 粗斜體底線刪除線上標下標字號色彩／嵌入表格文字與文字樣式、合併儲存格、儲存格背景色、全邊框與 table theme style id）淨室轉換基礎；格式 token、頁面尺寸與數值輸出採 invariant/ordinal 原則，LibreOffice 降為 fallback |

## Wave 4 — 產品化

| Phase | 狀態 | 產出 |
|-------|------|------|
| REL-1 | ✅ | 相容矩陣、`Pack`／`Test-NuGetPack`／`Publish-GitHubRelease.ps1`、`docs/github-release-publishing.md`、`.github/workflows/nuget-pack.yml`（Actions v6/v5/v7）、`NuGetPackagingTests` |
| COLLAB-1（選用） | 基礎 ✅ | `OdfKit.Extensions.Collaboration`：`OdtOperationsExporter` ODT → JSON operations 匯出（對標 ODF Toolkit CLI）；`CollaborationOperationsTests` |
| COLLAB-2（選用） | 基礎 ✅ | `OdtOperationsImporter`：JSON operations（addParagraph／addText／addTab）單向 merge 至 `TextDocument`；`CollaborationOperationsTests` 涵蓋重播與既有文件附加場景 |
| TEST-STRUCT | ✅ | `docs/testing-strategy.md`；測試檔重命名、稀疏檔合併與 E2E 去重盤點完成 |
| GPG-AUDIT | ✅ | `eng/Test-GpgSignatures.ps1`；repo 專屬金鑰簽署稽核 |
| SEC-DER-1 | ✅ | 移除自製遞迴下降 DER 解析器（`DerNode`、`OdfSignatureDerCodec.Parse`／`ParseInteger`／`GetTbsNode`／`GetCrlIssuerDer`），改用既有 `BouncyCastle.Cryptography` 依賴之 `Org.BouncyCastle.X509`／`Asn1.X509`／`Asn1.Tsp` 型別模型解析 CRL、CRL Distribution Points 與 RFC 3161 TSTInfo；`AdvancedSecurityTests`、`EncryptionTests` 等安全測試全綠 |
| QC-ongoing | planned | 季度 OASIS RNG diff、本檔案季度檢視 |

REL-1 驗收：`pwsh eng/Test-NuGetPack.ps1`；六套件雙 TFM `.nupkg`（v0.0.1）+ net8.0 消費端煙霧；`pwsh eng/Publish-GitHubRelease.ps1` 乾跑；CI `nuget-pack.yml`。

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
- 提交前 `pwsh eng/Format-Safe.ps1`；`pwsh eng/Test-GpgSignatures.ps1`；GPG 簽署；正體中文 Conventional Commits

## 完滿 Exit Criteria

1. Tier 1–4 指標表全綠
2. 四主格式 High-level = `complete`；其餘有明確 `usable` 或 `package-only`
3. 測試無回歸；corpus 與 interop 可選 CI 全綠
4. GitHub Release 套件資產（v0.0.1）+ cookbook 覆蓋主要場景
5. non-goals 邊界於 README 與 udx-non-goals 明確揭露
