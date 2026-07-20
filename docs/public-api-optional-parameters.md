# 公開 API 可選參數規範（RS0026／RS0027）

本文件定義 OdfKit **v0.0.1 完滿基線**對可選參數公開多載的政策，對齊
[Adding Optional Parameters in Public API](https://github.com/dotnet/roslyn/blob/main/docs/Adding%20Optional%20Parameters%20in%20Public%20API.md)
與 `Microsoft.CodeAnalysis.PublicApiAnalyzers`。

**`CancellationToken` 另有專節**（見下方），其便利形狀對齊 .NET TAP／SDK 慣例，
**不得**被一般「恰好一個可選參數改明確鏈」規則機械拆除。

## 規則摘要

| 診斷 | 要求 |
|------|------|
| **RS0026** | 同一公開符號名稱下，**不得**有兩個以上「皆含可選參數」的多載 |
| **RS0027** | 若多載含可選參數，該多載必須是同名公開多載中**參數最多**者 |

正確模式（一般可選參數，建議）：

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

## CancellationToken 政策（公開／內部）

對齊 Microsoft TAP、CA1068，以及業界 SDK 慣例（公開可省略 CT、內部必填並傳遞）。
`default(CancellationToken)` 與 `CancellationToken.None` 等價。

### 公開 API（含 `public`／`protected` 可覆寫表面）

可取消的長時間或 I/O 非同步 API **必須**暴露 `CancellationToken`，且為**最後一個**參數。
呼叫端必須能**省略** CT。下列兩種形狀**皆合法**，同一 API 家族擇一保持一致：

| 形狀 | 範例 | 何時使用 |
|------|------|----------|
| **A. 尾端 `= default`** | `Task FooAsync(..., CancellationToken cancellationToken = default)` | 業界 SDK 最常見；WebFont／部分 extension 現況 |
| **B. 明確多載鏈** | `FooAsync(...)` 轉呼叫 `FooAsync(..., CancellationToken)`（最長必填 CT） | 核心 `LoadAsync`／`SaveAsync` 等現況；跨語言友善 |

```csharp
// 形狀 A（允許，不得為「收斂」而拆除）
public Task GenerateAsync(
    WebFontSubsetRequest request,
    string destinationDirectory,
    CancellationToken cancellationToken = default);

// 形狀 B（允許，與 A 等價）
public Task LoadAsync(string path) => LoadAsync(path, CancellationToken.None);
public Task LoadAsync(string path, CancellationToken cancellationToken) { … }
```

**禁止**（公開可取消 API）：

- 完全沒有 CT（P0 漏缺：長時間 I/O／CPU async）。
- 只有必填 CT、又**沒有**形狀 A 或 B 的便利路徑（P1：呼叫端無法省略）。
- 簽名有 CT 但實作忽略、未向下傳遞（P0：假取消）。
- 為「統一風格」把已存在的形狀 A 改成 B，或把 B 改成 A，而無其他功能理由。

### 內部／private／協作者

- `CancellationToken` **應必填**（不要 `= default`），強迫呼叫鏈顯式傳遞。
- 通過「不可取消點」後，才可改傳 `CancellationToken.None`。
- 同步包裝 async 時可傳 `default`／`None`；async 熱路徑必須把收到的 token 往下傳。

### 與 RS0026／RS0027 的關係

- 僅尾端 `CancellationToken cancellationToken = default`：**允許**，且
  `eng/Expand-OptionalParameters.py` **不得**將其展開拆除。
- CT 與**另一個**可選參數同列（例如 `renderer = null, cancellationToken = default`）：
  仍受 RS0026／RS0027 約束。優先把非 CT 可選改成明確多載或 options，**保留** CT 的
  可省略性。

### 稽核優先級

| 優先級 | 漏缺 | 處理 |
|--------|------|------|
| P0 | public async 可取消工作完全無 CT | 補 CT；公開用 A 或 B 提供可省略路徑 |
| P0 | 有 CT 但未傳遞 | 修正傳遞 |
| P1 | public 僅必填 CT、無可省略路徑 | 補 `= default` **或** 無 CT 短多載 |
| P2 | internal 使用 `= default` 導致易漏傳 | 改必填 CT |
| 非缺陷 | 已有 A 或 B | 保留，勿機械重寫 |

測試中呼叫可接受 `CancellationToken` 的 API 時，仍須傳
`TestContext.Current.CancellationToken`（見 `AGENTS.md`）。

## 收斂狀態

| 範圍 | 嚴重度 | 說明 |
|------|--------|------|
| 手寫公開 API | **error**（`.editorconfig`） | 不得再引入 RS0026／RS0027 |
| **恰好一個**尾端可選參數（**非** CT） | 偏好明確鏈 | `eng/Expand-OptionalParameters.py`（略過僅 CT 的 `= default`） |
| **僅**尾端 `CancellationToken = default` | **允許保留** | 對齊 .NET SDK；工具不得拆除 |
| **兩個以上**尾端可選參數 | 明確多載鏈或 **options 物件** | 高頻已用 options；新 API 禁止再加「多可選位置參數」 |
| 生成 DOM（`DOM/Generated`） | **none**（目錄覆寫）；**禁止手改 `.g.cs`** | 產生器輸出無 `prefix = null` |
| schema provider 產生碼 | **none**（`Compliance/Generated` 覆寫） | 非公開 API 形狀焦點 |

> **RS0026**：同一公開符號名稱下不得有**兩個以上**「皆含可選參數」的多載。  
> **RS0027**：若多載含可選參數，該多載必須是同名中參數最多者。  
> 一般可選參數：偏好明確多載鏈。  
> **`CancellationToken`：公開可 A（`= default`）或 B（多載鏈）；內部必填並傳遞。**

## 新增公開 API 檢查清單

1. 避免在多個同名多載上同時使用可選參數。  
2. 恰好一個可選參數且**不是** CT：優先明確多載鏈。  
3. 可取消 async：必須有 CT（最後一參數）；公開必須可省略（形狀 A 或 B）；實作必須傳遞。  
4. 兩個以上可選參數：優先 options；CT 與其他可選並存時先收斂非 CT 可選。  
5. 更新雙 TFM `PublicAPI.Unshipped.txt`（或 `pwsh eng/Generate-PublicApiBaseline.ps1 -Verify`）。  
6. 本機建置：`CI=true` 且 `RunAnalyzersDuringBuild=true`。

## 維護工具

- `python eng/Expand-OptionalParameters.py`：展開公開／保護方法上的尾端可選參數（略過 primary ctor、`out`／`ref`、**僅** `CancellationToken = default`）。  
- `python eng/Rewrite-ConvenienceSummaries.py`：高頻檔案便利多載摘要差異化。

## 高頻 options 物件（已落地）

| Options 型別 | 取代的多可選表面 |
|--------------|------------------|
| `OdfRichTextRunOptions` | `OdfRichText.AddRun`／`OdfCellRichTextBuilder.Append` 格式參數 |
| `OdsRowWriteOptions` | `OdsStreamWriter.WriteStartRow` 列高／樣式／最佳列高 |
| `OdfValidationOptions` | `OdfPackageValidator.Validate`／`OdfFlatDocumentValidator.Validate` |
| `OdfFlatXmlWriteOptions` | `OdfDocumentFactory.WriteFlatXml` 版本與 leaveOpen |
| `OdfSchemaRegistrationOptions` | `OdfSchemaRegistry.RegisterSchema` 合併／覆寫 |

0.x 尚未正式發布：上述表面**不**保留舊多可選多載相容層。

## 1.0 展望

- 穩定版後搭配 `PackageValidationBaselineVersion` 防破壞性變更。  
- CT 形狀 A／B 可依套件維持現況，不強制全庫單一風格。

## 相關

- [OdfKit/PublicAPI/README.md](../OdfKit/PublicAPI/README.md)  
- [docs/maintainability.md](maintainability.md)  
- [Microsoft TAP — Cancellation](https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap#cancellation-optional)  
- [Recommended patterns for CancellationToken](https://devblogs.microsoft.com/premier-developer/recommended-patterns-for-cancellationtoken/)  
- [CA1068](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1068)  
- `.editorconfig` 中 `dotnet_diagnostic.RS0026`／`RS0027`  
