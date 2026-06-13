# OdfKit Cookbook

本文件提供可直接改寫的常見 ODF 操作範例。範例只描述目前已有測試支撐的能力。

## 建立 ODT

```csharp
using OdfKit.Text;

using TextDocument document = TextDocument.Create();
document.Body.Headings.Add("會議記錄", 1);
document.Body.Paragraphs.Add("今日討論 ODF 文件自動化。");
document.Save("meeting.odt");
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
workbook.Save("data.ods");
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
dotnet run --project tools/OdfKit.Cli -- validate file.odt
dotnet run --project tools/OdfKit.Cli -- convert-flat file.odt file.fodt
dotnet run --project tools/OdfKit.Cli -- pack file.fodt file.odt
```
