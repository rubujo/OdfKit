# OdfKit NuGet 與雙 TFM 相容矩陣

本文件為 Wave 4 **REL-1** 產出，說明可發佈套件、目標框架與建議的消費端矩陣。

## 可發佈套件（1.0.0）

| 套件 ID | 說明 | 相依 |
|---------|------|------|
| `OdfKit` | 核心 ODF 處理程式庫 | — |
| `OdfKit.Extensions.Html` | HTML 匯出 | `OdfKit` |
| `OdfKit.Extensions.Imaging` | 影像渲染（SkiaSharp） | `OdfKit` |
| `OdfKit.Extensions.Ooxml` | OOXML 轉換（DOCX / XLSX） | `OdfKit` |
| `OdfKit.Extensions.Pdf` | PDF 匯出 | `OdfKit` |
| `OdfKit.Extensions.Rendering` | LibreOffice 程序後端渲染 | `OdfKit` |

**非 NuGet 發佈**：`OdfKit.Cli`、`OdfSchemaGenerator`、`OdfCorpusGenerator`、`OdfKit.Benchmarks`、`OdfKit.Tests`（`IsPackable=false` 或開發工具）。

## 程式庫目標框架（雙 TFM）

| 專案 | `TargetFrameworks` | 用途 |
|------|---------------------|------|
| `OdfKit` 與所有 Extensions | `net10.0;netstandard2.0` | 最新 .NET 與最大相容面 |
| `OdfKit.Tests` | `net10.0;net8.0` | 單元／整合測試（非套件） |
| `OdfKit.Cli` | `net10.0;net8.0` | 命令列工具（非套件） |

每個可發佈 `.nupkg` 內含：

- `lib/net10.0/<Assembly>.dll`
- `lib/netstandard2.0/<Assembly>.dll`
- `OdfKit` 另含套件 README、`LICENSE`、`THIRD-PARTY-NOTICES.md` 與 `.snupkg` 符號套件

## 建議消費端矩陣

| 消費端執行環境 | 建議參照 TFM | 驗證狀態 |
|----------------|-------------|----------|
| .NET 10 | `net10.0` | ✅ 主要開發與測試目標 |
| .NET 8 LTS | `net10.0` 或 `netstandard2.0` | ✅ `OdfKit.Tests` net8.0 全綠 |
| .NET Standard 2.0 相容專案（含 .NET Framework 4.6.1+） | `netstandard2.0` | ✅ 程式庫雙 TFM 建置；消費端煙霧見 `eng/Test-NuGetPack.ps1` |

## 安裝（發佈後）

```powershell
dotnet add package OdfKit
dotnet add package OdfKit.Extensions.Ooxml
```

本機驗證封裝：

```powershell
pwsh eng/Pack-NuGet.ps1 -Configuration Release
pwsh eng/Test-NuGetPack.ps1 -Configuration Release
```

## 版本與授權

- **版本**：`1.0.0`（與各 `.csproj` 之 `<Version>` 同步）
- **授權**：CC0-1.0（專案原創程式碼）；第三方套件維持其原授權（見 `THIRD-PARTY-NOTICES.md`）

## 已知限制

- Extensions 依賴原生或重型套件（如 SkiaSharp、PDFsharp、ClosedXML），消費端須自行處理執行環境相依。
- `OdfKit.Extensions.Rendering` 需本機 LibreOffice 或相容程序後端，見 [`rendering-backend-deployment.md`](rendering-backend-deployment.md)。
- NuGet 上尚未發佈至 nuget.org 時，請使用本機 `artifacts/nuget` 或私有 feed。