# eng 目錄總覽

本目錄收錄 OdfKit 的建置、測試、封裝與開發輔助腳本。多數為 PowerShell 7+
(`#Requires -Version 7.0`)，少數為 Python（視覺差異比對）。

`eng/` 下的腳本分兩大類：

1. **常用維護腳本**（下表列出）：CI 與本機開發會持續呼叫，文件（`AGENTS.md`、各
   `docs/*.md`）會個別引用其中部分腳本。
2. **歷史性一次性重構腳本**（`Split-*`／`Merge-*`／`Migrate-*`／`Rename-*`，共
   102 個）：詳見下方「歷史重構腳本」段落，不建議重新執行。

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
| `Test-OdfTypedDomCoverage.ps1` | 執行 typed DOM 對 ODF schema 的覆蓋率報告與門檻檢查。 |
| `Test-OoxmlVisualGolden.ps1` | 執行 OOXML 轉換視覺 golden file 驗收。 |
| `Test-RenderingBackends.ps1` | 執行 `OdfKit.Extensions.Rendering` 相關單元測試。 |
| `Test-TrimSmoke.ps1` | 建置並執行 OdfKit trimming（Native AOT）煙霧測試。 |

### 效能

| 腳本 | 用途 |
|------|------|
| `Benchmark-Performance.ps1` | 執行 OdfKit 效能相關單元測試與簡易計時。 |
| `Benchmark-Regression.ps1` | 執行 BenchmarkDotNet 微基準並與 `eng/baselines/performance-baselines.json` 基準線比對。 |

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

### `eng/scripts/`

| 檔案 | 用途 |
|------|------|
| `PdfVisualDiff.py` | OOXML/PDF 視覺差異比對（供 [ooxml-visual-golden-matrix.md](../docs/ooxml-visual-golden-matrix.md) 流程呼叫）。 |

### `eng/baselines/`

| 檔案 | 用途 |
|------|------|
| `performance-baselines.json` | `Benchmark-Regression.ps1` 比對用的效能基準線資料。 |

## 歷史重構腳本

`Split-*.ps1`（拆分巨型型別為 partial）、`Merge-*.ps1`（合併過度細碎的 partial）、
`Migrate-*.ps1`（遷移特定屬性存取/函式分派模式）、`Rename-*.ps1`（重命名 partial
檔案）共 102 個腳本，是 god class 拆分計畫（對應 commit 歷史 `7b6f1f79`～`f5189e8d`
等 Phase 1-21）執行過程中針對特定型別（如
`OdfElement`、`DefaultFormulaEvaluator`、`OdfPackage`、`TextDocument` 等）產生的
**一次性**腳本。

這些腳本綁定當時的程式碼結構與檔案路徑，**不建議重新執行**；保留是為了維持重構過程的
可稽核性。若需了解某個型別當年如何被拆分，可直接以檔名搜尋（例如
`Split-OdfElementEnumParsersDPartials.ps1`）並對照 `git log` 找到對應提交。

若日後需要對新的巨型型別做類似拆分，建議參考這些歷史腳本的手法，但應依目標型別重新
撰寫腳本，而非修改或重跑舊腳本。
