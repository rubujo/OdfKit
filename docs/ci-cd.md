# CI/CD 驗證設計

本文件是 OdfKit GitHub Actions 驗證與交付流程的長期說明。它不是臨時計畫檔，
而是維護 CI/CD 分層、逾時與診斷輸出的規則來源。

## 主 CI

`.github/workflows/ci.yml` 是每次程式碼變更的快速回歸入口。

- **`maintainability` job**（先跑、ubuntu）：靜態閘門，失敗則不啟動完整測試矩陣。
  - `Test-MergeConflictMarkers.ps1`
  - `Test-OneLineXmlSummary.ps1 -FailOnIssues`
  - `Test-LocalizerKeyParity.ps1 -FailOnIssues`
  - `Generate-LocalizerExceptionsFromJson.ps1 -VerifyOnly`（JSON ↔ C# 一致）
  - `dotnet build OdfKit`（`RunAnalyzersDuringBuild=true`，含 PublicApiAnalyzers）
- `full-regression` job 依賴 `maintainability`，在 Ubuntu 對 `net8.0` 與 `net10.0`
  執行完整測試套件並上傳 TRX；它是每次 PR 與 main push 的必要回歸證據。
- `test` job 依賴 `maintainability`；`ubuntu-latest` 與 `windows-latest` 都必須執行。
- `net8.0` 與 `net10.0` 都必須先建置 `OdfKit.Tests`。
- `net8.0` 與 `net10.0` 都必須執行 `Category=Smoke`，避免只有較新 TFM 有測試證據。
- 建置與測試分成不同步驟；煙霧測試再依 docs、api、package-entries、
  package-roundtrip-core、package-roundtrip-embedded、package-roundtrip-preservation、
  vertical-slice、core-security
  分成不同步驟，避免單一 testhost 長時間承載整批測試時難以定位停滯。
- 測試步驟有較短逾時，避免整個 job 黑箱卡到總逾時。
- 測試輸出 TRX 與 blame 診斷檔，並以產物上傳。
- 雙 TFM 套件相容性（`EnablePackageValidation`）由 `nuget-pack.yml`／`Test-NuGetPack.ps1` 在 pack 時把關。

Windows 與 `net10.0` 曾在單一 `Category=Smoke` 批次或單一
`PackageRoundTripTests` 批次中留下 `OdfKit.Tests.exe` 測試子行程，即使個別測試單跑可通過，
也可能讓 VSTest/testhost 無法結束。煙霧測試分片是針對這個 testhost 收尾風險的必要設計，
不得合回單一全量煙霧測試步驟，也不得把所有 package round-trip 測試合回單一步驟。
完整格式矩陣來回讀寫（`PackageRoundTripMatrixTests`）保留在 `Regression` / `Compliance`
分層並標示為 explicit，不放入主 CI Smoke，避免 VSTest filter 與大量矩陣案例組合造成
testhost 收尾不穩。

煙霧測試只放「快速、無外部 Office / LibreOffice 依賴、可在 GitHub-hosted runner 穩定完成」
的回歸案例。需要外部應用程式、真實大型 corpus、視覺比對或效能統計的工作不得塞入主 CI。

## 專用驗證工作流程

| 工作流程 | 目的 | 是否自動跑 |
|----------|------|------------|
| `odf-corpus.yml` | repo 內 ODF corpus 與可選外部 corpus 驗證 | PR / main |
| `odf-external-baseline.yml` | 以固定版本 Jing 與 ODF Validator 驗證完整 RELAX NG／package 對標 | PR / main / 手動 |
| `odf-policy.yml` | 安全與政策規則測試 | PR / main |
| `typed-dom-coverage.yml` | typed DOM coverage floor 與產物 | PR / main |
| `trim-smoke.yml` | Native AOT / trim smoke | PR / main |
| `nuget-pack.yml` | 十九個 NuGet 套件的單次封裝、WebFont 發布演練與四平台 consumer smoke，包含 Imaging native runtime | PR / main |
| `performance-benchmark.yml` | DOM 與 ODS 串流效能／配置量回歸基準 | 每週 / 手動 |
| `libreoffice-interop.yml` | 目前穩定版 LibreOffice 的真實雙 TFM 互通 | 每週 / 手動 |
| `api-docs.yml` | 17 語系 GitHub Pages API reference 建置（DocFX）與部署；結構與閘門見 [api-docs-site.md](api-docs-site.md) | PR（僅建置）/ main / 手動 |
| `github-release.yml` | tag 驅動的發佈流程 | tag |

發行 workflow 只負責交付快照，不是 `v0.0.1` 完滿條件。完滿狀態由每個 `main` 提交的必要
CI 與專用排程證據持續維持；不得把契約內缺口延後到下一個 tag。

LibreOffice、Microsoft Office COM 與 PDF 像素級比對屬外部環境驗收，必須由專用工作流程
或本機腳本明確啟用，不得混入主 CI 的煙霧測試。LibreOffice 互通由排程工作流程持續驗證；
Microsoft Office COM 與像素級比對仍依可用環境手動執行。

外部 RELAX NG baseline 與快速 corpus job 分離。工作流程從
`eng/external-tools.json` 讀取 Jing 與 ODF Validator 的固定版本、來源 URL 與 SHA-256；各自的
cache key 同時包含來源、cache revision、版本與完整雜湊，且不設定 `restore-keys`。無論 cache
是否命中，安裝腳本都會重新計算 archive 與 JAR 的 SHA-256；已存在但不符時立即終止，不用
重新下載掩蓋錯誤。確認 cache 項目異常後，必須明確遞增 `cacheRevision` 取得新 key。
cache miss 的下載先寫入唯一暫存檔，驗證成功後才移入正式路徑。CI 不快取 corpus 驗證輸出、
暫存 manifest 或測試結果，因此舊結果不會被誤當成本次驗證證據。

NuGet 驗證由 Ubuntu 僅封裝一次，產生 `SHA256SUMS` 後以上傳 artifact 將同一份短期快照
分送至 Linux x64、Windows x64、Windows ARM64 與 macOS ARM64 runner。每個 consumer job
都會先驗證 SHA-256，再以安裝發布套件的獨立專案執行 managed 與 Imaging native runtime
smoke。artifact 保留一天且不作為跨次 workflow cache；NuGet restore cache 則刻意只以
`runner.os` 與相依檔雜湊分區，不加入架構或 RID，因為套件快取本身可攜帶多個 RID 資產，
加入架構只會複製同一份下載內容。此工作流程只在 PR、`main` push 與手動觸發時執行，
沒有每週排程。

Jing 20241231 會直接以 repo 內 OASIS ODF 1.1～1.4 schema 驗證該 schema 適用的 flat 文件，
以及 ZIP package 的 `content.xml`、`styles.xml`、`meta.xml` 與 `settings.xml`。通用 schema
未定義 `office:formula`，所以 Formula／FormulaTemplate／FlatFormula 由 Jing 報告明列排除，
並保留在內部 package gate。ODF Validator 0.13.0
另對適用的 ZIP package 執行容器／分類對標與正負 canary。Database 因該版本拒絕其合法
mimetype，Formula／FormulaTemplate 因該版本在 ODF 1.4 觸發上游 NPE，僅從 ODF Validator
集合排除。兩條外部 baseline 都是阻擋 gate。

## 逾時與診斷

CI 必須優先產生可診斷失敗，而不是只延長逾時。

- job 保留整體逾時，防止 runner 無限占用。
- 主要 build / test 步驟也要設定逾時，讓卡住的位置能被定位。
- 一般煙霧測試步驟使用較短停滯逾時；來回讀寫與 ZIP / Flat XML 互轉測試可使用稍長
  停滯逾時，但仍須拆成小分片，不得只靠單一長逾時掩蓋 testhost 停滯。
- `dotnet test` 必須輸出 TRX。
- 煙霧測試啟用 blame hang；日常 CI 使用 `--blame-hang-dump-type none` 避免產物爆量。
- Crash dump 與 hang dump type `mini` / `full` 僅用於手動診斷重跑，避免日常 CI 在 Windows
  testhost 收尾階段被 dump collector 反向拖慢或放大產物。

## 分層規則

新增或調整測試時，請依下列原則標記：

- `Smoke`：快速、穩定、跨平台、無外部二進位依賴。
- `Regression`：功能回歸，可由完整測試或專用工作流程執行。
- `Scenario`：較完整的高階情境測試。
- `Interop`：外部格式或應用程式互通。
- `Corpus`：manifest / fixture corpus 驗證。
- `Policy`：安全、profile、sanitization 與治理規則。
- `Stress` / `Performance`：不得進入主 CI 煙霧測試。

如果某個測試只在 Windows 或特定 TFM 會執行特殊路徑，該測試不應只靠單一矩陣格背書；
至少要在 CI 設計上讓另一個 TFM 或 OS 能提供可比較資料。
