# 核心 SDK 快速開始

本文件提供 OdfKit 核心 SDK 的最短實作路徑，聚焦於純受控程式碼的 ODF 建立、載入、驗證與低記憶體匯出。需要格式轉換或 LibreOffice 渲染時，再接續閱讀對應擴充套件文件。

## 安裝定位

核心套件是 `OdfKit`，目標是讓應用程式在不啟動 LibreOffice、UNO、Microsoft Office 或 Java 的情況下處理 ODF 封裝與常見文件模型。

```powershell
dotnet add package OdfKit --version 0.0.1 --source odfkit-github-release
```

若以原始碼方式整合，請先在儲存庫根目錄執行：

```powershell
dotnet build
dotnet test
```

## 建立文字文件

```csharp
using OdfKit.Compliance;
using OdfKit.Text;

using TextDocument document = TextDocument.Create();
document.Title = "季度報告";
document.Creator = "OdfKit";
document.Body.Headings.Add("季度報告", 1);
document.Body.Paragraphs.Add("這份文件是在不啟動辦公室軟體執行階段的情況下產生。");
document.Save("quarterly-report.odt");

OdfValidationReport report = OdfValidator.Validate("quarterly-report.odt");
Console.WriteLine(report.IsValid ? "Valid" : "Invalid");
```

## 建立試算表

```csharp
using OdfKit.Spreadsheet;

using SpreadsheetDocument workbook = SpreadsheetDocument.Create();
OdfTableSheet sheet = workbook.Worksheets.Add("Data");
sheet.Cells["A1"].CellValue = "Name";
sheet.Cells["B1"].CellValue = "Amount";
sheet.Cells["A2"].CellValue = "ODF";
sheet.Cells["B2"].CellValue = 42;
sheet.Ranges["A1:B2"].NameAs("DataRange");
workbook.Save("data.ods");
```

## 低記憶體大量匯出

大量資料匯出時，優先使用 `OdsStreamWriter` 的嚴格順序模式。這條路徑不需要先建立完整 DOM，適合報表、資料庫匯出與批次作業。

```csharp
using OdfKit.Spreadsheet;

await using FileStream output = File.Create("export.ods");
using var writer = new OdsStreamWriter(output);

writer.WriteStartSheet("資料列");
writer.WriteStartRow();
writer.WriteCell("識別碼");
writer.WriteCell("名稱");
writer.WriteEndRow();

for (int i = 1; i <= 100_000; i++)
{
    writer.WriteStartRow();
    writer.WriteCell(i);
    writer.WriteCell("資料列 " + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
    writer.WriteEndRow();
}

writer.WriteEndSheet();
```

## 載入與安全限制

載入非信任來源文件時，使用 `OdfLoadOptions` 明確設定 ZIP 與 XML 限制。預設值已包含防禦性上限；只有在受信任大型文件情境中才建議放寬。

```csharp
using OdfKit.Core;

var options = new OdfLoadOptions
{
    ValidateMimeType = true,
    MaxZipEntries = 5000,
    MaxEntrySize = 500 * 1024 * 1024,
    MaxTotalUncompressedSize = 1024L * 1024L * 1024L,
    MaxXmlCharactersInDocument = 64L * 1024L * 1024L
};

using OdfDocument document = OdfDocument.Load("input.odt", options);
Console.WriteLine(document.DocumentKind);
```

## 儲存、版本與確定性輸出

需要可重複封裝輸出時，啟用 `Deterministic`。需要強制輸出特定 ODF 版本時，設定 `ForceVersion`。

```csharp
using OdfKit.Core;
using OdfKit.Text;

using TextDocument document = TextDocument.Create();
document.Body.Paragraphs.Add("可重複產生的封裝輸出。");
document.Save("repeatable.odt", new OdfSaveOptions
{
    Deterministic = true,
    ForceVersion = OdfVersion.Odf14
});
```

## 下一步

| 需求 | 文件 |
|------|------|
| 更多情境範例 | [實作食譜](cookbook.md) |
| API 分層與命名契約 | [API 表面分層](api-surface-layers.md) |
| 格式支援深度 | [ODF 格式支援矩陣](odf-format-support.md) |
| LibreOffice 互通 | [LibreOffice 互通矩陣](libreoffice-interop-matrix.md) |
| 效能量測 | [效能基準線](performance-baselines.md) |
