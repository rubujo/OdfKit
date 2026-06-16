# OdfKit 程式碼來源稽核（Provenance）

本目錄記錄 OdfKit 各模組的來源、授權與重寫策略，供貢獻者與審查者查閱。

## 授權總覽

| 範圍 | 授權 |
|------|------|
| OdfKit 原創程式碼 | CC0-1.0 Universal |
| PDFsharp | MIT |
| CommunityToolkit.HighPerformance | MIT |
| System.Security.Cryptography.Xml | MIT |
| OASIS ODF RNG Schema | OASIS 標準文件 |

## 產生式程式碼（可安全追溯）

以下檔案由 `tools/OdfSchemaGenerator` 與 `eng/Generate-OdfSchemaProvider.ps1` 從 OASIS RNG 產生，**不應手動編輯**：

- `OdfKit/DOM/GeneratedDomWrappers.g.cs`
- `OdfKit/Compliance/Odf*OfficialSchemaProvider.g.cs`

## 建議 Clean Room 重寫區塊

以下區塊邏輯密集且與外部試算表引擎語意相近，建議以 **規格驅動（OpenFormula / ODF 1.3）** 方式逐步重寫並補強測試：

| 模組 | 檔案 | 說明 |
|------|------|------|
| 公式評估 | `DefaultFormulaEvaluator.*.cs` | 財務與統計函式語意 |
| 結構驗證 | `OdfSchemaPatternValidator.cs` | 複雜 pattern 比對邏輯 |

## 已確認無直接抄襲

截至 2026-06 稽核：

- 未發現 NPOI、Apache POI 或 Java 原始碼嵌入
- 未發現 LibreOffice 原始碼複製
- DOM 包裝器由 OASIS schema generator 產生，授權合規

## 維護建議

1. 新增第三方參考實作時，記錄於本目錄並標註授權
2. 財務函式變更需附 OpenFormula 規格連結與對照測試
3. 勿將產生式 `.g.cs` 納入 partial 拆分範圍