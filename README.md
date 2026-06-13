# OdfKit

OdfKit 是純 managed C# / .NET 的 ODF (Open Document Format) 文件處理程式庫。核心目標是讓應用程式可以在不啟動 LibreOffice、OpenOffice、UNO、Microsoft Office 或 Java 的情況下，建立、載入、保存、驗證並保留 ODF 文件內容。

目前預設新建文件為 ODF 1.4，主要程式庫目標框架為 `net10.0` 與 `netstandard2.0`。測試專案目前覆蓋 `net10.0` 與 `net8.0`。

## 目前狀態

- 支援 17 種主要 ODF extension 的格式偵測、最小建立、載入、保存、驗證與 round-trip。
- ODT、ODS、ODP、ODG 已有常用高階建立與編輯 API。
- ODC、ODF formula、ODI、ODB 已有 typed wrapper 與最小高階 story。
- Package 與 DOM round-trip 會保留未知 package entries、foreign XML、processing instructions、comments 與 prefix。
- 驗證器包含 package / flat XML 檢查、ODF 1.4 schema metadata、profile rules、positive / negative corpus 測試。
- CLI 提供 `validate`、`validate-corpus`、`info`、`metadata`、`convert-flat` 與 `pack`，其中驗證命令可輸出文字或 JSON，支援 CI 失敗條件，並可選用外部 ODF Validator baseline 與 documented exceptions。

完整格式矩陣請見 [docs/odf-format-support.md](docs/odf-format-support.md)，ODF 1.4 覆蓋狀態請見 [docs/odf14-coverage.md](docs/odf14-coverage.md)，ODF Toolkit 對標線請見 [docs/odf-toolkit-parity.md](docs/odf-toolkit-parity.md)，typed DOM 覆蓋狀態請見 [docs/typed-dom-coverage.md](docs/typed-dom-coverage.md)。

## 安裝與建置

目前此 repo 以原始碼專案形式使用：

```powershell
dotnet build
dotnet test
```

CLI 可透過專案執行：

```powershell
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate file.odt
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate docs --recursive --format json
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- info file.ods
```

## 快速開始

### 建立 ODT

```csharp
using OdfKit.Text;

using TextDocument document = TextDocument.Create();
document.Body.Headings.Add("報告", 1);
document.Body.Paragraphs.Add("這是一份 ODF 文字文件。");
document.Save("report.odt");
```

### 建立 ODS

```csharp
using OdfKit.Spreadsheet;

using SpreadsheetDocument workbook = SpreadsheetDocument.Create();
OdfTableSheet sheet = workbook.Worksheets.Add("Sheet1");
sheet.Cells["A1"].CellValue = "項目";
sheet.Cells["B1"].CellValue = "金額";
sheet.Cells["B2"].CellValue = 1200;
sheet.Cells["B3"].Formula = "of:=SUM([.B2:.B2])";
workbook.Save("report.ods");
```

### 建立 ODP

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
    "標題");
deck.Save("slides.odp");
```

### 建立 ODG

```csharp
using OdfKit.Drawing;
using OdfKit.Presentation;
using OdfKit.Styles;

using DrawingDocument drawing = DrawingDocument.Create();
OdfDrawPage page = drawing.Pages.Add("Canvas");
OdfShape rect = page.AddShape(
    OdfShapeType.Rectangle,
    OdfLength.FromCentimeters(1),
    OdfLength.FromCentimeters(1),
    OdfLength.FromCentimeters(4),
    OdfLength.FromCentimeters(2));
rect.FillColor = "#ffcc00";
drawing.Save("drawing.odg");
```

### 驗證文件

```csharp
using OdfKit.Compliance;

OdfValidationReport report = OdfValidator.Validate("report.odt");
if (!report.IsValid)
{
    foreach (OdfValidationIssue issue in report.Issues)
    {
        Console.WriteLine($"{issue.Severity}: {issue.RuleId} {issue.Message}");
    }
}
```

### 保留未知內容

對於 OdfKit 尚未提供高階語意 API 的內容，建議使用 package / DOM 入口載入並保存。測試目前覆蓋未知 package entry、foreign namespace、未知屬性、comments 與 processing instructions 的 round-trip。

```csharp
using OdfKit.Core;

using OdfDocument document = OdfDocument.Load("input.odt");
document.Save("output.odt");
```

## CLI

```powershell
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate file.odt
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate file.odt --format json
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate samples --recursive --fail-on warning
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate file.odt --profile OASIS_ODF_1_4_Extended
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate file.odt --baseline odf-validator --baseline-jar C:\tools\odfvalidator.jar
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate file.odt --baseline odf-validator --baseline-exceptions docs\baseline-exceptions.json
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate-corpus tests\fixtures\corpus\manifest.json --format json
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate-corpus corpus\manifest.json --root corpus --format json
.\eng\Test-OdfCorpus.ps1
.\eng\Initialize-OdfExternalCorpus.ps1 -OutputRoot D:\Corpus\OdfKit
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- info file.ods
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- metadata file.odt
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- convert-flat input.odt output.fodt
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- pack input.fodt output.odt
```

`validate` 的 exit code 固定為：通過為 `0`，驗證錯誤或 `--fail-on warning` 命中為 `1`，參數錯誤或路徑不存在為 `2`。

## 限制

- 高階 API 覆蓋度依格式不同而不同；請以 support matrix 與測試證據為準。
- 圖表、公式、影像與資料庫文件目前是最小高階 story，不是完整的辦公套件物件模型。
- 舊版 ODF 文件的版本差異處理屬 best-effort；保存時可使用 `OdfSaveOptions.ForceVersion` 明確指定輸出版本。
- LibreOffice rendering 擴充仍需外部 LibreOffice 或相容執行檔，不屬於核心 OdfKit 的純 managed 路徑。

## 授權

OdfKit 原創程式碼採用 CC0-1.0 Universal。第三方相依套件維持各自授權；主要相依套件包含 PDFsharp、CommunityToolkit.HighPerformance、System.Security.Cryptography.Xml 與 System.Security.Cryptography.Pkcs。
