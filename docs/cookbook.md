# OdfKit 實作食譜

本文件提供可直接改寫的常見 ODF 操作範例。範例只描述目前已有測試支撐的能力。

## 擷取跨格式純文字

`ExtractText` 是 ODT、ODS、ODP、ODG 與其它 ODF 文件共用的內容擷取入口。預設保留段落、
儲存格、投影片與頁面邊界，但不包含註解、已刪除的追蹤修訂內容或簡報備忘稿。

```csharp
using OdfKit.Core;

using OdfDocument document = OdfDocument.Load("input.odt");
string visibleText = document.ExtractText();
string auditText = document.ExtractText(new OdfTextExtractionOptions
{
    IncludeAnnotations = true,
    IncludeTrackedChanges = true,
    IncludePresentationNotes = true
});
```

## 管理內嵌 ODF 子文件

```csharp
using OdfKit.Core;
using OdfKit.Spreadsheet;
using OdfKit.Text;

using TextDocument host = TextDocument.Create();
using SpreadsheetDocument worksheet = SpreadsheetDocument.Create();
worksheet.Worksheets.Add("Data");
worksheet.SetValue("Data", "A1", "Embedded");

OdfEmbeddedObjectInfo added = host.Package.AddEmbeddedDocument("Object 1", worksheet);
using Stream contentXml = added.OpenContent();

foreach (OdfEmbeddedObjectInfo item in host.Package.GetEmbeddedObjectInfos())
{
    Console.WriteLine($"{item.Path}: {item.DocumentKind} ({item.MediaType})");
}

host.Package.RemoveEmbeddedObject("Object 1");
```

`AddEmbeddedDocument` 會複製子文件的核心 XML 與資源並建立 manifest 目錄項目；
`ReplaceEmbeddedDocument` 以相同路徑執行移除後新增的生命週期。

## 複雜文件場景總覽

四主格式高階 API 採相同的集合生命週期：用 `Get*` 取得集合、`Find*` 查找單項、
`Remove*` 移除指定項目，並以 `Clear*` 清空集合。載入既有文件後使用相同 facade，
不需要切換到另一套 reader 物件模型。完整契約與破壞性遷移方式見
[四主格式語意 facade reference](reference/semantic-facades.md)及
[高階 API 遷移指南](migration-high-level-api.md)。

| 格式 | 代表性完整生命週期 |
| --- | --- |
| ODT | `FindFormControl`、`RemoveFormControl`、`ClearFormControls` |
| ODS | `FindEmbeddedChart`、`RemoveEmbeddedChart`、`ClearEmbeddedCharts` |
| ODP | slide、notes、media、table、shape、connector 與 group 的 typed CRUD |
| ODG | `FindGradient`、`RenameGradient`、`RemoveGradient`、`ClearGradients` |

OdfKit 的高階 API 目標是讓 C# / .NET 開發者用少量程式碼建立中高複雜度 ODF 文件；
目前建議以既有外觀層與 builder 混用，而不是直接操作 ZIP 或 XML：

- **年度報告（ODT）**：使用 `TextDocument.Builder()`、標題階層、目錄、段落富文字、表格、註腳、
  註解、區段、頁首／頁尾與嵌入圖表，最後可接 Markdown/RTF 延伸套件。
- **財務模型（ODS）**：使用 `SpreadsheetDocument.Create()`、多工作表、公式 helper、
  命名範圍、條件格式、資料驗證與嵌入圖表；大量資料輸出仍使用 `OdsStreamWriter`
  的嚴格順序低記憶體模式。
- **商業簡報（ODP）**：使用 `PresentationDocument.Builder()` 建立標題、內容、雙欄、
  圖表投影片、講者備註與轉場。
- **流程圖／架構圖（ODG）**：使用 `DrawingDocument.Builder()` 與頁面外觀層建立形狀、
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

`SaveAsSvg` 定義於 `OdfKit.Extensions.Html`（`OdfManagedTextExportExtensions`），此範例需另參考該套件。

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

匯入端目前支援 TDF changes 封包、typed operation log、未知欄位來回讀寫、段落與文字新增、Tab、換行、基本 range 字元格式（含前景色、背景色、大小寫轉換、small-caps 與上標／下標）、
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

## 安全重算與儲存公式

```csharp
using OdfKit.Core;
using OdfKit.Formula;
using OdfKit.Spreadsheet;

using SpreadsheetDocument workbook = SpreadsheetDocument.Load("calc.ods");
var limits = new OdfFormulaEvaluationOptions
{
    MaxFormulaCount = 100_000,
    MaxOperations = 10_000_000,
    MaxCellReads = 10_000_000,
    TimeLimit = TimeSpan.FromSeconds(30)
};

OdfFormulaEvaluationReport report =
    workbook.EvaluateFormulas(limits, cancellationToken);
Console.WriteLine(
    $"已評估 {report.EvaluatedFormulaCount} 式，" +
    $"讀取 {report.CellReadCount} 格。");

workbook.Save(
    "calc-updated.ods",
    new OdfSaveOptions
    {
        FormulaStrategy = OdfFormulaSaveStrategy.Calculate,
        FormulaEvaluationOptions = limits
    });
```

需要反覆修改輸入時，建立一次增量工作階段即可保留相依圖。第一次呼叫會完整重算，
之後只計算受影響的公式子圖：

```csharp
OdfFormulaEvaluationSession session =
    workbook.CreateFormulaEvaluationSession(limits);
session.Recalculate(cancellationToken);

workbook.Worksheets["Calc"].Cells["A1"].CellValue = 25;
OdfFormulaEvaluationReport incremental =
    session.Recalculate(cancellationToken);
Console.WriteLine($"本輪只重算 {incremental.EvaluatedFormulaCount} 式。");
```

若透過儲存格 API 以外的方式大幅重組文件，呼叫 `session.Invalidate()`，讓下一輪重建
完整相依狀態。工作階段不具執行緒安全性。

若只要讓下一個試算表應用程式重算，改用
`OdfFormulaSaveStrategy.MarkForRecalculation`；它會清除舊快取但保留公式與格式。
預設的 `PreserveCachedValues` 不改公式、快取或顯示文字。外部參照預設只讀既有快取；
啟用 `AllowConfiguredResolver` 代表呼叫端明確信任已設定的 resolver。取消會擲出
`OperationCanceledException`；其它安全或支援失敗會以
`OdfFormulaEvaluationException.Report` 回報且不部分寫回。

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

建議使用短名 facade `ImageDocument`；既有 `OdfImageDocument` 入口仍保留相容性。

```csharp
using OdfKit.Image;
using OdfKit.Styles;

byte[] bytes = File.ReadAllBytes("photo.png");
using ImageDocument image = ImageDocument.Create();
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

using ImageDocument loadedImage = ImageDocument.Load("photo.odi");
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

## 建立進階實務圖表

```csharp
using OdfKit.Chart;
using OdfKit.DOM;
using OdfKit.Spreadsheet;

using ChartDocument bubble = ChartDocument.CreateBubble(
    "銷售機會泡泡圖",
    new OdfBubbleChartSeriesRequest(
        "Sales.$A$2:.$A$20",
        "Sales.$B$2:.$B$20",
        "Sales.$C$2:.$C$20",
        "Sales.$B$1"));

using ChartDocument stock = ChartDocument.CreateStock(
    "OHLC 股票圖",
    new OdfStockChartSeriesRequest(
        "Stock.$B$2:.$B$31",
        "Stock.$C$2:.$C$31",
        "Stock.$D$2:.$D$31",
        "Stock.$E$2:.$E$31",
        "Stock.$F$2:.$F$31"));

stock.ApplyStockMarkerStyle(new OdfStockMarkerStyle(
    new OdfChartSurfaceStyle("GainStyle", FillColor: "#2E7D32"),
    new OdfChartSurfaceStyle("LossStyle", FillColor: "#C62828"),
    new OdfChartSurfaceStyle("RangeStyle", StrokeColor: "#555555")));

using ChartDocument column3d = ChartDocument.FromTable(
    "Sales",
    new OdfCellRange(0, 0, 12, 3, "Sales"),
    OdfChartPreset.Column3D,
    "年度營收 3D 圖");

column3d.Apply3DOptions(new OdfChart3DOptions
{
    Projection = OdfDr3dProjection.Perspective,
    AngleOffset = 45,
    LightingMode = true,
    WallStyle = new OdfChartSurfaceStyle("WallStyle", FillColor: "#EEEEEE"),
    FloorStyle = new OdfChartSurfaceStyle("FloorStyle", FillColor: "#DDDDDD"),
    Lights =
    {
        new OdfChartLightRequest("(0 0 1)", "#FFFFFF", Enabled: true),
    },
});
```

## 檢查實務互通風險

```csharp
using OdfKit.Compliance;

OdfPracticalCompatibilityReport report =
    OdfPracticalCompatibilityValidator.Validate(
        column3d,
        OdfPracticalCompatibilityProfile.MicrosoftOfficeOdf);

foreach (OdfPracticalCompatibilityIssue issue in report.Issues)
{
    Console.WriteLine($"{issue.RuleId}: {issue.Message}");
    Console.WriteLine(issue.Suggestion);
}
```

`OdfPracticalCompatibilityValidator` 偏向實務提示，不取代 OASIS schema 驗證。它會針對巨集／腳本、非可攜影像格式、巢狀文字方塊、複雜群組、樣式過度分裂、進階圖表與頁首頁尾版面等跨工具編輯風險給出建議。

## 低魔法模板填值

```csharp
using OdfKit;
using OdfKit.Text;

using TextDocument template = TextDocument.Create();
template.AddParagraph("客戶：{{Name}}");
template.AddParagraph("金額：{{Amount}}");

TemplateBinder.Bind(template, new Dictionary<string, object?>
{
    ["Name"] = "星河股份有限公司",
    ["Amount"] = 1200,
});

template.Save("contract.odt");
```

集合資料可用 `{{Items[].Field}}` 展開。`OdfTemplateBindReport` 會回報展開集合、
命中占位符、未解析 token 與非致命警告；若同一個模板節點混用多個集合，會記錄
警告而不做含糊展開。

```csharp
OdfTemplateBindReport bindReport = TemplateBinder.Bind(
    template,
    new Dictionary<string, object?>
    {
        ["Items"] = new[]
        {
            new { Name = "設計", Amount = 1000 },
            new { Name = "驗證", Amount = 800 },
        },
    },
    new OdfTemplateBindOptions());
```

## 試算表範圍批次操作

```csharp
using OdfKit.Spreadsheet;

using SpreadsheetDocument workbook = SpreadsheetDocument.Create();
OdfTableSheet sheet = workbook.Worksheets.Add("Data");

sheet.SetValues(
    new OdfCellAddress(0, 0, "Data"),
    new object?[,]
    {
        { "Name", "Amount" },
        { "A", 10d },
    });

workbook.AppendRows(
    "Data",
    [
        ["B", 20d],
        ["C", 30d],
    ]);

OdfCellRange? used = workbook.GetUsedRange("Data");
```

若只是產生大型 ODS，仍應優先使用 `OdsStreamWriter`；上述 range helper 適合已載入
DOM 後的修改、樣式與公式工作流。

## 簡報、繪圖與圖片批次更新

```csharp
using OdfKit.Presentation;

using PresentationDocument deck = PresentationDocument.Create();
OdfSlide slide = deck.AddSlide("Intro");
OdfPicture picture = slide.AddPicture(imageBytes, 1.Cm(), 3.Cm(), 4.Cm(), 3.Cm());
picture.Id = "hero";

deck.ReplaceText("{{Name}}", "OdfKit");
deck.UpdatePictures([
    new OdfPictureUpdateRequest
    {
        Name = "hero",
        AltText = "Hero image",
        Width = 5.Cm(),
    },
]);
```

`ImageDocument.InspectImages()` 只做檢查與建議，會回報非可攜格式、缺少替代文字、
過大圖片、裁切／旋轉與重複圖片 bytes；核心不做影像轉檔。

## 建立 ODF 公式（Fluent Builder）

```csharp
using OdfKit.Formula;

using FormulaDocument formula = FormulaDocument.Builder()
    .WithIdentifierEquation("F", "ma")
    .Build();
formula.Save("equation.odf");
```

## 編輯 ODF 公式 token tree

```csharp
using OdfKit.Formula;

using FormulaDocument formula = FormulaDocument.Builder()
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

using FormulaDocument formula = FormulaDocument.Create();
formula.SetMathRow(
    OdfMathToken.Identifier("x"),
    OdfMathToken.Operator("="),
    OdfMathToken.Number("1"));
formula.Save("equation.odf");

using FormulaDocument loadedFormula = FormulaDocument.Load("equation.odf");
Console.WriteLine(loadedFormula.MathText);
Console.WriteLine(loadedFormula.MathTokens[0].Text);
```

## 建立 ODB 資料來源描述

建議使用短名 facade `DatabaseDocument`；既有 `OdfDatabaseDocument` 入口仍保留相容性。

```csharp
using OdfKit.Database;

using DatabaseDocument database = DatabaseDocument.Create();
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

using DatabaseDocument loaded = DatabaseDocument.Load("data.odb");
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

此路徑適合在只需要讀取、儲存或做有限修改時使用。未知 XML 與未知 package entries 的保真由來回讀寫測試覆蓋。

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

## 讀取／編輯大型 ODS 的記憶體模型

當要處理的檔案可能達到數百 MB 時，應依「只需唯讀匯出」或「需要讀取後修改再寫回」
選擇不同路徑，兩者的記憶體特性差異很大：

| 場景 | 建議路徑 | 記憶體特性 |
| --- | --- | --- |
| 只需要把 ODS 內容匯出成 CSV、灌入資料庫、或做逐列彙總，不需要修改原始檔案 | `OdsStreamReader`（實作 `DbDataReader`，SAX 風格逐列讀取） | 不建立完整 DOM；仍配置目前列、儲存格文字及 ZIP／XML 緩衝，並受 Reader options 限制 |
| 需要載入既有 ODS、修改儲存格/樣式/公式後存回 | `SpreadsheetDocument.Load(...)` / `OdfDocument.Load(...)` DOM 路徑 | 見下方說明 |

`OdsStreamReader` 的用法見前面〈匯出任意物件序列或 EF Core 查詢結果〉一節的
`SqlBulkCopy` 範例；它完全不建立 DOM，一次只解析目前列，適合單向匯出。

### DOM 編輯路徑的兩層記憶體最佳化

透過 `SpreadsheetDocument.Load`／`OdfDocument.Load` 載入既有文件時，DOM 樹**並非**
一次性完整攤平載入，而是有兩層彼此獨立的最佳化：

1. **文件層級延遲載入（lazy loading）**：`content.xml` 中超過 8192 bytes 的
   `table:table`（以及巢狀的 `office:meta`／`office:settings`／`text:list` 等）
   元素，載入時只保留原始 UTF-8 位元組，不會立即展開成 `OdfNode`／`OdfElement` 樹；
   直到第一次真正存取該表格內容（例如讀取某個儲存格）時，才會透過內部的
   `EnsureMaterialized()` 一次性展開該表格的行/儲存格 DOM。這代表：只要程式沒有
   走訪到某張工作表，那張工作表就不會佔用 DOM 記憶體。
2. **表格儲存格採稀疏原生分頁儲存，而非 `OdfElement` 樹**：一旦表格被存取，其
   儲存格資料是以固定 40 bytes 的原生結構（`NativeCell`），依 128×128 分頁
   （每頁約 655 KB）配置在非受控記憶體中，**不是**以一般的 `OdfElement`/`XElement`
   物件樹表示。系統會維護一組「熱頁」（未壓縮、可直接讀寫）與「冷頁」（以
   `DeflateStream` 壓縮，通常可壓縮至原始 1/10 大小）：存取超出熱頁上限時，最久
   未存取的頁面會被自動壓縮轉為冷頁；之後若再次存取，該頁會自動解壓還原為熱頁，
   同時視需要淘汰其他熱頁以維持上限。

因此，即使某張工作表有數百萬個儲存格，只要熱頁上限固定，「熱」記憶體佔用也會被
鎖在一個常數量級（詳見下一節），不會隨儲存格總數線性無界成長；真正會隨檔案大小
成長的是壓縮後的冷頁位元組陣列（通常遠小於原始資料）。

### 調整熱頁上限（`TableTableElement.MaxHotPages`）

熱頁上限預設為 16 頁（約 16 × 655 KB ≈ 10.5 MB），可依需求逐表調整：

```csharp
using OdfKit.Spreadsheet;

using var doc = SpreadsheetDocument.Load("huge-report.ods");
var sheet = doc.Worksheets[0];

// 若存取範圍分散在整張工作表（如逐欄彙總），調高熱頁上限可減少反覆壓縮/解壓縮，
// 用記憶體換取更少的 CPU 週期。
sheet.MaxHotPages = 64; // 約 64 × 655 KB ≈ 41 MB 熱記憶體上限

// 若記憶體極度受限（如容器環境），可調低上限，用更多壓縮/解壓縮換取更低的記憶體佔用。
sheet.MaxHotPages = 4; // 約 4 × 655 KB ≈ 2.6 MB 熱記憶體上限
```

此設定隨時可調整，新上限會於下一次寫入觸發淘汰檢查時立即套用；調低上限不會立刻
壓縮既有的熱頁，而是等到下一次有新頁面被配置、觸發淘汰檢查時才會生效。

### 目前的限制

DOM 編輯路徑目前沒有「逐列串流讀取＋修改＋寫回」的中間方案：一旦存取某張工作表，
該工作表的行結構（`table:table-row` 這層）仍會一次性具現化。若編輯場景是「只改
少數幾列、其餘原封不動地保留」，建議評估是否能改用 `OdsStreamReader` 讀出所需資料、
另以 `OdsStreamWriter` 重新輸出整份檔案，而非開啟既有檔案做原地編輯。

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
`sanitize` 會移除巨集、指令碼參照與簽章產物，並另存為新的 ODF 檔案；輸出會包含
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

## 實務深度 helper

### ODS 表格化資料、篩選與排序

`OdfSpreadsheetTable` 是建立在 ODF `table:database-range` 與 named range 上的實務 facade，適合一般 C# 應用程式做可篩選、可排序、可調整範圍的資料區塊。它不執行樞紐分析或公式重算。

```csharp
using OdfKit.Spreadsheet;

using SpreadsheetDocument workbook = SpreadsheetDocument.Create();
OdfTableSheet sheet = workbook.Worksheets.Add("Data");
sheet.SetValues(
    new OdfCellAddress(0, 0, "Data"),
    new object?[,]
    {
        { "Name", "Amount" },
        { "A", 10d },
        { "B", 20d },
    });

OdfSpreadsheetTable table = workbook.CreateTable(
    "Sales",
    new OdfCellRange(0, 0, 2, 1, "Data"));
table.ApplyFilter(new OdfDatabaseFilterConditionInfo(1, ">", "10"));
table.ApplySort(new OdfDatabaseSortRuleInfo(0, ascending: true));
table.Resize(new OdfCellRange(0, 0, 3, 1, "Data"));
```

若資料來源已經是 C# 物件，可以直接寫入 POCO 並建立可篩選表格；標題列會優先使用
`DisplayNameAttribute` 或 `DisplayAttribute.Name`，否則使用屬性名稱。

```csharp
using System.ComponentModel;
using OdfKit.Spreadsheet;
using OdfKit.Styles;

using SpreadsheetDocument workbook = SpreadsheetDocument.Create();
OdfTableSheet sheet = workbook.Worksheets.Add("Data");
var map = new OdfObjectColumnMap();
map.Map(nameof(SalesRow.Amount), "Total", order: 0).Format = new OdfObjectColumnFormat
{
    NumberFormat = "N2",
    Width = 3.Cm(),
    HeaderStyleName = "HeaderCell",
    StyleName = "MoneyCell",
};
map.Map(nameof(SalesRow.Customer), "Client", order: 1).Aliases.Add("Customer Name");
map.Map(nameof(SalesRow.Closed), ignore: true);

sheet.WriteObjects(
    new OdfCellAddress(0, 0, "Data"),
    new[]
    {
        new SalesRow { Customer = "A", Amount = 10m, Closed = true },
        new SalesRow { Customer = "B", Amount = 20m, Closed = false },
    },
    new OdfObjectBindingOptions { CreateTableName = "Sales", ColumnMap = map });

OdfSpreadsheetTable table = workbook.FindTable("Sales")!;
table.ApplyFilter("Client", "=", "A");
table.ApplySort("Total", ascending: false);

OdfObjectBindingReport readReport = new();
IReadOnlyList<SalesRow> rows = sheet.ReadObjects<SalesRow>(
    new OdfCellRange(0, 0, 2, 1, "Data"),
    new OdfObjectReadOptions
    {
        ColumnMap = map,
        ConversionErrorPolicy = OdfObjectConversionErrorPolicy.WarnAndUseDefault,
        Report = readReport,
    });

public sealed class SalesRow
{
    [DisplayName("Customer")]
    public string? Customer { get; set; }

    public decimal Amount { get; set; }

    public bool Closed { get; set; }
}
```

這個 API 針對一般業務資料匯入／匯出設計，支援字串、布林、數值、enum、`Guid`、
`DateTime`、`DateTimeOffset` 與 nullable 型別；`OdfObjectColumnMap` 可控制欄名、
順序、忽略欄位、讀取別名、必要欄位、空白值預設值與常見欄位格式。讀取轉換錯誤
預設會擲出，也可用 `ConversionErrorPolicy` 改成記錄診斷並保留預設值或略過整列。
大型資料串流仍建議使用 `OdsStreamWriter`，巢狀集合或 ORM 追蹤則留給呼叫端處理。

匯入使用者維護的 ODS 時，可以先驗證欄位與資料品質，再依 key 更新或 upsert
既有資料列。`UpsertObjects` 會保留已存在的未對應儲存格，新增列時也可從範本列複製
樣式與公式；公式預設會位移相對列參照，例如將 `of:=[.B3]*2` 複製到下一列時變成
`of:=[.B4]*2`。這適合「表格資料由程式更新、旁邊公式與格式由使用者維護」的工作流。

```csharp
var importMap = new OdfObjectColumnMap();
importMap.Map(nameof(SalesRow.Customer)).RequiredColumn = true;
importMap.Map(nameof(SalesRow.Amount)).RequiredValue = true;
importMap.Map(nameof(SalesRow.Closed)).DefaultValue = false;

OdfObjectBindingValidationReport validation = sheet.ValidateObjectBinding<SalesRow>(
    new OdfCellRange(0, 0, 20, 3, "Data"),
    new OdfObjectReadOptions
    {
        ColumnMap = importMap,
        UnknownColumnPolicy = OdfObjectUnknownColumnPolicy.Warn,
        DuplicateHeaderPolicy = OdfObjectDuplicateHeaderPolicy.WarnAndUseFirst,
    });

if (!validation.HasErrors)
{
    OdfObjectBindingReport update = sheet.UpsertObjects(
        new OdfCellRange(0, 0, 20, 3, "Data"),
        incomingRows,
        new OdfObjectUpdateOptions
        {
            ColumnMap = importMap,
            KeyColumn = nameof(SalesRow.Customer),
            CopyStylesFromTemplateRow = true,
            FillFormulasFromTemplateRow = true,
            FormulaCopyMode = OdfFormulaCopyMode.ShiftRelativeReferences,
            ResizeTable = true,
        });
}
```

若要完全保留模板公式文字，可將 `FormulaCopyMode` 設為 `CopyAsIs`；若新列不應帶入公式，
可設為 `Clear`。

### 模板圖片占位符

`TemplateBinder` 支援 `{{Image:Logo}}`。為了避免版面不可預期，圖片占位符必須獨占整個 ODT 段落，或獨占 ODP／ODG 文字方塊內容。

```csharp
using OdfKit;
using OdfKit.Text;

using TextDocument document = TextDocument.Create();
document.AddParagraph("{{Image:Logo}}");

OdfTemplateBindReport report = TemplateBinder.Bind(
    document,
    new Dictionary<string, object?>
    {
        ["Logo"] = new OdfTemplateImageValue(
            File.ReadAllBytes("logo.png"),
            "logo.png",
            AltText: "Company logo")
    },
    new OdfTemplateBindOptions());
```

### 圖表標記與軸格式

Line／scatter 常見標記樣式與座標軸數字格式可透過 chart style round-trip。這是資料呈現 helper，不保證 Microsoft Office 與 LibreOffice 像素級一致。

```csharp
using OdfKit.Chart;
using OdfKit.Spreadsheet;

using ChartDocument chart = ChartDocument.FromTable(
    "Data",
    new OdfCellRange(0, 0, 3, 1, "Data"),
    OdfChartPreset.Line,
    "Trend");

chart.GetSeriesEditor(0).ApplyMarkerStyle(
    new OdfChartMarkerStyle("circle", "0.25cm", "#FF0000", "#333333"));
chart.SetAxisNumberFormat("y", "N2");
```

常用 `data-style-name` 可以對應到呼叫端已建立或希望保留的資料樣式名稱，例如 `N2` 表示兩位小數、`Percent2` 表示百分比兩位小數、`CurrencyTwd` 表示新臺幣格式。OdfKit 會把名稱寫入 chart style，實際顯示格式由文件中的 number style 與開啟端決定。

若圖表嵌入 ODS，可用同一組實務 options 套用 3D、marker、axis 與資料標籤設定；重新載入後可透過 `GetEmbeddedChartDocument` 繼續編輯。

```csharp
var options = new OdfEmbeddedChartOptions
{
    Preset = OdfChartPreset.Column3D,
    Title = "Embedded 3D",
    YAxisNumberFormat = "N2",
    ThreeDOptions = new OdfChart3DOptions
    {
        Projection = OdfDr3dProjection.Parallel,
        AngleOffset = 30,
    },
};
options.MarkerStyles.Add(new OdfChartMarkerStyle("circle", "0.25cm", "#FF0000", "#333333"));

OdfChartDocument embedded = workbook.InsertChartFromRange(
    "Data",
    new OdfCellAddress(0, 3, "Data"),
    new OdfCellRange(0, 0, 10, 2, "Data"),
    options);
```

### 簡報與繪圖批次圖形更新

`UpdateShapes` 可依 id 或 name 批次更新位置、大小、圖層、填滿、筆觸與 z-index。繪圖順序可用 `BringToFront`／`SendToBack`／`MoveBefore`／`MoveAfter` 調整。

```csharp
using OdfKit.Presentation;
using OdfKit.Styles;

using PresentationDocument deck = PresentationDocument.Create();
OdfSlide slide = deck.AddSlide("Intro");
OdfShape shape = slide.AddShape(OdfShapeType.Rectangle, 1.Cm(), 1.Cm(), 2.Cm(), 1.Cm());
shape.Id = "status";

deck.UpdateShapes(
[
    new OdfShapeUpdateRequest
    {
        Name = "status",
        FillColor = "#00AA66",
        StrokeColor = "#333333",
        ZIndex = 5
    }
]);
deck.BringToFront("status");
deck.MoveAfter("status", "title");
```

### 實務相容性規則選項

`OdfPracticalCompatibilityOptions` 可停用特定 rule id、覆寫嚴重性，或限制回傳數量。validator 仍只回報風險，不宣稱跨套件版面完全一致。

```csharp
using OdfKit.Compliance;

var options = new OdfPracticalCompatibilityOptions { MaximumIssueCount = 20 };
options.DisabledRuleIds.Add("PRAC0400");
options.SeverityOverrides["PRAC0401"] = OdfIssueSeverity.Info;

OdfPracticalCompatibilityReport report =
    OdfPracticalCompatibilityValidator.Validate(
        workbook,
        OdfPracticalCompatibilityProfile.PortableEditing,
        options);
```

常見 rule id：

| Rule id | Profile | 觸發情境 | 建議 |
| --- | --- | --- | --- |
| `PRAC0001` | 全部 | 封裝含巨集或腳本 | 移除巨集或改以外部流程執行 |
| `PRAC0002` | 全部 | 圖片不是 PNG、JPEG 或 SVG | 改用可攜圖片格式 |
| `PRAC0100` | 全部 | 複雜巢狀文字方塊 | 簡化版面或攤平成單層文字方塊 |
| `PRAC0101` | 全部 | 複雜群組圖形 | 減少群組深度 |
| `PRAC0102` | 全部 | 自動樣式過度分裂 | 合併重複樣式 |
| `PRAC0200` | Microsoft Office / Portable editing | bubble、stock、3D、wall/floor 等進階圖表 | 以 LibreOffice 為主要互通目標，或提供替代圖表 |
| `PRAC0300` | Microsoft Office | 頁首／頁尾版面可能不同 | 簡化頁面樣式並以目標套件檢視 |
| `PRAC0301` | Microsoft Office | ODT 同時含目錄／索引、多欄區段或內嵌物件，可能觸發 Word 復原提示 | 以 Word 實機開啟代表檔，或簡化 Office 交換版 |
| `PRAC0400` | Microsoft Office / Portable editing | ODS 明確欄寬／列高 | 以目標套件檢查版面 |
| `PRAC0401` | Microsoft Office / Portable editing | ODS 列印設定 | 以目標套件檢查列印預覽 |
| `PRAC0500` | Microsoft Office / Portable editing | ODP／ODG／ODI 圖片裁切或旋轉 | 優先烘焙成可攜圖片或提供替代版面 |

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
