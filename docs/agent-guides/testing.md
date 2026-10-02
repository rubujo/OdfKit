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

推送前必須讓整個方案以 CI 的分析器設定建置一次，不能只建置有改動的專案：CI 對所有專案都跑分析器，
只驗證測試專案會漏掉原始碼專案的診斷（`CA2249`、`CA1865`、`CA1512`、`xUnit2017` 都曾因此在 CI 才失敗）。
本機實測（Windows、整個 `OdfKit.slnx`、Release、`--no-incremental`）：不含分析器約 1 分 3 秒，含分析器約
1 分 53 秒，多出約 50 秒；增量建置只重編有改動的專案，通常更快。

```powershell
$env:CI = 'true'
dotnet build OdfKit.slnx -c Release
Remove-Item Env:CI
```

建置必須是 0 個警告、0 個錯誤才推送；測試專案同樣維持 0 個警告（陣列字面值使用集合運算式、
測試方法名稱使用 PascalCase、不要新增底線）。
