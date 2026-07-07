# 效能基準線

本文件說明 OdfKit 的效能基準線、穩定量測設定檔與報告產生方式。基準測試結果受 CPU、記憶體、磁碟、作業系統、電源設定與 JIT 狀態影響，因此本文件記錄「如何量測」與「目前關卡」，不把單一本機輸出宣告為跨機器服務等級承諾。

## 目前回歸關卡

`eng/Benchmark-Regression.ps1` 會執行 `DomInsertBenchmarks.SequentialInsertAfter` 並與 `eng/baselines/performance-baselines.json` 比對。

| 基準測試 | 基準線平均值 | 容許範圍 | 用途 |
|-----------|---------------|-----------|------|
| `DomInsertBenchmarks.SequentialInsertAfter` | `123.9 us` | `+40%` | 偵測 DOM 循序插入效能的重大回歸 |

執行：

```powershell
pwsh eng/Benchmark-Regression.ps1 -Configuration Release
```

更新基準線只應在刻意重訂效能基準時執行：

```powershell
pwsh eng/Benchmark-Regression.ps1 -Configuration Release -UpdateBaseline
```

## 穩定本機量測設定檔

正式比較效能變更時，使用較長且時間導向的穩定量測設定檔：

```powershell
pwsh eng/Benchmark-Stable.ps1 -Filter "*OdsStreamWriter*"
```

預設設定：

| 設定 | 值 |
|------|----|
| BenchmarkDotNet 工作 | `Medium` |
| 單次迭代目標時間 | `250 ms` |
| 量測迭代次數 | `9` 到 `15` |
| 診斷欄位 | 記憶體、例外 |
| 輸出政策 | BenchmarkDotNet 產物不提交，摘要報告可另存 |

## 產生基準線報告

使用 `eng/Benchmark-BaselineReport.ps1` 可用穩定量測設定檔跑指定基準測試，並產生 Markdown 報告：

```powershell
pwsh eng/Benchmark-BaselineReport.ps1 -Filter "*OdsStreamWriter*" -OutputPath artifacts/performance/ods-stream-writer.md
```

此報告適合附在 pull request、發行說明或本機效能調查紀錄中。若要保留長期版本化摘要，請人工挑選穩定機器與代表性基準測試，再將摘要數字更新至本文件或基準線 JSON。

## 代表性基準測試分層

| 層級 | Benchmark | 觀察重點 |
|------|-----------|----------|
| 封裝 / DOM | `DomInsertBenchmarks`、`DomTextContentBenchmarks`、`OdfPackageLoadBenchmarks` | DOM 變更、文字內容存取、封裝載入 |
| 試算表串流 | `OdsStreamWriterBenchmarks`、`OdfTableSheetCellAccessBenchmarks` | 大量列寫入、儲存格存取快取 |
| 文件來回讀寫 | `OdtRoundTripBenchmarks` | 建立、儲存、載入大型 ODT |
| 公式 | `FormulaParseBenchmarks` | 公式剖析配置量與延遲 |
| 協作 | `CollaborationOperationBenchmarks` | TDF JSON operation 剖析與重播 |

## 判讀規則

- 先看配置量，再看平均值；配置量突增通常比小幅平均值波動更值得追。
- 同一台機器上比較同一個 filter、同一個量測設定檔、同一個電源模式。
- 對微型基準測試結果保持保守；若 BenchmarkDotNet 提示 minimum iteration time 過短，請提高 `-IterationTime` 或資料量。
- CI 適合跑煙霧測試與回歸關卡，不適合以單一本機毫秒數作為跨平台硬門檻。
