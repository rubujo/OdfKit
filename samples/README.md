# OdfKit .NET 10.0 單檔 C# Script 範例使用說明

本目錄包含了一個全新設計的 `OdfKit` 全功能使用範例。
此範例採用 **C# 14** 與 **.NET 10.0** 引入的**單檔指令碼 (File-based apps)** 特性，您不需要建立傳統的 `.csproj` 專案檔，即可直接執行本範例程式碼。

---

## 技術背景與最佳實踐

微軟在 .NET 10.0 中引入了更強大的單檔 C# Script 執行模式：
- **免專案檔運行**：使用 `dotnet run <file.cs>` 即可直接編譯並執行單個 `.cs` 檔案。
- **檔案指令 (Directives)**：在程式碼頂部使用以 `#:` 開頭的指令，可以直接在程式碼內處理專案依賴。
  - `#:project <path.csproj>`：可用於直接參考本地 C# 專案。
  - `#:package <package>@<version>`：可用於下載並參考 NuGet 套件。

本範例 `Sample.cs` 即是透過頂部的檔案指令，直接連結專案目錄下的主程式庫與擴充套件：
```csharp
#:project ../OdfKit/OdfKit.csproj
#:project ../OdfKit.Extensions.Pdf/OdfKit.Extensions.Pdf.csproj
#:project ../OdfKit.Extensions.Html/OdfKit.Extensions.Html.csproj
```

---

## 系統需求

- 已安裝 **.NET 10.0 SDK** (建議版本 10.0.300+)。
  您可以在終端機執行此命令確認您的環境版本：
  ```bash
  dotnet --version
  ```

---

## 執行步驟

1. 開啟終端機 (PowerShell 或 Command Prompt)。
2. 切換至本專案的根目錄 `OdfKit/`。
3. 執行以下指令：
   ```bash
   dotnet run samples/Sample.cs
   ```
4. 執行完成後，範例將在 `samples/output/` 目錄下產生示範檔案。

---

## 示範涵蓋功能說明

本範例程式碼完整展示了 `OdfKit` 以下的核心能力：

1. **文字文件 (ODT) 建立與編排**：
   - 建立標題與段落，並套用粗體、斜體等字型樣式。
   - 建立多層級清單。
   - 建立 3x2 的自訂表格，並寫入表頭與資料格。
   - 插入二進位 PNG 影像。
2. **試算表 (ODS) 建立與公式**：
   - 建立試算表並新增多個工作表。
   - 寫入數值、字串。
   - 實作 ODF 公式計算（如計算總和的 `SUM` 公式）。
   - 套用儲存格樣式。
3. **簡報 (ODP) 建立與轉場特效**：
   - 採用 `OdfKit` 專屬的 Fluent Builder 模式建立簡報。
   - 自訂投影片標題、文字框與幾何圖形 (Shape)。
   - 新增講者備忘錄 (Speaker Notes) 與投影片切換轉場 (Transition) 特效。
4. **低記憶體高效能串流寫入 (OdsStreamWriter)**：
   - 示範在大數據情境下，以小於 1 MB 記憶體佔用的超高效率模式，流式寫入多達 100 列以上的表格明細，有效杜絕記憶體不足 (OOM) 錯誤。
5. **中介資料 (Metadata) 讀取與更新**：
   - 展示如何載入既有檔案、讀取文件 Metadata 標題與建立者資訊，並進行修改更新與二次存檔。
6. **進階轉檔 Extensions (PDF / HTML 導出)**：
   - 使用 `OdfPdfExporter` 將 ODT 轉換並渲染匯出為 PDF 檔案。
   - 使用 `OdfHtmlExporter` 將 ODT 轉換並匯出為 HTML 網頁。

---

## 預期產出檔案

執行成功後，您可以在 `samples/output/` 資料夾下找到以下產出：

| 檔名 | 格式 | 說明 |
| :--- | :--- | :--- |
| **`output_text.odt`** | ODF 文字文件 | 包含格式段落、表格與圖片的文字文件。 |
| **`output_text_updated.odt`** | ODF 文字文件 | 更新中介資料（標題與建立者）後的版本。 |
| **`output_spreadsheet.ods`** | ODF 試算表 | 包含銷售統計資料與總計 SUM 公式的表格。 |
| **`output_presentation.odp`** | ODF 簡報 | 包含兩張投影片、轉場效果與形狀的簡報。 |
| **`output_stream.ods`** | ODF 試算表 | 透過 `OdsStreamWriter` 大量串流寫入的明細表。 |
| **`output_pdf.pdf`** | PDF 檔案 | 將 ODT 內容完美轉譯後的 PDF 格式文件。 |
| **`output_html.html`** | HTML 網頁 | 將 ODT 內容轉換後的純 HTML 網頁。 |
