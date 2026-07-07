# API Surface Layers

本文件描述 OdfKit 公開 API 的分層。它不是逐項 API 清單，而是協助使用者與維護者判斷「應該從哪一層開始」以及「新增 API 時應放在哪一層」。

## 分層總覽

| Layer | 主要命名空間 / 型別 | 使用者 | 穩定性期待 |
|-------|----------------------|--------|------------|
| L1 Document facade | `TextDocument`、`SpreadsheetDocument`、`PresentationDocument`、`DrawingDocument`、`OdfDocument` | 一般應用程式與 SDK 使用者 | 最重視可讀性、範例與相容性 |
| L2 Domain builders | `*.Builder()`、domain-specific builder / facade | 建立中高複雜度文件的使用者 | 保持 fluent workflow 清楚，避免暴露封裝細節 |
| L3 Streaming and data | `OdsStreamWriter`、`OdsStreamReader`、`ObjectDataReader<T>` | 批次匯出、資料管線、低記憶體場景 | 重視效能、配置量、取消與資源釋放 |
| L4 Package and DOM | `OdfPackage`、`OdfPackageEntry`、`OdfNode`、`OdfElement` | 需要保留未知內容或做低階互通的進階使用者 | 保留 round-trip 行為與 XML/ZIP 安全邊界 |
| L5 Compliance and diagnostics | `OdfValidator`、`OdfValidationReport`、`OdfLocalizer`、diagnostics types | 驗證、CI、匯入閘門 | 診斷資料應穩定且可機器讀取 |
| L6 Security and signatures | `OdfLoadOptions`、`OdfSaveOptions`、`OdfSigner`、crypto providers | 加密、簽章、安全敏感工作流 | 預設安全，錯誤訊息在地化，取消語意明確 |
| L7 Extensions | `OdfKit.Extensions.*` | 需要 HTML、PDF、OOXML、Rendering、RDF、Collaboration 的使用者 | 與核心解耦，避免把 runtime-heavy 相依帶入核心 |
| L8 Tools and engineering | `tools/OdfKit.Cli`、`eng/*.ps1`、benchmarks | 維護者、CI、發佈流程 | 可重跑、可稽核，避免本機狀態污染 repo |

## 建議使用路徑

新使用者應從 L1 開始：`TextDocument.Create()`、`SpreadsheetDocument.Create()` 或 `OdfDocument.Load()`。只有在需要大量資料匯出時，才直接使用 L3 streaming API；只有在需要保留或修改未知 ZIP/XML 內容時，才進入 L4。

## 新 API 放置準則

| 情境 | 應放層級 | 命名提示 |
|------|----------|----------|
| 常見文件操作，一般使用者應該看得懂 | L1 / L2 | 使用 domain vocabulary，例如 `AddHeading`、`FindSheet` |
| 大量資料匯入/匯出或不建 DOM 的流程 | L3 | 明確標示 streaming、buffering、ordering 限制 |
| ZIP entry、manifest、raw XML 或 unknown content | L4 | 保留 ODF/ZIP 語意，不把低階行為包裝成高階承諾 |
| 驗證、report、policy 或 corpus 診斷 | L5 | 優先結構化資料，避免只回傳字串 |
| 加密、簽章、外部資源或不可信輸入 | L6 | 預設防禦式設定，例外訊息使用 `OdfLocalizer` |
| 需要 LibreOffice、PDF、OOXML 或大型第三方相依 | L7 | 放在 extension，不進核心套件 |

## 命名契約摘要

- `Find*`：單一 nullable lookup。
- `Get*`：非 nullable 讀取、集合 snapshot、或狀態查詢。
- `Add*`：新增項目並通常回傳新建物件或 fluent context。
- `Remove*`：移除指定項目；指定項目移除 API 優先回傳 `bool`。
- `Clear*`：清空一組狀態，通常 no-op 安全。
- `Load*` / `Save*`：封裝或文件生命週期操作；async overload 必須支援 `CancellationToken`。

詳細盤點請見 [API Surface Inventory](api-surface-inventory.md) 與 [API Surface Consistency](api-surface-consistency.md)。

## 文件品質要求

L1 到 L6 的 public / protected API XML 文件應優先避免模板句，例如 `Provides the ... API` 或 `Executes the ... operation`。摘要應描述使用者可觀察的行為、輸入輸出語意或安全限制。新增 API 時，同步補齊英文與正體中文說明。
