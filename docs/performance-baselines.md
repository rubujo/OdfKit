# Performance Baselines

本文件說明 OdfKit 的效能基準線、穩定量測 profile 與報告產生方式。Benchmark 結果受 CPU、記憶體、磁碟、OS、電源設定與 JIT 狀態影響，因此本文件記錄「如何量測」與「目前 gate」，不把單一本機輸出宣告為跨機器 SLA。

## 目前回歸 gate

`eng/Benchmark-Regression.ps1` 會執行 `DomInsertBenchmarks.SequentialInsertAfter` 並與 `eng/baselines/performance-baselines.json` 比對。

| Benchmark | Baseline mean | Tolerance | 用途 |
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

## 穩定本機 profile

正式比較效能變更時，使用較長且時間導向的 stable profile：

```powershell
pwsh eng/Benchmark-Stable.ps1 -Filter "*OdsStreamWriter*"
```

預設設定：

| 設定 | 值 |
|------|----|
| BenchmarkDotNet job | `Medium` |
| Iteration time | `250 ms` |
| Measured iterations | `9` 到 `15` |
| Diagnostics | Memory、Exceptions |
| Output policy | BenchmarkDotNet artifacts 不提交，摘要報告可另存 |

## 產生 baseline report

使用 `eng/Benchmark-BaselineReport.ps1` 可用 stable profile 跑指定 benchmark，並產生 Markdown 報告：

```powershell
pwsh eng/Benchmark-BaselineReport.ps1 -Filter "*OdsStreamWriter*" -OutputPath artifacts/performance/ods-stream-writer.md
```

此報告適合附在 PR、release note 或本機效能調查紀錄中。若要保留長期版本化摘要，請人工挑選穩定機器與代表性 benchmark，再將摘要數字更新至本文件或基準線 JSON。

## 代表性 benchmark 分層

| 層級 | Benchmark | 觀察重點 |
|------|-----------|----------|
| Package / DOM | `DomInsertBenchmarks`、`DomTextContentBenchmarks`、`OdfPackageLoadBenchmarks` | DOM 變更、文字內容存取、封裝載入 |
| Spreadsheet streaming | `OdsStreamWriterBenchmarks`、`OdfTableSheetCellAccessBenchmarks` | 大量列寫入、儲存格存取快取 |
| Document round-trip | `OdtRoundTripBenchmarks` | 建立、儲存、載入大型 ODT |
| Formula | `FormulaParseBenchmarks` | 公式剖析配置量與延遲 |
| Collaboration | `CollaborationOperationBenchmarks` | TDF JSON operation parse / replay |

## 判讀規則

- 先看 allocation，再看 mean；配置量突增通常比小幅 mean 波動更值得追。
- 同一台機器上比較同一個 filter、同一個 profile、同一個電源模式。
- 對 microbenchmark 結果保持保守；若 BenchmarkDotNet 提示 minimum iteration time 過短，請提高 `-IterationTime` 或資料量。
- CI 適合跑 smoke / regression gate，不適合以單一本機毫秒數作為跨平台硬門檻。
