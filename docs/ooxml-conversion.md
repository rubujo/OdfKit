# OOXML 轉換涵蓋範圍

`OdfKit.Extensions.Ooxml`（依賴 ClosedXML 與 Open XML SDK）提供 ODF 與 OOXML 的雙向轉換。本文件記錄
**XLSX → ODS** 與 **DOCX → ODT** 兩個方向實際保留的內容、已知限制，以及驗證方式。只列出有測試鎖定的行為；
未列出的 Word 或 Excel 功能不保證轉換。

| 方向 | 入口 |
|------|------|
| XLSX → ODS | `XlsxToOdfConverter.Convert(Stream)` |
| ODS → XLSX | `OdfToXlsxConverter.Convert(SpreadsheetDocument, Stream)` |
| DOCX → ODT | `DocxToOdtConverter.Convert(Stream)` |
| ODT → DOCX | `OdfToDocxConverter.Convert(TextDocument, Stream)` |

## XLSX → ODS

| 內容 | 行為 |
|------|------|
| 儲存格值 | 字串、數字、布林與日期。連續空格、開頭與結尾空格、定位字元與換行以 `text:s`、`text:tab`、`text:line-break` 寫出，符合規範的消費端不會折疊 |
| 日期 | Excel 的日期序號沒有時區，轉成不含時區的 `office:date-value`（例如 `2017-09-23T00:00:00`），不會依執行機器的時區位移，也不會被標成 UTC |
| 公式 | 轉為 OpenFormula，保留 Excel 儲存的結果值 |
| 合併儲存格 | 轉為 `number-columns-spanned`、`number-rows-spanned` 與 `table:covered-table-cell`。每個工作表被覆蓋的儲存格上限為 1,000,000，超過的範圍略過，避免 `A1:XFD1048576` 這類極小的輸入展開成無法完成的轉換 |
| 稀疏資料 | 只走訪實際存在的儲存格（有內容或格式）。先前走訪整個已使用範圍並為每個空白儲存格建立節點，位於遠端位址的少數儲存格就會膨脹成數十萬個空白儲存格 |
| 資料驗證、條件式格式、基本儲存格格式 | 保留（色階、資料橫條、圖示集、整數區間等） |
| 圖表、樞紐分析表 | 轉換結構；樞紐分析表另有保留資料驗證與格式的後備路徑 |

## DOCX → ODT

| 內容 | 行為 |
|------|------|
| 段落與標題 | 段落樣式名稱、縮排、對齊與標題階層；字元樣式與行內格式（粗體、斜體、底線、色彩、字級） |
| 空白字元 | 連續空格、定位字元（`w:tab`）、換行（`w:br`）與不斷行連字號保留 |
| 超連結 | 轉為 `text:a`（帶有 `xlink:type="simple"`）；目標為 `javascript:`、`vbscript:`、`data:` 時只保留連結文字 |
| 內容控制項、簡單欄位、自訂 XML | 展開其中的文字，不再整段丟棄 |
| 清單 | `w:numPr`（含段落樣式帶入的編號）轉為巢狀的 `text:list`，並依編號定義建立 `text:list-style`：專案符號字元、編號格式、前綴後綴、起始值、縮排。被其他內容打斷後回到同一份編號時延續編號 |
| 表格 | 以格線位置放置儲存格：水平合併（`w:gridSpan`）、垂直合併（`w:vMerge`）、巢狀表格都保留，欄數上限 1,024。儲存格內的段落走與本文相同的轉換，標題、清單、註腳、超連結與圖片都保留 |
| 註腳與章節附註 | 轉為 `text:note`，引用標記依序編號，多段內文各自成為 `text:p` |
| 分頁符號 | `w:br w:type="page"` 與 `w:pageBreakBefore` 轉為 `fo:break-before="page"`；只承載分頁符號的段落不產生空白段落，段落中間的分頁符號把段落切成兩段 |
| 頁首與頁尾 | 走與本文相同的轉換；依 `w:titlePg` 與 `w:evenAndOddHeaders` 建立首頁與偶數頁版本。頁碼與總頁數（複雜欄位與簡單欄位的 `PAGE`、`NUMPAGES`）轉為 `text:page-number` 與 `text:page-count`，不使用 Word 儲存的結果文字（否則會變成固定的「1」） |
| 圖片 | 內嵌圖片與尺寸 |
| 追蹤修訂 | 插入、刪除與格式變更 |

### 已知限制

- **多個章節**各自不同的頁首頁尾：只採用文件最後一個章節的設定。
- **欄位**：除了 `PAGE`、`NUMPAGES` 與 `SECTIONPAGES`，其他欄位保留 Word 儲存的結果文字，不轉成 ODF 欄位。
- **分頁符號**：只處理執行區段（`w:r`）層級的 `w:br w:type="page"`，位於超連結等容器內的不處理。
- 本文件沒有列出的 Word 功能（例如文字方塊、目錄欄位）不保證轉換。

## 驗證方式

行為由兩組測試鎖定：

- `OdfKit.Tests/RealWorldConverterFidelityTests.cs`：以 Open XML SDK 與 ClosedXML 產生含已知真值的文件，轉換後對照真值，
  並以 ODF 1.4 schema 驗證轉出的文件。
- `OdfKit.Tests/OoxmlConversionTests.cs`：雙向轉換的既有行為。

另有以真實 LibreOffice 驗證的互通測試（`LibreOfficeInteropTests.RealDocuments.cs`，沒有 LibreOffice 時略過），
涵蓋日期、合併儲存格、稀疏資料、清單、註腳、分頁、頁尾欄位，以及 OdfKit 載入 LibreOffice 儲存的文件，
見 [LibreOffice 互通性矩陣](libreoffice-interop-matrix.md)。

官方 ODFDOM 範例檔（`docs/examples/odfdom-sample-corpus/manifest.json`）的試算表與文字範例本身幾乎沒有內容，
驗證力有限；轉換缺陷主要由上述含已知真值的文件與 LibreOffice 的輸出發現。
