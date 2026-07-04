# Changelog

本檔案依 [Keep a Changelog](https://keepachangelog.com/) 慣例，記錄 OdfKit 對外可見的重大里程碑。

## [0.0.1] - 未發行（GitHub Release 資產，非 nuget.org）

### 新增

- **核心 ODF 支援**：24 種主要 ODF extension（ODT/ODS/ODP/ODG 及其範本、母片、Flat XML、次格式變體）之偵測、建立、載入、保存、驗證與 round-trip。
- **四主格式高階 API**：ODT、ODS、ODP、ODG 已達 `complete` 分級，涵蓋常用建立、編輯、樣式、公式、加密、追蹤修訂、條件格式、樞紐分析表等場景。
- **規範可信度**：ODF 1.1/1.2/1.3/1.4 官方 RELAX NG schema 驗證、profile 規則（OASIS Strict/Extended、ISO/IEC 26300、EU、ROC Taiwan）、266 筆 corpus fixtures。
- **安全性**：PBKDF2（≤ 50,000 次迭代）、Argon2id、OpenPGP（RSA/ElGamal/ECDH X25519 與傳統曲線）加密；XAdES 數位簽章與時間戳記驗證；XXE／Zip Slip／OOM DoS 防禦。
- **轉換與互通**：
  - OOXML：ODT↔DOCX、ODS↔XLSX（含具名段落／字元樣式、公式、圖表）。
  - PresentationML：ODP↔PPTX（投影片、主題色票、表格、動畫時間軸與 build list）。
  - Managed-first 淨室轉換：ODT↔Markdown／RTF、ODG→SVG，LibreOffice 降為 fallback。
  - LibreOffice headless 互通矩陣（26.x）、OOXML 視覺 golden file 比對。
- **協作格式**：ODT ↔ JSON operations 雙向轉換（對標 ODF Toolkit CLI，`OdfKit.Extensions.Collaboration`）。
- **RDF／中繼資料**：`manifest.rdf` triple CRUD 與 SPARQL 查詢橋接（`OdfKit.Extensions.Rdf`）。
- **效能**：`OdsStreamWriter` 串流寫入記憶體佔用 < 1MB；公式剖析採 `ref struct` + `ReadOnlySpan<char>` 零配置設計；XML 標籤字串池化；ZIP 載入 `ArrayPool` 緩衝。
- **泛型物件序列匯出**：新增 `ObjectDataReader<T>`（將任意 `IEnumerable<T>`／`IAsyncEnumerable<T>` 轉接為 `DbDataReader`）與對應的 `OdsStreamWriter.WriteDataAsync<T>` 多載，可將任意物件序列（例如 Entity Framework Core `IQueryable<T>.AsNoTracking().Select(...).AsAsyncEnumerable()` 查詢投影）低記憶體串流匯出成 ODS，亦可與 `SqlBulkCopy` 等外部 `DbDataReader` 消費者互通；核心不因此新增任何外部 ORM 或資料庫套件相依。
- **套件與發行**：8 個套件（`OdfKit` 核心 + 7 個 `OdfKit.Extensions.*`）雙 TFM（`net10.0` + `netstandard2.0`）NuGet 封裝，透過 GitHub Release 資產發佈（非 nuget.org）。

### 架構

- 採用協作者抽取模式拆分上帝類別。
- 所有公開 `*Async` 方法統一帶 `CancellationToken cancellationToken = default`。
- 測試套件依分層命名規則整理，移除歷史開發階段命名與重複測試檔。

### 修正

- 修正 `OdsStreamWriter.SwitchToSheet` 緩衝寫入路徑產生結構錯誤 `content.xml` 的問題（`<office:spreadsheet` 起始標籤未透過同一個 `XmlWriter` 正確關閉即被後續原始位元組覆蓋，導致無法被嚴格 XML 剖析器讀回）；改為統一透過 `XmlWriter.WriteRaw` 寫入緩衝工作表片段，並補上以 `OdsStreamReader` 嚴格剖析回讀的迴歸測試。
- 修正 `OdfDirectIoReadableStream.Dispose` 未等待背景預讀工作（`_prefetchTask`）完成即釋放原生檔案控制代碼／對齊緩衝區的資源生命週期競爭，改為先取出並等待該工作，再釋放底層資源。
- 修正 `OdfTableSheetRepeatSplitEngine.GetRepeatCount` 未對 `number-rows-repeated`／`number-columns-repeated` 設上限的問題，改為與 `OdsStreamReader` 一致地截斷至 1,048,576／16,384，避免文件宣告超大重複計數被呼叫端當成迴圈上限而放大為阻斷服務風險。
- 修正 `FormulaParser.ParsePower` 對 `^` 運算子左結合與優先序的處理，讓其符合 OpenFormula 規範（`2^3^2` 應為 `64`，且 `-2^2` 應視為 `(-2)^2 = 4`）；並修正連續前置一元運算子（如 `--2`）因遞迴解析誤改而無法剖析的問題。
- 修正 `OdfPackageEntry.SetContent(byte[])` 未釋放先前指派之 `Stream` 內容的資源洩漏問題，改為與 `SetContent(Stream)` 一致，於覆蓋內容前先行釋放。
- 修正 `OdfPackageFlatXmlLoader`／`OdfStreamingMailMerge` 於修正整數溢位風險時，意外將 `MaxTotalUncompressedSize = 0` 的語意由「拒絕任何非空內容」改為「不限制」，與 `OdfPackageZipLoader` 及既有慣例不一致的問題。
- 修正 `FormulaParser.ParseFactor` 解析 `*`／`/` 右運算元時未延伸至乘方層級，導致 `^` 出現在因數運算右側時剖析失敗（如 `2*3^2`）的問題。
- 修正 `FormulaStringFunctionHandlers.EvaluateSubstitute` 於搜尋文字為空字串時，未帶出現次數引數會擲出未處理的 `System.ArgumentException`、帶出現次數引數則計數邏輯失真的問題，改為直接回傳 `#VALUE!` 診斷。
- 修正 `OdfTableSheetVisibilityEngine.IsRowVisible`／`IsColumnVisible` 未透過 `OdfTableSheetRepeatSplitEngine.GetRepeatCount` 截斷重複計數上限的問題，避免惡意宣告超大重複計數導致索引整數溢位。
- 修正 `OdfPackageEntry.OpenReader()` 對以 `Stream` 支援內容的專案直接回傳內部共用資料流本體，導致該資料流於呼叫端 `using` 區塊結束後即被釋放、往後任何存取皆擲出 `ObjectDisposedException` 的問題，改為回傳不會連動釋放底層資料流的包裝資料流。
- 統一簽章描述檔路徑 `META-INF/documentsignatures.xml` 參照至既有的 `OdfSignerConstants.SignaturePath` 常數，避免多處獨立硬編碼字面值於日後路徑調整時各自失步。
- 修正 `OdfBouncyCastleOpenPgpProvider` 兩處硬編碼中文例外訊息未透過 `OdfLocalizer.GetMessage` 在地化的問題。
- 修正 `OdfChartDocument.GetPositiveRepeatCount` 未截斷嵌入圖表本地資料表重複計數上限的問題，改為與 `OdfSpreadsheetLimits.CsvMaxRepeat`／`FormulaMaxRepeat` 一致地截斷至 10,000。
- 修正 `OdfDrawPageShapeReadEngine.CollectGroupsRecursive` 遞迴走訪 `draw:g` 群組無深度上限的問題，比照 `OdfDatabaseDocument` 既有的巢狀深度防護慣例，於超過 64 層時擲出可攔截的例外，避免惡意或損毀文件觸發 `StackOverflowException`。
- 修正 `SpreadsheetDocumentEmbeddedChartReadEngine.TryReadChartMetadata` 於任何大小限制生效前即以 `ReadToEnd()` 無界讀入嵌入圖表 `content.xml` 的問題，改為透過 `OdfBoundedStreamReader` 以 `OdfLoadOptions.MaxEntrySize` 為上限邊界複製。
- 修正 `PptxToOdpConverter.ConvertGraphicFrame` 未檢查 PPTX 表格儲存格自帶的 `RowSpan`／`GridSpan` 是否與實際表格列欄數一致，格式不一致時會擲出 `ArgumentOutOfRangeException` 中止整個轉換的問題，改為依實際表格邊界夾限合併範圍。
- 統一媒體項目路徑前綴 `"Pictures/"` 參照至既有的 `OdfMediaManager.PicturesEntryPrefix` 常數，並修正多處大小寫比對不一致（`StringComparison.OrdinalIgnoreCase` 與 `Ordinal` 混用）的問題。
- 修正 `OdfToDocxConverter.LoadStylesEntry` 未設定 `MaxCharactersInDocument` 的問題，與同專案內 `OdfToXlsxConverter` 保持一致。
- 修正 `OdfComment.FromXmlNodeSingle` 在節點具有 `dc:date` 屬性時，會跳過解析 `dc:creator`／`text:p` 子節點導致註解作者與內容遺失的問題。
- 修正 `OdtStreamReader.CaptureCurrentElement` 一律以 Text 命名空間讀取 `style-name` 屬性，導致表格儲存格（`table:table-cell`）樣式名稱讀取失敗的問題，改為依節點型別選用 Table 或 Text 命名空間。
- 修正 `OdfPackageEntryAccessEngine.ExtractObjectStream` 於內嵌物件名稱含結尾斜線時，串接出雙斜線路徑（如 `Object 1//content.xml`）導致無法讀取內嵌物件內容的問題。
- 修正 OpenPGP PKESK 封包解析在惡意／毀損輸入下擲出型別不一致例外的問題，並新增隨機化邊界測試取代外部模糊測試工具鏈。
- 修正 `OdfKit.Extensions.Rendering`／`OdfKit.Extensions.Imaging`／`OdfKit.Extensions.Rdf` 等擴充套件之 REST 重試緩衝重用、SKTypeface 資源釋放、RDF 相對 IRI 解析等缺陷。
- 修正資料庫（`OdfKit.Extensions.*` Database 相關）表單元件遞迴深度未設上限與重複鍵檢查缺失的問題。
- 修正 DOM 註解／CDATA 節點掃描邏輯，改依終止符掃描避免誤判；修正合規性掃描器（Compliance）略過規則時未回報、及掃描後未還原串流位置的問題。
- 修正批次套印（Mail Merge）改為真正非同步執行並修正首筆資料遺失的問題；修正 OOXML 公式翻譯過程誤改寫字串常數內容的問題。
- 強化 `OdfSignatureSigner`／`OdfSignatureVerifier` 之 OpenPGP 金鑰抹除與 XAdES 節點走訪安全性；強化封裝（`OdfPackage`）輸入安全與完整性、排序驗證。
- 修正 CSV 匯出（`OdfCsvExporter`）未防範 CSV 公式注入（CSV Injection）的問題，依 OWASP 建議對以 `=`、`+`、`-`、`@` 開頭之文字值加上單引號前綴（新增 `OdfCsvOptions.SanitizeFormulas`，預設啟用）。
- 修正 `OdfPdfExporter` 未釋放 `PdfDocumentRenderer.PdfDocument` 造成資源洩漏的問題。
- 修正 `OdfSlide.AddEmbeddedObject` 未將反斜線正規化為正斜線，導致內嵌物件 `href` 不符合 ODF 封裝路徑規範的問題。
- 修正 `OdfChartDocument` 讀取圖表序列（series）時，若缺少 `values-cell-range-address` 屬性即整筆略過，導致採用內嵌圖表資料的序列完全遺失的問題（`OdfChartSeriesInfo.ValuesCellRangeAddress` 隨之改為可為 `null`）。
- 修正 `AdvancedSecurityTests` 中 5 個測試方法直接對 `ErrorMessage` 斷言英文子字串、未強制文化特性，導致系統語系為 zh-TW 等非英文環境時測試失敗的問題，比照既有 `SecurityComplianceTests` 慣例暫時切換至 `en-US` 文化特性；另發現並修正僅切換 `Thread.CurrentThread.CurrentCulture`／`CurrentUICulture` 於完整測試套件中仍不穩定的問題——`OdfLocalizer.GetMessage` 實際優先採用靜態的 `OdfLocalizer.DefaultCulture`（會被 `EncryptionTests`／`OdfValidationReportTests` 等其他測試類別設定後即不再還原），因此改為同時暫存並還原 `OdfLocalizer.DefaultCulture`，確保不受其他測試執行順序影響。
- 修正 `CliTests`／`DomTest`／`EncryptionTests`／`LibreOfficeRendererBoundaryTests`／`LibreOfficeRendererDiagnosticsTests`／`OdfValidationReportTests`／`PresentationAndRenderingTests` 共 7 個測試類別於建構子設定全域靜態的 `OdfLocalizer.DefaultCulture` 後從未還原、污染同一測試行程後續測試的問題，改為實作 `IDisposable`，暫存原始值並於 `Dispose()` 還原。
- 統一 XAdES 命名空間 URI `http://uri.etsi.org/01903/v1.3.2#`（原於 `OdfSignatureSigner`／`OdfSignatureX509Utilities`／`OdfSignatureVerifier` 共 23 處硬編碼字面值）至 `OdfNamespaces.Xades` 常數。
- 合併 `OdfPackageFlatXmlLoader`／`OdfStreamingMailMerge`／`XlsxToOdfConverter`／`UnoserverRestBackend` 各自獨立實作的溢位安全大小檢查邏輯，統一改為呼叫 `OdfBoundedStreamReader.AddBytes`／`EnsureInitialBytes` 的既有多載或新增的 `exceptionFactory` 多載。
- 移除 `OdfDatabaseFormDesigner` 與 `OdfNamespaces` 重複宣告的 5 個命名空間常數，改為直接參照 `OdfNamespaces` 既有常數。
- 修正 `OdfBorder.Parse` 遇到格式不正確的 `#RRGGBB` 色彩片段時靜默退回黑色、掩蓋損毀樣式資料的問題，改用 `OdfColor.TryParse` 驗證格式，格式不正確時記錄診斷警告並略過該色彩片段。
- 重構 `OdfMediaManager.DetectImageFormat` 由循序 if-else 鏈改為資料驅動的 magic bytes 比對表格，行為不變。
- 合併 `OdfSignatureVerifier`（`.Dsig.cs`／`.Revocation.cs`／`.Timestamp.cs`）中重複的「設定 ErrorCode／ErrorMessage／Warnings 並回傳 false」錯誤處理樣式為共用私有輔助方法，並保留控制流程互異（`throw`、迴圈 `break`、條件式覆寫）的呼叫點不變。
- 於 `OdfLength` 新增 `FromEmu`／`ToEmu` 與 `EmusPerInch` 常數，集中 OOXML EMU 單位換算的推導來源；`DocxToOdtConverter`／`PptxToOdpConverter`／`OdpToPptxConverter`／`OdfToDocxConverter` 中原各自獨立推導、數學上等價的 EMU 換算常數與運算，統一改為參照此單一來源。
- 抽取 `OdfFormulaLatexConverter.AppendAtom` 中 `munderover`／`munder`／`mover`／`msubsup`／`msub`／`msup` 六個分支重複的「將子節點包入 `<mrow>` 並輸出標籤對」邏輯為區域函式，判斷樹與 MathML 輸出語意不變。
- 拆分 `OdfPackageFlatXmlLoader.Initialize`：將巢狀內嵌文件抽取邏輯抽出為 `ExtractNestedDocuments`，將 content／styles／meta／settings 四棵 `XElement` 樹的切分邏輯抽出為 `SplitDocumentSections`，核心 XML 串流剖析迴圈維持不變。

