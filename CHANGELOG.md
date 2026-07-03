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
- 修正 `FormulaParser.ParsePower` 對 `^` 運算子採左結合的問題，改為右結合以符合 OpenFormula 規範（如 `2^3^2` 應等於 `512`）；並修正單元負號與乘方的優先權順序，使 `-2^2` 正確等於 `-4` 而非 `4`。
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

