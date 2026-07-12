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
