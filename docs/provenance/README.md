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

以下檔案由 `tools/OdfSchemaGenerator` 與 `eng/Generate-OdfSchemaProvider.ps1` 從 OASIS RNG 產生，**不可手動編輯**：

- `OdfKit/DOM/Generated/*.g.cs`：typed DOM wrapper、factory case、typed attribute 與 schema child collection。
- `OdfKit/Compliance/Generated/Odf*OfficialSchemaProvider.g.cs`：ODF 1.1/1.2/1.3/1.4 官方 schema metadata provider。

重產流程：

```powershell
dotnet run --project tools/OdfSchemaGenerator --framework net10.0
pwsh eng/Test-OdfTypedDomCoverage.ps1
```

重產後必須確認 `eng/Test-OdfTypedDomCoverage.ps1` 的 coverage guard、`OdfSchemaGeneratorTests`
與 `TypedDomParityTests` 仍通過。若產生器導致 wrapper 或 schema metadata 大幅變動，
提交訊息需說明對應的 OASIS schema 來源與差異原因。

## 同步維護的大型資源表

`OdfKit/Compliance/OdfLocalizer.Exceptions.cs` 是類產生式成品：它不是由單一
工具每次完整重產，但必須作為同步資源表維護。新增或修改錯誤訊息時，需同時更新所有支援語言
（`en`, `zh-TW`, `de`, `fr`, `nl`, `nb`, `pt`, `it`, `sk`, `da`, `ms`, `ko`），並保留
`OdfLocalizer` 的文化回退測試。不得只修改單一文化或在呼叫端硬編碼例外訊息。

`OdfKit/Compliance/OdfLocalizer.ComplianceSuggestions.cs` 維護非例外的合規建議補充
翻譯；新增內建 compliance rule 或 suggested-fix key 時，也必須讓 12 個支援語言
合併後的鍵值集合與英文一致。

`OdfKit/Compliance/OdfLocalizer.ExtensionDiagnostics.cs` 維護 Extensions 套件的診斷
訊息翻譯。新增 `OdfKitDiagnostics.Warn`、`Info` 或 `Error` 訊息時，呼叫端不得傳入硬編碼
文字，必須透過 `OdfLocalizer.GetMessage` 取得 12 語系同步資源。

## Clean-room 來源索引

公式評估、schema pattern validator 與 OpenPGP 加密的規格來源、可接受參考、不可接受來源與
golden / regression 測試契約集中記錄於 [Clean-room 來源索引](clean-room-source-index.md)。
這些區塊邏輯密集且與外部試算表引擎、驗證器或密碼學實作語意相近，後續變更必須以規格、
自有 corpus 與可再散布 fixture 驅動，不得複製外部原始碼。

| 模組 | 檔案 | 說明 |
|------|------|------|
| 公式評估 | `DefaultFormulaEvaluator.*.cs` | 財務與統計函式語意 |
| 結構驗證 | `OdfSchemaPatternValidator.cs` | 複雜 pattern 比對邏輯 |
| OpenPGP 加密 | `OdfBouncyCastleOpenPgpProvider*.cs` | PKESK 封包解析、ECDH／RFC 6637 KDF |

## 已確認無直接抄襲

截至 2026-06 稽核：

- 未發現 NPOI、Apache POI 或 Java 原始碼嵌入
- 未發現 LibreOffice 原始碼複製
- DOM 包裝器由 OASIS schema generator 產生，授權合規

## 維護建議

1. 新增第三方參考實作時，記錄於本目錄並標註授權
2. 財務函式與 schema pattern 變更需附規格來源、來源範圍與對照測試
3. 勿將產生式 `.g.cs` 納入 partial 拆分範圍
