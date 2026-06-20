# OdfKit

OdfKit 是純 managed C# / .NET 的 ODF (Open Document Format) 文件處理程式庫。核心目標是讓應用程式可以在不啟動 LibreOffice、OpenOffice、UNO、Microsoft Office 或 Java 的情況下，建立、載入、保存、驗證並保留 ODF 文件內容。

目前預設新建文件為 ODF 1.4，主要程式庫目標框架為 `net10.0` 與 `netstandard2.0`。測試專案目前覆蓋 `net10.0` 與 `net8.0`。

## 目前狀態

- 支援 24 種主要 ODF extension 的格式偵測、最小建立、載入、保存、驗證與 round-trip。
- ODT、ODS、ODP、ODG 已有常用高階建立與編輯 API。
- ODC 已有圖表類型、標題、圖例、座標軸標題、資料來源標籤、分類與序列 API；ODF formula 已有 MathML XML 讀寫、基礎 token helper、根節點與純文字摘要 API；ODI 已有主要影像寫入、摘要、讀回與框架版面 API；ODB 已有連線、資料來源設定、資料表與查詢查找 / 移除 API。
- Package 與 DOM round-trip 會保留未知 package entries、foreign XML、processing instructions、comments 與 prefix。
- 驗證器包含 package / flat XML 檢查、ODF 1.4 schema metadata、profile rules、positive / negative corpus 測試。
- ODS 與 CSV 雙向轉換已內建於核心程式庫（`OdfCsvExporter` / `OdfCsvImporter`）；CLI 提供選用的 `convert-csv` 薄包裝。
- CLI 提供 `validate`、`validate-corpus`、`info`、`metadata`、`sanitize`、`convert-flat`、`convert-csv` 與 `pack`，其中驗證命令可輸出文字或 JSON，支援 CI 失敗條件，並可選用外部 ODF Validator baseline 與 documented exceptions。

## 文件導覽

| 分類 | 文件 |
|------|------|
| 格式支援與相容性 | [格式矩陣](docs/odf-format-support.md)、[ODF 1.4 覆蓋](docs/odf14-coverage.md)、[ODF Toolkit 對標線](docs/odf-toolkit-parity.md)、[typed DOM 覆蓋](docs/typed-dom-coverage.md)、[內建 Profile 來源](docs/odf-profile-sources.md)、[Foreign 擴充政策](docs/foreign-extension-policy.md)、[非目標範圍](docs/udx-non-goals.md) |
| 驗證與 Corpus | [官方 Corpus 來源](docs/odf-official-corpus-sources.md)、[Corpus Manifest 規則](docs/corpus-manifest.md)、[Interop Corpus 總覽](docs/interop-corpus.md) |
| 轉換與互通 | [Managed-first 轉檔策略](docs/managed-first-conversion-strategy.md)、[Rendering 後端部署](docs/rendering-backend-deployment.md)、[LibreOffice 互通矩陣](docs/libreoffice-interop-matrix.md)、[OOXML 視覺驗收矩陣](docs/ooxml-visual-golden-matrix.md) |
| 套件與發行 | [NuGet 相容矩陣](docs/nuget-compatibility-matrix.md)、[GitHub Release 發佈流程](docs/github-release-publishing.md) |
| 測試與工程流程 | [測試分層策略](docs/testing-strategy.md)、[Async 重構準則](eng/AsyncRefactor-Plan.md)、[上帝類別拆分計畫](eng/GodClassRefactor-Plan.md)、[效能優化計畫](eng/OdfKit-Performance-Plan.md)、[完滿化路線圖](eng/OdfKit-Completeness-Plan.md) |
| 範例與來源 | [Cookbook 操作範例](docs/cookbook.md)、[各模組來源與授權](docs/provenance/README.md)、[第三方套件授權聲明](THIRD-PARTY-NOTICES.md) |
| 開發紀錄 | [CHANGELOG](CHANGELOG.md)（高階版本歷程）、[IMPLEMENTATION_PLAN](IMPLEMENTATION_PLAN.md)（逐任務工程細節，供 AI 開發 agent 接續使用） |

## 安裝與建置

### 套件封裝與 GitHub Release（REL-1）

主要發佈方式為 **GitHub 原始碼**；套件（`.nupkg`）以 **GitHub Release** 資產提供，**目前不發佈至 nuget.org**。目標框架為 `net10.0` 與 `netstandard2.0`。

雙 TFM 相容矩陣見 [docs/nuget-compatibility-matrix.md](docs/nuget-compatibility-matrix.md)。

```powershell
pwsh eng/Pack-NuGet.ps1 -Configuration Release
pwsh eng/Test-NuGetPack.ps1 -Configuration Release
pwsh eng/Publish-GitHubRelease.ps1           # 乾跑（預設 v0.0.1）
pwsh eng/Publish-GitHubRelease.ps1 -CreateRelease  # 需 gh CLI
```

詳見 [docs/github-release-publishing.md](docs/github-release-publishing.md)。

### 原始碼

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

### ODS 與 CSV 互轉

核心 API（主要使用方式）：

```csharp
using OdfKit.Csv;
using OdfKit.Spreadsheet;

// ODS → CSV
using (SpreadsheetDocument workbook = SpreadsheetDocument.Load("report.ods"))
{
    OdfCsvExporter.ExportToFile(workbook, "report.csv");
}

// CSV → ODS
using (SpreadsheetDocument imported = OdfCsvImporter.ImportFromFile("report.csv"))
{
    imported.Save("imported.ods");
}
```

選項請見 `OdfCsvOptions`（分隔字元、編碼、匯出工作表索引等）。若只需腳本或 CI 轉檔，也可使用 CLI：

```powershell
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- convert-csv report.ods report.csv
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- convert-csv report.csv imported.ods
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
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- sanitize input.odt sanitized.odt
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- sanitize encrypted.odt sanitized.odt --password old-secret --output-password new-secret --encryption aes256
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- typed-dom-coverage --format json
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- convert-flat input.odt output.fodt
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- pack input.fodt output.odt
```

`validate` 的 exit code 固定為：通過為 `0`，驗證錯誤或 `--fail-on warning` 命中為 `1`，參數錯誤或路徑不存在為 `2`。
`sanitize` 可用 `--password` 開啟加密輸入，並用 `--output-password` 重新加密輸出。

## 限制

- 高階 API 覆蓋度依格式不同而不同；請以 support matrix 與測試證據為準。
- Template、master 與 Flat XML 變體可透過 `OdfFormatInfo.IsTemplate`、
  `OdfFormatInfo.IsMasterDocument`、`OdfFormatInfo.IsFlatXml` 與
  `OdfDocument` 的格式摘要屬性辨識；目前它們共用對應內容種類的高階
  wrapper，尚未提供完整變體專屬物件模型。
- 圖表、公式、影像與資料庫文件已有基礎高階 story，但不是完整的辦公套件物件模型。
- ODF 1.1/1.2/1.3 schema 驗證已改為以 OASIS 官方獨立 RNG 衍生的真實版本專屬 schema；ODF 1.0 因 OASIS 從未發布獨立 RNG，仍維持以 1.4 schema 進行 best-effort 近似驗證（詳見 [docs/odf-official-corpus-sources.md](docs/odf-official-corpus-sources.md)）。保存時可使用 `OdfSaveOptions.ForceVersion` 明確指定輸出版本。
- LibreOffice rendering 擴充仍需外部 LibreOffice 或相容執行檔，不屬於核心 OdfKit 的純 managed 路徑。

## 授權

OdfKit 專案採用 [CC0-1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/deed.zh_TW)。第三方相依套件維持各自授權；主要相依套件包含 PDFsharp、CommunityToolkit.HighPerformance、System.Security.Cryptography.Xml 與 System.Security.Cryptography.Pkcs。
