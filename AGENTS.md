# OdfKit 專案 Agent 開發規範

本檔案是所有開發 Agent 的共同入口。它只保留全域工作契約、非顯而易見的不變量、
文件路由與最低驗證；領域細節依工作範圍讀取連結文件。

## 1. 工作契約

- **回答、解釋、審查、診斷或規劃**：讀取相關檔案並提出有證據的結論；除非使用者同時
  要求實作，否則不得修改檔案。
- **實作、修正、重構或建置**：完成要求範圍內的本機變更，並執行與風險相稱的非破壞性
  驗證，不需為預期的唯讀操作、範圍內編輯或測試另行詢問。
- **需要確認的動作**：外部寫入、發布、推送、購買、破壞性操作，以及實質擴大任務範圍
  前，必須取得使用者明確授權。
- **最小必要範圍**：保留使用者既有變更；不得修改無關檔案、產生碼或為機械指標擴大重構。
- 必要需求不明且不同選擇會實質改變結果時，只詢問最小必要資訊；若可由專案內容安全
  推斷，明示假設並繼續。

變更完成前必須：

1. 實現要求行為且維持本檔案的不變量。
2. 執行最相關的格式化、建置、測試與專案閘門；無法執行時說明原因。
3. 檢查差異，確認沒有非預期檔案、手改產生碼或合併衝突標記。
4. 回覆完成內容、驗證結果與剩餘風險；已有足夠證據時停止。

## 2. 專案與全域不變量

- OdfKit 使用 C#／.NET；TFM、條件式相依套件與版本一律以各 `.csproj` 為準。跨 TFM
  共用程式碼必須驗證所有受影響目標。
- 專案原創程式碼採 CC0-1.0 Universal；第三方套件維持其原授權。
- 手寫 C# 使用檔案範圍命名空間、啟用的可空性與周遭程式碼的命名、註解密度及慣用語法。
- 公開與受保護 API 維持英文＋正體中文 XML 文件；一般中文註解使用臺灣地區用語。
- 例外訊息使用 `OdfLocalizer.GetMessage`，不得硬編碼；在地化 JSON 是產生字典的來源。
- XML 節點與屬性以 `NamespaceURI`＋`LocalName` 比對，不依賴前綴。
- ZIP entry 路徑使用 `/`。UTC 日期格式為 `yyyy-MM-ddTHH:mm:ssZ`，本地日期格式為
  `yyyy-MM-ddTHH:mm:ss`，並保護 `DateTime.MinValue`／`DateTime.MaxValue` 邊界。
- XML／ZIP 輸入必須維持 XXE、實體展開、DoS 與 Zip Slip 防禦。
- `OdsStreamWriter`／`OdtStreamWriter` 維持串流、低常駐設計；效能宣稱只使用可重現基準，
  不使用未限定的「小於 1 MB」敘事。
- `DOM/Generated`、schema provider `.g.cs` 與 `OdfLocalizer.Exceptions.<culture>.cs`
  不可手改；修改其產生器或來源後重產。
- 核心預設內建 ODF 1.0～1.4 官方 schema 是產品不變量；不得為瘦身套件而拆成可選套件
  或刪減多版本覆蓋。
- 禁止因行數、token 或機械 KPI 切割 partial／大型型別，也不得重跑
  `eng/historical-refactor/Split-*`。
- **不得在方案根目錄直接執行無專案範圍的 `dotnet format`**。雙 TFM 測試專案可能被
  寫入合併衝突標記；一律使用 `eng/Format-Safe.ps1`。

## 3. 按工作範圍載入文件

只讀取目前任務需要的文件：

| 修改範圍 | 先讀 |
| --- | --- |
| 手寫 C#、XML 文件 | [`docs/agent-guides/code-style.md`](docs/agent-guides/code-style.md) |
| 測試程式碼 | [`docs/agent-guides/testing.md`](docs/agent-guides/testing.md) |
| Partial、產生碼、schema 或大型結構 | [`docs/maintainability.md`](docs/maintainability.md) |
| Public API／可選參數 | [`OdfKit/PublicAPI/README.md`](OdfKit/PublicAPI/README.md) 與 [`docs/public-api-optional-parameters.md`](docs/public-api-optional-parameters.md) |
| 在地化鍵與例外字典 | [`OdfKit/Compliance/i18n/README.md`](OdfKit/Compliance/i18n/README.md) |
| CI workflow、cache 或 artifact | [`docs/ci-cd.md`](docs/ci-cd.md) 與 `eng/ci-resource-policy.json` |
| API／文件網站 | [`docs/api-docs-site.md`](docs/api-docs-site.md) |
| 效能程式碼或公開數值 | [`docs/performance-baselines.md`](docs/performance-baselines.md) 與 [`docs/performance-comparison.md`](docs/performance-comparison.md) |
| 建立 Git commit | [`docs/agent-guides/commits.md`](docs/agent-guides/commits.md) |
| 尋找工具或驗證腳本 | [`eng/README.md`](eng/README.md) |

任何 `*plan*.md` 或名稱含「計畫」的檔案都只能作為短期工作暫存，不得被長期 Agent
規範引用；完成後應移除。

## 4. 最低驗證矩陣

依實際修改範圍執行最小但充分的驗證：

| 修改類型 | 最低驗證 |
| --- | --- |
| 僅函式庫程式碼 | `pwsh eng/Format-Safe.ps1`，並建置或測試受影響專案與 TFM |
| 包含測試程式碼 | `pwsh eng/Format-Safe.ps1 -IncludeTests`，並以 `-p:RunAnalyzersDuringBuild=true` 建置對應測試 TFM |
| 在地化 | `pwsh eng/Test-LocalizerKeyParity.ps1 -FailOnIssues` 與 `pwsh eng/Generate-LocalizerExceptionsFromJson.ps1 -VerifyOnly` |
| 公開 API 或 schema | `pwsh eng/Generate-PublicApiBaseline.ps1 -Verify` |
| CI 資源政策 | `pwsh eng/Test-CiResourcePolicy.ps1` |
| API／文件網站 | 依 `docs/api-docs-site.md` 執行 `Build-ApiDocs.ps1`，並檢查桌面與窄螢幕 |
| 封裝或跨套件 TFM | `pwsh eng/Test-NuGetPack.ps1` |
| 效能文件數值 | `pwsh eng/Benchmark-Competitive.ps1` |

`Format-Safe.ps1` 已包含合併衝突、環境變數隔離、一行式 XML summary 與雙語 XML
文件閘門。不要以重跑 CI 取代本機可重現的診斷。

## 5. 跨 Agent 相容性

- 本文件只定義可觀察成果、授權邊界、不變量、文件路由與驗證要求，不依賴特定模型。
- 需要專屬入口檔的工具應以墊片指向本檔案，不得複製全文。
- Skills 可提供按需工作流程，但 repository 文件與腳本仍是跨工具的權威來源。
- 新增規則前，確認它能改變 Agent 行為或修正重複發生的缺口；可由程式碼、工具或現有
  文件清楚推斷的內容不放回主檔。
