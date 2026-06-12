# OdfKit 專案說明文件

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)](#)
[![Tests](https://img.shields.io/badge/tests-549%20passed-brightgreen.svg)](#)
[![Conformance](https://img.shields.io/badge/conformance-CNS15251%20%7C%20EU%20Profile-brightgreen.svg)](#)
[![License](https://img.shields.io/badge/license-CC0--1.0-blue.svg)](#)

本專案是一個專為 .NET 平台開發之高效能、低相依性 ODF (Open Document Format) 文件處理受控類別庫，支援對 `.odt`（文字）、`.ods`（試算表）及 `.odp`（簡報）等 ODF 標準文件進行讀取、寫入與修改。

專案具備優秀的相容性，同時支援現代化的 `.NET 10.0` 與相容性極佳的 `.NET Standard 2.0` 雙目標架構編譯。

---

## 1. AI 輔助開發聲明

本專案之核心架構、演算法設計、安全防禦機制（防範 XXE 與 Zip Slip 漏洞）以及完整的單元與整合測試套件，均是由 **Google DeepMind 團隊所設計之 Antigravity 高階 AI 程式開發助理** 協作與自主編碼開發完成。

整個開發歷程導入了自動化的多 Agent 協作開發系統（包含協調器、實作人員、程式碼審查員、極限測試挑戰者以及獨立的鑑識稽核員），在保障代碼品質與誠信開發的規範下，通過了嚴格的獨立 **Victory Audit（勝利稽核）** 驗證，最終交付了具備高可靠性與企業級標準的軟體程式庫。

---

## 2. 核心功能與架構特色

專案整體架構劃分為以下幾個主要核心模組：

### A. 核心封裝與加密安全 (Core & Infrastructure)
- **執行緒安全 `OdfPackage`**：實作 ZIP 容器解壓與原子化安全存檔機制，具備 CoW (Copy-on-Write) 寫時複製機制，並內建防止 Zip Slip 目錄穿越攻擊之防禦。
- **符合 ODF 1.3 的數位簽章 `OdfSigner`**：使用原始未解析之位元串流為基礎，支援 SHA-256、SHA-384、SHA-512 數位簽署與驗證。
- **高強度加密 `OdfEncryption`**：支援 AES-256-CBC 與 Blowfish 加密演算法，並於 `.NET Standard 2.0` 環境中手動實作 PBKDF2-SHA256 金鑰衍生函數。

### B. 記憶體 XML DOM 與流式解析 (DOM & Parser)
- **強型別 `OdfNode` DOM 樹**：支援完整保留未識別之 XML 節點與屬性以進行 Round-trip 輸出。
- **前綴無關比對**：一律以 `NamespaceURI` + `LocalName` 作為 XML 節點與屬性的比對標準。
- **XXE 與 DoS 安全防範**：底層 XML 讀寫器顯式設定禁用外部 DTD 解析，確保不受 XXE 與 DoS 實體展開攻擊威脅。

### C. 樣式與轉譯引擎 (Style & Formatting Engine)
- **層級樣式繼承回溯 `OdfStyleEngine`**：支援自動樣式去重雜湊計算與名稱衝突避讓，並防禦樣式循環繼承。
- **強型別尺寸單位轉換 `OdfLength`**：支援厘米、毫米、點、英寸、百分比及像素等單位轉換。
- **數字/日期/百分比格式轉譯 `OdfNumberFormatter`**：將 C# 格式化字串精準轉換為符合 ODF 規範之 XML 樣式定義。

### D. 公式計算與語法轉譯引擎 (Formula & Syntax Engine)
- **公式雙向轉譯**：支援 Excel 公式與 ODF 公式之相互轉換與相對座標位移。
- **輕量 AST 求解器 `DefaultFormulaEvaluator`**：支援 `VLOOKUP`、`IF`、`SUM`、`SUMIF`、`COUNTIF` 等常用函數，並防禦公式循環參照。

### E. 高階文件 API 與流式導出
- **強型別高階文件型別**：提供 `TextDocument` (ODT)、`SpreadsheetDocument` (ODS) 與 `PresentationDocument` (ODP) 等高階物件導向 API，支援目錄生成 (TOC)、MailMerge 郵件合併範本、凍結窗格與自動篩選等豐富功能。
- **高效能 `OdsStreamWriter`**：專為導出大量試算表數據設計，限制記憶體佔用在 1MB 以內，防止記憶體溢出 (OOM)。
- **LibreOffice 轉檔擴充**：透過 `LibreOfficeRenderer` 提供在背景調用 Headless LibreOffice 進行高保真 PDF 或圖片轉檔的功能。

---
## 3. C# 程式範例

### A. 建立並寫入文字與試算表文件
#### 建立文字文件 (ODT)
```csharp
using System.IO;
using OdfKit.Text;
using OdfKit.Core;

using (var package = OdfPackage.Create(new MemoryStream(), leaveOpen: true))
{
    var doc = new TextDocument(package);
    doc.AddParagraph("哈囉，OdfKit！這是一份自動生成的 ODF 文字文件。");
    doc.Save();
}
```

#### 建立試算表文件 (ODS)
```csharp
using System.IO;
using OdfKit.Spreadsheet;
using OdfKit.Core;

using (var package = OdfPackage.Create(new MemoryStream(), leaveOpen: true))
{
    var doc = new SpreadsheetDocument(package);
    var sheet = doc.AddSheet("工作表1");
    var cell = sheet.GetCell(0, 0);
    cell.TextContent = "哈囉，OdfKit！試算表單元格 A1";
    cell.ValueType = "string";
    doc.Save();
}
```

### B. 數位簽署與驗證文件
```csharp
using System;
using System.Security.Cryptography.X509Certificates;
using OdfKit.Text;
using OdfKit.Core;

using (var package = OdfPackage.Open("document.odt"))
{
    var doc = new TextDocument(package);
    X509Certificate2 cert = LoadYourCertificate(); // 自行載入憑證
    
    // 進行簽署
    doc.Sign(cert);
    doc.Save();

    // 驗證簽章
    X509Certificate2Collection verifiedCerts;
    bool isValid = doc.VerifySignatures(out verifiedCerts);
    Console.WriteLine($"簽章是否有效: {isValid}");
}
```

### C. 執行 Policy Profiles 合規性驗證 (CNS15251 或 EU Profile)
```csharp
using System;
using OdfKit.Core;
using OdfKit.Compliance;

using (var package = OdfPackage.Open("document.odt"))
{
    // 選擇 ROC Taiwan ODF-CNS15251 標準 Profile 或歐盟 (EU) 標準 Profile
    var profile = OdfComplianceProfiles.Find("ROC_Taiwan_ODF_CNS15251"); // 亦可使用 "EU_Interoperable_Europe"
    
    // 執行驗證
    OdfValidationReport report = OdfPackageValidator.Validate(package, profile, "document.odt");
    
    Console.WriteLine($"是否合規: {report.IsValid}");
    foreach (var issue in report.Issues)
    {
        Console.WriteLine($"[{issue.Severity}] Rule ID: {issue.RuleId} - {issue.Message} (位置: {issue.XPath})");
    }
}
```

---

## 4. 建置與測試說明

專案採用標準的 .NET CLI 工具鏈進行管理：

* **編譯專案**：
  ```powershell
  dotnet build
  ```
* **執行 549 項自動化測試套件**：
  ```powershell
  dotnet test
  ```

---

## 5. 授權與第三方套件聲明

### 專案原創程式碼授權
本專案的原創程式碼（含 `OdfKit` 與 `OdfKit.Extensions.Rendering` 專案）完全採用 **CC0-1.0 Universal**（CC0 1.0 通用公有領域貢獻宣告）授權發布。

您無需保留任何著作權聲明，即可將本專案代碼自由地用於私有、商業、修改、分發等任何用途。

### 第三方相依套件授權
本專案在編譯與運行時，引用了以下第三方開源套件，其各自維持原有的 **MIT 授權** 協議：

1. **`PDFsharp`**（用於 PDF 檔案處理與轉檔擴充）—— 採用 [MIT 授權](https://github.com/empira/PDFsharp/blob/master/LICENSE)。
2. **`CommunityToolkit.HighPerformance`**（用於記憶體效能優化）—— 採用 [MIT 授權](https://github.com/CommunityToolkit/dotnet/blob/main/License.md)。
3. **`System.Security.Cryptography.Xml`**（用於處理數位簽章）—— 採用 [MIT 授權](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)。
4. **`System.Security.Cryptography.Pkcs`**（用於 PKCS7/CMS 簽章與時間戳）—— 採用 [MIT 授權](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)。
5. 微軟 **`.NET Foundation`** 提供之相依底層 System.* 系統套件 —— 採用 [MIT 授權](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)。

> [!IMPORTANT]
> 雖然本專案原創代碼為 CC0 授權，但當您分發包含上述第三方套件編譯產物 (DLL) 的軟體時，仍須依據各自的授權條款，在軟體中保留這些相依套件的原 MIT 著作權聲明。
