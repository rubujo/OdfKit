# API Surface Consistency

本文件定義 OdfKit 公開 API 的長期命名與分層規則。它是實作與 review
時判斷「是否需要一級 C# 物件模型」的依據，並同步排除專案既有文件已確認
不是核心目標的項目。

目前靜態盤點結果見 `docs/api-surface-inventory.md`。

## 1. API 分層

| Level | 名稱 | 完成線 | 主要入口 |
|------|------|--------|----------|
| L0 | Package API | 所有 ODF package entry、manifest、media、RDF 與 unknown entry 可存取、保存與 round-trip。 | `OdfPackage`、`OdfDocument` |
| L1 | Typed DOM API | 所有 ODF 1.4 schema element / attribute 可透過 generated wrapper、typed attribute helper 或 schema-aware DOM 存取。 | `OdfNode`、`OdfElement`、generated DOM wrappers、`OdfTypedDomCoverage` |
| L2 | Semantic Facade API | 常見文件工作流程具備高階 C# facade，不需要呼叫端理解 XML 細節。 | `TextDocument`、`SpreadsheetDocument`、`PresentationDocument`、`DrawingDocument`、Chart / Formula / Image / Database facade |
| L3 | External Engine Boundary | 需要辦公軟體行為、外部環境或不可穩定 managed 化的能力，只能作為 extension 或 optional validation。 | `OdfKit.Extensions.Rendering`、LibreOffice / Office interop scripts |

「所有 ODF 功能可用 C# 存取」表示 L0 + L1 必須完整；「所有常見工作流程有
一級 C# 物件模型」表示 L2 必須覆蓋高價值使用情境。L3 不屬於核心 API
完成條件。

## 2. 命名契約

新增或 breaking rename 公開 API 時，請使用以下規則；本專案尚未正式發佈
package，因此允許一次性 refactor repo 內所有使用點，不保留舊名稱 shim。

| Pattern | 用途 | 回傳語意 |
|---------|------|----------|
| `Create` / `Load` / `Save` / `Validate` | 文件生命週期。 | 依既有文件 API 慣例。 |
| `Builder()` | Fluent 建立入口。 | 回傳 builder；builder 使用 `With*` 設定狀態、`Add*` 新增內容。 |
| `Add*` | 新增節點、實體或 relation。 | 回傳新增項目或 `void`，不得表示查找。 |
| `Get*` | 讀取 snapshot、summary、集合或不可變資訊。 | 集合讀取統一使用 `Get*` 或屬性。 |
| `Set*` | 設定或覆寫單一狀態。 | 回傳更新後 facade 或 `void`。 |
| `Update*` | 依 callback、request 或批次規則修改既有內容。 | 回傳變更數量或更新結果。 |
| `Remove*` | 移除指定內容。 | 不存在時優先回傳 `false`；輸入無效才拋例外。 |
| `Find*` | 查找單一可選項。 | 找不到回傳 `null`。不得用於集合。 |
| `Try*` | 嘗試取得或執行可能失敗的操作。 | 回傳 `bool`，搭配 `out` 或明確結果。 |

`List*` 不作為新的公開 API 命名。若需要列出集合，使用 `Get*` 或唯讀集合屬性。

低階 DOM / typed attribute accessor 可保留 `Get*`，即使回傳 nullable；這些 API
描述的是「讀取目前節點或屬性的值」，不是依 domain key 查找 semantic object。
例如 `OdfNode.GetAttribute`、`OdfElement.Get*AttributeValue` 與 generated wrapper
屬性 setter 內部使用的 remove helper，不適用 L2 facade 的 `Find*` 規則。

`Clear*` 表示清空一段可重建狀態或重設目前設定，可維持 no-op 命令語意與
`void` 回傳；若 API 需要表達「移除某個指定項目是否成功」，應提供或改用
`Remove*`，並遵守 `bool` 成功/失敗語意。

Database 與 Image 目前不強制補 `Builder()` 入口。Database 的主要 L2 workflow
是 table / query / form / report CRUD 與 schema 設定；Image 的主要 L2 workflow
是載入、框架、濾鏡與圖片 bytes 變更。這兩者沒有足以降低複雜度的常見 fluent
建立流程時，可保留 `Create` / `Load` / `Set*` / `Add*` / `Find*` 組合。

### 已套用的 breaking rename

第一批已將集合型 `Find*` 改為 `Get*`，以符合「`Find*` 只回傳單一可選項」：

| 範圍 | 新名稱 | 理由 |
|------|--------|------|
| Schema name-class collection query | `GetMatchingNameClasses` | 回傳多個 name class。 |
| RDF triple collection query | `GetTriples` | 回傳多個 RDF triple。 |
| Math token collection query | `GetAll` | 回傳多個 Math token。 |
| Workbook formula-cell predicate query | `GetFormulaCells` overload | 回傳多個公式儲存格。 |
| Worksheet formula-cell predicate query | `GetFormulaCells` overload | 回傳多個公式儲存格。 |
| Tracked-change affected-node query | `GetAffectedNodesForFormatChange` | 回傳多個 DOM 節點。 |

第二批已將語意明確的單一 nullable lookup 從 `Get*` 改為 `Find*`：

| 範圍 | 新名稱 | 理由 |
|------|--------|------|
| Workbook sheet lookup by name | `FindSheet` | 依名稱查找單一工作表，找不到回傳 `null`。 |
| Formula annotation lookup by encoding | `FindAnnotation` | 依 encoding 查找單一 MathML annotation，找不到回傳 `null`。 |
| Spreadsheet cell annotation lookup | `FindAnnotation` | 查找單一儲存格批注，找不到回傳 `null`。 |

第三批已統一指定項目移除 API 的成功/失敗語意：

| 範圍 | 回傳語意 |
|------|----------|
| Package entry removal | 移除 entry、manifest 項目或 entry order 項目時回傳 `true`。 |
| DOM attribute / child removal | 目標存在且已移除時回傳 `true`；目標不屬於目前節點或不存在時回傳 `false`。 |
| Spreadsheet hyperlink / annotation removal | 移除既有內容時回傳 `true`；沒有可移除內容時回傳 `false`。 |
| Spreadsheet row / column page break removal | 目標分頁符存在且已移除時回傳 `true`；不存在時不建立新節點並回傳 `false`。 |
| Presentation placeholder removal | 至少移除一個指定型態的 placeholder 時回傳 `true`。 |

第四批已將依 key、name 或 dimension 查找單一 nullable 項目的 `Get*` 改為 `Find*`：

| 範圍 | 新名稱 |
|------|--------|
| Database query optional children | `FindQueryOrderStatement`、`FindQueryFilterStatement`、`FindQueryUpdateTable` |
| Chart series optional children | `FindDataLabels`、`FindErrorIndicator`、`FindRegressionCurve`、`FindMeanValue` |
| Chart document optional lookups | `FindSeriesDataLabels`、`FindAxisInfo`、`FindAxisTitle` |
| Presentation page layout lookup | `FindPresentationPageLayout` |
| Image frame filter lookup | `FindImageFilter` |
| Package entry encryption lookup | `FindEntryEncryptionInfo` |
| Custom metadata property lookup | `FindCustomProperty` |

## 3. 明確排除的目標

下列項目已由專案文件確認不屬於核心 API 目標，不應因 API 一致性工作而被
重新納入：

- 完整物理分頁排版器、像素級高保真渲染與所有 LibreOffice 版本的像素一致性。
- 試算表完整公式重算、樞紐表重算、volatile function、多使用者計算狀態與任意外部連結資料來源。
- SmartArt 智慧圖形、複雜形狀布局器與 Office 專屬 layout 相容層。
- 完整多人協同演算法、任意衝突合併、undo stack、OT、CRDT、drawing operation、動態表格擴張與 header/footer/note selection 完整語意。
- 通用 RELAX NG validator；核心 validator 只服務內建 ODF profile gate。
- 外部 Office / LibreOffice / PDF pixel diff / large corpus 驗收進入主 CI Smoke。
- 不可再散布 corpus、商業 SDK 輸出或外部專案原始碼作為 golden。

相關邊界來源：`docs/udx-non-goals.md`、`docs/rendering-backend-deployment.md`、
`docs/libreoffice-interop-matrix.md`、`docs/ci-cd.md`、
`docs/provenance/clean-room-source-index.md`。

## 4. 文件與相容性要求

- 每次 breaking rename 必須同步更新 repo 內所有使用點，包含 tests、samples、docs、
  tools、CLI 與 extensions。
- 若舊名稱存在於 corpus fixture、JSON wire shape 或外部格式資料中，不改資料格式；
  只改 C# API。
- 手寫 public / protected API 必須具備雙語 XML 文件。generated DOM wrapper 可由
  generator 層統一豁免或改善，不進行逐檔手改。
- 新 API 不得以 `#pragma warning disable 1591` 規避文件要求。
