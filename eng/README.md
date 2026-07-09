# eng 目錄總覽

本目錄收錄 OdfKit 的建置、測試、封裝與開發輔助腳本。多數為 PowerShell 7+
(`#Requires -Version 7.0`)，少數為 Python（視覺差異比對）。

`eng/` 下的腳本分兩大類：

1. **常用維護腳本**（下表列出）：CI 與本機開發會持續呼叫，文件（`AGENTS.md`、各
   `docs/*.md`）會個別引用其中部分腳本。
2. **歷史性一次性重構腳本**（`Split-*`／`Merge-*`／`Migrate-*`／`Rename-*`）：
   已移至 [`historical-refactor/`](historical-refactor/README.md)，**預設不要重跑**。
   Partial／在地化準則見 [docs/maintainability.md](../docs/maintainability.md)。

## 常用維護腳本

### 建置與相依

| 腳本 | 用途 |
|------|------|
| `Ensure-OdfKitBuilt.ps1` | 確保 OdfKit net10.0 組件已建置且與來源同步。 |
| `Get-PackageVersion.ps1` | 讀取 `eng/OdfKit.Package.props` 中的套件版本號。 |

### 格式化

| 腳本 | 用途 |
|------|------|
| `Format-Safe.ps1` | 安全格式化：避免全方案 `dotnet format` 污染 `OdfKit.Tests`（雙 TFM + analyzer 修正）。 |
| `Format-Workspace.ps1` | 格式化 OdfKit 工作區，排除會觸發 Visual Studio 合併標記的 `OdfKit.Tests` 專案。 |

### 測試與驗證

| 腳本 | 用途 |
|------|------|
| `Test-GpgSignatures.ps1` | 驗證此 repo 所有提交皆為有效 GPG 簽署，且僅使用 repo 專屬金鑰。 |
| `Test-LibreOfficeInterop.ps1` | 執行 LibreOffice headless 實機互通性測試。 |
| `Test-MergeConflictMarkers.ps1` | 掃描原始碼中的合併衝突標記（CS8300 成因）。 |
| `Test-NuGetPack.ps1` | 驗證 OdfKit NuGet 封裝結構與 net8.0 消費端煙霧建置。 |
| `Test-OdfCorpus.ps1` | 執行內建（與選用外部）corpus 驗證，詳見 [corpus-manifest.md](../docs/corpus-manifest.md)。 |
| `Test-OdfPolicy.ps1` | 執行 `Category=Policy` 測試，覆蓋巨集淨化、外部資源 policy、加密文件重新加密與相關安全邊界。 |
| `Test-OdfTypedDomCoverage.ps1` | 執行 typed DOM 對 ODF schema 的覆蓋率報告與門檻檢查。 |
| `Test-BilingualXmlDocs.ps1` | 靜態掃描公開／受保護 API 的雙語 XML 文件覆蓋率；預設 report mode，`-FailOnNewIssues` 會以現行基線阻止新增債務。 |
| `Test-OneLineXmlSummary.ps1` | 掃描手寫 C# 是否含禁止的一行式 `<summary>`；`-FailOnIssues` 時失敗退出。 |
| `Test-LocalizerKeyParity.ps1` | 檢查 12 語系 `OdfLocalizer.Exceptions.*.cs` 訊息鍵集合與 `en` 對等；`-FailOnIssues` 時失敗退出。 |
| `Test-OoxmlVisualGolden.ps1` | 執行 OOXML 轉換視覺 golden file 驗收。 |
| `Test-RenderingBackends.ps1` | 執行 `OdfKit.Extensions.Rendering` 相關單元測試。 |
| `Test-TrimSmoke.ps1` | 建置並執行 OdfKit trimming（Native AOT）煙霧測試。 |

### 效能

| 腳本 | 用途 |
|------|------|
| `Benchmark-Performance.ps1` | 執行 OdfKit 效能相關單元測試與簡易計時。 |
| `Benchmark-Regression.ps1` | 執行 BenchmarkDotNet 微基準並與 `eng/baselines/performance-baselines.json` 基準線比對。 |
| `Benchmark-Stable.ps1` | 以較長且時間導向的 BenchmarkDotNet profile 執行本機穩定效能量測。 |
| `Benchmark-BaselineReport.ps1` | 執行 stable benchmark profile 並產生 Markdown 效能基準報告。 |

### 封裝與發行

| 腳本 | 用途 |
|------|------|
| `Pack-NuGet.ps1` | 建置並封裝所有可發佈的 OdfKit NuGet 套件。 |
| `Publish-GitHubRelease.ps1` | 將已驗證的 NuGet 套件附加至 GitHub Release，詳見 [github-release-publishing.md](../docs/github-release-publishing.md)。 |

### Schema 與 Corpus 產生

| 腳本 | 用途 |
|------|------|
| `Generate-OdfSchemaProvider.ps1` | 從 OASIS RNG schema manifest（`tools/OdfSchemaGenerator/`）產生 schema provider 程式碼。 |
| `Initialize-OdfExternalCorpus.ps1` | 將外部 corpus manifest 與 baseline exception 範本複製到指定資料夾，詳見 [corpus-manifest.md](../docs/corpus-manifest.md)。 |

### 程式碼結構診斷

| 腳本 | 用途 |
|------|------|
| `Analyze-PartialSplits.ps1` | 分析目前 partial 型別的拆分狀況，列出明確保留邊界（schema 驅動、功能區切割、加密管線等）的巨型型別。 |
| `Build-AnalyzerReport.ps1` | 產生 OdfKit 建置 binlog 供 Analyzer Summary 剖析（本機診斷用）。 |
| `Detect-TypeBoundaries.ps1` | 在指定檔案中以正規表達式找出符合的行號（通用搜尋輔助）。 |
| `List-LargeCsFiles.ps1` | 列出 `OdfKit` 中超過指定行數門檻的最大 `.cs` 檔案，用於評估是否需要 god class 拆分。 |
| `Generate-PublicApiBaseline.ps1` | 以 PublicApiAnalyzers RS0016 code fix 重產雙 TFM 的 `PublicAPI.Unshipped.txt`；建議加 `-Verify`。 |

### `eng/scripts/`

| 檔案 | 用途 |
|------|------|
| `PdfVisualDiff.py` | OOXML/PDF 視覺差異比對（供 [ooxml-visual-golden-matrix.md](../docs/ooxml-visual-golden-matrix.md) 流程呼叫）。 |

### `eng/baselines/`

| 檔案 | 用途 |
|------|------|
| `performance-baselines.json` | `Benchmark-Regression.ps1` 比對用的效能基準線資料。 |

## 歷史重構腳本

`Split-*`／`Merge-*`／`Migrate-*`／`Rename-*` 已全部移至
[`historical-refactor/`](historical-refactor/README.md)（約 102 個腳本）。
它們是 god class 拆分計畫（commit 歷史 `7b6f1f79`～`f5189e8d` 等）的**一次性**產物，
**預設不要重跑**。日常 partial 準則見 [docs/maintainability.md](../docs/maintainability.md)；
診斷用 `Analyze-PartialSplits.ps1`／`List-LargeCsFiles.ps1`。
