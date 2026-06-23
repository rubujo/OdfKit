# OdfKit 套件相容矩陣

本文件以「套件清單 + 平台支援 + 安裝策略」格式整理 OdfKit
目前可交付的封裝內容。

## 1. 可封裝套件（0.0.1）

| 套件 ID | 目標框架 | 說明 | 相依或部署注意事項 |
|---------|----------|------|--------------------|
| `OdfKit` | `net10.0;netstandard2.0` | 核心 ODF 處理程式庫 | 所有擴充套件的共同基礎 |
| `OdfKit.Extensions.Html` | `net10.0;netstandard2.0` | HTML / Markdown / RTF managed 匯出 | 內容轉出與 Web 預覽 |
| `OdfKit.Extensions.Imaging` | `net10.0;netstandard2.0` | 影像渲染與圖表 fallback 視覺化 | 依賴 SkiaSharp / HarfBuzzSharp |
| `OdfKit.Extensions.Ooxml` | `net10.0;netstandard2.0` | OOXML 轉換（DOCX / XLSX） | 依賴 ClosedXML 與 Open XML SDK |
| `OdfKit.Extensions.Pdf` | `net10.0;netstandard2.0` | PDF 匯出 | 依賴 PDFsharp-MigraDoc |
| `OdfKit.Extensions.Rendering` | `net10.0;netstandard2.0` | LibreOffice 後端渲染 | 需本機 LibreOffice 或相容程序 |
| `OdfKit.Extensions.Rdf` | `net10.0;netstandard2.0` | `manifest.rdf` 與 RDF / SPARQL 橋接 | 依賴 dotNetRdf |
| `OdfKit.Extensions.Collaboration` | `net10.0;netstandard2.0` | ODF Toolkit 相容 JSON operations 匯出 | 適合協作編輯流程 |

**非套件發佈**：`OdfKit.Cli`、`OdfSchemaGenerator`、`OdfCorpusGenerator`、
`OdfKit.Benchmarks`、`OdfKit.Tests`（`IsPackable=false` 或開發工具）。

## 2. 專案目標框架

| 專案類型 | 目標框架 | 用途 |
|----------|----------|------|
| 核心與所有可封裝 Extensions | `net10.0;netstandard2.0` | 最新 .NET 與最大消費端相容面 |
| `OdfKit.Cli` | `net10.0;net8.0` | 命令列工具 |
| `OdfKit.Tests` | `net10.0;net8.0` | 單元、整合與互通驗證 |

每個 `.nupkg` 內含：

- `lib/net10.0/<Assembly>.dll`
- `lib/netstandard2.0/<Assembly>.dll`
- `OdfKit` 核心套件另含套件 README、`LICENSE`、`THIRD-PARTY-NOTICES.md`
  與 `.snupkg` 符號套件

## 3. 建議消費端矩陣

| 消費端執行環境 | 建議參照 TFM | 驗證狀態 |
|----------------|-------------|----------|
| .NET 10 | `net10.0` | ✅ 主要開發與測試目標 |
| .NET 8 LTS | `netstandard2.0` 或依情境使用 `net10.0` 產物 | ✅ CLI / 測試專案覆蓋 `net8.0`；程式庫雙 TFM 建置 |
| .NET Standard 2.0 相容專案（含 .NET Framework 4.6.1+） | `netstandard2.0` | ✅ 程式庫雙 TFM 建置；消費端煙霧見 `eng/Test-NuGetPack.ps1` |

## 4. 發佈與安裝策略

| 管道 | 說明 |
|------|------|
| **GitHub 原始碼** | 主要使用方式（clone、`ProjectReference`） |
| **GitHub Release** | 附加 `.nupkg`／`.snupkg` 資產，供本機 NuGet feed |
| **nuget.org** | **非目前目標** |

自 GitHub Release 下載後：

```powershell
dotnet nuget add source C:\path\to\release-assets --name odfkit-github-release
dotnet add package OdfKit --version 0.0.1 --source odfkit-github-release
```

若需固定套件選型與導入順序，請先讀
[套件目錄與選型指南](package-catalog.md)。

## 5. 驗證與封裝

```powershell
pwsh eng/Pack-NuGet.ps1 -Configuration Release
pwsh eng/Test-NuGetPack.ps1 -Configuration Release
```

完整發佈流程見 [GitHub Release 發佈指南](github-release-publishing.md)。

## 6. 版本、授權與交付

- **版本**：`0.0.1`（權威來源：`eng/OdfKit.Package.props`）
- **授權**：CC0-1.0（專案原創程式碼）；第三方套件維持各自授權
- **版本與交付資訊**：見 [版本與交付資訊](version-delivery.md)

## 7. 已知限制

- 部分擴充套件依賴原生或重型第三方套件；部署前應評估其平台與授權需求。
- `OdfKit.Extensions.Rendering` 需外部 LibreOffice 或相容程序後端，詳見
  [Rendering 後端部署](rendering-backend-deployment.md)。
