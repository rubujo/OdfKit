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
| `OdfKit.Extensions.Scripting` | `net10.0;netstandard2.0` | ODF 1.0～1.4 指令碼、事件與 LibreOffice 文件巨集 CRUD | 純 managed；不執行巨集或重新簽章 |
| `OdfKit.WebFonts.Abstractions` | `net10.0;netstandard2.0` | WebFont 契約、manifest 與 Profile 模型 | 不綁定特定字型引擎或網站框架 |
| `OdfKit.WebFonts.OpenType` | `net10.0;netstandard2.0` | 純 managed OpenType 解析與有界子集化 | 不依賴 Python、Node.js 或原生 FontTools |
| `OdfKit.WebFonts.Profiles` | `net10.0;netstandard2.0` | 版本化 CNS 11643 與自訂 Profile 載入 | 字型及外部對照資料不隨套件再散布 |
| `OdfKit.WebFonts.Encoding.Legacy` | `net10.0;netstandard2.0` | Big5、Big5E 與 legacy mapping | 對缺字與衝突採明確錯誤 |
| `OdfKit.WebFonts.Data.SqlServer` | `net10.0;netstandard2.0` | SQL Server mapping provider | 資料庫由消費端部署與管理 |
| `OdfKit.WebFonts.Windows` | `net10.0;netstandard2.0` | Windows EUDC／`.tte` 輸入整合 | 僅在合法且明確設定的來源上使用 |
| `OdfKit.WebFonts.Build` | `net10.0` | CLI 與 MSBuild 預產生工具 | 以 .NET tool 形式封裝 |
| `OdfKit.WebFonts.Worker` | `net10.0` | 有界背景產生與內容定址快取 | 不代表多節點 durable store 已內建 |
| `OdfKit.WebFonts.Hosting.AspNetCore` | `net10.0` | ASP.NET Core 動態與靜態 WebFont 託管 | 支援授權、CSP、CORS 與反向代理部署設定 |
| `OdfKit.WebFonts.Hosting.SystemWeb` | `net48` | ASP.NET Web Forms／System.Web Handler | 支援 `Web.config` 設定與靜態 fallback |
| `OdfKit.Extensions.Html.WebFonts` | `net10.0;netstandard2.0` | HTML exporter 的 WebFont integration | WebFont 是獨立產品能力，HTML 僅為整合之一 |

**非套件發佈**：`OdfKit.Cli`、`OdfSchemaGenerator`、`OdfCorpusGenerator`、
`OdfKit.Benchmarks`、`OdfKit.Tests`（`IsPackable=false` 或開發工具）。

## 2. 專案目標框架

| 專案類型 | 目標框架 | 用途 |
|----------|----------|------|
| 核心與可攜式 managed 程式庫 | `net10.0;netstandard2.0` | 最新 .NET 與最大消費端相容面 |
| WebFont Build／Worker／ASP.NET Core | `net10.0` | 建置工具、背景工作與現代網站託管 |
| WebFont System.Web | `net48` | ASP.NET Web Forms 與傳統 IIS 網站託管 |
| `OdfKit.Cli` | `net10.0;net8.0` | 命令列工具 |
| `OdfKit.Tests` | `net10.0;net8.0` | 單元、整合與互通驗證 |

程式庫 `.nupkg` 依其 TFM 契約內含對應的 `lib/<TFM>/<Assembly>.dll`；雙 TFM
程式庫同時包含 `net10.0` 與 `netstandard2.0`，System.Web 僅包含 `net48`，Build 則採
.NET tool 的 `tools/net10.0` 資產形狀。套件另依需要包含：

- 套件 README、`LICENSE`、`THIRD-PARTY-NOTICES.md`
  與 `.snupkg` 符號套件

## 3. 建議消費端矩陣

| 消費端執行環境 | 建議參照 TFM | 驗證狀態 |
|----------------|-------------|----------|
| .NET 10 | `net10.0` | ✅ 主要開發與測試目標 |
| .NET 8 LTS | `netstandard2.0` | ✅ CLI / 測試專案覆蓋 `net8.0`；程式庫雙 TFM 建置 |
| .NET Framework 4.8 | `netstandard2.0` | ✅ `OdfKit.NetFramework48Smoke` 以本機 nupkg 在 CLR 4.x 執行四主格式 round-trip 與 7 個 extensions 最小入口 |
| 其他 .NET Standard 2.0 相容專案 | `netstandard2.0` | ⚠️ 提供相容資產；低於 .NET Framework 4.8 的 CLR 尚未列為實機執行門檻 |

### 桌面作業系統與架構

| 作業系統與架構 | CI 驗證方式 | Imaging 原生資產契約 |
|----------------|-------------|----------------------|
| Linux x64 | GitHub-hosted `ubuntu-latest` 實機 consumer smoke | `SkiaSharp.NativeAssets.Linux` |
| Windows x64 | GitHub-hosted `windows-latest` 實機 consumer smoke | `SkiaSharp.NativeAssets.Win32` |
| Windows ARM64 | GitHub-hosted `windows-11-arm` 實機 consumer smoke | `SkiaSharp.NativeAssets.Win32` |
| macOS ARM64 | GitHub-hosted `macos-15` 實機 consumer smoke | `SkiaSharp.NativeAssets.macOS` |
| macOS x64 | 可由 `osx-x64` RID restore／publish；未列入每次 PR 實機矩陣 | `SkiaSharp.NativeAssets.macOS` |

上述四個實機 consumer job 使用同一次 Ubuntu 封裝產生的 artifact，並在執行前驗證
`SHA256SUMS`，因此驗證的是同一份候選套件，而不是各 runner 各自產生的封裝。核心與純
managed 擴充套件不綁定特定 CPU 架構；Imaging 套件則明確宣告 Linux、Windows 與 macOS
原生資產相依，由消費端 RID 選取對應檔案。

## 4. 發佈與安裝策略

| 管道 | 說明 |
|------|------|
| **GitHub 原始碼** | 主要使用方式（clone、`ProjectReference`） |
| **CI 候選資產** | 每次工作流程產生 commit-bound 候選套件，供驗證與人工決策 |
| **GitHub Release** | 發佈自動化已備妥，但目前尚未建立公開 Release |
| **nuget.org** | **非目前目標** |

首個經人工核准的 GitHub Release 建立後，可下載資產並執行：

```powershell
dotnet nuget add source C:\path\to\release-assets --name odfkit-github-release
dotnet add package OdfKit --version 0.0.1 --source odfkit-github-release
```

若需固定套件選型與導入順序，請先讀
[套件目錄與選型指南](package-catalog.md)。

在公開 Release 建立前，請使用原始碼／`ProjectReference`，或使用相同提交的 CI 候選資產；
不得把工作流程演練產物描述為已公開發佈的套件。

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

- 部分擴充套件依賴原生或重型第三方套件；部署前應評估其平台與授權需求。macOS x64
  目前有 RID restore／publish 契約，但不在每次 PR 的 GitHub-hosted 實機矩陣內。
- `OdfKit.Extensions.Rendering` 需外部 LibreOffice 或相容程序後端，詳見
  [Rendering 後端部署](rendering-backend-deployment.md)。
