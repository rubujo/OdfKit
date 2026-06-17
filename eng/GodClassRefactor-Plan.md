# OdfKit 上帝類別重構路線圖

## 目標

在 partial 拆分已完成的前提下，將巨型類別中的**可獨立職責**解耦為協作者型別／服務，同時**不破壞 public API**。

## 決策原則

| 策略 | 適用 | 不適用 |
|------|------|--------|
| **協作者提取** | 無狀態 DOM/演算法、驗證管線、 coercion 工具 | 需直接存取 10+ 私有欄位的核心生命週期 |
| **薄門面保留** | `TextDocument`、`OdfSigner`、`OdfPackage` 公開 API | — |
| **維持 partial** | `OdfElement` schema 驅動存取器 | 可抽出為 `internal static` 的純函式群 |

## 優先順序（依風險／收益）

| 順位 | 型別 | 總行數 | Phase | 提取目標 |
|------|------|--------|-------|----------|
| 1 | `TextDocument` | ~2671 | **1** | `OdfTrackedChangeDom`、`OdfTrackedChangePurger`、`OdfTrackedChangeTextExtractor` |
| 2 | `DefaultFormulaEvaluator` | ~4763 | **1** | `FormulaCoercion`；還原 `Matrix`/`Lookup` partial |
| 3 | `OdfSigner` | ~1668 | **1** | `OdfSignatureVerifier`（驗證管線） |
| 4 | `OdfPackage` | ~2615 | **2** | `OdfManifestLoader`、`OdfPackageSaver` + mutation context |
| 5 | `OdfSchemaPatternValidator` | ~2664 | **4** | 屬性/內容比對子驗證器 |
| 6 | `OdfElement` | ~5533 | **3** | enum parser / typed attribute 靜態註冊表（不破壞 DOM 聚合根） |
| 7 | `OdfTableSheet` | ~2082 | **4** | 圖表、條件格式、列欄群組引擎 |

## Phase 1（已完成）

- [x] 計畫文件
- [x] `OdfTrackedChangePurger`、`OdfTrackedChangeDom`、`OdfTrackedChangeTextExtractor`
- [x] `FormulaCoercion` + `DefaultFormulaEvaluator.Matrix/Lookup` partial
- [x] `OdfSignatureVerifier` 驗證管線（薄門面，委派 `OdfSigner` partial）
- [x] `dotnet build` / `dotnet test` / `dotnet format`

## Phase 2（已完成）

- [x] `OdfTrackedChangesEngine`（整合接受／拒絕）
- [x] `OdfManifestLoader` + `OdfManifestLoadContext`
- [x] `OdfSignatureSigner`（簽署管線薄門面）
- [x] `OdfPackageSaver` + `OdfPackageSaveCollaborators`
- [x] `eng/Format-Safe.ps1` 安全格式化規範

## Phase 3（已完成）

- [x] `FormulaBuiltinFunctionRegistry` 取代 `FunctionDispatch` 巨型 switch
- [x] `OdfPackageLoader` + `OdfPackageZipLoader` 載入引擎
- [x] `OdfElementSchemaRegistry` 靜態 enum token 註冊表（8 個 EnumParsers 遷移）

## Phase 4（已完成）

- [x] `OdfSchemaPatternAttributeMatcher` + `OdfSchemaPatternContentMatcher` 子驗證器
- [x] `OdfSchemaPatternMatchContext` 比對狀態提取
- [x] `OdfTableSheetChartEngine`、`OdfTableSheetConditionalFormatEngine`、`OdfTableSheetRowColumnGroupEngine`
- [x] `OdfTableSheetMutationContext` 協作存取器

## Phase 5（已完成）

- [x] `OdfTableSheetPrintSettingsEngine`、`OdfTableSheetVisibilityEngine`、`OdfTableSheetNamedRangeEngine`
- [x] `OdfTableSheetViewEngine`、`OdfTableSheetLayoutEngine`、`OdfTableSheetDomHelper`
- [x] `TextDocumentSearchReplaceEngine`、`TextDocumentHtmlFragmentEngine`

## Phase 6（已完成）

- [x] `TextDocumentMutationContext`、`TextDocumentDomHelper`
- [x] `TextDocumentFormControlsEngine`、`TextDocumentFieldsEngine`、`TextDocumentNotesEngine`
- [x] `OdfPackageFlatXmlLoader`、`OdfPackageArchiveWriter`、`OdfPackageXmlNamespaceHelper`
- [x] 擴充 `OdfPackageLoadCollaborators`／`OdfPackageSaveCollaborators`

## Phase 7（已完成）

- [x] `OdfSignatureSigner` 簽署實作遷移（`OdfSigner.Signing` 薄門面）
- [x] `TextDocumentTrackChangesRecordingEngine`
- [x] `FormulaLogicalFunctionHandlers`（邏輯函式 category 首輪遷移）
- [x] `OdfPackageMacroSanitizer` + `OdfPackageMacroSanitizeCollaborators`

## Phase 8（已完成）

- [x] `OdfPackageManifestWriter`（`SaveManifestToEntries` 遷移）
- [x] `FormulaStringFunctionHandlers`、`FormulaMatrixFunctionHandlers`、`FormulaDatabaseFunctionHandlers`
- [x] `eng/Migrate-FormulaCategoryHandler.ps1` category 遷移腳本

## Phase 9（已完成）

- [x] `FormulaStatisticalFunctionHandlers`（Statistical + Helpers.Statistical.\*）
- [x] `FormulaLookupFunctionHandlers`（Lookup + Helpers.Lookup）
- [x] `FormulaMathFunctionHandlers`（Math.Standard + Math.Rounding + Math.Trigonometry）
- [x] `DefaultFormulaEvaluator.FunctionRegistry` 委派更新

## Phase 10（已完成）

- [x] `FormulaDateTimeFunctionHandlers`（DateTime + Helpers.DateTime）
- [x] `FormulaFinancialFunctionHandlers`（Financial.\* + Helpers.Financial）
- [x] `DefaultFormulaEvaluator` 全部 category handler 遷移完成
- [x] 修正 `Migrate-FormulaCategoryHandler.ps1` using 標頭排版

## Phase 11（已完成）

- [x] `FormulaBuiltinFunctionRegistry` 內建註冊表自給自足（移除 `FunctionRegistry` partial）
- [x] `FormulaDocumentEvaluationEngine`、`FormulaPrefixNormalizer`
- [x] `DefaultFormulaEvaluator` 收斂為薄門面（刪除 20 個 category stub partial）
- [x] `OdfPackageRdfMetadataEngine`、`OdfPackageSaveHooksEngine`、`OdfPackageSignaturePurgeEngine`

## Phase 12（已完成）

- [x] `OdfSignatureVerifier` 驗證實作遷移（反轉薄門面委派方向）
- [x] `OdfSignatureDerCodec`、`OdfSignatureCrlUtilities`、`OdfSignatureX509Utilities`、`OdfSignatureTsaClient`
- [x] `OdfSigner` 收斂為公開 API 薄門面（刪除 6 個 verification／utility partial）

## Phase 13（已完成）

- [x] `OdfPackageXmlMacroSanitizer`（`SanitizeXmlNode` 實作遷移）
- [x] `OdfPackageEntryNameSanitizer`（Zip Slip 防禦 `SanitizeEntryName` 遷移）
- [x] `OdfPackage.MacroSanitize` 收斂為公開 API 薄門面

## Phase 14（已完成）

- [x] `OdfElementPrimitiveAttributeAccess`（int／bool／decimal 解析引擎）
- [x] `OdfElementComplexAttributeAccess`（DateTime／OdfLength／OdfColor 等複合型別引擎）
- [x] `OdfElementEnumAttributeAccess`（schema 枚舉 token getter 委派）
- [x] `eng/Migrate-OdfElementEnumAttributeAccess.ps1`（10 個 Attribute partial 遷移腳本）

## Phase 15（已完成）

- [x] `OdfElementDomainAttributeAccess`（幾何／語系／儲存格位址／百分比等領域值引擎）
- [x] `eng/Migrate-OdfElementDomainAttributeAccess.ps1`（5 個 Attribute partial 遷移腳本）

## Phase 16（已完成）

- [x] `TextDocumentCoreCollaborators` 協作存取器
- [x] `TextDocumentSettingsEngine`、`TextDocumentTocEngine`、`TextDocumentFormulaEngine`
- [x] `TextDocumentCommentsEngine`、`TextDocumentContentMergeEngine`、`TextDocumentMailMergeBatchEngine`
- [x] `TextDocumentPageFieldsEngine`、`TextDocumentCjkFontEngine`、`TextDocumentFontFaceEngine`
- [x] `TextDocument.cs` 收斂為薄門面（`DecodeHtmlEntities` 遷至 `TextDocumentDomHelper`）

## Phase 17（已完成）

- [x] `OdfDocumentMergeEngine`（`AppendDocument` 與樣式合併管線）
- [x] `OdfDocumentStyleRemapEngine`（`RemapStylesInNodes` 遷移）
- [x] `OdfDocumentMergeCollaborators` 協作存取器
- [x] `OdfDocument.Merging` 收斂為薄門面

## Phase 18（已完成）

- [x] `OdfTableSheetDomAccessEngine`（列／欄／儲存格 DOM 存取與列舉）
- [x] `OdfTableSheetRepeatSplitEngine`（重複列／欄／儲存格拆分）
- [x] `OdfTableSheetMutationContext` 改委派 DomAccessEngine
- [x] `OdfTableSheet.Internals`／`Internals.CellAccess` 收斂為薄門面

## 剩餘工作（Phase 19+）

| 型別 | 約略行數 | 待提取 |
|------|----------|--------|
| `OdfDocument` | ~1200 | Helpers／Metadata／Lifecycle |
| `OdfPackage` | ~900 | 公開 API 項目讀寫（需保留薄門面） |
| `OdfElement` | ~640 | Wrapper partial（typed DOM 元素，低優先） |

## 驗證

每輪變更：`dotnet build` → `dotnet test`（1131+）→ `pwsh eng/Format-Safe.ps1` → GPG 簽署提交。