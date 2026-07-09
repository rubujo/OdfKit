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
| 手寫公開 API | **suggestion**（`.editorconfig`） | 新增 API 必須遵守上方模式；既有多載分批收斂（明確多載轉呼叫） |
| 生成 DOM（`DOM/Generated`） | 與手寫相同；**禁止手改 `.g.cs`** | 產生器輸出 `Type()`／`Type(string? prefix)`／`Type(params OdfNode[])`，**無** `prefix = null`；改 `DomWrappersCSharpWriter` 後 `pwsh eng/Generate-OdfSchemaProvider.ps1` 重產 |
| schema provider 產生碼 | 不適用公開多載規則為主 | 非公開 API 形狀焦點 |
| 示範收斂 | `TableTableElement.InsertRows`／`DeleteRows` | 無預設最長多載 + 短多載轉呼叫 |

## 新增公開 API 檢查清單

1. 避免在多個同名多載上同時使用可選參數。  
2. 優先：`Foo(required…)` + `Foo(required…, optional…)`（最長可不帶 `=` 預設，改由短多載轉呼叫）。  
3. 更新雙 TFM `PublicAPI.Unshipped.txt`（或 `Generate-PublicApiBaseline.ps1`）。  
4. 本機建置：`CI=true` 且 `RunAnalyzersDuringBuild=true`。

## 1.0 展望

- 手寫路徑可將 RS0026／RS0027 升為 **warning** 或 **error**。  
- 生成 DOM 若要收斂，應改 `OdfSchemaGenerator` 樣板後整批重產與基線更新。  
- 穩定版後搭配 `PackageValidationBaselineVersion` 防破壞性變更。

## 相關

- [OdfKit/PublicAPI/README.md](../OdfKit/PublicAPI/README.md)  
- [docs/maintainability.md](maintainability.md)  
- `.editorconfig` 中 `dotnet_diagnostic.RS0026`／`RS0027`  
