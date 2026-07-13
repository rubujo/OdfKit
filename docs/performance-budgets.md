# 效能預算

三格式標準基準目前處於 `collecting` 階段。固定 workflow 累積至少三次結果後，才以三次
中位數填入 `eng/performance-budgets.json`，不得以單次 GitHub hosted runner 結果建立門檻。

- checksum、輸出可重載與案例參數屬硬性正確性條件。
- managed allocation 超過已建立基線 20% 時屬硬性 regression。
- 穩態時間與峰值工作集超過基線 30% 時先列為提醒。
- ODS、ODT、ODP 只與同格式、同情境及同 API 層級的歷史結果比較。

公開引用量測結果時，必須同時提供提交 SHA、執行日期、runtime、作業系統、處理器資訊及
workflow artifact。`status` 尚未改為 `active` 前，不得宣稱專案已有穩定絕對效能保證。

每份 workflow 樣本使用 schema v2 envelope，必須記錄 commit SHA、UTC 量測時間、workflow
run／attempt、runner OS／架構、.NET runtime、CPU 型號與 artifact 名稱，並完整包含九個標準情境。CI 以
`pwsh eng/Test-PerformanceBudgets.ps1 -SamplePath <path>` 驗證樣本；缺少 metadata、情境、
正值量測或語意 checksum 時不得納入三次樣本。未提供 `-SamplePath` 時，腳本只驗證
`eng/performance-budgets.json` 的 collecting／active 狀態契約。

累積至少三份同 runner OS、架構、.NET runtime、CPU 型號與 artifact 身分的樣本後，以
`eng/New-PerformanceBudgetCandidate.ps1` 產生 `status: candidate` 的中位數報告。腳本拒絕重複
workflow run／attempt，並對每個情境分別計算耗時、managed allocation 與峰值工作集的中位數。
候選檔不會自動修改 `eng/performance-budgets.json`；維護者仍須檢視 runner 漂移、checksum、
樣本分布與 artifact 後，才能手動將審核過的 scenarios 寫入並把狀態改為 `active`。

`active` 預算必須完整包含九個情境及 runner OS／架構／runtime／CPU／artifact 身分。驗證當次樣本時，
managed allocation 超過基線加 `allocationRegressionPercent` 會硬失敗；耗時或峰值工作集超過
基線加 `advisoryRegressionPercent` 只輸出 warning，保留 hosted runner 雜訊的人工複核空間。
checksum、輸出大小及九情境完整性無論狀態皆為硬性條件。

```powershell
pwsh -Command "& ./eng/New-PerformanceBudgetCandidate.ps1 `
  -SamplePath @('sample-1.json', 'sample-2.json', 'sample-3.json')"
```
