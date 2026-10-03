# 統一匯出 Facade

HTML、Markdown、SVG 與 PDF exporter 使用一致的 `ExportToStream`、`ExportToPath`、
`ExportToStreamAsync`、`ExportToPathAsync` 形狀，並回傳 `OdfExportReport`。Report 包含格式、
backend 識別值、寫入位元組數及結構化 diagnostic codes。

Stream 一律由呼叫端擁有，exporter 不會將其關閉。非同步多載接受 `CancellationToken`；
HTML、Markdown 與 SVG 非同步寫入編碼後內容，PDF 則先執行同步排版，再以可取消的非同步
I/O 寫入目的地。純 DOM mutation 不提供假 async。

```csharp
using var output = new MemoryStream();
OdfExportReport report = await OdfHtmlExporter.ExportToStreamAsync(
    document, output, options, cancellationToken);
```

Backend-specific options 維持具型別：`OdfHtmlExportOptions`、`OdfMarkdownExportOptions`、
`OdfSvgExportOptions`。PDF managed backend 目前直接接受 `TextDocument`；實體排版結果仍受字型
及 backend 能力影響，呼叫端應保留 report 與視覺驗證證據。

## 涵蓋範圍

以 LibreOffice 當裁判，對真實的 LibreOffice ODT 匯出後重新讀入並比對文字（`LibreOfficeAuthoredOdtKeepsAllTextInEveryExportFormat`）。
HTML 與 PDF 涵蓋的結構如下；表中沒有列出的結構不保證輸出。Markdown 與 RTF 以「文字完整」為驗證標準，
結構（清單、表格、連結的呈現）沒有逐項驗證。

| 結構 | HTML | PDF |
|------|------|-----|
| 段落、標題、字元格式（粗體、斜體、底線、色彩，含亞洲字型屬性） | ✅ | ✅（字級與色彩一併輸出） |
| 超連結 | ✅（`javascript:` 等不安全協定只保留文字） | ✅（網頁、郵件、電話、FTP 協定成為連結註解，其餘只保留文字） |
| 清單 | ✅（有序、無序與巢狀；有序清單保留字母、羅馬數字與起始值） | ✅（巢狀縮排與標籤） |
| 表格 | ✅（標題列、合併儲存格、儲存格內清單；尾端空白重複列不展開） | ✅（合併儲存格、標題列；儲存格內不支援巢狀表格，其文字以段落輸出） |
| 連續空格、定位字元、換行、書籤 | ✅ | ✅（空格、定位字元、換行） |
| 圖片 | ✅（資料 URI 內嵌，單張上限 5 MB） | 以 base64 嵌入（單張上限 5 MB；只驗證匯出不失敗，沒有驗證渲染結果） |
| 欄位（頁碼、日期、標題等） | 儲存的顯示文字 | 儲存的顯示文字 |

## SVG 與影像匯出的涵蓋範圍

SVG（`OdfSvgExporter`）、工作表影像（`OdfImageExporter`）與圖表備援圖（`OdfChartRenderer`）以 LibreOffice 26.2 與 Chrome 對照驗證：

| 項目 | 行為 |
|------|------|
| SVG 圖形樣式 | 填色、線條模式（含「無線條」）、顏色、線寬、虛線、端點與接合、不透明度由圖形樣式（含 `parent-style-name` 繼承）解析；圖形元素上的內嵌屬性優先。矩形圓角（`draw:corner-radius`）輸出 `rx`／`ry` |
| SVG 頁面與文字 | 頁面大小取自版面主頁（沒有時用 `DefaultWidth`／`DefaultHeight` 並依內容撐大）；文字框從內距與第一行字級決定基線，字型取自文字樣式；多行文字不自動換行 |
| 工作表 PNG／JPEG | 固定欄寬列高的格線；文字使用備援字型（中文不再是方框）、顯示儲存格的顯示文字（格式化後的數字、日期）並裁切在儲存格內。不支援合併儲存格、對齊、儲存格色彩與邊框、實際欄寬 |
| 圖表備援圖 | 長條（多系列分組並排）、折線、散佈、圓餅；中文標題、刻度與圖例選用有字形的字型。不支援堆疊、雙軸與樣式細節 |
| 文字量測（`OdfTextMeasurer`） | 粗體、斜體用同家族的真實字面；缺字的字元用系統備援字型。以 Chrome `measureText` 比對誤差在 0.65% 以內（Arial、Times New Roman、Consolas、標楷體、微軟正黑體） |
| PDF 簽章 | `adbe.pkcs7.detached` 增量更新；`ByteRange` 涵蓋簽章值以外的全部位元組，沿用原檔的 `/Info` 與 `/ID`。簽章欄位是不可見的（`/Rect [0 0 0 0]`），沒有時間戳記與憑證鏈驗證 |
