# OdfKit Cookbook

本文件提供可直接改寫的常見 ODF 操作範例。範例只描述目前已有測試支撐的能力。

## 複雜文件場景總覽

OdfKit 的高階 API 目標是讓 C# / .NET 開發者用少量程式碼建立中高複雜度 ODF 文件；
目前建議以既有 facade 與 builder 混用，而不是直接操作 ZIP 或 XML：

- **年度報告（ODT）**：使用 `TextDocument.Builder()`、標題階層、目錄、段落富文字、表格、註腳、
  註解、區段、頁首／頁尾與嵌入圖表，最後可接 Markdown/RTF 延伸套件。
- **財務模型（ODS）**：使用 `SpreadsheetDocument.Create()`、多工作表、公式 helper、
  命名範圍、條件格式、資料驗證與嵌入圖表；大量資料輸出仍使用 `OdsStreamWriter`
  的嚴格順序低記憶體模式。
- **商業簡報（ODP）**：使用 `PresentationDocument.Builder()` 建立標題、內容、雙欄、
  圖表投影片、講者備註與轉場。
- **流程圖／架構圖（ODG）**：使用 `DrawingDocument.Builder()` 與頁面 facade 建立形狀、
  連接線、文字框、圖片與 SVG 匯出。

## 年度報告（ODT）

```csharp
using OdfKit.Chart;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
using OdfKit.Text;

using TextDocument report = TextDocument.Builder()
    .WithMetadata(metadata => metadata.Title("年度報告").Author("OdfKit"))
    .WithTheme(OdfDesignTheme.Flowchart)
    .WithStyles(OdfStyleSet.BusinessReport)
    .WithPageSetup(page => page.Header("年度報告"))
    .AddCoverPage("年度報告", "2026 年營運成果", "OdfKit", "2026 年")
    .AddTableOfContents("目錄", 2)
    .AddHeading("營運摘要", 2)
    .AddParagraph(paragraph => paragraph
        .Append("營收年增 ")
        .Append("18%", format => format.Bold().Color("#0066CC").BackgroundColor("#FFF2CC"))
        .Append("。")
        .AddFootnote("1", "示範資料，非實際財務數字。")
        .AddComment("reviewer", "請財務團隊確認最終數字。"))
    .AddTable(3, 2, table => table
        .SetCell(1, 1, "季度")
        .SetCell(1, 2, "營收")
        .SetCell(2, 1, "Q1")
        .SetCell(2, 2, "120")
        .SetCell(3, 1, "Q2")
        .SetCell(3, 2, "148"))
    .AddSection("ExecutiveSection", 2, OdfLength.FromCentimeters(0.5), section => section
        .AddParagraph("本區段使用雙欄版面呈現重點。")
        .Protected())
    .AddParagraph(paragraph => paragraph
        .Append("圖表摘要")
        .AddChart(new OdfChartDefinition
        {
            ChartType = OdfChartType.Bar,
            Title = "季度營收",
            DataRange = new OdfCellRange(0, 0, 2, 1, "Data"),
            HasLegend = true,
        }, OdfLength.FromCentimeters(8), OdfLength.FromCentimeters(5)))
    .AddParagraph(paragraph => paragraph
        .Append("品牌視覺 ")
        .AddImage(File.ReadAllBytes("logo.png"), OdfLength.FromCentimeters(2), OdfLength.FromCentimeters(2), "AnnualLogo"))
    .Build();
report.Save("annual-report.odt");
```

## 財務模型（ODS）

```csharp
using OdfKit.Chart;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using OdfKit.Styles;

using SpreadsheetDocument workbook = SpreadsheetDocument.Builder()
    .WithMetadata(metadata => metadata.Title("財務模型").Author("OdfKit"))
    .WithTheme(OdfDesignTheme.Flowchart)
    .WithStyles(OdfStyleSet.BusinessReport)
    .AddSheet("銷售", sheet => sheet
        .ImportTable(
            new[]
            {
                new { Month = "一月", Revenue = 120d, Cost = 72d },
                new { Month = "二月", Revenue = 148d, Cost = 83d },
            },
            row => [row.Month, row.Revenue, row.Cost],
            ["月份", "營收", "成本"])
        .AddFormulaColumn("D", "毛利", 2, 3, row => $"of:=[.B{row}]-[.C{row}]")
        .AddNamedRange("SalesModel", "A1:D3")
        .AddConditionalFormat("D2:D3", "cell-content()>50", "ProfitStyle")
        .AddDataBarFormat(new OdfCellRange(1, 3, 2, 3, "銷售"), new OdfColor("#638ec6"))
        .AddDecimalValidation("B2:C3", 0, 1000, "輸入範圍", "請輸入 0 到 1000 之間的數值。")
        .InsertChart("A1:D3", OdfChartType.Line, chart => chart.ChartTitle = "毛利趨勢")
        .AddPivotTable("SalesPivot", "A1:D3", "G1", pivot => pivot
            .AddRowField("月份")
            .AddDataField("營收", OdfPivotFunction.Sum)))
    .AddSheet("摘要", sheet => sheet.SetFormula("A1", "of:='銷售'.D2"))
    .Build();
workbook.Save("financial-model.ods");
```

若公式不是單一欄模型，可改用 `SetFormulaRange("D2:F20", (row, column) => ...)`
依儲存格位置批次產生公式。

## 商業簡報（ODP）

```csharp
using OdfKit.Presentation;
using OdfKit.Styles;

using PresentationDocument deck = PresentationDocument.Builder()
    .WithMetadata(metadata => metadata.Title("董事會簡報"))
    .WithTheme(OdfDesignTheme.Flowchart)
    .WithStyles(OdfStyleSet.BusinessReport)
    .WithLayoutPreset(OdfLayoutPreset.BusinessDeck)
    .WithMasterPage("BoardTheme", "#F6F8FB")
    .AddTitleSlide("Executive Summary", "年度重點", "營收成長與產品化路線")
    .AddTwoColumnSlide(
        "Roadmap",
        "下一季路線圖",
        ["Complex DSL", "JSON Collaboration subset"],
        ["Managed fidelity", "Corpus parity"],
        slide => slide
            .AddShape(OdfShapeType.Rectangle, 1, 11, 3, 1, shape => shape.WithId("roadmap_highlight"))
            .AddEntranceEffect("roadmap_highlight", OdfAnimationEffect.Fade))
    .AddChartSlide("Metrics", "營運指標", slide => slide
        .WithSpeakerNotes("先說結論，再切入財務模型。")
        .WithTransition(OdfTransitionType.Fade))
    .Build();
deck.Save("business-deck.odp");
```

## 流程圖／架構圖（ODG）

```csharp
using OdfKit.Drawing;
using OdfKit.Export;
using OdfKit.Styles;

using DrawingDocument drawing = DrawingDocument.Builder()
    .WithMetadata(metadata => metadata.Title("匯入流程"))
    .WithTheme(OdfDesignTheme.Flowchart)
    .WithStyles(OdfStyleSet.BusinessReport)
    .WithLayoutPreset(OdfLayoutPreset.FlowDiagram)
    .AddPage("主流程", page => page
        .AddLayer("流程")
        .AddFlowStep("load", "載入 ODF", 0, configure: shape => shape.OnLayer("流程"))
        .AddFlowStep("validate", "驗證封裝", 1, OdfShapeType.Ellipse, shape => shape.OnLayer("流程"))
        .AddFlowStep("export", "輸出報告", 2, configure: shape => shape.OnLayer("流程"))
        .AddConnector("load", "validate", OdfConnectorType.Straight)
        .AddConnector("validate", "export", OdfConnectorType.Straight)
        .AddGroup("圖例", group => group
            .AddRectangle(13, 4, 1, 1)
            .AddTextBox("完成節點", 14.2, 4, 3, 1)))
    .Build();
drawing.Save("flow.odg");
drawing.SaveAsSvg("flow.svg");
```

## TDF JSON Collaboration 相容子集合

```csharp
using OdfKit.Collaboration;
using OdfKit.Text;

using TextDocument document = TextDocument.Create();
document.Body.Paragraphs.Add("協作段落");

string tdfJson = OdtOperationsExporter.ExportToJson(
    document,
    OdtOperationCompatibilityOptions.CreateTdfCompatibility());

using TextDocument merged = OdtOperationsImporter.Merge(
    tdfJson,
    OdtOperationCompatibilityOptions.CreateTdfCompatibility(),
    out OdtOperationImportReport report);

Console.WriteLine(report.ReplayedCount);
```

匯入端目前支援 TDF changes 封包、typed operation log、未知欄位 round-trip、段落與文字新增、Tab、換行、基本 range 字元格式（含前景色、背景色、大小寫轉換、small-caps 與上標／下標）、
單段落刪除／移動、最上層段落分割／合併、基本清單段落、固定尺寸文字表格填值、欄位、comment、header/footer、font declaration 與安全 drawing placeholder。
完整 OT／CRDT、任意衝突合併、跨段落刪除／移動、完整 drawing DOM 與 header/footer/note selection
仍屬非目標；不明或無法安全套用的 operation 會進入 import report 診斷。

## 建立 ODT

```csharp
using OdfKit.Text;

using TextDocument document = TextDocument.Create();
document.Body.Headings.Add("會議記錄", 1);
document.Body.Paragraphs.Add("今日討論 ODF 文件自動化。");
document.Save("meeting.odt");

using TextDocument loaded = TextDocument.Load("meeting.odt");
Console.WriteLine(loaded.Body.Headings.Items[0].TextContent);
Console.WriteLine(loaded.Body.Paragraphs.Items[0].TextContent);
```

## 建立 ODS

```csharp
using OdfKit.Spreadsheet;

using SpreadsheetDocument workbook = SpreadsheetDocument.Create();
OdfTableSheet sheet = workbook.Worksheets.Add("Data");
sheet.Cells["A1"].CellValue = "Name";
sheet.Cells["B1"].CellValue = "Amount";
sheet.Cells["A2"].CellValue = "ODF";
sheet.Cells["B2"].CellValue = 42;
sheet.Ranges["A1:B2"].NameAs("DataRange");
sheet.FreezePanes(1, 0);
workbook.Save("data.ods");

using SpreadsheetDocument loadedWorkbook = SpreadsheetDocument.Load("data.ods");
OdfTableSheet loadedSheet = loadedWorkbook.Worksheets["Data"];
Console.WriteLine(loadedSheet.NamedRanges[0].Name);
Console.WriteLine(loadedSheet.FrozenPanes.Rows);
```

## 讀取儲存格

```csharp
using OdfKit.Spreadsheet;

using SpreadsheetDocument workbook = SpreadsheetDocument.Load("data.ods");
string text = workbook.Worksheets[0].Cells["A2"].DisplayText;
```

## 設定公式

```csharp
using OdfKit.Spreadsheet;

using SpreadsheetDocument workbook = SpreadsheetDocument.Create();
OdfTableSheet sheet = workbook.Worksheets.Add("Calc");
sheet.Cells["A1"].CellValue = 10;
sheet.Cells["A2"].CellValue = 20;
sheet.Cells["A3"].Formula = "of:=SUM([.A1:.A2])";
workbook.Save("calc.ods");
```

## 搜尋與更新試算表公式

```csharp
using OdfKit.Spreadsheet;

using SpreadsheetDocument workbook = SpreadsheetDocument.Load("calc.ods");
foreach (OdfFormulaCellInfo formulaCell in workbook.GetFormulaCells(
    cell => cell.Formula.Contains("SUM", StringComparison.Ordinal)))
{
    Console.WriteLine($"{formulaCell.ExcelAddress}: {formulaCell.Formula}");
}

workbook.ReplaceFormulaText("SUM", "AVERAGE");
workbook.Save("calc-updated.ods");
```

## 建立 ODP（Fluent Builder）

```csharp
using OdfKit.Presentation;

using PresentationDocument deck = PresentationDocument.Builder()
    .WithMetadata(metadata => metadata.Title("產品簡報"))
    .AddSlide("開場", slide => slide
        .AddTitle("歡迎使用 OdfKit")
        .WithSpeakerNotes("介紹產品定位")
        .WithTransition(OdfTransitionType.Fade))
    .Build();
deck.Save("intro.odp");
```

## 建立 ODP

```csharp
using OdfKit.Presentation;
using OdfKit.Styles;

using PresentationDocument deck = PresentationDocument.Create();
OdfSlide slide = deck.Slides.Add("Intro");
slide.AddTextBox(
    OdfLength.FromCentimeters(1),
    OdfLength.FromCentimeters(1),
    OdfLength.FromCentimeters(10),
    OdfLength.FromCentimeters(2),
    "簡報標題");
deck.Save("intro.odp");

using PresentationDocument loadedDeck = PresentationDocument.Load("intro.odp");
Console.WriteLine(loadedDeck.Slides[0].TextBoxes[0].Text);
```

## 讀取 ODP 圖片參照

```csharp
using OdfKit.Presentation;

using PresentationDocument deck = PresentationDocument.Load("intro.odp");
foreach (OdfPicture picture in deck.Slides[0].Pictures)
{
    Console.WriteLine(picture.ImageHref);
}
```

## 建立 ODG（Fluent Builder）

```csharp
using OdfKit.Drawing;

using DrawingDocument drawing = DrawingDocument.Builder()
    .WithMetadata(metadata => metadata.Title("流程圖草稿"))
    .AddPage("主畫布", page => page
        .AddRectangle(1, 1, 4, 2)
        .AddTextBox("開始", 1, 4, 3, 1))
    .Build();
drawing.Save("drawing.odg");
```

## 建立並讀取 ODG

```csharp
using OdfKit.Drawing;
using OdfKit.Presentation;
using OdfKit.Styles;

using DrawingDocument drawing = DrawingDocument.Create();
OdfDrawPage page = drawing.Pages.Add("Canvas");
page.AddShape(
    OdfShapeType.Rectangle,
    OdfLength.FromCentimeters(1),
    OdfLength.FromCentimeters(1),
    OdfLength.FromCentimeters(4),
    OdfLength.FromCentimeters(2));
page.AddTextBox(
    OdfLength.FromCentimeters(1),
    OdfLength.FromCentimeters(4),
    OdfLength.FromCentimeters(6),
    OdfLength.FromCentimeters(1),
    "流程圖");
drawing.Save("drawing.odg");

using DrawingDocument loadedDrawing = DrawingDocument.Load("drawing.odg");
Console.WriteLine(loadedDrawing.Pages[0].Shapes[0].LocalName);
Console.WriteLine(loadedDrawing.Pages[0].TextBoxes[0].Text);
```

## 建立 ODI 影像文件

```csharp
using OdfKit.Image;
using OdfKit.Styles;

byte[] bytes = File.ReadAllBytes("photo.png");
using OdfImageDocument image = OdfImageDocument.Create();
image.SetImageLayout(
    OdfLength.FromCentimeters(1),
    OdfLength.FromCentimeters(2),
    OdfLength.FromCentimeters(6),
    OdfLength.FromCentimeters(4),
    "ProductImage",
    "產品照片",
    "一張用於型錄的產品照片。");
image.SetImage(bytes, "photo.png");
image.Save("photo.odi");

using OdfImageDocument loadedImage = OdfImageDocument.Load("photo.odi");
Console.WriteLine(loadedImage.FrameTitle);
Console.WriteLine(loadedImage.FrameWidth);
```

## 建立 ODC 圖表

```csharp
using OdfKit.Chart;
using OdfKit.Spreadsheet;
using OdfKit.Styles;

using ChartDocument chart = ChartDocument.Builder()
    .WithType(OdfChartType.Bar)
    .WithTitle("年度營收")
    .WithStyles(OdfStyleSet.BusinessReport)
    .WithDataRange("Sales", new OdfCellRange(0, 0, 4, 2), firstRowAsHeader: true, firstColumnAsLabel: true)
    .WithLegend(position: "end")
    .WithAxis("y", axis => axis.WithTitle("營收（萬元）").WithMinimum(0))
    .ConfigureSeries(0, series => series
        .WithStyle(style => style.FillColor = "#4472C4")
        .WithDataLabels(OdfChartDataLabelPreset.ValueAndCategoryName))
    .Build();
chart.Save("revenue.odc");

using ChartDocument loadedChart = ChartDocument.Load("revenue.odc");
Console.WriteLine(loadedChart.ChartTitle);
Console.WriteLine(loadedChart.FindSeriesDataLabels(0)?.ShowCategoryName);
```

## 建立 ODF 公式（Fluent Builder）

```csharp
using OdfKit.Formula;

using OdfFormulaDocument formula = OdfFormulaDocument.Builder()
    .WithIdentifierEquation("F", "ma")
    .Build();
formula.Save("equation.odf");
```

## 編輯 ODF 公式 token tree

```csharp
using OdfKit.Formula;

using OdfFormulaDocument formula = OdfFormulaDocument.Builder()
    .WithTokens(
        OdfMathToken.Superscript(
            OdfMathToken.Identifier("x"),
            OdfMathToken.Number("2")),
        OdfMathToken.Operator("+"),
        OdfMathToken.Identifier("y"))
    .Build();

OdfMathToken root = OdfMathToken.Row(formula.GetMathTokens().ToArray());
OdfMathToken? exponent = root.FindFirst(OdfMathTokenKind.Number);
IEnumerable<OdfMathToken> identifiers = root.GetAll(OdfMathTokenKind.Identifier);

OdfMathToken updatedRoot = root.ReplaceFirst(
    token => token.Kind == OdfMathTokenKind.Number && token.Text == "2",
    token => OdfMathToken.Number("3"));

OdfMathToken denominator = OdfMathToken.Fraction(
    OdfMathToken.Identifier("a"),
    OdfMathToken.Identifier("b"))
    .WithChild(1, OdfMathToken.Identifier("c"));

formula.SetMathRow(updatedRoot, OdfMathToken.Operator("/"), denominator);
formula.Save("edited-equation.odf");
```

## 建立 ODF 公式

```csharp
using OdfKit.Formula;

using OdfFormulaDocument formula = OdfFormulaDocument.Create();
formula.SetMathRow(
    OdfMathToken.Identifier("x"),
    OdfMathToken.Operator("="),
    OdfMathToken.Number("1"));
formula.Save("equation.odf");

using OdfFormulaDocument loadedFormula = OdfFormulaDocument.Load("equation.odf");
Console.WriteLine(loadedFormula.MathText);
Console.WriteLine(loadedFormula.MathTokens[0].Text);
```

## 建立 ODB 資料來源描述

```csharp
using OdfKit.Database;

using OdfDatabaseDocument database = OdfDatabaseDocument.Create();
database.SetConnection("sdbc:embedded:hsqldb");
database.AddDataSourceSetting("AppendTableAliasName", OdfDatabaseDataSourceSettingType.Boolean, "true");
database.AddTable("Customers", "SELECT * FROM Customers");
database.AddQuery(
    "ActiveCustomers",
    "SELECT * FROM Customers WHERE IsActive = TRUE",
    "Active customers",
    "只列出啟用中的客戶。",
    escapeProcessing: true);
database.Save("data.odb");

using OdfDatabaseDocument loaded = OdfDatabaseDocument.Load("data.odb");
Console.WriteLine(loaded.ConnectionHref);
Console.WriteLine(loaded.FindDataSourceSetting("AppendTableAliasName")?.Values[0]);
Console.WriteLine(loaded.Tables[0].Name);
Console.WriteLine(loaded.FindQuery("ActiveCustomers")?.Command);
```

## 驗證文件

```csharp
using OdfKit.Compliance;

OdfValidationReport report = OdfValidator.Validate("intro.odp");
Console.WriteLine(report.IsValid ? "valid" : "invalid");
```

## 保留未知內容

```csharp
using OdfKit.Core;

using OdfDocument document = OdfDocument.Load("vendor-file.odt");
document.Save("vendor-file-copy.odt");
```

此路徑適合在只需要讀取、保存或做有限修改時使用。未知 XML 與未知 package entries 的保真由 round-trip 測試覆蓋。

## 串流寫入大型 ODS

以下範例使用嚴格順序寫入模式：每張工作表以 `WriteStartSheet` 開始、
以 `WriteEndSheet` 結束後再寫下一張，適合低記憶體輸出。若需要在多張工作表之間
交錯寫入，`SwitchToSheet` 會使用暫存緩衝，便利性較高但不屬於純串流模式。

```csharp
using OdfKit.Spreadsheet;

using FileStream output = File.Create("large.ods");
using OdsStreamWriter writer = new(output);
writer.WriteStartSheet("Data");
for (int row = 0; row < 1000; row++)
{
    writer.WriteStartRow();
    writer.WriteCell("Row " + row);
    writer.WriteCell(row);
    writer.WriteEndRow();
}
writer.WriteEndSheet();
```

## 匯出任意物件序列或 EF Core 查詢結果

`OdsStreamWriter.WriteDataAsync<T>` 透過 `ObjectDataReader<T>` 將任意
`IEnumerable<T>`／`IAsyncEnumerable<T>` 轉接為 `DbDataReader`，把 `T` 的每個
可讀公開屬性對應成一個資料行，寫入時低記憶體串流，不需要整個序列先載入記憶體。

```csharp
using OdfKit.Spreadsheet;

SalesRow[] rows =
[
    new SalesRow { Region = "North", Amount = 120.5 },
    new SalesRow { Region = "South", Amount = 98.2 },
];

await using FileStream output = File.Create("sales.ods");
await using OdsStreamWriter writer = new(output);
writer.WriteStartSheet("Sales");
await writer.WriteDataAsync(rows, includeColumnNames: true);
writer.WriteEndSheet();

// 若貼入單一頂層陳述式檔案（top-level statements），型別宣告必須放在
// 所有執行陳述式之後；若貼入既有類別/方法內，則可放在任何位置。
public sealed class SalesRow
{
    public string? Region { get; set; }
    public double Amount { get; set; }
}
```

若資料來源是 Entity Framework Core 查詢，建議先 `AsNoTracking()` 並用
`.Select(...)` 投影成 DTO，再以 `AsAsyncEnumerable()` 交給
`WriteDataAsync<T>`，資料會逐列從資料庫串流到 ODS，不需一次載入整個結果集：

```csharp
using Microsoft.EntityFrameworkCore;
using OdfKit.Spreadsheet;

IAsyncEnumerable<SalesRow> query = dbContext.Sales
    .AsNoTracking()
    .Select(sale => new SalesRow { Region = sale.Region, Amount = sale.Amount })
    .AsAsyncEnumerable();

await using FileStream output = File.Create("sales.ods");
await using OdsStreamWriter writer = new(output);
writer.WriteStartSheet("Sales");
await writer.WriteDataAsync(query, includeColumnNames: true);
writer.WriteEndSheet();
```

若要反向把 ODS 內容批次灌入 SQL Server，`OdsStreamReader` 本身就是
`DbDataReader`，可直接交給 `SqlBulkCopy`，不需要額外的轉接層：

```csharp
using Microsoft.Data.SqlClient;
using OdfKit.Spreadsheet;

using OdsStreamReader reader = new(File.OpenRead("sales.ods"));
await using SqlConnection connection = new(connectionString);
await connection.OpenAsync();
using SqlBulkCopy bulkCopy = new(connection) { DestinationTableName = "Sales", EnableStreaming = true };
await bulkCopy.WriteToServerAsync(reader);
```

## CLI 驗證與轉換

```powershell
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate file.odt
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate file.odt --format json
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate samples --recursive --fail-on warning
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate file.odt --profile OASIS_ODF_1_4_Extended
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- sanitize input.odt sanitized.odt
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- sanitize encrypted.odt sanitized.odt --password old-secret --output-password new-secret --encryption aes256
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- typed-dom-coverage --format json
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- convert-flat file.odt file.fodt
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- pack file.fodt file.odt
```

`validate` 在 CI 中可用 exit code 判斷結果：`0` 表示通過，`1` 表示驗證錯誤或 `--fail-on warning` 命中，`2` 表示參數或路徑錯誤。
`sanitize` 會移除巨集、指令碼參照與簽章 artifact，並另存為新的 ODF 檔案；輸出會包含
`removed-artifacts`，方便 CI 稽核實際移除數量。加密文件可用 `--password` 載入，並用
`--output-password` 重新加密輸出；密碼錯誤時會以 exit code `2` 回報，不會產生輸出檔。

## Web 應用程式檔案下載

在 Web 應用程式中動態產生 ODF 文件並提供下載時，建議直接將其儲存至記憶體串流 (MemoryStream) 或位元組陣列，避免在伺服器上產生實體暫存檔。

### ODF MIME 類型對照表

在設定 HTTP 回應時，請根據下載的文件格式設定正確的 MIME 類型：

| 擴充副檔名 | 文件類型 | MIME 類型 (Content-Type) |
| :--- | :--- | :--- |
| `.odt` | ODF 文字文件 | `application/vnd.oasis.opendocument.text` |
| `.ods` | ODF 試算表 | `application/vnd.oasis.opendocument.spreadsheet` |
| `.odp` | ODF 簡報 | `application/vnd.oasis.opendocument.presentation` |
| `.odg` | ODF 繪圖 | `application/vnd.oasis.opendocument.graphics` |

### ASP.NET Core Razor Pages (非同步)

在 ASP.NET Core 中，應優先使用 `SaveToStreamAsync` 將文件寫入 `MemoryStream`，並回傳 `FileStreamResult`。這可以避免在大量請求時阻塞執行緒。

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OdfKit.Spreadsheet;

public class DownloadModel : PageModel
{
    public async Task<IActionResult> OnGetDownloadOdsAsync()
    {
        // 1. 建立 ODS 試算表文件
        using SpreadsheetDocument workbook = SpreadsheetDocument.Create();
        OdfTableSheet sheet = workbook.Worksheets.Add("Data");
        sheet.Cells["A1"].CellValue = "Name";
        sheet.Cells["B1"].CellValue = "Amount";
        sheet.Cells["A2"].CellValue = "OdfKit User";
        sheet.Cells["B2"].CellValue = 100;

        // 2. 將文件非同步寫入 MemoryStream 記憶體串流
        var stream = new MemoryStream();
        await workbook.SaveToStreamAsync(stream);
        
        // 3. 將串流指標移回起點以供讀取
        stream.Position = 0;

        // 4. 設定 ODF 試算表 MIME 類型與下載檔名
        string contentType = "application/vnd.oasis.opendocument.spreadsheet";
        string fileName = "report.ods";

        // 回傳 FileStreamResult，ASP.NET Core 會自動在 HTTP 回應完成後關閉並釋放串流
        return File(stream, contentType, fileName);
    }
}
```

### ASP.NET WebForms (同步)

在傳統的 ASP.NET WebForms 中，建議使用 `SaveToBytes` 將文件轉為位元組陣列，寫入 `HttpResponse`，並呼叫 `HttpContext.Current.ApplicationInstance.CompleteRequest()`。這可避免 WebForms 繼續執行 Page 生命週期而將額外的 HTML 標記寫入檔案，導致下載的 ODF 檔案損毀。

```csharp
using System;
using System.IO;
using System.Web;
using OdfKit.Spreadsheet;

protected void btnDownload_Click(object sender, EventArgs e)
{
    // 1. 建立 ODS 試算表文件
    using SpreadsheetDocument workbook = SpreadsheetDocument.Create();
    OdfTableSheet sheet = workbook.Worksheets.Add("Data");
    sheet.Cells["A1"].CellValue = "Name";
    sheet.Cells["B1"].CellValue = "Amount";
    sheet.Cells["A2"].CellValue = "OdfKit User";
    sheet.Cells["B2"].CellValue = 100;

    // 2. 將文件寫入位元組陣列
    byte[] fileBytes = workbook.SaveToBytes();

    // 3. 設定 HTTP 回應標頭與內容
    HttpResponse response = HttpContext.Current.Response;
    response.Clear();
    response.ClearHeaders();
    response.ContentType = "application/vnd.oasis.opendocument.spreadsheet";
    response.AddHeader("Content-Disposition", "attachment; filename=\"report.ods\"");
    response.AddHeader("Content-Length", fileBytes.Length.ToString());
    response.BinaryWrite(fileBytes);
    
    // 4. 結束回應，避免 WebForms 繼續渲染 HTML 頁面內容而導致檔案損毀
    response.Flush();
    HttpContext.Current.ApplicationInstance.CompleteRequest();
}
```
