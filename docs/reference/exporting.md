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
