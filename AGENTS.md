# OdfKit 專案 Agent 開發規範 (AGENTS.md)

本檔案為所有參與此專案之 AI 開發 Agent（如 Codex、Claude Code、GitHub Copilot、Antigravity、Grok Build 等）的**單一事實來源 (Single Source of Truth)**。

---

## 1. 專案背景與技術棧
- **專案名稱**：OdfKit
- **程式語言**：C# / .NET
- **目標架構**：核心 `OdfKit` 及全部 `OdfKit.Extensions.*` 擴充套件為 `net10.0` 與 `netstandard2.0`（雙平台編譯）；`OdfKit.Tests` 與 `tools/OdfKit.Cli` 為 `net10.0` 與 `net8.0`（雙 TFM，非 netstandard2.0）；`tools/OdfSchemaGenerator` 與 `OdfKit.Tests/MockSoffice` 僅為 `net8.0`；`OdfKit.Benchmarks`、`tools/OdfCorpusGenerator`、`tools/OdfKit.TrimSmoke` 僅為 `net10.0`。詳細版本請以各專案之 `.csproj` 為準。
- **核心第三方相依套件**（詳細版本參見專案檔，如 `OdfKit.csproj`；`PDFsharp` 等格式擴充套件相依僅存在於對應的 `OdfKit.Extensions.*` 專案，不屬於核心套件）：
  - `BouncyCastle.Cryptography` (採用 MIT 授權)
  - `CommunityToolkit.HighPerformance` (採用 MIT 授權)
  - `CSharpMath` (採用 MIT 授權)
  - `System.Security.Cryptography.Xml` (採用 MIT 授權)
  - `System.Security.Cryptography.Pkcs` (採用 MIT 授權)
  - `Sylvan.Data.Csv` (採用 MIT 授權)
  - 另有依 TFM 條件引入之 BCL 套件（如 `net10.0` 限定的 `System.Numerics.Tensors`、`System.IO.Hashing`，與 `netstandard2.0` 之 polyfill 套件），詳見 `OdfKit.csproj`。
- **授權協議**：**CC0-1.0 Universal** (專案原創程式碼屬公有領域；第三方套件維持其原 MIT 授權)。

---

## 2. 核心架構與編碼規則
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
  - 所有錯誤訊息鍵值必須在 `OdfLocalizer.Exceptions.<culture>.cs`（12 語系表）註冊，並由 `OdfLocalizer.Exceptions.cs` 入口彙整；鍵值集合必須與 `en` 完全對等（見下方 i18n 閘門）。
  - 翻譯與 XML 註解文字一律使用正體中文臺灣地區用語，並遵守「盤古之白」排版規範（如中文字元與半形英文/數字/符號之間主動加半形空格），且小心檢查句尾標點符號不贅餘。
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
- **在地化**：新增 `Err_*`／`Warn_*`／`Cli_*` 等鍵時，必須同步 `OdfLocalizer.Exceptions.<culture>.cs` 全部 12 語系；可用 `pwsh eng/Add-LocalizerKey.ps1` 一次腳手架，提交前執行 `pwsh eng/Test-LocalizerKeyParity.ps1 -FailOnIssues`。非 en／zh-TW 語系不得長期只留英文佔位（新增後應潤飾）。
- **公開 API 表面**（業界黃金標準，`Microsoft.CodeAnalysis.PublicApiAnalyzers`）：
  - 核心套件以雙 TFM 基線追蹤：`OdfKit/PublicAPI/$(TargetFramework)/PublicAPI.{Shipped,Unshipped}.txt`。
  - **0.x** 期間新增／變更的公開 API 登錄於 **Unshipped**；**1.0** 發佈時再整批移入 Shipped。
  - 變更公開表面後必須更新對應 TFM 的基線，或執行 `pwsh eng/Generate-PublicApiBaseline.ps1 -Verify`。
  - RS0016／RS0017 為 error；RS0026／RS0027（可選參數多載設計）為 suggestion（既有表面 grandfather，新 API 仍應優先採單一最長可選參數多載）。
  - 說明見 [`OdfKit/PublicAPI/README.md`](OdfKit/PublicAPI/README.md)。
- **套件雙 TFM 相容性**（.NET Package Validation／ApiCompat）：可發佈套件已啟用 `EnablePackageValidation`；`dotnet pack`／`eng/Test-NuGetPack.ps1` 會檢查各 TFM 公開表面前向相容。若故意讓某 TFM 多出 API，須以官方 suppress 檔或條件編譯明確記錄理由，不得靜默關閉驗證。
- **XML 摘要**：提交前可執行 `pwsh eng/Test-OneLineXmlSummary.ps1 -FailOnIssues` 防止一行式 `<summary>` 回流。
- **產生碼**：`OdfKit/DOM/Generated` 與 schema provider `.g.cs` 不可手改；schema 重產後若公開表面變動，須重跑 Public API 基線腳本。

### D. Git 提交規範 (Conventional Commits)
- **規範標準**：嚴格遵循「慣例式提交 (Conventional Commits) v1.0.0」規範。
- **結構要求**：禁止單行式提交。必須包含「主旨 (Subject)」與「內文 (Body)」，必要時加「腳註 (Footer)」。
  - 主旨：限制在 50 字元內，描述變更類型（如 `feat`, `fix`, `docs`, `refactor`）與簡要描述，結尾不加句點。
  - 內文：每行限 72 字元內，說明變更原因與細節，排版須緊湊，避免過多空白換行。
- **語言限制**：一律使用正體中文臺灣地區用語撰寫提交訊息，僅在必要時使用英文或原文。
- **GPG 簽署要求**：所有 Git 提交均必須進行 GPG 簽署（即啟用 `commit.gpgsign` 或使用 `-S` 參數）。在非互動式背景環境中執行時，請確保簽署金鑰的密碼已妥善快取於 `gpg-agent`，以避免簽署程序卡死。

---

## 3. 開發常用指令
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
- **新增在地化鍵腳手架**（一次寫入 12 語系，再潤飾非 en／zh-TW）：
  ```powershell
  pwsh eng/Add-LocalizerKey.ps1 -Key Err_Example_Failed -EnMessage "Failed: {0}." -ZhTwMessage "失敗：{0}。"
  ```
- **語系鍵值對等**（新增 Err／Warn／Cli 鍵後必跑）：
  ```powershell
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
- **NuGet 封裝與雙 TFM 相容性**（含 Package Validation）：
  ```powershell
  pwsh eng/Test-NuGetPack.ps1
  ```
- **測試套件分層與整理準則**：以 `TestCategories` trait、CI workflow 與對應測試檔為準。
- **臨時計畫檔邊界**：任何 `*plan*.md` 或名稱含「計畫」的整理檔都只能作為短期工作暫存，不得被 `AGENTS.md`、`CLAUDE.md`、`.github/copilot-instructions.md` 或其它 Agent 規範引用為長期規則來源；完成後應移除。

---

## 4. 規範擴充與維護
若要針對特定工具（如 Claude Code 的 `CLAUDE.md` 或 GitHub Copilot 的 `.github/copilot-instructions.md`）配置專屬規則，必須採用**墊片指向 (Shim)** 的方式，直接連結至本 [`AGENTS.md`](AGENTS.md) 檔案，嚴禁複製與同步重複的文本內容。
