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
| 5 | `OdfSchemaPatternValidator` | ~2664 | **2** | 屬性/內容比對子驗證器 |
| 6 | `OdfElement` | ~5533 | **3** | enum parser / typed attribute 靜態註冊表（不破壞 DOM 聚合根） |
| 7 | `OdfTableSheet` | ~2082 | **3** | 圖表、條件格式、列欄群組引擎 |

## Phase 1（已完成）

- [x] 計畫文件
- [x] `OdfTrackedChangePurger`、`OdfTrackedChangeDom`、`OdfTrackedChangeTextExtractor`
- [x] `FormulaCoercion` + `DefaultFormulaEvaluator.Matrix/Lookup` partial
- [x] `OdfSignatureVerifier` 驗證管線（薄門面，委派 `OdfSigner` partial）
- [x] `dotnet build` / `dotnet test` / `dotnet format`

## Phase 2（進行中）

- [x] `OdfTrackedChangesEngine`（整合接受／拒絕）
- `OdfSignatureSigner`（簽署管線）
- `OdfPackageSaver` + `OdfPackageLoadContext`
- `OdfManifestLoader`

## Phase 3（後續）

- `OdfPackageLoader` 完整載入引擎
- `FormulaBuiltinFunctionRegistry` 取代 `FunctionDispatch` 巨型 switch
- `OdfElement` 靜態 schema 註冊表

## 驗證

每輪變更：`dotnet build` → `dotnet test`（1131+）→ `dotnet format` → GPG 簽署提交。