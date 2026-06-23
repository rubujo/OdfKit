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

## 2. 選擇安裝模式

### 原始碼整合

適合需要追蹤最新主幹、客製化功能或直接使用 `ProjectReference` 的團隊。

```powershell
git clone https://github.com/OdfKit/OdfKit.git
cd OdfKit
dotnet build
dotnet test
```

### GitHub Release 套件整合

適合要建立固定版本 feed、以 `.nupkg` 方式管理相依的情境。

```powershell
dotnet nuget add source C:\path\to\release-assets --name odfkit-github-release
dotnet add package OdfKit --version 0.0.1 --source odfkit-github-release
```

完整套件清單與目標框架請見
[NuGet 相容矩陣](nuget-compatibility-matrix.md)。

## 3. 選擇套件

| 需求 | 建議套件 |
|------|----------|
| ODF 建立、載入、保存、驗證 | `OdfKit` |
| 匯出 HTML / Markdown / RTF | `OdfKit.Extensions.Html` |
| 匯出 PDF | `OdfKit.Extensions.Pdf` |
| 匯出影像或圖表渲染 | `OdfKit.Extensions.Imaging` |
| 與 DOCX / XLSX 互通 | `OdfKit.Extensions.Ooxml` |
| 需要 LibreOffice 後端渲染 | `OdfKit.Extensions.Rendering` |
| RDF / SPARQL 中介資料橋接 | `OdfKit.Extensions.Rdf` |
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
| 規劃套件發佈流程 | [GitHub Release 發佈指南](github-release-publishing.md) |
| 確認版本原則與交付方式 | [版本與交付資訊](version-delivery.md) |
