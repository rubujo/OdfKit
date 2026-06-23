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

## 建立 ODF 公式（Fluent Builder）

```csharp
using OdfKit.Formula;

using OdfFormulaDocument formula = OdfFormulaDocument.Builder()
    .WithIdentifierEquation("F", "ma")
    .Build();
formula.Save("equation.odf");
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
