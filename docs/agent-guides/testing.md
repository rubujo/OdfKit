# 測試開發指南

修改 `OdfKit.Tests` 或 `tests/` 下的測試時適用本指南。

## 測試慣例

- 呼叫可接受 `CancellationToken` 的非同步 API 時，傳入
  `TestContext.Current.CancellationToken`。這包含 `Task.Delay`、`ReadToEndAsync`、
  `WaitAsync`、`IAsyncEnumerable` 工廠方法與專案自訂 async API。
- 只有刻意驗證預取消或自訂取消語意時，才建立或使用 linked token。
- 集合中是否存在符合條件的項目，使用 `Assert.Contains(collection, predicate)` 或
  `Assert.DoesNotContain(collection, predicate)`；不要以
  `Assert.NotEmpty(query.Where(...))`、`Assert.Empty(query.Where(...))`、
  `Assert.True(query.Any(...))` 或等價 LINQ 形狀表達。

## 驗證

測試格式化只執行 whitespace，避免雙 TFM analyzer code fix 寫入合併衝突標記：

```powershell
pwsh eng/Format-Safe.ps1 -IncludeTests
```

一般本機建置預設關閉 build-time analyzer。驗證測試時必須明確啟用：

```powershell
dotnet build OdfKit.Tests/OdfKit.Tests.csproj -c Release --framework net10.0 `
  --no-restore -p:RunAnalyzersDuringBuild=true
```

若變更影響 `net8.0` 或跨 TFM 共用程式碼，再以相同命令驗證 `net8.0`。針對
`tests/` 下的獨立專案時，以受影響 `.csproj` 與 TFM 取代上述路徑。
