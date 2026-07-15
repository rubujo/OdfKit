# 變更紀錄

本檔案依 [Keep a Changelog](https://keepachangelog.com/) 慣例，記錄 OdfKit 對外可見的重大里程碑。

## 尚未發佈

- 效能：`SegmentText` 導入每呼叫平面字型快取，逐字元的內建規則鏈評估（多次 `Contains`）攤提為每平面一次；125k 字元混排基準下，fall-through 家族（如 Noto Sans）由 362ms 降至 10ms（−97%）、MingLiU −76%、TW-Kai −32%，純 BMP 快路徑維持零額外配置。
- 新增全字庫（CNS 11643 open data）整合：`OdfCns11643MappingTable` 官方對照表解析與聯結、`OdfBig5EEncoding` 資料驅動 Big5E 編碼（可餵入 CSV 匯入匯出）、`OdfDocument.MigrateTextCodePoints` 文件碼位遷移（舊版全字庫 PUA 自造字 → 新版 Unicode 正式碼位，回傳統計報告）。維持「機制內建、資料外部」：對照表由使用者自政府資料開放平臺下載，倉庫不內建資料。新增 cns11643-baseline CI workflow 以釘選版本官方資料驗收 10.4 萬碼位的平面路由、CP950 差異白名單（2 字）與 Big5E 全碼位往返。
- CNS 11643 字型遞補入口擴及圖表與簡報嵌入表格：新增 `OdfChartDocument.SetChartTitle(title, options)`／`SetAxisTitle(dimension, title, options)`（含 `ChartDocumentBuilder`／`ChartAxisBuilder.WithTitle(title, options)`）與 `OdfEmbeddedTable.SetCellText(row, column, text, options)` 多載，重用同一套分段與 font-face 宣告基礎；至此所有承載 ODF 文字 run 的高階入口皆支援字型遞補選項（頁首頁尾經由 `OdfParagraph` 既有多載涵蓋，MathML 公式內容不適用）。
- 字型子系統重構為 `OdfFontContext` 單一實例模型（實例為核心＋靜態 `Default` 單例，對齊 `JsonSerializerOptions.Default` 業界慣例）：字型註冊、替代對照、平面對應、子集化器與警告快取全數由情境執行個體承載；**移除** `OdfFontResolver` 與 `OdfFontSegmenter` 靜態類別（0.x 未發佈期間之刻意破壞性重整）。隔離注入點兩層：`OdfDocument.FontContext`（文件層級，含存檔時字型內嵌）與 `OdfTextFontFallbackOptions.FontContext`（單次呼叫層級），優先序「選項 → 文件 → Default」。已知限制：PDF 匯出因 PDFsharp 全域字型解析器為行程層級，一律使用 `Default`。
- 新增自訂罕字字型擴充點：`OdfFontContext.RegisterSupplementaryPlaneFontMapping` 可註冊「基礎字型 → Unicode 平面（1–16）→ 字型名稱」對應（優先於內建規則、`IDisposable` 還原、無鎖讀取熱路徑）；`OdfFontFaceInfo` 公開化為 `sealed record` 並新增 `OdfTextFontFallbackOptions.Custom` 工廠，讓使用者不修改 OdfKit 即可接上自備的 Ext B–J 罕字字型（如黑體系補字字型）。核心維持字型中立，不內建任何第三方字型名稱。
- CLI `convert-csv --encoding` 開放 IANA 編碼名稱與代碼頁編號（如 `big5`、`shift_jis`、`gb18030`、`950`），支援舊系統傳統編碼 CSV；UTF-7 維持 .NET 預設封鎖。
- `docs/odf-format-support.md` 新增 Unicode 版本相容性聲明（平面路由與版本無關，Unicode 17.0 Ext J 自動歸入 Plane 3）與內建對應表 Plane 3 覆蓋現況。
- 完成第二階段 API 人體工學與效能精修：新增 `OdfDiagnostic` 統一診斷模型（八個 report 類別加掛強型別 `Diagnostics` 檢視，見 `docs/reference/diagnostics.md`）、`ImportRecordsAsync`／`ReadRecordsAsync` 非同步物件繫結、`OdfTextMatch.Paragraph`／`ParagraphOffset` 段落定位資訊與搜尋取代單次 traversal 重構、HTML／PDF 匯出低緩衝寫入；同步擴大效能基準與 CI 迴歸閘門（find/replace、物件繫結、export 記憶體）。
- 補齊少數 XML 讀取點的 `MaxCharactersInDocument` 上限（Flat XML 二次解析、串流套印範本、簽章檔載入、混合 PDF XMP 中繼資料），使 XXE／DoS 防禦姿態全庫一致。
- 完成 ODT／ODS／ODP／ODG 高階 facade 的一致 CRUD 生命週期契約，加入逐 topic semantic coverage、隨機 mutation、重複保存載入、clean-room provenance 與 Office 修改另存驗證；同步更新 Public API 基線及破壞性重整遷移指南。
- 新增 `net48` Windows CLR consumer smoke，從本地 NuGet 套件驗證四主格式 round-trip、binding redirect、native imaging 與 7 個 extensions 最小執行入口。
- 新增 `OdfVersionCompatibilityReport` 與 `AnalyzeVersionCompatibility`，在 ODF 1.4 語意降版至 1.1～1.3 前後提供元素／屬性、命名空間及 DOM 路徑的結構化診斷；保存仍保留無法映射與 foreign namespace 內容，不捏造等價語意。
- 新增 ODS／ODT 串流 Reader 資源限制選項與真正非同步讀取；repeat、列欄、節點及文字超限時改為失敗，不再靜默截斷。
- 修正 `OdsStreamReader.GetValue` 的 `DbDataReader` 語意：空值回傳 `DBNull.Value`，公式儲存格回傳已儲存快取值；新增 `GetCell` 保留公式、值類型、貨幣及顯示文字。這是 1.0 前的刻意破壞性修正。
- ODS／ODT Writer 新增非同步 flush／complete 路徑；ZIP 中央目錄提交因 BCL `ZipArchive` 限制仍為同步步驟。
- 新增效能預算、能力 claims、證據索引及 12 語系 GitHub Pages API reference 建置流程。
- API 文件站台重構為 DocFX 站內多語系結構（根層導覽與首頁、12 語系入口改為站內內容頁）：修復模板 logo 全站 404、搜尋框不可見與語系入口孤立問題；移除指向未渲染 `OdfKit.DOM.*` 頁面的失效連結；建置腳本新增語系契約驗證與站內連結健檢閘門（見 `docs/api-docs-site.md`）。
- API 文件站升級至 DocFX 2.78.5 modern 模板，加入 12 語系原生 TOC、站內權威聲明、sitemap、共用 footer 及 modern 輸出驗證。
- API 文件站新增自訂 404 頁（DocFX 內容頁）：建置時注入站台根 `<base>` 使任意深度缺失路徑下樣式與導覽正常、自 sitemap 移除 404 條目，並新增對應建置閘門。

## [0.0.1] - 持續維護

`v0.0.1` 是持續完滿的產品身分，不以升版作為補齊必要功能、文件或品質債務的手段。
GitHub Release 資產若建立，只代表特定提交的交付快照；目前未發佈至 nuget.org。

### 新增

- **核心 ODF 支援**：24 種主要 ODF extension（ODT/ODS/ODP/ODG 及其範本、母片、Flat XML、次格式變體）之偵測、建立、載入、保存、驗證與來回讀寫。
- **四主格式高階 API**：ODT、ODS、ODP、ODG 已達 `complete` 分級，涵蓋常用建立、編輯、樣式、公式、加密、追蹤修訂、條件格式、樞紐分析表等場景。
- **規範可信度**：ODF 1.1/1.2/1.3/1.4 官方 RELAX NG 衍生的版本化 schema metadata／pattern 驗證、profile 規則（OASIS Strict/Extended、ISO/IEC 26300、EU、ROC Taiwan）、266 筆 corpus fixtures，以及由獨立 CI 以固定版本與 SHA-256 執行的外部 ODF Validator baseline。
- **安全性**：PBKDF2（≤ 50,000 次迭代）、Argon2id、OpenPGP（RSA/ElGamal/ECDH X25519 與傳統曲線）加密；XAdES 數位簽章與時間戳記驗證；XXE／Zip Slip／OOM DoS 防禦。
- **轉換與互通**：
  - OOXML：ODT↔DOCX、ODS↔XLSX（含具名段落／字元樣式、公式、圖表）。
  - PresentationML：ODP↔PPTX（投影片、主題色票、表格、動畫時間軸與 build list）。
  - Managed-first 淨室轉換：ODT↔Markdown／RTF、ODG→SVG，LibreOffice 降為 fallback。
  - LibreOffice headless 互通矩陣（26.x）、OOXML 視覺 golden file 比對。
- **協作格式**：ODT ↔ JSON operations 雙向轉換（對標 ODF Toolkit CLI，`OdfKit.Extensions.Collaboration`）。
- **RDF／中繼資料**：`manifest.rdf` triple CRUD 與 SPARQL 查詢橋接（`OdfKit.Extensions.Rdf`）。
- **效能**：`OdsStreamWriter` 以串流寫入降低常駐記憶體（公開跨套件對比見 `docs/performance-comparison.md`；勿與 GC 累積配置量混淆）；公式剖析採 `ref struct` + `ReadOnlySpan<char>` 低配置設計；XML 標籤字串池化；ZIP 載入 `ArrayPool` 緩衝。
- **泛型物件序列匯出**：新增 `ObjectDataReader<T>`（將任意 `IEnumerable<T>`／`IAsyncEnumerable<T>` 轉接為 `DbDataReader`）與對應的 `OdsStreamWriter.WriteDataAsync<T>` 多載，可將任意物件序列（例如 Entity Framework Core `IQueryable<T>.AsNoTracking().Select(...).AsAsyncEnumerable()` 查詢投影）低記憶體串流匯出成 ODS，亦可與 `SqlBulkCopy` 等外部 `DbDataReader` 消費者互通；核心不因此新增任何外部 ORM 或資料庫套件相依。
- **實務相容性檢查器**：新增 `OdfPracticalCompatibilityValidator`，依 `OdfPracticalCompatibilityProfile`（LibreOffice 現行版本、Microsoft Office ODF、跨辦公軟體可攜編輯）掃描封裝、內容、內嵌圖表與影像，回報 `OdfPracticalCompatibilityReport`／`OdfPracticalCompatibilityIssue` 常見跨工具編輯風險（含 Microsoft Word ODT 復原風險提示）。
- **圖表深度 API**：新增 `OdfChartPreset` 任務導向預設（長條、折線、圓餅、面積、散佈等）、泡泡圖與股價圖系列（`OdfBubbleChartSeriesInfo`／`OdfBubbleChartSeriesRequest`、`OdfStockChartSeriesInfo`／`OdfStockChartSeriesRequest`）與 `OdfChart3DOptions`（投影模式、角度偏移、雙面光照、光源清單），補齊圖表建立與樣式高階 API 深度。
- **TemplateBinder 情境強化**：擴充文字、試算表、簡報、影像與繪圖等文件類型的占位符繫結情境涵蓋範圍，並補上對應 cookbook 範例。
- **ODF 1.4 coverage 契約**：新增 `OdfCoverageContractTests` 等測試鎖定 ODF 1.4 規格覆蓋契約與 typed DOM audit 入口，明確區分規格覆蓋、package lifecycle、high-level facade 與 interop behavior 四個持續追蹤層次。
- **套件與發行**：8 個套件（`OdfKit` 核心 + 7 個 `OdfKit.Extensions.*`）雙 TFM（`net10.0` + `netstandard2.0`）NuGet 封裝，透過 GitHub Release 資產發佈（非 nuget.org）。
- **串流寫入熱路徑（ODS／ODT）**：將批次原始 XML 組裝與字元防線抽至共用 `OdfRawXmlWriter`／`OdfXmlCharacterGuard`（`OdfKit.Core`），`OdsStreamWriter` 與 `OdtStreamWriter` 段落／標題／清單／儲存格熱路徑共用；關閉 `XmlWriter.CheckCharacters` 後仍以 `Err_OdfStreamWriter_InvalidXmlCharacter` 快速失敗；補齊 ODS／ODT fast-path 與字元邊界測試。`docs/performance-comparison.md` 於 2026-07-09 重跑 ODS 百萬列對比（第 2 次：約 4.96 s／472 MB 配置／38 MB 峰值，與 MiniExcel 耗時接近持平）。
- **合規文件**：新增 `docs/ip-compliance.md`（複合授權、AI 產製、clean-room、DCO、採用者盡職調查）；README 補強「何時使用／不使用」與效能敘事對齊。
- **可維護性**：`OdfLocalizer.Exceptions` 按 12 語系拆檔；新增 `docs/maintainability.md`、產生碼目錄 README、`eng/Test-OneLineXmlSummary.ps1`；歷史 `Split-*`／`Merge-*` 等腳本移至 `eng/historical-refactor/`；合併弱 partial（`OdfAnimation`、簽章 `Common`）並移除空殼 partial 根檔。
- **公開 API 形狀與文件完滿基線（v0.0.1）**：
  - 手寫公開 API 將 RS0026／RS0027 升為 **error**；生成 DOM／schema 目錄覆寫為 none，且禁止手改 `.g.cs`。
  - 單一尾端可選參數改明確多載鏈；多可選高頻面改 **options 物件**（`OdfRichTextRunOptions`、`OdsRowWriteOptions`、`OdfValidationOptions`、`OdfFlatXmlWriteOptions`、`OdfSchemaRegistrationOptions`）；其餘展開為短多載轉呼叫（`Expand-OptionalParameters.py --dry-run` = 0）。0.x **不**保留舊多可選相容層。
  - PublicApiAnalyzers 雙 TFM Unshipped 基線與 Package Validation；在地化 JSON 產線（12 語系 × 鍵對等閘門）。
  - 雙語 XML **missing** 清零；`Test-BilingualXmlDocs.ps1` 基線 `TOTAL=0`／`FILES=0`。
  - 高頻 API（`OdfDocumentFactory`、`OdfPackage`、`OdfDocument`、`OdfValidator`、`OdsStreamWriter` 等）便利多載摘要差異化（`eng/Rewrite-ConvenienceSummaries.py`）。
  - 產品品質分層入口見 `docs/product-quality-gates.md`（提交前 A／PR 與 main 之 B／外部環境與穩定量測之 C）。
  - God-class 採人機 KEEP 準則與協作者地圖（`docs/human-agent-maintainability.md`、`docs/architecture-collaborators.md`），禁止為行數機械切檔。
  - 多版官方 schema（1.1～1.4）內建為**產品選擇**（封存／存量流通）；為瘦身拆成可選 NuGet 是版本無關的**永久非目標**。

### 架構

- 採用協作者抽取模式拆分上帝類別；大型 façade 維持領域 partial／engine 邊界（見協作者地圖）。
- 所有公開 `*Async` 方法統一帶 `CancellationToken cancellationToken = default`。
- 測試套件依分層命名規則整理，移除歷史開發階段命名與重複測試檔。
- 可選參數與 options 表面對齊 `docs/public-api-optional-parameters.md`；新增公開 API 須更新 PublicAPI 基線。

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
- 修正 `OdfPackageFlatXmlLoader.ExtractNestedDocuments` 巢狀內嵌文件 `objectId` 僅做 `TrimStart`／`TrimEnd` 而未呼叫既有的 `OdfPackage.SanitizeEntryName`，導致惡意 `xlink:href` 中的 `..` 片段未被清除、可能覆寫封裝內任意項目的 Zip Slip 變體問題。
- 修正 `OdfPackageEntryNameSanitizer.Sanitize` 逐段比對 `".."` 時，未考慮 Windows 會靜默去除路徑片段尾端句點與空白，導致 `".. "` 等片段可繞過 Zip Slip 防禦的問題，改為比對前先 `TrimEnd('.', ' ')`。
- 修正 `OdfPackageMacroSanitizer.Sanitize` 於 XML 項目淨化失敗時僅記錄警告、原始未淨化內容原樣寫回封裝的問題，改為收集失敗項目並於處理完畢後擲出 `InvalidDataException`，讓呼叫端可感知淨化未完整成功。
- 修正 `OdfMmfZipInfo` 記憶體映射快速路徑解析 ZIP 中央目錄時，未驗證壓縮資料偏移量與大小是否超出實體檔案長度，導致損毀或惡意 ZIP 延後至 `OpenStream` 建立記憶體對應檢視時才擲出未經處理例外的問題，改為於解析階段即驗證並略過越界項目。
- 修正 `OdfUtf8XmlReader` 解析未閉合的 Processing Instruction（截斷於 `<?xml ...` 無 `?>`）時，掃描迴圈已觸及緩衝區尾端仍無條件前進兩個位元組，導致切片越界擲出 `ArgumentOutOfRangeException` 的問題，改為切片前以 `Math.Min` 夾限至緩衝區長度。
- 修正 `OdfNodeChildList.Unlink` 每次移除子節點皆從串列頭重新索引全部節點、違反其宣稱之 O(1) 移除複雜度（實為 O(N²)）的問題，改為僅從被移除節點的原位置往後重新索引。
- 修正 `OdfNode.CloneNode`／`OdfElement.CloneNode`（及間接使用其結果的 `OdfNode.ImportNode`）遞迴複製子節點無深度上限，與 `OdfXmlReader.MaxElementDepth` 剖析路徑防護不一致，透過純 DOM API 疊出的極深巢狀樹可能觸發 `StackOverflowException` 的問題，改為以執行緒個別遞迴深度計數器比照剖析器上限攔截。
- 修正 `OdfElementContentModel.Table.SetSparseCellValue` 將儲存格值設為 `null` 時，未清除該儲存格既有的 `FormulaPtr`／`StyleNamePtr`，導致透過 `ImportData` 覆寫為 `null` 的儲存格仍會殘留並輸出舊公式／樣式的問題。
- 修正 `OdfDrawPageShapeReadEngine` 中 `WalkDrawingNodes`／`FindImageHref`／`ExtractTextBoxContent`／`ContainsDescendant` 遞迴走訪缺少與 `CollectGroupsRecursive` 一致的巢狀深度上限的問題，避免深巢狀 `draw:g` 觸發 `StackOverflowException`。
- 修正 `OdfChartRenderer` 具體化圖表資料範圍為陣列（`GetRangeStrings`／`GetRangeDoubles`）前未限制範圍總儲存格數的問題，惡意圖表範圍（如指向整欄）可能嘗試配置巨量陣列造成記憶體耗盡，改為新增 `OdfSpreadsheetLimits.ChartRenderMaxCells` 上限並於超出時視為無效範圍。
- 為公式引擎多個函式的無界迭代加上與 `OdfSpreadsheetLimits` 一致的上限：`OFFSET` 之 `height`／`width`、`IPMT` 之期數、`WORKDAY`／`NETWORKDAYS` 之日期跨距（新增 `OdfSpreadsheetLimits.FormulaMaxDateSpanDays`），避免惡意公式引數觸發近乎無限迴圈的阻斷服務風險。
- 修正 `OdfSignatureVerifier.Revocation` 中一段永遠無法觸發的死碼判斷（先前的例外處理路徑必定已提前 `return`），並修正線上 CRL 多個下載位址逐一嘗試失敗時僅保留「最後一個」例外訊息、其餘失敗原因遺失不利除錯稽核的問題，改為保留並回報所有失敗訊息。
- 修正 `OdfSchemaPatternValidator.MatchAttributeValueNode` 之 `Ref` 分支為唯一跳過循環參照防護（`EnterReference`／`LeaveReference`）的參照解析路徑，自我遞迴的屬性值 pattern 可能觸發 `StackOverflowException` 的問題，比照其餘解析路徑補上防護。
- 修正 `OdfSchemaPatternValidator` 之 `pattern` facet 使用 `Regex.IsMatch` 未設定逾時的問題，由於 `OdfSchemaRegistry.RegisterSchema` 為公開 API，不受信任來源的 pattern facet 存在 ReDoS 風險，改為附加 2 秒逾時並攔截 `RegexMatchTimeoutException`。
- 修正 `OdfDesignTheme.GetAccentFillColor`／`OdfStyleSet.GetChartPaletteColor` 以 `Math.Abs(index) % length` 正規化索引，當 `index` 為 `int.MinValue` 時會擲出 `OverflowException` 的問題，改為使用不依賴 `Math.Abs` 的正規化運算。
- 修正 `OdfXmlStringPools` 之 `ThreadLocal<PoolHolder>` 誤用 `trackAllValues: true`（實際未使用該追蹤功能），導致伺服器情境下執行緒集區churn 時舊執行緒的字串池於程序生命週期內持續被強引用而緩慢洩漏記憶體的問題。
- 修正 `FormulaLookupFunctionHandlers.EvaluateIndex` 於範圍為真正二維矩陣（多列且多欄）卻僅提供單一索引引數時，未依規範回傳 `#REF!`、而是靜默將索引當作列號並預設欄號為 1 的問題。
- 修正 `OoxmlUnitConverter.TryParseOdfLengthToTwips` 將換算後的 twip 值轉為 `int` 前未檢查是否超出 `int` 可表示範圍的問題，異常巨大的 ODF 長度字串換算後可能產生無意義的極端值。
- 修正 `TtfFontNameReader` 於 TrueType Collection（TTC）中單一子字型名稱表損毀時，因僅有單一外層 `catch` 包覆整個方法而中止其餘子字型名稱擷取的問題，改為逐一子字型獨立捕捉例外並記錄診斷警告；並將原本完全靜默的頂層例外壓制改為透過 `OdfKitDiagnostics.Warn` 留下可追蹤紀錄。
- 修正 `OdfMediaManager.ScanExistingMedia` 建構時整檔載入既有 `Pictures/` 媒體項目計算 SHA-256 卻未套用大小上限的問題，改為透過 `OdfBoundedStreamReader` 以 `OdfLoadOptions.MaxEntrySize` 為界複製，超出上限的項目會記錄警告並略過。
- 修正 `FormulaDateTimeFunctionHandlers.EvaluateWeekNum` 未實作 ISO 8601 週數規則（`type=21`）、原本一律套用美式簡化公式的問題，改為改用符合 ISO 8601「第一週須包含該年第一個星期四」規則的手動計算（netstandard2.0 無 `System.Globalization.ISOWeek` 可用）。
- 修正 `XlsxToOdfConverter.ReadOpenXmlCellValue` 於 SharedString 索引越界時靜默回傳 `null`、與同檔案 `CopyCharts`／`CopyPivotTables` 皆會記錄診斷警告的慣例不一致的問題，改為透過 `OdfKitDiagnostics.Warn` 留下可追蹤紀錄。
- 修正 `DocxToOdtConverter.AppendDrawing` 讀取 DOCX 內嵌 `ImagePart` 前未套用大小上限的問題，改為透過 `OdfBoundedStreamReader` 以 `OdfLoadOptions.MaxEntrySize` 為界複製，避免超大內嵌圖片造成記憶體放大風險。
- 修正 `TableTableElement` 稀疏儲存格頁面配置（`EnsurePageAllocated`／`GetOrCreateCell`）未對列／欄索引設定上限的問題，改為與 ODF 試算表格線規格（1,048,576 列／16,384 欄）一致地拒絕越界索引，避免透過 `ImportData` 匯入異常寬/高的資料來源時觸發無界原生記憶體配置。
- 修正 `TryWriteOverride` 儲存每一列時固定掃描至 ODF 規範上限 16,384 欄以尋找該列已用欄位範圍的問題，改為改用序列化前一次性掃描得出的整表最大已用欄位索引，稀疏且列數龐大的表格可大幅減少不必要的掃描次數。
- 修正 `OdfPackageArchiveWriter` 合併 `content.xml`／`styles.xml` 自動樣式時，以 `Elements().FirstOrDefault(...)` 線性掃描既有樣式名稱去重、隨樣式數量呈 O(n²) 成長的問題，改用 `HashSet<string>` 追蹤已加入的樣式名稱。
- 修正 `OdfSchemaPatternValidator` 之 `MatchAttributePatternReference`／`MatchListReference` 於偵測到同名參照已在作用中堆疊時直接判定為循環並拒絕比對、未比照 `Content.Sequence`／`ElementMatching` 既有慣例改用 `CreateRecursiveContext` 建立巢狀內容繼續比對的問題，導致合法的巢狀或跨分支共用具名 pattern 參照可能被誤判為循環而驗證失敗。
- 修正 `OdfTransformHelper.ParseTransform` 只要字串中任何位置出現 `matrix(...)`，即直接以該矩陣做為結果並略過其餘變換函式（如 `"rotate(0.5) matrix(...)"` 會遺失 `rotate` 部分）的問題，改為僅當整個（去除頭尾空白後的）字串恰為單一 `matrix(...)` 呼叫時才套用此快速路徑。
- 修正 `OdfElementContentModel.Table.AllocatePageMemory` 於 netstandard2.0 分支逐位元組迴圈歸零新配置頁面（約 655,360 bytes）的問題，改用 `Span<byte>.Clear()` 批次歸零。
- 修正 `OdfElementComplexAttributeAccess.GetDateTime` 僅接受精確的 `yyyy-MM-ddTHH:mm:ss`（或附加字面 `Z`）格式，導致含次秒精度或數值時區偏移（如 `+08:00`）等合法 xsd:dateTime 寫法解析失敗、中繼資料時間戳記靜默遺失的問題，改為依序嘗試含次秒精度與數值時區偏移的格式組合。
- 修正 `OdfNode.MigrateMediaReferences` 搬移內嵌物件（非 `Pictures/` 媒體）子項目時，若部分項目搬移失敗仍會繼續將 `href` 改指向該不完整資料夾並儲存 manifest 的問題，改為失敗時移除已寫入的殘缺項目、保留原始參照不變；並將內嵌物件資料夾隨機後綴由 `Guid.NewGuid().ToString("N").Substring(0,8)`（32 位元碰撞空間）改為完整 32 位元十六進位 GUID，降低高併發匯入下的檔名碰撞風險。
- 修正 `OdsStreamWriter.Dispose` 對 `WriteStyles()` 失敗僅記錄警告後吞掉例外的問題，導致輸出封裝可能實際缺少 manifest 已宣告的 `styles.xml` 卻不被呼叫端察覺，改為讓例外傳播（並以 `finally` 確保 `_zip` 資源仍會釋放）。
- 修正 `OdfFontResolver._warnedMissingFonts`／`OdfNumberFormatter.Parsing.FormatInfoPool` 兩處僅供效能／診斷用途的靜態快取無上限成長的問題，長時間執行的轉換服務處理大量不重複字型名稱或格式字串時會緩慢洩漏記憶體，改為加上大小上限，超過時清空重來。
- 修正 `OdfProfileRuleValidator.ValidateMacroOrScriptAttributes` 對文件中每個元素的每個屬性值都做 `Contains("vnd.sun.star.script:")` 全字串掃描的問題，改為限縮至 `href` 屬性，該巨集 URI 僅可能出現於連結類屬性中。
- 為 `DrawImageElement.Crop` 補充 `<remarks>` 說明：方法簽章引數順序 (top, bottom, left, right) 與依 CSS `rect()` 語法規範輸出的 `fo:clip` 屬性值順序 (top, right, bottom, left) 不同屬預期行為，避免呼叫端誤解為缺陷。
- 統一 `OdfSchemaPatternValidator` 三處各自獨立實作、行為曾經分歧過的 RELAX NG `zeroOrMore`／`oneOrMore` 重複比對「frontier（前緣）狀態展開」演算法（內容模型依子元素索引、清單語彙依 token 索引、屬性模式依已消耗屬性位元遮罩）至共用泛型輔助方法 `OdfSchemaPatternFrontierMatcher.ExpandRepeated`。
- 將 `OdfSchemaPatternValidator` 屬性模式比對（`Attributes.Matching`）的「已消耗屬性」狀態，由逗號分隔字串（每次比對節點皆需 `Split`／`int.TryParse`／排序／重新組字串）改為 `BigInteger` 位元遮罩，避免屬性數量較多時的字串配置開銷；`BigInteger` 不像 `ulong` 受 64 位元限制，任意屬性數量皆可正確表示。
- 為 `OdfStyleEngine` 補充 `<remarks>`，明確標示其內部快取（一般 `Dictionary`）非執行緒安全，若需並行處理應為每份文件建立獨立執行個體。

