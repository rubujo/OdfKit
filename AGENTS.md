# OdfKit 專案 Agent 開發規範 (AGENTS.md)

本檔案為所有參與此專案之 AI 開發 Agent（如 Codex、Claude Code、GitHub Copilot、Antigravity、Grok Build 等）的**單一事實來源 (Single Source of Truth)**。

---

## 1. 專案背景與技術棧
- **專案名稱**：OdfKit
- **程式語言**：C# / .NET
- **目標架構**：`net10.0` 與 `netstandard2.0`（雙平台編譯）
- **核心第三方相依套件**（詳細版本參見專案檔，如 `OdfKit.csproj`；`PDFsharp` 等格式擴充套件相依僅存在於對應的 `OdfKit.Extensions.*` 專案，不屬於核心套件）：
  - `BouncyCastle.Cryptography` (採用 MIT 授權)
  - `CommunityToolkit.HighPerformance` (採用 MIT 授權)
  - `CSharpMath` (採用 MIT 授權)
  - `System.Security.Cryptography.Xml` (採用 MIT 授權)
  - `System.Security.Cryptography.Pkcs` (採用 MIT 授權)
  - `Sylvan.Data.Csv` (採用 MIT 授權)
- **授權協議**：**CC0-1.0 Universal** (專案原創程式碼屬公有領域；第三方套件維持其原 MIT 授權)。

---

## 2. 核心架構與編碼規則
在修改或擴充此程式庫時，所有 Agent 必須嚴格遵守以下設計約束：

### A. 程式碼風格與完整性
- **語言版本**：採用目標 SDK 支援之最新 C# 語法（C# 10 至 C# 12+）。
- **命名空間宣告**：手寫 C# 檔案一律採用檔案範圍命名空間（File-scoped Namespace）宣告（例如 `namespace OdfKit.Core;`）。
- **新式語法特性**：在適當場合優先使用語法糖，包括集合運算式 `[...]`、主要建構函式（Primary Constructors）、目標類型 `new()` 運算式、`is not null` 模式比對等。
- **註解與文件**：
  - 手寫之公開（Public）與受保護（Protected） API 必須具備完整的 XML 說明文件，不得隨意使用 `#pragma warning disable 1591` 壓制。
  - 所有 XML 註解與程式碼說明（包括既有之英文或其他語言的註解與 XML 註解）一律必須翻譯且使用正體中文臺灣地區用語，僅在必要時可保留英文專用術語或原文。
  - 必須嚴格遵守「盤古之白」排版規範，在中文字元與半形英文單字、數字、符號之間主動加上一個半形空格。
  - 必須小心檢查註解中標點符號的使用，不可遺留任何不需要或重複的標點符號（如重複的句點、不對稱括號、結尾贅餘的標點符號等），文字應保持精簡俐落。
- **可空性 (Nullability)**：專案已啟用可空類型標記 (`<Nullable>enable</Nullable>`)，請撰寫 Null 安全的程式碼。
- **例外處理與在地化 (i18n)**：
  - 針對 ZIP 串流解析、XML 讀寫等底層操作，務必進行防禦性異常攔截與資源釋放。
  - 所有拋出的例外訊息一律禁止 Hard-coded 中文或英文。必須統一透過 `OdfLocalizer.GetMessage` 取得在地化錯誤訊息。
  - 當新增錯誤訊息時，其鍵值（Key）命名格式應遵循 `Err_[類別名稱]_[錯誤簡稱]`（以英文駝峰命名，簡述錯誤原因，例如 `Err_ChartDocument_NotHighOrderChart`），以提高人類可讀性與維護性。
  - 所有錯誤訊息鍵值必須在 `OdfLocalizer.Exceptions.cs` 中註冊，並提供支援的所有語言（`en`, `zh-TW`, `de`, `fr`, `nl`, `nb`, `pt`, `it`, `sk`, `da`, `ms`, `ko`）之翻譯對照。
  - 翻譯與 XML 註解文字一律使用正體中文臺灣地區用語，並遵守「盤古之白」排版規範（如中文字元與半形英文/數字/符號之間主動加半形空格），且小心檢查句尾標點符號不贅餘。
- **程式碼排版與格式化**：在提交任何變更前，必須執行安全格式化腳本 `eng/Format-Safe.ps1`，確保其完全符合 `.editorconfig` 規範。
  - **禁止**在方案根目錄直接執行 `dotnet format`（無專案範圍）：`OdfKit.Tests` 為雙 TFM（`net10.0` + `net8.0`），全方案格式化會觸發 IDE multi-target 合併失敗，將 `<<<<<<< TODO: 取消合併專案 …` 標記寫入 `.cs` 並導致 **CS8300**。
  - 格式化後必須通過 `eng/Test-MergeConflictMarkers.ps1`（已內建於 `Format-Safe.ps1`）。
  - 若僅修改函式庫，使用 `pwsh eng/Format-Safe.ps1`；需連同測試檔排版時，加上 `-IncludeTests`（測試專案僅執行 `whitespace`，不套用 analyzer 程式碼修正）。

### B. ODF 與 XML 協定規格
- **命名空間處理**：一律使用與前綴無關的 `NamespaceURI` + `LocalName` 作為 XML 節點與屬性的比對基準。
- **ZIP 檔案路徑**：ZIP 封裝容器內的所有 Entry 路徑分隔符號必須統一使用正斜線 (`/`)，以符合 ODF 標準規範。
- **日期時間格式**：
  - UTC 日期格式：`"yyyy-MM-ddTHH:mm:ssZ"`
  - 本地日期格式：`"yyyy-MM-ddTHH:mm:ss"`
  - 必須安全處理 `DateTime.MinValue` 與 `DateTime.MaxValue` 的邊界值，防止時區轉換位移導致程式崩潰。

### C. 效能與記憶體安全
- **高效流式寫入**：`OdsStreamWriter` 必須採用超低記憶體設計，確保在導出大數據時，記憶體佔用小於 1MB。善用 `CommunityToolkit.HighPerformance` 或 `Span<T>` / `ReadOnlySpan<T>` 等無分配 API。
- **XXE 與 DoS 防禦**：顯式設定 `XmlReaderSettings`，禁用外部 DTD 解析與 XML 實體展開，以杜絕 XXE 安全漏洞。
- **Zip Slip 漏洞防禦**：對 ZIP 解壓的目標路徑進行嚴格的合法性檢查，防止目錄穿越攻擊。

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
- **測試套件分層與整理準則**：見 [`docs/testing-strategy.md`](docs/testing-strategy.md)。

---

## 4. 規範擴充與維護
若要針對特定工具（如 Claude Code 的 `CLAUDE.md` 或 GitHub Copilot 的 `.github/copilot-instructions.md`）配置專屬規則，必須採用**墊片指向 (Shim)** 的方式，直接連結至本 [`AGENTS.md`](file:///d:/Dev/Project/Application/OdfKit/AGENTS.md) 檔案，嚴禁複製與同步重複的文本內容。
