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
| `Format-Safe.ps1` | 安全格式化與共用靜態閘門：避免全方案 `dotnet format` 污染雙 TFM 測試專案，並檢查衝突標記、環境變數隔離、一行式 summary 與雙語 XML 文件。 |
| `Format-Workspace.ps1` | 格式化 OdfKit 工作區，排除會觸發 Visual Studio 合併標記的 `OdfKit.Tests` 專案。 |

### 測試與驗證

環境變數隔離由 eng/Test-EnvironmentVariableIsolation.ps1 檢查；所有指令碼與測試程式
必須只使用 process scope，並在 finally 中還原原值。

| 腳本 | 用途 |
|------|------|
| `Test-GpgSignatures.ps1` | 驗證此 repo 所有提交皆為有效 GPG 簽署，且僅使用 repo 專屬金鑰。 |
| `Test-CiResourcePolicy.ps1` | 驗證 GitHub Actions cache、artifact、timeout、排程與高成本工作 opt-in 的資源治理契約。 |
| `Test-CodeCoverage.ps1` | 驗證 Cobertura 全專案 line／branch coverage 最低門檻。 |
| `Test-GitHubActionsPolicy.ps1` | 驗證第三方 Actions 皆以完整 SHA 固定、附版本註解並由 Dependabot 每週追蹤最新版；`-Online` 會與官方 GitHub API 對照。 |
| `Test-LibreOfficeInterop.ps1` | 執行 LibreOffice headless 實機互通性測試。 |
| `Test-MergeConflictMarkers.ps1` | 掃描原始碼中的合併衝突標記（CS8300 成因）。 |
| `Test-NoEmptyCatch.ps1` | 掃描所有手寫 C#，拒絕會靜默吞掉例外的空白 `catch`。 |
| `Test-MarkdownLinks.ps1` | 驗證 `README.md`、`AGENTS.md`、`docs/` 與 eng 文件的本機連結目標及 Markdown anchor 存在，且未越出工作區。 |
| `Test-XmlReaderSecurity.ps1` | 掃描手寫 `XmlReaderSettings`，要求明確禁止 DTD 並停用外部 XML resolver。 |
| `Test-NuGetPack.ps1` | 驗證 OdfKit NuGet 封裝結構與 net8.0 消費端煙霧建置。 |
| `Test-WebFontReleaseRehearsal.ps1` | 以同批 nupkg 演練隔離本機 feed、SBOM 消費與 NuGet Audit。 |
| `Test-WebFontIisSustainedLoad.ps1` | 以鎖定 CNS 字型對 Web Forms Integrated／Classic 與 ASP.NET Core In／Out-of-Process 執行手動有界持續負載。 |
| `Test-WebFontFormatMatrix.ps1` | 以 SHA-256 鎖定的 CNS、多國 complex script、CID／名稱式 CFF、`seac`、靜態／variable CFF2、COLRv1、sbix 與 OpenType SVG corpus 驗證 managed 輸入、輸出及固定種子 mutation；`-NoRestore` 可供已還原的離線工作區使用。 |
| `Test-WebFontAdvancedCorpus.ps1` | 以採用者提供且鎖定 SHA-256 的 AAT、Graphite、variable 與 color 真實字型 corpus 驗證 table 分類及 managed 支援／明確拒絕邊界。 |
| `Test-WebFontLayoutBrowserSmoke.ps1` | 以 Chromium／Firefox／WebKit 比較真實來源與 managed WOFF2 的 Canvas RGBA bytes、文字 metrics 及截圖證據；color 模型只在實際支援的引擎要求彩色像素，其餘記錄為 `browser-unavailable`。 |
| `Test-WebFontWoff2Corpus.ps1` | 下載 SHA-256 鎖定的 W3C 與 Google Fonts production corpus，驗證 standalone 與多 face collection transformed WOFF2 解碼、官方 reference 逐表比對及固定種子 mutation。 |
| `Test-OdfCorpus.ps1` | 執行內建（與選用外部）corpus 驗證，詳見 [corpus-manifest.md](../docs/corpus-manifest.md)。 |
| `Test-OdfPolicy.ps1` | 執行 `Category=Policy` 測試，覆蓋巨集淨化、外部資源 policy、加密文件重新加密與相關安全邊界。 |
| `Test-OdfTypedDomCoverage.ps1` | 執行 typed DOM 對 ODF schema 的覆蓋率報告與門檻檢查。 |
| `Test-BilingualXmlDocs.ps1` | 靜態掃描公開／受保護 API 的雙語 XML 文件覆蓋率；預設 report mode。`-FailOnNewIssues` 以基線 `TOTAL=0`／`FILES=0` 阻止新增債務；`-FailOnIssues` 要求完全乾淨。 |
| `Rewrite-ConvenienceSummaries.py` | 將手寫 API 通用「便利多載」摘要改寫為含方法／參數名的雙語差異化摘要（預設掃 `OdfKit`／`OdfKit.Extensions.*`，略過 Generated）。 |
| `Rewrite-ExecuteOperationSummaries.py` | 將 `Executes the X operation` 占位摘要改寫為依方法名推導的雙語語意（保留已具領域說明的中文行）。 |
| `Test-OneLineXmlSummary.ps1` | 掃描手寫 C# 是否含禁止的一行式 `<summary>`；`-FailOnIssues` 時失敗退出。 |
| `Test-LocalizerKeyParity.ps1` | 檢查 17 語系 `OdfLocalizer.Exceptions.*.cs` 訊息鍵、佔位符與翻譯品質；`-FailOnIssues` 時失敗退出。 |
| `Add-LocalizerKey.ps1` | 於 17 語系 `i18n/exceptions.*.json` 新增鍵並重產 C#（支援 `-WhatIf`）。 |
| `Generate-LocalizerExceptionsFromJson.ps1` | 自 JSON 產生 `OdfLocalizer.Exceptions.<culture>.cs`；`-VerifyOnly` 檢查一致性。 |
| `Build-ApiDocs.ps1` | 建置 17 語系 GitHub Pages API reference 站台（DocFX modern），內建固定版本、語系 TOC、權威文件、footer、未渲染頁面 href、sitemap 與站內連結驗證；可用 `-OutputDirectory` 指定工作區內的替代輸出，見 [api-docs-site.md](../docs/api-docs-site.md)。 |
| `Test-ApiDocsTranslations.ps1` | 驗證 DocFX 正式文件的多語系翻譯契約（來源雜湊、必要 token 與同語系導覽）；同目錄 `Test-ApiDocsTranslations.Tests.ps1` 是其 Pester 自我測試。 |
| `Test-OoxmlVisualGolden.ps1` | 執行 OOXML 轉換視覺 golden file 驗收。 |
| `Test-RenderingBackends.ps1` | 執行 `OdfKit.Extensions.Rendering` 相關單元測試。 |
| `Test-TrimSmoke.ps1` | 建置並執行 OdfKit trimming（Native AOT）煙霧測試。 |
| `Test-SemanticCoverage.ps1` | 依 `docs/semantic-coverage.json` 驗證語意覆蓋 manifest 與對應測試證據。 |
| `Test-EvidenceClaims.ps1` | 依 `docs/claims.json` 驗證能力宣稱的維度與驗證層級，見 [evidence-index.md](../docs/evidence-index.md)。 |
| `Test-NetFramework48Smoke.ps1` | 以本機 nupkg 在 .NET Framework 4.8 消費端執行四主格式 round-trip 與 extension 入口煙霧測試。 |
| `Test-OdfRelaxNgBaseline.ps1` | 以固定版本 Jing 對 corpus 執行 RELAX NG 外部對標，需先以 `Install-Jing.ps1` 取得 JAR。 |
| `Test-OfficeGuiSmoke.ps1` | 執行 Microsoft Office GUI／COM 的 ODT／ODS／ODP 讀取煙霧驗收（僅 Windows，手動）。 |

產品品質分層與發版前建議清單見 [docs/product-quality-gates.md](../docs/product-quality-gates.md)。

### 效能

| 腳本 | 用途 |
|------|------|
| `Benchmark-Performance.ps1` | 執行 OdfKit 效能相關單元測試與簡易計時。 |
| `Benchmark-Regression.ps1` | 執行 BenchmarkDotNet 微基準並與 `eng/baselines/performance-baselines.json` 基準線比對。 |
| `Benchmark-Stable.ps1` | 以較長且時間導向的 BenchmarkDotNet profile 執行本機穩定效能量測。 |
| `Benchmark-BaselineReport.ps1` | 執行 stable benchmark profile 並產生 Markdown 效能基準報告。 |
| `Test-PerformanceBudgets.ps1` | 驗證效能預算、schema v2 樣本與候選；active 時執行 allocation 硬閘門及耗時／峰值提醒。 |
| `New-PerformanceBudgetCandidate.ps1` | 從至少三份同 OS／架構／runtime／CPU 且執行身分唯一的 schema v2 樣本計算九情境中位數候選；不會自動啟用門檻。 |
| `Benchmark-Competitive.ps1` | 執行 `OdsStreamWriter` 與 MiniExcel、ClosedXML 的跨套件串流寫入對比，是 [performance-comparison.md](../docs/performance-comparison.md) 公開數值的來源。 |
| `Benchmark-StandardDocuments.ps1` | 以獨立子處理程序執行 ODS／ODT／ODP 標準工作負載，輸出耗時、配置量、峰值工作集與語意 checksum，見 [performance-standard-documents.md](../docs/performance-standard-documents.md)。 |

### 封裝與發行

| 腳本 | 用途 |
|------|------|
| `Pack-NuGet.ps1` | 建置並封裝所有可發佈的 OdfKit NuGet 套件。 |
| `Publish-GitHubRelease.ps1` | 將已驗證的 NuGet 套件附加至 GitHub Release，詳見 [github-release-publishing.md](../docs/github-release-publishing.md)。 |
| `Test-ReleaseSbom.ps1` | 由完整方案 restore closure 與發布 nupkg 產生 SPDX 3.0.1 JSON-LD 主 SBOM，並產生 GitHub attestation 專用 SPDX 2.3 相容檔。 |
| `Publish-WebFontSidecar.ps1` | 建立 Windows NativeAOT WebFont sidecar 的可發布 ZIP 與 SHA-256 manifest，見 [webfont-sidecar-deployment.md](../docs/webfont-sidecar-deployment.md)。 |
| `Manage-WebFontSidecarService.ps1` | 安裝、更新、查詢或解除安裝 WebFont Sidecar Windows Service。 |

### WebFont 驗證

WebFont 驗證多半需要外部字型、瀏覽器或 IIS，屬手動或專用工作流程；能力範圍與人工閘門見
[webfont-evidence-matrix.md](../docs/webfont-evidence-matrix.md)。

| 腳本 | 用途 |
|------|------|
| `Test-WebFontSmoke.ps1` | 以真實 CNS 11643 字型驗證純 .NET 子集、HTTP 動態產字與三瀏覽器載入。 |
| `Test-WebFontPackageConsumer.ps1` | 從本次 nupkg 安裝 WebFont library 與 CLI，並以真實 CNS 字型離線產字。 |
| `Test-WebFontSupplyChain.ps1` | 驗證 WebFont 相依授權漂移，並產生可重現的 SPDX 2.3 SBOM。 |
| `Test-WebFontStandardsAndDependencies.ps1` | 驗證 WebFont 規範基準、相依政策與全專案 GitHub Actions 供應鏈政策；`-Online` 查詢官方 API 且連線失敗時 fail closed。 |
| `Test-WebFontOtsOracle.ps1` | 以 OpenType Sanitiser 對產出的 WebFont 資產做差分驗證。 |
| `Test-WebFontCmapScaleBrowserProof.ps1` | 以真實字型與三個瀏覽器引擎驗證 cmap format 4 的規模路徑。 |
| `Test-WebFontWorkerProcessSmoke.ps1` | 以兩個獨立 OS process 驗證 WebFont 動態產生與故障復原。 |
| `Test-WebFontIisExpressSmoke.ps1` | 以真實 IIS Express 驗證 ASP.NET Web Forms 動態 WebFont 部署。 |
| `Test-WebFontAspNetCoreIisExpressSmoke.ps1` | 以 IIS Express 與 ASP.NET Core Module 驗證 ASP.NET Core 動態 WebFont 部署。 |
| `Test-WebFontSidecarAot.ps1` | 發布 NativeAOT sidecar 並以 net48 用戶端產生真正的 WOFF2。 |
| `Test-WebFontSidecarWindowsService.ps1` | 發布並透過真實 Windows SCM 驗證 NativeAOT sidecar。 |
| `Test-PlaywrightFirefoxShortcutPolicy.ps1` | 驗證 Playwright Firefox 私密瀏覽捷徑清理範圍不會影響一般 Firefox。 |
| `Remove-PlaywrightFirefoxPrivateBrowsingShortcut.ps1` | 移除 Playwright Firefox 私密瀏覽代理程式與目前使用者開始功能表捷徑。 |
| `WebFontIisSmoke.Common.ps1` | 兩支 IIS Express 煙霧腳本共用的函式模組，不單獨執行。 |
| `Generate-WebFontPublicApiBaselines.ps1` | 重產各 WebFont 套件的 `PublicAPI.Unshipped.txt` 基線。 |

### 外部工具與資料安裝

外部工具版本、來源 URL 與 SHA-256 一律由 `eng/external-tools.json` 釘選；腳本在
cache 命中時仍會重新驗證雜湊。

| 腳本 | 用途 |
|------|------|
| `Install-Jing.ps1` | 依 manifest 下載並驗證固定版本 Jing RELAX NG 驗證器。 |
| `Install-LibreOfficeManifestSchema.ps1` | 依 manifest 下載並驗證與排程實機版本一致的 LibreOffice extended manifest schema。 |
| `Install-OdfValidator.ps1` | 依 manifest 下載並驗證固定版本 ODF Validator。 |
| `Install-Cns11643MappingTables.ps1` | 下載並驗證全字庫（CNS 11643 open data）中文碼對照表；資料不內建於儲存庫。 |

### Schema 與 Corpus 產生

| 腳本 | 用途 |
|------|------|
| `Generate-OdfSchemaProvider.ps1` | 從 OASIS RNG schema manifest（`tools/OdfSchemaGenerator/`）產生 schema provider 程式碼。 |
| `Generate-OpenFormulaNormativeCorpus.ps1` | 從 OASIS ODF 1.4 Part 4 HTML 擷取函式條文，並以固定 LibreOffice headless 產生 388 筆 Safe Large 離線 oracle。 |
| `Initialize-OdfExternalCorpus.ps1` | 將外部 corpus manifest 與 baseline exception 範本複製到指定資料夾，詳見 [corpus-manifest.md](../docs/corpus-manifest.md)。 |
| `Test-WholesomeManifestBaseline.ps1` | 由目前 CLI 產生 wholesome 加密 ODT，抽出 manifest 並以固定版本 LibreOffice schema 與 Jing 驗證。 |

### 程式碼結構診斷

| 腳本 | 用途 |
|------|------|
| `Analyze-PartialSplits.ps1` | 分析目前 partial 型別的拆分狀況，列出明確保留邊界（schema 驅動、功能區切割、加密管線等）的巨型型別。 |
| `Build-AnalyzerReport.ps1` | 產生 OdfKit 建置 binlog 供 Analyzer Summary 剖析（本機診斷用）。 |
| `Detect-TypeBoundaries.ps1` | 在指定檔案中以正規表達式找出符合的行號（通用搜尋輔助）。 |
| `List-LargeCsFiles.ps1` | 列出 `OdfKit` 中超過指定行數門檻的最大 `.cs` 檔案，用於評估是否需要 god class 拆分。 |
| `Expand-OptionalParameters.py` | 將「恰好一個」尾端可選參數的公開／保護方法展開為明確多載鏈（工程腳本，非執行時相依）。 |
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
