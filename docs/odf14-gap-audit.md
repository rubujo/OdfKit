# ODF 1.4 逐章稽核紀錄

本文件記錄針對 ODF 1.4（2025-12-03 正式核定為 OASIS Standard）四份正式規格文本，
逐章比對 OdfKit schema／驗證層／公式引擎是否有遺漏新增元素的稽核方法與結論。
稽核基準為 `docs.oasis-open.org/office/OpenDocument/v1.4/os/` 下四份官方文本：

- Part 1：Introduction
- Part 2：Packages
- Part 3：Schema
- Part 4：OpenFormula

## 稽核方法

每份規格文本皆附有非正式的「Appendix — Changes from Open Document Format
... v1.4」（或對應章節），列出該版本相對於前一版的異動摘要。本次稽核先讀取
各份文本的異動摘要章節，再針對異動項目與 OdfKit 現有程式碼逐一比對，
必要時直接下載官方 RelaxNG schema（`.rng`）做元素／屬性層級的機械化差異比對。

## Part 1（Introduction）

Part 1 為介紹與一致性聲明性質文件，其附錄僅為 Acknowledgments（貢獻者名單），
未附獨立的「異動摘要」章節。Part 1 本身不定義 schema 元素或屬性，因此不會是
schema／驗證層遺漏的來源。**結論：無需採取行動。**

## Part 2（Packages）

Part 2 附錄 D 明確聲明：

> No technical changes have been made to Part 2 of this specification for ODF v1.4.

即 manifest／digital signature／encryption 封裝層規格於 1.4 版**完全沒有技術異動**。
交叉核對 OdfKit 現有實作：

- `manifest:file-entry`、`encryption-data`、`algorithm`、`key-derivation`、
  `encrypted-key`、`start-key-generation` 已由 `OdfManifestLoader`／
  `OdfPackageManifestWriter` 處理。
- OpenPGP 型加密（`manifest:PGPData`／`PGPKeyID`／`PGPKeyPacket`）已由
  `OdfBouncyCastleOpenPgpProvider`／`OdfOpenPgpCryptographyProvider` 處理。
- 數位簽章（`ds:Signature`／`dsig:document-signatures`）已由
  `OdfSignatureSigner`／`OdfSignatureVerifier` 處理。

**結論：功能面無缺口。** 目前 manifest／dsig schema 未如主要內容 schema
（Part 3）納入 `OdfSchemaGenerator` 自動化產生管線，而是採手寫解析／寫入；
由於 Part 2 本次無任何規格異動，此非 ODF 1.4 造成的新缺口，暫列為後續可選的
架構強化項目（機械化 RNG 對照），不影響目前功能正確性或相容性。

## Part 3（Schema）

以官方 `OpenDocument-v1.4-schema.rng` 逐一擷取全部 `rng:element` 與
`rng:attribute` 定義名稱，與 OdfKit `tools/OdfSchemaGenerator` 產生的
schema metadata（`OdfKit/Compliance/Generated/Odf14OfficialSchemaProvider.g.cs`）
做機械化差異比對：

| 比對項目 | RNG 定義數 | OdfKit 產生數 | 差異 |
|---|---|---|---|
| 元素（Element） | 599 | 599 | **0（完全相符）** |
| 屬性（Attribute） | 1300（含 1 筆） | 1299 | 1 筆表面差異，經查證非真實缺口（見下） |

唯一的表面差異是 `office:process-content`：此屬性定義在官方 RNG 原始碼中
被整段 XML 註解（`<!-- removed from text as well ... -->`）包住，**規格文本本身
已停用此屬性**，並非 1.4 新增或現行有效定義。進一步核對 OdfKit 產生的
`Odf12OfficialSchemaProvider.g.cs` 確認此屬性確實以
`OdfVersionRange.Exact(OdfVersion.Odf12)` 正確註冊為僅 1.2 版有效——
即 OdfKit 對此屬性的版本範圍判定與規格沿革完全一致，不是遺漏。

**結論：Part 3 內容 schema 元素與屬性層級 100% 對齊官方 ODF 1.4 定義，
無遺漏。**

## Part 4（OpenFormula）

Part 4 附錄 A 列出以下異動：

- 說明文字調整：`YEAR`（§7.4，1583 年特例說明）。
- **新增函式**：`EASTERSUNDAY`（§6.10.8）。
- 定義變更（語意澄清，非新函式）：`CONVERT`（§6.16.18，Table 24 更新）、
  `COUNTA`、`INDEX`、`ISBLANK`、`ISFORMULA`、`ISLOGICAL`、`ISNONTEXT`、
  `ISNUMBER`、`ISREF`、`ISTEXT`、`NPER`、`PMT`。

逐一比對 `FormulaBuiltinFunctionRegistry` 與 `OdfFormulaSupport` 後，
發現以下真實缺口（已於本次稽核修復）：

| 函式 | 稽核前狀態 | 修復內容 |
|---|---|---|
| `EASTERSUNDAY` | 僅以 LibreOffice 供應商前綴 `ORG.OPENOFFICE.EASTERSUNDAY` 註冊；ODF 1.4 已將其標準化為無前綴函式名稱，稽核前呼叫 `EASTERSUNDAY(...)` 會得到 `#NAME?` | 新增 `EASTERSUNDAY` 標準名稱註冊，共用既有 Gauss 復活節演算法實作；保留舊前綴名稱以維持回溯相容 |
| `ISFORMULA` | 完全未實作（非 1.4 新增，屬既有缺口） | 新增 `FormulaLogicalFunctionHandlers.EvaluateIsFormula`，透過 `IEvaluationContext.GetCellFormula` 判斷儲存格是否含公式 |
| `ISNONTEXT` | 完全未實作（非 1.4 新增，屬既有缺口） | 新增 `FormulaLogicalFunctionHandlers.EvaluateIsNonText`，與既有 `ISTEXT` 對稱實作 |
| `CONVERT` | 完全未實作（非 1.4 新增，屬既有缺口） | 新增 `FormulaMathFunctionHandlers.EvaluateConvert` 與 `FormulaConvertUnitTable`，依 Table 24-26 實作 Area／Distance／Energy／Force／Information／MagneticFluxDensity／Mass／Power／Pressure／Speed／Temperature／Time／Volume 十三個單位群組、十進位與二進位字首換算 |

其餘 `COUNTA`／`INDEX`／`ISBLANK`／`ISLOGICAL`／`ISNUMBER`／`ISREF`／
`ISTEXT`／`NPER`／`PMT` 皆已實作，本次稽核未發現與 1.4 定義變更相關的
行為缺口（1.4 對這些函式的變更多為邊界案例措辭澄清，未變更既定計算語意）。

**結論：發現並修復 4 個真實缺口（1 個 ODF 1.4 新函式的標準命名缺口、
3 個既有規格缺口）。**

## 總結

| Part | 是否有新增元素／函式 | OdfKit 是否已涵蓋 | 本次動作 |
|---|---|---|---|
| Part 1 Introduction | 無（非 schema 承載文件） | N/A | 無需行動 |
| Part 2 Packages | 官方聲明零技術變更 | 是（功能面） | 無需行動；機械化 RNG 對照列為後續可選強化 |
| Part 3 Schema | 599 元素、屬性全數核對 | 是（100% 相符） | 無需行動 |
| Part 4 OpenFormula | 1 新函式＋12 項定義變更 | 修復前有 4 項缺口 | 已修復 `EASTERSUNDAY`／`ISFORMULA`／`ISNONTEXT`／`CONVERT`，測試通過 |

修復程式碼位置：`OdfKit/Formula/FormulaLogicalFunctionHandlers.cs`、
`OdfKit/Formula/FormulaMathFunctionHandlers.cs`、
`OdfKit/Formula/FormulaConvertUnitTable.cs`、
`OdfKit/Formula/FormulaBuiltinFunctionRegistry.cs`、
`OdfKit/Formula/OdfFormulaSupport.cs`；對應測試見
`OdfKit.Tests/FormulaAndStylesTest.cs`、`OdfKit.Tests/OpenFormulaSupportTests.cs`。
