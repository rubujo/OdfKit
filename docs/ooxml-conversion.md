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
| 公式（LibreOffice 儲存的 ODS） | OpenFormula 的方括號參照改成 A1 參照（`[.B2]`、`[.B2:.C5]`、`[$Sheet.B2]`、需要引號的工作表名稱），函數參數分隔的分號改成逗號；字串常數內的分號與點不動 |
| 值的類型 | 日期（`office:date-value`）、時間、百分比與貨幣轉成對應的 Excel 值；日期與時間不會變成文字 |
| 合併儲存格（ODS → XLSX） | `number-columns-spanned`、`number-rows-spanned` 轉為合併範圍 |
| 數字格式 | ODF 資料樣式轉成 Excel 格式字串：日期與時間（年月日、星期、時分秒、AM/PM）、千分位與小數位數、百分比、貨幣 |
| 只有樣式的重複列 | LibreOffice 對有欄格式的資料會一路寫到第 1,048,576 列的空白重複列；轉成整欄的預設格式而不逐格展開（修正前這類檔案使轉換幾乎不會結束） |
| 資料驗證、條件式格式、基本儲存格格式 | 保留（色階、資料橫條、圖示集、整數區間等） |
| 圖表、樞紐分析表 | 轉換結構；樞紐分析表另有保留資料驗證與格式的後備路徑 |

## ODT → DOCX

| 內容 | 行為 |
|------|------|
| 段落、標題、字元格式 | 段落樣式、標題階層、粗體、斜體、底線、色彩、字級 |
| 清單 | `text:list` 轉為帶 `w:numPr` 的段落並依 ODF 清單樣式建立編號定義：項目符號字元（LibreOffice 的 OpenSymbol 私用區字元改用標準圓點）、編號格式（數字、字母、羅馬數字、國字數字）、前綴後綴、起始值、顯示多層編號、縮排與懸掛縮排；巢狀清單層級正確。各個頂層清單獨立重新編號（ODF 的預設），標示延續編號的清單沿用前一份編號 |
| 表格 | 基本表格與邊框 |
| 圖片、追蹤修訂 | 保留 |
| 輸出的合法性 | 屬性容器（`w:pPr`、`w:rPr`、`w:tblPr`、`w:tblBorders`、`w:tcPr` 等）的子元素依 ECMA-376 序列排列，表格都有 `w:tblGrid`；輸出通過 Open XML SDK 驗證（Word 對元素順序很嚴格，違反時可能提示檔案損毀） |

已知限制：頁首與頁尾只轉換純文字；註腳、章節分欄、目錄、書籤與欄位未轉換；表格儲存格以單一段落輸出。

## DOCX → ODT

| 內容 | 行為 |
|------|------|
| 段落與標題 | 段落樣式名稱、縮排、對齊與標題階層；字元樣式與行內格式（粗體、斜體、底線、色彩、字級） |
| 空白字元 | 連續空格、定位字元（`w:tab`）、換行（`w:br`）與不斷行連字號保留 |
| 超連結 | 轉為 `text:a`（帶有 `xlink:type="simple"`）；目標為 `javascript:`、`vbscript:`、`data:` 時只保留連結文字。Word 常以複雜欄位儲存的 `HYPERLINK`（含 `\l` 書籤錨點）同樣轉為 `text:a` |
| 內容控制項、簡單欄位、自訂 XML | 展開其中的文字，不再整段丟棄 |
| 清單 | `w:numPr`（含段落樣式帶入的編號）轉為巢狀的 `text:list`，並依編號定義建立 `text:list-style`：專案符號字元、編號格式、前綴後綴、起始值、縮排。被其他內容打斷後回到同一份編號時延續編號 |
| 表格 | 以格線位置放置儲存格：水平合併（`w:gridSpan`）、垂直合併（`w:vMerge`）、巢狀表格都保留，欄數上限 1,024。儲存格內的段落走與本文相同的轉換，標題、清單、註腳、超連結與圖片都保留 |
| 註腳與章節附註 | 轉為 `text:note`，引用標記依序編號，多段內文各自成為 `text:p` |
| 分頁符號 | `w:br w:type="page"` 與 `w:pageBreakBefore` 轉為 `fo:break-before="page"`；只承載分頁符號的段落不產生空白段落，段落中間（含超連結內）的分頁符號把段落切成兩段，超連結在切開後各段保留相同的目標 |
| 頁首與頁尾 | 走與本文相同的轉換；依 `w:titlePg` 與 `w:evenAndOddHeaders` 建立首頁與偶數頁版本。頁碼與總頁數（複雜欄位與簡單欄位的 `PAGE`、`NUMPAGES`）轉為 `text:page-number` 與 `text:page-count`，不使用 Word 儲存的結果文字（否則會變成固定的「1」） |
| 欄位 | 頁碼與總頁數見上；`DATE`、`TIME` 轉為自動更新的 `text:date`、`text:time`，`TITLE`、`SUBJECT`、`AUTHOR`、`FILENAME`、`NUMWORDS`、`NUMCHARS` 轉為對應的 ODF 文件屬性欄位，Word 儲存的結果文字作為顯示內容 |
| 書籤與交互參照 | `w:bookmarkStart`／`w:bookmarkEnd` 轉為 `text:bookmark-start`／`text:bookmark-end`（終點以編號配對名稱，Word 只供游標位置用的 `_GoBack` 略過）；`REF`、`PAGEREF` 欄位轉為 `text:bookmark-ref`（文字或頁碼），結果文字作為顯示內容；內部超連結的 `#書籤` 目標因此存在 |
| 目錄 | `TOC` 複雜欄位（起點、數個目錄項目段落、終點）轉為 `text:table-of-content`：`\o "1-3"` 的標題層級範圍寫入 `text:outline-level`，Word 儲存的目錄項目（含超連結與頁碼）放在 `text:index-body`，欄位起訖字元不產生空白段落 |
| 區塊層級內容控制項 | `w:sdt` 與自訂 XML 區塊內的段落與表格視為本文（封面、書目、目錄常包在其中），頁首頁尾內的頁碼建置區塊也一樣 |
| 分欄 | 多欄章節（`w:cols`）的內容包進 `text:section`，套用 `style:columns`（欄數、欄距、分隔線、不等寬的 `style:rel-width`） |
| 章節起點 | 頁面設定相同但不是連續章節時換頁（`fo:break-before`）；`w:pgNumType` 的起始頁碼寫成 `style:page-number`，搭配目前的主頁面（LibreOffice 只在帶主頁面的換頁上套用起始頁碼） |
| 多個章節 | 第一個章節使用預設主頁面；之後頁面大小、四邊邊界、頁首或頁尾與前一個章節不同的章節各建立一個主頁面（`DocxSection{n}`），並套用在該章節的第一個區塊（ODF 於該處換頁）。沒有自己參照的章節沿用前一個章節的頁首頁尾（Word 的繼承規則），設定相同的章節共用主頁面 |
| 圖片 | 內嵌圖片與尺寸 |
| 追蹤修訂 | 插入、刪除與格式變更 |

### 已知限制

- **章節**：只轉換頁面大小（`w:pgSz`）、四邊邊界（`w:pgMar`）、頁首頁尾、分欄與起始頁碼。連續章節（`w:type="continuous"`）不換頁，因此不能改頁面設定或重新起算頁碼（ODF 只在換頁處更換主頁面）；頁首頁尾距離、欄間分欄符號（`w:br w:type="column"`）與章節內的橫向旗標未處理。同時是下一頁章節又有多欄時，主頁面指定落在 `text:section` 內的第一個段落，各編輯器對這種組合的處理未驗證。
- **欄位**：除了上列欄位，其他欄位（`SEQ`、`INDEX`、`TOA`、`NOTEREF` 等）保留 Word 儲存的結果文字，不轉成 ODF 欄位。目錄只轉換 `TOC`，內容是 Word 儲存的快取項目，不會依標題重新產生，更新目錄由 ODF 編輯器執行。
- **分頁符號**：處理執行區段（`w:r`）與超連結（`w:hyperlink`）直接子層的 `w:br w:type="page"`；位於內容控制項、簡單欄位等其他容器內的不處理。
- 本文件沒有列出的 Word 功能（例如文字方塊）不保證轉換。

## 驗證方式

行為由兩組測試鎖定：

- `OdfKit.Tests/RealWorldConverterFidelityTests.cs`：以 Open XML SDK 與 ClosedXML 產生含已知真值的文件，轉換後對照真值，
  並以 ODF 1.4 schema 驗證轉出的文件。
- `OdfKit.Tests/OoxmlConversionTests.cs`：雙向轉換的既有行為。

另有以真實 LibreOffice 驗證的互通測試（`LibreOfficeInteropTests.RealDocuments.cs`，沒有 LibreOffice 時略過），
涵蓋日期、合併儲存格、稀疏資料、清單、註腳、分頁、頁尾欄位、多章節頁面設定、超連結欄位、目錄、書籤、分欄與起始頁碼，以及 OdfKit 載入 LibreOffice 儲存的文件，
見 [LibreOffice 互通性矩陣](libreoffice-interop-matrix.md)。

簡報方面，ODP ↔ PPTX 以 LibreOffice 當裁判驗證（頁數與文字，並用 Open XML SDK 驗證 PPTX）：嵌入表格有 `table:table-column` 並放在 `draw:frame` 內，版面配置（`style:presentation-page-layout`）寫在 `office:styles`，預留位置以 `presentation:object` 標示類型，輸出符合 ODF 1.4 schema。

官方 ODFDOM 範例檔（`docs/examples/odfdom-sample-corpus/manifest.json`）的試算表與文字範例本身幾乎沒有內容，
驗證力有限；轉換缺陷主要由上述含已知真值的文件與 LibreOffice 的輸出發現。
