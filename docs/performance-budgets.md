# 效能預算

三格式標準基準目前處於 `collecting` 階段。固定 workflow 累積至少三次結果後，才以三次
中位數填入 `eng/performance-budgets.json`，不得以單次 GitHub hosted runner 結果建立門檻。

- checksum、輸出可重載與案例參數屬硬性正確性條件。
- managed allocation 超過已建立基線 20% 時屬硬性 regression。
- 穩態時間與峰值工作集超過基線 30% 時先列為提醒。
- ODS、ODT、ODP 只與同格式、同情境及同 API 層級的歷史結果比較。

公開引用量測結果時，必須同時提供提交 SHA、執行日期、runtime、作業系統、處理器資訊及
workflow artifact。`status` 尚未改為 `active` 前，不得宣稱專案已有穩定絕對效能保證。
