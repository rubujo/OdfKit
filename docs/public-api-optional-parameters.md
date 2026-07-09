# 公開 API 可選參數規範（RS0026／RS0027）

本文件定義 OdfKit **v0.0.1 完滿基線**對可選參數公開多載的政策，對齊
[Adding Optional Parameters in Public API](https://github.com/dotnet/roslyn/blob/main/docs/Adding%20Optional%20Parameters%20in%20Public%20API.md)
與 `Microsoft.CodeAnalysis.PublicApiAnalyzers`。

## 規則摘要

| 診斷 | 要求 |
|------|------|
| **RS0026** | 同一公開符號名稱下，**不得**有兩個以上「皆含可選參數」的多載 |
| **RS0027** | 若多載含可選參數，該多載必須是同名公開多載中**參數最多**者 |

正確模式（建議）：

```csharp
// 無預設的明確多載
public void InsertRows(int position) => InsertRows(position, 1);

// 最長、可含預設（此專案偏好「最長不帶預設」以利跨語言相容）
public void InsertRows(int position, int count) { … }
```

錯誤模式：

```csharp
public void Foo(int a = 0) { }
public void Foo(string s = "") { } // RS0026：多個皆可選
```

## 收斂狀態

| 範圍 | 嚴重度 | 說明 |
|------|--------|------|
| 手寫公開 API | **error**（`.editorconfig`） | 不得再引入 RS0026／RS0027 |
| **恰好一個**尾端可選參數 | 已全面改明確鏈 | 以 `eng/Expand-OptionalParameters.py` 批次處理（約 300 方法；含 DOM 屬性 Get／Set 的 `version`／`prefix` 等） |
| **兩個以上**尾端可選參數 | 單一方法可保留 `=` | 不觸發 RS0026；支援具名略過中間參數（如 `OdfSchemaPatternNode`、`OdfSchemaSet`、部分 Builder／動畫 API）。若強制全改明確鏈會破壞具名呼叫語意，需改 options 物件或補齊組合多載 |
| 生成 DOM（`DOM/Generated`） | **none**（目錄覆寫）；**禁止手改 `.g.cs`** | 產生器輸出 `Type()`／`Type(string? prefix)`／`Type(params OdfNode[])`，**無** `prefix = null` |
| schema provider 產生碼 | **none**（`Compliance/Generated` 覆寫） | 非公開 API 形狀焦點 |

> **RS0026**：同一公開符號名稱下不得有**兩個以上**「皆含可選參數」的多載。  
> **RS0027**：若多載含可選參數，該多載必須是同名中參數最多者。  
> 本專案對「恰好一個可選參數」偏好改為**無 `=` 的明確多載鏈**；多可選參數則允許留在最長單一方法上（與 Roslyn 建議相容）。

## 新增公開 API 檢查清單

1. 避免在多個同名多載上同時使用可選參數。  
2. 恰好一個可選參數：優先 `Foo(required…)` + `Foo(required…, optional…)`（最長可不帶 `=`，由短多載轉呼叫）。  
3. 兩個以上可選參數：優先 options 物件；或單一最長方法保留尾端 `=`（勿再加第二個帶可選的多載）。  
4. 更新雙 TFM `PublicAPI.Unshipped.txt`（或 `pwsh eng/Generate-PublicApiBaseline.ps1 -Verify`）。  
5. 本機建置：`CI=true` 且 `RunAnalyzersDuringBuild=true`。

## 維護工具

- `python eng/Expand-OptionalParameters.py`：僅展開**恰好一個**尾端可選參數的公開／保護方法（略過 primary ctor、`out`／`ref`、多可選參數）。

## 1.0 展望

- 多可選參數 API 可依使用頻率遷至 options 物件。  
- 穩定版後搭配 `PackageValidationBaselineVersion` 防破壞性變更。

## 相關

- [OdfKit/PublicAPI/README.md](../OdfKit/PublicAPI/README.md)  
- [docs/maintainability.md](maintainability.md)  
- `.editorconfig` 中 `dotnet_diagnostic.RS0026`／`RS0027`  
