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
| 手寫公開 API | **error**（`.editorconfig`） | 不得再引入 RS0026／RS0027；既有核心路徑（`Save`／`Load`／`Open`／文件變體等）已改為明確多載鏈 |
| 生成 DOM（`DOM/Generated`） | **none**（目錄覆寫）；**禁止手改 `.g.cs`** | 產生器輸出 `Type()`／`Type(string? prefix)`／`Type(params OdfNode[])`，**無** `prefix = null`；改 `DomWrappersCSharpWriter` 後 `pwsh eng/Generate-OdfSchemaProvider.ps1` 重產 |
| schema provider 產生碼 | **none**（`Compliance/Generated` 覆寫） | 非公開 API 形狀焦點 |
| 示範收斂 | `TableTableElement.InsertRows`／`DeleteRows`、`OdfDocument.Save`／`LoadAsync` | 無預設最長多載 + 短多載轉呼叫 |

> 說明：單一公開方法可保留尾端可選參數（不觸發 RS0026）；**兩個以上同名多載皆含可選參數**才違反 RS0026。RS0027 要求含可選參數的多載必須是同名中參數最多者。本專案對高頻 API 偏好「全部無 `=` 預設的明確鏈」，以利跨語言與 PublicAPI 基線穩定。

## 新增公開 API 檢查清單

1. 避免在多個同名多載上同時使用可選參數。  
2. 優先：`Foo(required…)` + `Foo(required…, optional…)`（最長可不帶 `=` 預設，改由短多載轉呼叫）。  
3. 更新雙 TFM `PublicAPI.Unshipped.txt`（或 `pwsh eng/Generate-PublicApiBaseline.ps1 -Verify`）。  
4. 本機建置：`CI=true` 且 `RunAnalyzersDuringBuild=true`；RS0026／RS0027 為 **error**。

## 1.0 展望

- 其餘仍帶單一可選參數的公開 API，可依使用頻率分批改為明確多載鏈（非 RS 強制）。  
- 穩定版後搭配 `PackageValidationBaselineVersion` 防破壞性變更。

## 相關

- [OdfKit/PublicAPI/README.md](../OdfKit/PublicAPI/README.md)  
- [docs/maintainability.md](maintainability.md)  
- `.editorconfig` 中 `dotnet_diagnostic.RS0026`／`RS0027`  
