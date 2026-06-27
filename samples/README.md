# OdfKit .NET 10.0 單檔 C# Script 範例使用說明

本目錄包含一個以 `Sample.cs` 為主的 `OdfKit` 全功能展示範例。
此範例採用 **C# 14** 與 **.NET 10.0** 引入的 **單檔指令碼 (File-based apps)** 特性，
不需要建立傳統 `.csproj`，即可直接執行。

---

## 技術背景與最佳實踐

微軟在 .NET 10.0 中引入了更強大的單檔 C# Script 執行模式：
- **免專案檔運行**：使用 `dotnet run <file.cs>` 即可直接編譯並執行單個 `.cs` 檔案。
- **檔案指令 (Directives)**：在程式碼頂部使用以 `#:` 開頭的指令，可以直接在程式碼內處理專案依賴。
  - `#:project <path.csproj>`：可用於直接參考本地 C# 專案。
  - `#:package <package>@<version>`：可用於下載並參考 NuGet 套件。

本範例 `Sample.cs` 透過頂部檔案指令，直接參考核心程式庫與多個擴充套件：
```csharp
#:project ../OdfKit/OdfKit.csproj
#:project ../OdfKit.Extensions.Pdf/OdfKit.Extensions.Pdf.csproj
#:project ../OdfKit.Extensions.Html/OdfKit.Extensions.Html.csproj
#:project ../OdfKit.Extensions.Ooxml/OdfKit.Extensions.Ooxml.csproj
#:project ../OdfKit.Extensions.Collaboration/OdfKit.Extensions.Collaboration.csproj
#:project ../OdfKit.Extensions.Rdf/OdfKit.Extensions.Rdf.csproj
#:project ../OdfKit.Extensions.Imaging/OdfKit.Extensions.Imaging.csproj
```

因此它是**整合展示範例**，不是最小入門範例。若只需要最短上手流程，請先讀
[docs/getting-started.md](../docs/getting-started.md)。

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
4. 執行完成後，範例將在 `samples/output/` 目錄下產生示範檔案與轉換結果。

---

## 示範涵蓋功能說明

本範例程式碼展示了 `OdfKit` 與多個擴充套件的整合能力：

1. **文字文件 (ODT) 建立與編排**：
   - 建立標題與段落，並套用粗體、斜體等字型樣式。
   - 建立多層級清單。
   - 建立 3x2 的自訂表格，並寫入表頭與資料格。
   - 插入二進位 PNG 影像。
2. **試算表 (ODS) 建立與公式**：
   - 建立試算表並新增多個工作表。
   - 寫入數值、字串。
   - 實作 ODF 公式計算（如計算總和的 `SUM` 公式）。
   - 搜尋公式儲存格並輸出公式位址。
   - 套用儲存格樣式。
3. **簡報 (ODP) 建立與轉場特效**：
   - 採用 `OdfKit` 專屬的 Fluent Builder 模式建立簡報。
   - 自訂投影片標題、文字框與幾何圖形 (Shape)。
   - 新增講者備忘錄 (Speaker Notes) 與投影片切換轉場 (Transition) 特效。
4. **Profile 驗證與 i18n 在地化**：
   - 使用 `OdfComplianceProfiles.RocTaiwanOdfCns15251` 對產出的 ODP 執行 Profile 驗證。
   - 使用 `OdfLocalizer.DefaultCulture` 與 `OdfLocalizer.GetMessage(...)` 展示 `zh-TW` 在地化訊息查找。
   - 驗證結果與在地化訊息會輸出到主控台，不額外產生獨立輸出檔案。
5. **低記憶體高效能串流寫入 (OdsStreamWriter)**：
   - 示範在大數據情境下，以順序工作表寫入模式將記憶體佔用控制在小於 1 MB，流式寫入多達 100 列以上的表格明細，有效杜絕記憶體不足 (OOM) 錯誤。
   - `SwitchToSheet` 支援交錯多工作表寫入，但會使用暫存緩衝，適合便利性優先而非嚴格低記憶體的情境。
6. **中介資料 (Metadata) 讀取與更新**：
   - 展示如何載入既有檔案、讀取文件 Metadata 標題與建立者資訊，並進行修改更新與二次存檔。
7. **進階轉檔與擴充套件整合**：
   - 使用 `OdfPdfExporter` 將 ODT 轉換並渲染匯出為 PDF 檔案。
   - 使用 `OdfHtmlExporter` 將 ODT 轉換並匯出為 HTML 網頁。
   - 使用 `OdfToDocxConverter` / `OdfToXlsxConverter` 轉出 OOXML。
   - 使用 `OdtOperationsExporter` / `OdtOperationsImporter` 展示協作操作匯出與回讀。
   - 使用 `RdfMetadata` 展示 RDF 三元組寫入與 SPARQL 查詢。
   - 使用 `OdfImageExporter` 將工作表渲染為 PNG。
8. **低記憶體串流寫入**：
   - 示範 `OdsStreamWriter` 與 `OdtStreamWriter` 的串流輸出。

---

## 預期產出檔案

執行成功後，您可以在 `samples/output/` 資料夾下找到以下主要產出：

| 檔名 | 格式 | 說明 |
| :--- | :--- | :--- |
| **`output_text.odt`** | ODF 文字文件 | 包含格式段落、表格與圖片的文字文件。 |
| **`output_text_updated.odt`** | ODF 文字文件 | 更新中介資料（標題與建立者）後的版本。 |
| **`output_spreadsheet.ods`** | ODF 試算表 | 包含銷售統計資料與總計 SUM 公式的表格。 |
| **`output_presentation.odp`** | ODF 簡報 | 包含兩張投影片、轉場效果與形狀的簡報。 |
| **`output_stream.ods`** | ODF 試算表 | 透過 `OdsStreamWriter` 大量串流寫入的明細表。 |
| **`output_stream.odt`** | ODF 文字文件 | 透過 `OdtStreamWriter` 串流寫入的文字文件。 |
| **`output_pdf.pdf`** | PDF 檔案 | 將 ODT 內容完美轉譯後的 PDF 格式文件。 |
| **`output_html.html`** | HTML 網頁 | 將 ODT 內容轉換後的純 HTML 網頁。 |
| **`output_csv.csv`** | CSV 檔案 | 由 ODS 匯出之 CSV。 |
| **`output_docx.docx`** | Word 文件 | 由 ODT 轉出的 DOCX。 |
| **`output_xlsx.xlsx`** | Excel 文件 | 由 ODS 轉出的 XLSX。 |
| **`output_collaboration_imported.odt`** | ODF 文字文件 | 由協作操作 JSON 重新匯入產生。 |
| **`output_sheet_rendering.png`** | PNG 圖片 | 由工作表格線渲染出的影像。 |

若目標檔名已被占用，`Sample.cs` 也可能建立 `output_stream_backup.ods` 或
`output_stream_backup.odt` 做為備援輸出。

此外，主控台會額外顯示：

- `ROC CNS 15251` Profile 驗證結果
- `zh-TW` 語系的在地化訊息範例

## 此範例目前未明說的限制

- `Sample.cs` 為**大型整合展示腳本**，覆蓋面廣，但不適合作為每個 API 的最小示例。
- RDF 展示會輸出查詢結果到主控台，但不額外產生獨立 RDF 檔案。
- Profile 驗證結果與 i18n 訊息展示輸出到主控台，不會另外建立報告檔。
- 範例假設本 repository 結構完整存在，無法單獨複製 `Sample.cs` 到其他目錄直接執行。

## 相關文件

- [tools/README.md](../tools/README.md)
- [docs/getting-started.md](../docs/getting-started.md)
- [docs/cookbook.md](../docs/cookbook.md)
