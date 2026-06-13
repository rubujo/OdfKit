# OdfKit Cookbook

本文件提供可直接改寫的常見 ODF 操作範例。範例只描述目前已有測試支撐的能力。

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
string text = workbook.Worksheets[0].Cells["A2"].Value;
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

using OdfChartDocument chart = OdfChartDocument.Create();
chart.ChartClass = "bar";
chart.ChartTitle = "營收";
chart.SetLegend("top");
chart.SetCategories("Sheet1.A1:C1");
chart.XAxisTitle = "月份";
chart.YAxisTitle = "金額";
chart.AddSeries("Sheet1.A1:A3", "Sheet1.A1");
chart.Save("revenue.odc");

using OdfChartDocument loadedChart = OdfChartDocument.Load("revenue.odc");
Console.WriteLine(loadedChart.XAxisTitle);
Console.WriteLine(loadedChart.Series[0].ValuesCellRangeAddress);
```

## 建立 ODB 資料來源描述

```csharp
using OdfKit.Database;

using OdfDatabaseDocument database = OdfDatabaseDocument.Create();
database.SetConnection("sdbc:embedded:hsqldb");
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
`sanitize` 會移除巨集、指令碼參照與簽章 artifact，並另存為新的 ODF 檔案；加密文件可用 `--password` 載入，並用 `--output-password` 重新加密輸出。
