# OdfKit 快速開始

本文件提供常用的入門路徑：環境需求、安裝方式、第一個文件、
CLI 驗證與下一步閱讀建議。

## 1. 環境需求

| 項目 | 說明 |
|------|------|
| 開發建議 | .NET 10 SDK |
| 最佳執行路徑 | `net10.0` |
| 相容執行路徑 | `netstandard2.0` 消費端 |
| CLI | `net10.0` 或 `net8.0` |

若要使用 `OdfKit.Extensions.Rendering`，還需要本機 LibreOffice 或相容的
後端程序；詳見 [rendering-backend-deployment.md](rendering-backend-deployment.md)。

## 2. 選擇導入模式

### 原始碼整合

適合需要追蹤最新主幹、客製化功能或直接使用 `ProjectReference` 的團隊。

```powershell
git clone https://github.com/OdfKit/OdfKit.git
cd OdfKit
dotnet build
dotnet test
```

### ProjectReference 整合

適合產品程式碼與 OdfKit 同倉或同工作區開發的情境。

```powershell
dotnet add YourApp.csproj reference path\to\OdfKit\OdfKit\OdfKit.csproj
```

## 3. 選擇元件

| 需求 | 建議元件 |
|------|----------|
| ODF 建立、載入、保存、驗證 | `OdfKit` |
| 匯出 HTML / Markdown / RTF | `OdfKit.Extensions.Html` |
| 匯出 PDF | `OdfKit.Extensions.Pdf` |
| 匯出影像或圖表渲染 | `OdfKit.Extensions.Imaging` |
| 與 DOCX / XLSX 互通 | `OdfKit.Extensions.Ooxml` |
| 需要 LibreOffice 後端渲染 | `OdfKit.Extensions.Rendering` |
| RDF / SPARQL 中繼資料橋接 | `OdfKit.Extensions.Rdf` |
| 協作操作匯出 | `OdfKit.Extensions.Collaboration` |

更完整的選型說明請見 [套件目錄與選型指南](package-catalog.md)。

## 4. 第一個 ODF 文件

### 建立 ODT

```csharp
using OdfKit.Text;

using TextDocument document = TextDocument.Create();
document.Body.Headings.Add("報告", 1);
document.Body.Paragraphs.Add("這是一份 ODF 文字文件。");
document.Save("report.odt");
```

### 常用文件種類速覽

| 格式 | 入口 | 常見用途 |
|------|------|----------|
| ODT | `TextDocument` | 報告、合約、郵件合併、文字範本 |
| ODS | `SpreadsheetDocument` | 試算表、CSV 匯入匯出、公式、圖表、大量資料輸出 |
| ODP | `PresentationDocument` | 投影片、講者備註、轉場、簡報樣板 |
| ODG | `DrawingDocument` | 流程圖、架構圖、形狀、連接線 |
| ODC | `ChartDocument` | 獨立或嵌入式 ODF 圖表 |
| ODF | `FormulaDocument` | MathML 公式與 LaTeX / token helper |
| ODI | `ImageDocument` | ODF 影像封裝、框架、裁切、濾鏡 |
| ODB | `DatabaseDocument` | 資料來源、查詢、表單、報表參照與 schema 描述 |

```csharp
using OdfKit.Formula;

using FormulaDocument formula = FormulaDocument.Builder()
    .WithIdentifierEquation("F", "ma")
    .Build();
formula.Save("equation.odf");
```

### 驗證 ODT

```csharp
using OdfKit.Compliance;

OdfValidationReport report = OdfValidator.Validate("report.odt");
Console.WriteLine(report.IsValid ? "Valid" : "Invalid");
```

### 使用 CLI 驗證

```powershell
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate report.odt
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- info report.odt
```

## 5. 常見下一步

| 下一步 | 先讀哪份文件 |
|--------|--------------|
| 找更多 API 範例 | [Cookbook](cookbook.md) |
| 想直接跑整套樣本 | [samples/README.md](../samples/README.md) |
| 確認格式支援深度 | [ODF 格式支援矩陣](odf-format-support.md) |
| 確認可用 Profile 與規則來源 | [ODF Profile 來源](odf-profile-sources.md) |
| 確認語系訊息與在地化機制 | [i18n 與在地化](i18n-localization.md) |
| 確認版本原則與交付方式 | [版本與交付資訊](version-delivery.md) |
