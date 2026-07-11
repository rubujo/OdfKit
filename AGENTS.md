# OdfKit 專案 Agent 開發規範 (AGENTS.md)

本檔案為所有參與此專案之 AI 開發 Agent 的**單一事實來源 (Single Source of Truth)**。規範以工作成果與專案不變量為核心，不依賴特定供應商、產品、模型家族或推理模式。

---

## 1. Agent 工作契約

### A. 任務授權

- **回答、解釋、審查、診斷或規劃**：讀取相關檔案並提出有證據的結論；除非使用者同時要求實作，否則不得修改檔案。
- **實作、修正、重構或建置**：完成要求範圍內的本機變更，並執行與風險相稱的非破壞性驗證，不需為預期的唯讀操作、範圍內編輯或測試另行詢問。
- **需要確認的動作**：外部寫入、發布、推送、購買、破壞性操作，以及實質擴大任務範圍前，必須取得使用者明確授權。
- **最小必要範圍**：保留使用者既有變更；不得修改無關檔案，也不得為追求行數、形式一致或機械指標而擴大重構。

### B. 完成條件與停止規則

變更型任務必須符合下列條件後才算完成：

- 使用者要求的行為已實現，且未破壞本文件所列專案不變量。
- 已執行最相關的格式化、建置、測試及專案閘門；若無法執行，必須說明原因與下一個最佳驗證方式。
- 已檢查變更差異，確認沒有非預期檔案、產生碼手動修改或合併衝突標記。
- 最終回覆須說明完成內容、驗證結果及仍存在的風險或阻礙。
- 已有足夠證據回答核心要求時即停止；只有必要事實仍缺漏、驗證失敗或使用者要求完整調查時才繼續查找。

當必要需求不明且不同選擇會實質改變結果時，只詢問最小必要資訊；若可從專案內容安全推斷，應明示假設並繼續執行。

---

## 2. 專案背景與技術棧

- **專案名稱**：OdfKit
- **程式語言**：C# / .NET
- **目標架構**：核心與擴充套件採多目標框架；實際 TFM、條件式相依套件與版本一律以各 `.csproj` 為準。修改跨 TFM 共用程式碼時，必須驗證所有受影響目標。
- **相依套件**：核心與格式擴充套件的相依範圍以各 `.csproj` 為準，不得因文件中的歷史清單推斷目前相依關係。
- **授權協議**：**CC0-1.0 Universal** (專案原創程式碼屬公有領域；第三方套件維持其原 MIT 授權)。

---

## 3. 核心架構與編碼規則
在修改或擴充此程式庫時，所有 Agent 必須嚴格遵守以下設計約束：

### A. 程式碼風格與完整性
- **語言版本**：採用目標 SDK 支援之最新 C# 語法（C# 10 至 C# 12+）。
- **命名空間宣告**：手寫 C# 檔案一律採用檔案範圍命名空間（File-scoped Namespace）宣告（例如 `namespace OdfKit.Core;`）。
- **新式語法特性**：在適當場合優先使用語法糖，包括集合運算式 `[...]`、主要建構函式（Primary Constructors）、目標類型 `new()` 運算式、`is not null` 模式比對等。
- **註解與文件**：
  - 手寫之公開（Public）與受保護（Protected） API 必須具備完整的**雙語**（英文＋正體中文）XML 說明文件，不得隨意使用 `#pragma warning disable 1591` 壓制。此規則僅適用於公開／受保護成員的 XML 文件；private／internal 成員與一般程式碼內行內註解維持下方純正體中文規則，不需雙語化。
  - 雙語格式慣例：
    - `<summary>`／`<remarks>` 等多行區塊：英文摘要獨立一行在前（精簡、符合 .NET API 文件慣例的描述句，非逐字翻譯，句尾加英文句號），正體中文摘要獨立一行在後。
    - **禁止**一行式 `<summary>`（例如 `/// <summary>…</summary>` 寫在同一行）。`<summary>` 與 `</summary>` 必須各自獨佔一行，內容置於兩者之間的獨立 `///` 行；internal／private 成員的純中文 XML 摘要亦同，不得使用一行式。
    - `<param>`／`<returns>`／`<exception>`／`<typeparam>` 等單行區塊：同一行內，英文說明在前、以` / `分隔、正體中文說明在後（例如 `/// <param name="path">The file path to load. / 要載入的檔案路徑。</param>`）。
    - 既有僅含正體中文之公開 API XML 文件，於下次修改該成員時補上英文摘要即可，不要求一次性全面回填；大規模回填依個別遷移計畫批次處理。
  - 所有 XML 文件與程式碼說明中的正體中文部分，一律翻譯且使用正體中文臺灣地區用語，僅在必要時可保留英文專用術語或原文；新增之英文摘要句不受下方「盤古之白」排版規範約束（該規範僅適用中文段落本身與其鄰接的半形字元）。
  - 必須嚴格遵守「盤古之白」排版規範，在中文字元與半形英文單字、數字、符號之間主動加上一個半形空格。
  - 必須小心檢查註解中標點符號的使用，不可遺留任何不需要或重複的標點符號（如重複的句點、不對稱括號、結尾贅餘的標點符號等），文字應保持精簡俐落；英文句子使用半形句號，正體中文句子使用全形句號，不可混用。
- **可空性 (Nullability)**：專案已啟用可空類型標記 (`<Nullable>enable</Nullable>`)，請撰寫 Null 安全的程式碼。
- **例外處理與在地化 (i18n)**：
  - 針對 ZIP 串流解析、XML 讀寫等底層操作，務必進行防禦性異常攔截與資源釋放。
  - 所有拋出的例外訊息一律禁止 Hard-coded 中文或英文。必須統一透過 `OdfLocalizer.GetMessage` 取得在地化錯誤訊息。
  - 當新增錯誤訊息時，其鍵值（Key）命名格式應遵循 `Err_[類別名稱]_[錯誤簡稱]`（以英文駝峰命名，簡述錯誤原因，例如 `Err_ChartDocument_NotHighOrderChart`），以提高人類可讀性與維護性。
  - 所有錯誤訊息鍵值必須加入 `OdfKit/Compliance/i18n/exceptions.<culture>.json`；各語系鍵值集合必須與 `en` 完全對等。`OdfLocalizer.Exceptions.<culture>.cs` 是由工具產生的輸出，不得手動修改。
- **程式碼排版與格式化**：在提交任何變更前，必須執行安全格式化腳本 `eng/Format-Safe.ps1`，確保其完全符合 `.editorconfig` 規範。
  - **禁止**在方案根目錄直接執行 `dotnet format`（無專案範圍）：`OdfKit.Tests` 為雙 TFM（`net10.0` + `net8.0`），全方案格式化會觸發 IDE multi-target 合併失敗，將 `<<<<<<< TODO: 取消合併專案 …` 標記寫入 `.cs` 並導致 **CS8300**。
  - 格式化後必須通過 `eng/Test-MergeConflictMarkers.ps1`（已內建於 `Format-Safe.ps1`）。
  - 若僅修改函式庫，使用 `pwsh eng/Format-Safe.ps1`；需連同測試檔排版時，加上 `-IncludeTests`（測試專案僅執行 `whitespace`，不套用 analyzer 程式碼修正）。
- **測試與 xUnit analyzer 規範**：
  - 測試中呼叫任何可接受 `CancellationToken` 的非同步 API 時，必須傳入 `TestContext.Current.CancellationToken`，包含 `Task.Delay`、`ReadToEndAsync`、`WaitAsync`、`IAsyncEnumerable` 工廠方法與專案自訂 async API；只有刻意驗證預取消或自訂取消語意時，才可使用測試內建立的 linked token。
  - 不得用 `Assert.NotEmpty(query.Where(...))`、`Assert.Empty(query.Where(...))`、`Assert.True(query.Any(...))` 或等價 LINQ 形狀檢查集合是否存在符合條件的項目；應改用 `Assert.Contains(collection, predicate)` 或 `Assert.DoesNotContain(collection, predicate)`，避免觸發 xUnit analyzer 警告並讓失敗訊息更精準。
  - 新增或修改測試後，至少執行對應 TFM 的 `dotnet build OdfKit.Tests/OdfKit.Tests.csproj -c Release --framework net10.0 --no-restore`，必要時再補 `net8.0`，確認 xUnit analyzer 無警告。

### B. ODF 與 XML 協定規格
- **命名空間處理**：一律使用與前綴無關的 `NamespaceURI` + `LocalName` 作為 XML 節點與屬性的比對基準。
- **ZIP 檔案路徑**：ZIP 封裝容器內的所有 Entry 路徑分隔符號必須統一使用正斜線 (`/`)，以符合 ODF 標準規範。
- **日期時間格式**：
  - UTC 日期格式：`"yyyy-MM-ddTHH:mm:ssZ"`
  - 本地日期格式：`"yyyy-MM-ddTHH:mm:ss"`
  - 必須安全處理 `DateTime.MinValue` 與 `DateTime.MaxValue` 的邊界值，防止時區轉換位移導致程式崩潰。

### C. 效能與記憶體安全
- **高效流式寫入**：`OdsStreamWriter`／`OdtStreamWriter` 必須採用串流／低常駐設計，避免將整份文件 DOM 常駐記憶體；熱路徑共用 `OdfRawXmlWriter`／`OdfXmlCharacterGuard`。公開效能敘事以峰值工作集與可重現基準為準（見 `docs/performance-comparison.md`、`docs/performance-baselines.md`），不得再使用未加限定的「小於 1MB」口號。善用 `CommunityToolkit.HighPerformance` 或 `Span<T>` / `ReadOnlySpan<T>` 等低配置 API，並在熱路徑維持輸出正確性與 XML 字元合法性。
- **XXE 與 DoS 防禦**：顯式設定 `XmlReaderSettings`，禁用外部 DTD 解析與 XML 實體展開，以杜絕 XXE 安全漏洞。
- **Zip Slip 漏洞防禦**：對 ZIP 解壓的目標路徑進行嚴格的合法性檢查，防止目錄穿越攻擊。

### C2. 可維護性（複雜度債）
完整準則見 [`docs/maintainability.md`](docs/maintainability.md)。Agent 必須遵守：
- **Partial**：禁止機械切檔；診斷用 `eng/Analyze-PartialSplits.ps1`；禁止重跑 `eng/historical-refactor/Split-*` 等一次性腳本。
- **在地化**：以 `pwsh eng/Add-LocalizerKey.ps1` 或手動編輯來源 JSON，再執行 `pwsh eng/Generate-LocalizerExceptionsFromJson.ps1`；提交前執行 `Test-LocalizerKeyParity.ps1 -FailOnIssues` 與 `Generate-LocalizerExceptionsFromJson.ps1 -VerifyOnly`。非 en／zh-TW 不得長期只留英文佔位。
- **公開 API 表面**（`Microsoft.CodeAnalysis.PublicApiAnalyzers`）：
  - 雙 TFM 基線：`OdfKit/PublicAPI/$(TargetFramework)/PublicAPI.{Shipped,Unshipped}.txt`。
  - **0.x** 全量在 **Unshipped**；**1.0** 再移入 Shipped。
  - 變更後更新基線或 `Generate-PublicApiBaseline.ps1 -Verify`。
  - RS0016／RS0017 為 error；RS0026／RS0027 手寫路徑為 error（生成 DOM／schema 目錄覆寫為 none）。恰好一個可選參數應改明確多載鏈（`eng/Expand-OptionalParameters.py`）；多可選參數可留在最長單一方法。政策見 [`docs/public-api-optional-parameters.md`](docs/public-api-optional-parameters.md)。
  - 說明見 [`OdfKit/PublicAPI/README.md`](OdfKit/PublicAPI/README.md)。
- **套件雙 TFM 相容性**（Package Validation）：`EnablePackageValidation`；`Test-NuGetPack.ps1` 於 pack 時檢查。
- **協作者／大型結構（人機平衡，非為拆而拆）**：見 [`docs/human-agent-maintainability.md`](docs/human-agent-maintainability.md) 與 [`docs/architecture-collaborators.md`](docs/architecture-collaborators.md)。拆分只為清楚領域邊界以利人類審閱與 Agent 限域修改；**禁止**因行數、token 或機械 KPI 而切檔；禁止重跑 `historical-refactor/Split-*`。
- **XML 摘要**：`Test-OneLineXmlSummary.ps1 -FailOnIssues`。
- **產生碼**：`DOM/Generated` 與 schema provider `.g.cs` 不可手改；改 ctor／多載形狀須改產生器後重產；schema 重產後須重跑 Public API 基線。
- **Schema 與流通性（非目標）**：核心預設內建 ODF **1.1～1.4** 官方 schema 覆蓋，以支援存量檔、封存與跨 LO／舊版互通；**禁止**為瘦身 nupkg 或「看起來模組化」而將 schema 拆成可選套件或刪減多版覆蓋。體積代價以建置／分析器策略與文件說明吸收；詳見 [`docs/maintainability.md`](docs/maintainability.md)「Schema 與流通性」。

### D. Git 提交規範 (Conventional Commits)
- **規範標準**：嚴格遵循「慣例式提交 (Conventional Commits) v1.0.0」規範。
- **結構要求**：禁止單行式提交。必須包含「主旨 (Subject)」與「內文 (Body)」，必要時加「腳註 (Footer)」。
  - 主旨：限制在 50 字元內，描述變更類型（如 `feat`, `fix`, `docs`, `refactor`）與簡要描述，結尾不加句點。
  - 內文：每行限 72 字元內，說明變更原因與細節，排版須緊湊，避免過多空白換行。
- **語言限制**：一律使用正體中文臺灣地區用語撰寫提交訊息，僅在必要時使用英文或原文。
- **GPG 簽署要求**：所有 Git 提交均必須進行 GPG 簽署（即啟用 `commit.gpgsign` 或使用 `-S` 參數）。在非互動式背景環境中執行時，請確保簽署金鑰的密碼已妥善快取於 `gpg-agent`，以避免簽署程序卡死。

---

## 4. 驗證矩陣與常用指令

依實際修改範圍執行最小但充分的驗證；不得以完整測試成本較高為由略過可執行的針對性驗證。

| 修改類型 | 最低驗證 |
| --- | --- |
| 僅函式庫程式碼 | `pwsh eng/Format-Safe.ps1`，並建置或測試受影響專案與 TFM |
| 包含測試程式碼 | `pwsh eng/Format-Safe.ps1 -IncludeTests`，並至少建置對應的測試 TFM |
| 在地化 | `Test-LocalizerKeyParity.ps1 -FailOnIssues` 與 `Generate-LocalizerExceptionsFromJson.ps1 -VerifyOnly` |
| 公開 API 或 schema | `Generate-PublicApiBaseline.ps1 -Verify` |
| XML 文件 | `Test-OneLineXmlSummary.ps1 -FailOnIssues` |
| 封裝或跨套件 TFM 相容性 | `Test-NuGetPack.ps1` |
| 效能文件數值 | `Benchmark-Competitive.ps1` |

- **建置專案**：
  ```powershell
  dotnet build
  ```
- **執行單元與整合測試**：
  ```powershell
  dotnet test
  ```
- **安全格式化程式碼**（提交前必用）：
  ```powershell
  pwsh eng/Format-Safe.ps1
  ```
- **含測試專案排版**（僅 whitespace）：
  ```powershell
  pwsh eng/Format-Safe.ps1 -IncludeTests
  ```
- **檢查合併衝突標記**：
  ```powershell
  pwsh eng/Test-MergeConflictMarkers.ps1
  ```
- **稽核提交簽署金鑰**：
  ```powershell
  pwsh eng/Test-GpgSignatures.ps1
  ```
- **新增在地化鍵**（寫入 JSON 並重產 C#）：
  ```powershell
  pwsh eng/Add-LocalizerKey.ps1 -Key Err_Example_Failed -EnMessage "Failed: {0}." -ZhTwMessage "失敗：{0}。"
  ```
- **自 JSON 重產／驗證例外字典**：
  ```powershell
  pwsh eng/Generate-LocalizerExceptionsFromJson.ps1
  pwsh eng/Generate-LocalizerExceptionsFromJson.ps1 -VerifyOnly
  pwsh eng/Test-LocalizerKeyParity.ps1 -FailOnIssues
  ```
- **一行式 summary 閘門**：
  ```powershell
  pwsh eng/Test-OneLineXmlSummary.ps1 -FailOnIssues
  ```
- **重產公開 API 基線**（大量表面變更或 schema 重產後）：
  ```powershell
  pwsh eng/Generate-PublicApiBaseline.ps1 -Verify
  ```
- **跨套件效能對比（更新 docs 數字前）**：
  ```powershell
  pwsh eng/Benchmark-Competitive.ps1
  ```
- **NuGet 封裝與雙 TFM 相容性**（含 Package Validation）：
  ```powershell
  pwsh eng/Test-NuGetPack.ps1
  ```
- **測試套件分層與整理準則**：以 `TestCategories` trait、CI workflow 與對應測試檔為準。
- **臨時計畫檔邊界**：任何 `*plan*.md` 或名稱含「計畫」的整理檔都只能作為短期工作暫存，不得被 `AGENTS.md`、`CLAUDE.md`、`.github/copilot-instructions.md` 或其它 Agent 規範引用為長期規則來源；完成後應移除。

---

## 5. 跨 Agent 與模型相容性

- 本文件只定義可觀察的工作成果、授權邊界、專案不變量與驗證要求，不要求特定模型揭露內部推理，也不依賴特定 Agent 的專有工具名稱或提示語法。
- Codex、Claude Code、GitHub Copilot 及其它 Agent 若支援專案規範檔，應直接讀取本文件。若工具需要專屬入口檔，必須採用**墊片指向 (Shim)** 連結至本 [`AGENTS.md`](AGENTS.md)，不得複製規範全文。
- 模型能力不足以可靠完成任務時，應縮小工作範圍、提高驗證強度或交由能力較高的模型處理；不得降低安全、正確性或驗證標準。
- 新增規則前，先確認是否會改變 Agent 行為；不重複模型已可靠執行的一般流程，也不加入只適用單一模型的性格、冗長度或推理指示。
