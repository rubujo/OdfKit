# CI/CD 驗證設計

本文件是 OdfKit GitHub Actions 驗證與交付流程的長期說明。它不是臨時計畫檔，
而是維護 CI/CD 分層、timeout 與診斷輸出的規則來源。

## 主 CI

`.github/workflows/ci.yml` 是每次程式碼變更的快速回歸入口。

- `ubuntu-latest` 與 `windows-latest` 都必須執行。
- `net8.0` 與 `net10.0` 都必須先建置 `OdfKit.Tests`。
- `net8.0` 與 `net10.0` 都必須執行 `Category=Smoke`，避免只有較新 TFM 有測試證據。
- 建置與測試分成不同 step；Smoke 測試再依 docs、api、package-entries、
  package-roundtrip、vertical-slice、core-security
  分成不同 step，避免單一 testhost 長時間承載整批測試時難以定位 hang。
- 測試 step 有較短 timeout，避免整個 job 黑箱卡到總 timeout。
- 測試輸出 TRX 與 blame 診斷檔，並以 artifact 上傳。

Windows `net10.0` 曾在單一 `Category=Smoke` 批次中留下 `OdfKit.Tests.exe` 測試子行程，
即使個別測試單跑可通過也可能讓 VSTest/testhost 無法結束。Smoke shard 是針對這個
testhost 收尾風險的必要設計，不得合回單一全量 Smoke step。

Smoke 測試只放「快速、無外部 Office/LibreOffice 依賴、可在 GitHub-hosted runner 穩定完成」
的回歸案例。需要外部應用程式、真實大型 corpus、視覺比對或效能統計的工作不得塞入主 CI。

## 專用驗證 workflow

| Workflow | 目的 | 是否自動跑 |
|----------|------|------------|
| `odf-corpus.yml` | repo 內 ODF corpus 與可選外部 corpus 驗證 | PR / main |
| `odf-policy.yml` | 安全與政策規則測試 | PR / main |
| `typed-dom-coverage.yml` | typed DOM coverage floor 與 artifact | PR / main |
| `trim-smoke.yml` | Native AOT / trim smoke | PR / main |
| `nuget-pack.yml` | NuGet 封裝與 net8.0 consumer smoke | PR / main |
| `performance-benchmark.yml` | 效能回歸 benchmark | 手動 |
| `github-release.yml` | tag 驅動的發佈流程 | tag |

LibreOffice、Microsoft Office COM 與 PDF 像素級比對屬外部環境驗收，必須由專用 workflow
或本機腳本明確啟用，不得混入主 CI 的 Smoke。

## Timeout 與診斷

CI 必須優先產生可診斷失敗，而不是只延長 timeout。

- job 保留整體 timeout，防止 runner 無限占用。
- 主要 build/test step 也要設定 timeout，讓卡住的位置能被定位。
- `dotnet test` 必須輸出 TRX。
- Smoke 測試啟用 blame hang；日常 CI 使用 `--blame-hang-dump-type none` 避免 artifact 爆量。
- Crash dump 與 hang dump type `mini` / `full` 僅用於手動診斷重跑，避免日常 CI 在 Windows
  testhost 收尾階段被 dump collector 反向拖慢或放大 artifact。

## 分層規則

新增或調整測試時，請依下列原則標記：

- `Smoke`：快速、穩定、跨平台、無外部二進位依賴。
- `Regression`：功能回歸，可由完整測試或專用 workflow 執行。
- `Scenario`：較完整的高階情境測試。
- `Interop`：外部格式或應用程式互通。
- `Corpus`：manifest / fixture corpus 驗證。
- `Policy`：安全、profile、sanitization 與治理規則。
- `Stress` / `Performance`：不得進入主 CI Smoke。

如果某個測試只在 Windows 或特定 TFM 會執行特殊路徑，該測試不應只靠單一矩陣格背書；
至少要在 CI 設計上讓另一個 TFM 或 OS 能提供可比較資料。
