# OdfKit .NET 10.0 單檔 C# 應用程式範例使用說明

本目錄包含一個以 `Sample.cs` 為主的 `OdfKit` 整合展示範例。
此範例採用 **C# 14** 與 **.NET 10.0** 引入的 **單檔應用程式 (File-based apps)** 特性，
不需要建立傳統 `.csproj`，即可直接執行。

WebFont 專用範例另見：

- [`WebFonts.AspNetCore`](WebFonts.AspNetCore/README.md)：可執行的 ASP.NET Core、嚴格 CSP、
  同源或 CDN 託管範例。
- [`WebFonts.WebForms`](WebFonts.WebForms/README.md)：ASP.NET Web Forms Handler、
  `Web.config` 與 ASPX 範例。
- [`docs/webfonts.md`](../docs/webfonts.md)：Dapper、EF Core、ADO.NET、Big5／Big5E、CSP、
  CORS、CDN 與自動內容掃描 cookbook。

---

## 技術背景與最佳實踐

微軟在 .NET 10.0 中引入了單檔 C# 應用程式執行模式：
- **免專案檔執行**：使用 `dotnet run <file.cs>` 即可直接編譯並執行單一 `.cs` 檔案。
- **檔案指令 (Directives)**：在程式碼頂端使用以 `#:` 開頭的指令，可以直接在程式碼內處理專案相依性。
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

範例第 9 節示範 CNS 11643 罕字支援：全字庫字型遞補分段（`OdfTextFontFallbackOptions.Cns11643()`）、
自訂罕字字型情境（`OdfFontContext` + `Custom(baseFont, fontFaces, fontContext)`）、PUA 自造字
碼位遷移（`MigrateTextCodePoints`）與 Big5E 編碼 CSV 匯出（`OdfCns11643MappingTable` +
`OdfBig5EEncoding`，範例用合成小表；實務對照表請自[政府資料開放平臺](https://data.gov.tw/dataset/5961)下載）。
`MigrateTextCodePoints` 只替換文字節點內容，不會重新分段或重套既有字型樣式；若碼位跨越
Unicode 平面，應如範例先遷移未套用平面字型樣式的文字，再使用遞補選項分段。
支援邊界詳見 [docs/odf-format-support.md](../docs/odf-format-support.md)。

## 公文 ODT 範例

`TaiwanGovernmentLetter.cs` 示範使用外部 OTT 範本、既有欄位 API 與
`TemplateBinder` 產生公文形式的 ODT。未提供參數時，程式會建立一份由本專案
自行產生的最小參考範本；也可傳入已加入 ODF user field 或 `{{欄位名稱}}`
占位符的自有範本：

```powershell
dotnet run samples/TaiwanGovernmentLetter.cs
dotnet run samples/TaiwanGovernmentLetter.cs -- <範本.ott> <輸出.odt>
```

此範例只展示檔案載入、範本繫結與檔案產出，不內嵌或散布政府機關範本，
也不代表任何機關的正式版面或電子交換合規認證。

---

## 系統需求

- 已安裝 **.NET 10.0 SDK**（建議版本 10.0.300+）。
  您可以在終端機執行此命令確認您的環境版本：
  ```bash
  dotnet --version
  ```

---

## 執行步驟

1. 開啟終端機（PowerShell 或 Command Prompt）。
2. 切換至本專案的根目錄 `OdfKit/`。
3. 執行以下指令：
   ```bash
   dotnet run samples/Sample.cs
   ```
4. 執行完成後，範例將在 `samples/output/` 目錄下產生示範檔案與轉換結果。

提交前／發版前的 corpus、LibreOffice、效能基線等可執行檢查，見
[docs/product-quality-gates.md](../docs/product-quality-gates.md)。

### Smoke 模式

若只想確認範例能編譯執行並產生核心 ODF 文件，可使用環境變數切換到 smoke
模式。此模式會完整略過 `DemoExtensions`（PDF、HTML、CSV、OOXML 轉檔、
Collaboration JSON 往返與 RDF-SPARQL、影像渲染等展示），但仍會建立
ODT、ODS、ODP、ODG、ODC、ODF、ODI、ODB 與串流輸出文件：

```powershell
$env:ODFKIT_SAMPLE_SMOKE_ONLY = "true"
$env:ODFKIT_SAMPLE_OUTPUT_DIR = "$PWD\samples\output-smoke"
dotnet run samples/Sample.cs
```

---

## 示範涵蓋功能說明

本範例程式碼展示了 `OdfKit` 與多個擴充套件的整合能力：

1. **文字文件 (ODT) 建立與編排**：
   - 建立標題與段落，並套用粗體、斜體等字型樣式。
   - 建立有序清單。
   - 建立 3x2 的自訂表格，並寫入表頭與資料格。
   - 使用 `OdfMailMergeEngine`（`document.MailMerge(...)`）示範郵件合併，以
     `{{TableStart:Users}}...{{TableEnd:Users}}` 語法展開巢狀清單資料。
   - 插入二進位 PNG 影像。
2. **試算表 (ODS) 建立與公式**：
   - 建立試算表並新增多個工作表。
   - 以 `OdfRichTextRunOptions` 示範儲存格富文字格式（options 風格 API）。
   - `OdsStreamWriter` 以 `OdsRowWriteOptions` 示範列高／最佳列高。
   - 寫入數值、字串。
   - 實作 ODF 公式計算（如計算總和的 `SUM` 公式）。
   - 搜尋公式儲存格並輸出公式位址。
   - 套用儲存格樣式。
   - 使用 `workbook.AddChart(...)` 新增內嵌長條圖，並以
     `sheet.Ranges["A1:B5"].AddFilter(...)` 新增自動篩選 (AutoFilter)。
3. **簡報 (ODP) 建立與轉場特效**：
   - 採用 `OdfKit` 專屬的 Fluent Builder 模式建立簡報。
   - 自訂投影片標題、文字框與幾何圖形 (Shape)。
   - 新增講者備忘錄 (Speaker Notes) 與投影片切換轉場 (Transition) 特效。
   - 使用 `shape.Animate(OdfAnimationType.FadeIn, ...)` 為圖形設定進場動畫。
4. **次要格式文件建立**：
   - 使用 `DrawingDocument` 建立 ODG 流程圖。
   - 使用 `ChartDocument` 建立 ODC 圖表。
   - 使用 `FormulaDocument` 建立 ODF 公式。
   - 使用 `ImageDocument` 建立 ODI 影像文件。
   - 使用 `DatabaseDocument` 建立 ODB 資料來源描述。
5. **Profile 驗證與 i18n 在地化**：
   - 使用 `OdfComplianceProfiles.RocTaiwanOdfCns15251` 對產出的 ODP 執行 Profile 驗證。
   - 使用 `OdfLocalizer.DefaultCulture` 與 `OdfLocalizer.GetMessage(...)` 展示 `zh-TW` 在地化訊息查找。
   - 驗證結果與在地化訊息會輸出到主控台，不額外產生獨立輸出檔案。
6. **低記憶體高效能串流寫入 (OdsStreamWriter)**：
   - 示範以順序工作表寫入模式流式寫入 100 列以上的表格明細；實際峰值工作集依文件內容與執行環境而異，請以[效能基準線](../docs/performance-baselines.md)的可重現量測為準。
   - `SwitchToSheet` 支援交錯多工作表寫入，但會使用暫存緩衝，適合便利性優先而非嚴格低記憶體的情境（未在範例中實際執行，行為詳見 API 文件）。
7. **中繼資料 (Metadata) 讀取與更新**：
   - 展示如何載入既有檔案、讀取文件 metadata 標題與建立者資訊，並進行修改更新與二次存檔。
8. **進階轉檔與擴充套件整合**：
   - 使用 `OdfPdfExporter` 將 ODT 轉換並渲染匯出為 PDF 檔案。
   - 使用 `OdfHtmlExporter` 將 ODT 轉換並匯出為 HTML 網頁。
   - 使用 `OdfToDocxConverter` / `OdfToXlsxConverter` 轉出 OOXML。
   - 使用 `OdtOperationsExporter` / `OdtOperationsImporter` 展示協作操作匯出與回讀。
   - 使用 `RdfMetadata` 展示 RDF 三元組寫入與 SPARQL 查詢。
   - 使用 `OdfImageExporter` 將工作表渲染為 PNG。
9. **CNS 11643 罕字整合**：
   - 示範字型遞補分段、PUA 碼位遷移及合成 Big5E 對照表匯出；正式資料須由合法來源取得。
10. **文字文件串流寫入**：
   - 示範 `OdtStreamWriter` 的低常駐串流輸出與 `OdtStreamReader` 往返讀取。

---

## 預期產出檔案

執行成功後，您可以在 `samples/output/` 資料夾下找到以下主要產出：

| 檔名 | 格式 | 說明 |
| :--- | :--- | :--- |
| **`output_text.odt`** | ODF 文字文件 | 包含格式段落、表格與圖片的文字文件。 |
| **`output_text_updated.odt`** | ODF 文字文件 | 更新中繼資料（標題與建立者）後的版本。 |
| **`output_spreadsheet.ods`** | ODF 試算表 | 包含銷售統計資料與總計 SUM 公式的表格。 |
| **`output_presentation.odp`** | ODF 簡報 | 包含兩張投影片、轉場效果與形狀的簡報。 |
| **`output_drawing.odg`** | ODF 圖形文件 | 使用短名 facade 建立的流程圖。 |
| **`output_chart.odc`** | ODF 圖表文件 | 使用短名 facade 建立的季度營收圖表。 |
| **`output_formula.odf`** | ODF 公式文件 | 使用 `FormulaDocument` 建立的 MathML 公式。 |
| **`output_image.odi`** | ODF 影像文件 | 使用 `ImageDocument` 建立的影像封裝文件。 |
| **`output_database.odb`** | ODF 資料庫文件 | 使用 `DatabaseDocument` 建立的資料來源描述。 |
| **`output_stream.ods`** | ODF 試算表 | 透過 `OdsStreamWriter` 大量串流寫入的明細表。 |
| **`output_stream.odt`** | ODF 文字文件 | 透過 `OdtStreamWriter` 串流寫入的文字文件。 |
| **`cns11643-demo.odt`** | ODF 文字文件 | CNS 11643 罕字字型遞補與碼位遷移展示。 |
| **`cns11643-demo-big5e.csv`** | CSV 檔案 | 使用合成小型對照表產生的 Big5E 編碼展示。 |
| **`output_pdf.pdf`** | PDF 檔案 | 依 PDF 擴充套件目前能力轉譯的 PDF 文件。 |
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

- `Sample.cs` 為**大型整合展示應用程式**，覆蓋面廣，但不適合作為每個 API 的最小範例。
- RDF 展示會輸出查詢結果到主控台，但不額外產生獨立 RDF 檔案。
- Profile 驗證結果與 i18n 訊息展示輸出到主控台，不會另外建立報告檔。
- 範例假設本儲存庫結構完整存在，無法單獨複製 `Sample.cs` 到其他目錄直接執行。

## 相關文件

- [tools/README.md](../tools/README.md)
- [docs/getting-started.md](../docs/getting-started.md)
- [docs/cookbook.md](../docs/cookbook.md)
