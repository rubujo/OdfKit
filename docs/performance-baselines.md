# 效能基準線

本文件說明 OdfKit 的效能基準線、穩定量測設定檔與報告產生方式。基準測試結果受 CPU、記憶體、磁碟、作業系統、電源設定與 JIT 狀態影響，因此本文件記錄「如何量測」與「目前關卡」，不把單一本機輸出宣告為跨機器服務等級承諾。

## 目前回歸關卡

`eng/Benchmark-Regression.ps1` 會執行 `DomInsertBenchmarks.SequentialInsertAfter` 與
`OdsStreamWriterBenchmarks.WriteRows`，並與 `eng/baselines/performance-baselines.json` 比對。

| 基準測試 | 基準線平均值 | 容許範圍 | 用途 |
|-----------|---------------|-----------|------|
| `DomInsertBenchmarks.SequentialInsertAfter` | `123.9 us` | `+40%` | 偵測 DOM 循序插入效能的重大回歸 |
| `OdsStreamWriterBenchmarks.WriteRows` | 以 `eng/baselines/performance-baselines.json` 為準 | 時間 `+40%`、配置量 `+15%` | 保護 200,000 列雙欄 ODS 串流寫入的時間與配置量 |
| `FormulaEvaluationBenchmarks.FullRecalculation10000` | `223.9 ms`／`98.83 MB` | 時間 `+40%`、配置量 `+15%` | 保護 10K 獨立公式交易式全量重算 |
| `FormulaEvaluationBenchmarks.IncrementalOnePercentRecalculation10000` | `2.058 ms`／`1.82 MB` | 時間 `+40%`、配置量 `+15%` | 保護 10K 公式中 1% 受影響子圖的交易式增量重算 |

執行：

```powershell
pwsh eng/Benchmark-Regression.ps1 -Configuration Release
```

更新基準線只應在刻意重訂效能基準時執行：

```powershell
pwsh eng/Benchmark-Regression.ps1 -Configuration Release -UpdateBaseline
```

## 最新本機跨套件重新驗證

2026-07-26 以 Release 組件重新執行一百萬列 × 十欄的手動跨套件量測；
這是單機單次重新驗證，不取代回歸關卡，也不構成跨機器效能承諾。

| 情境 | 耗時 | GC 累積配置量 | 峰值工作集 |
|------|------|----------------|------------|
| `OdsStreamWriter` | `6,608 ms` | `472.4 MB` | `36.6 MB` |
| `MiniExcel` | `6,720 ms` | `3,354.3 MB` | `46.7 MB` |
| `ClosedXml` | `47,505 ms` | `10,949.9 MB` | `2,207.2 MB` |

完整環境、方法、輸出檔案大小與歷史結果見
[OdfKit 與同類套件的串流寫入效能對比](performance-comparison.md)。
同日亦完成 ODT 串流寫入、ODS／ODT 串流讀取、ODP 結構讀取及三格式 DOM
來回讀寫的九情境本機重新驗證，結果見
[ODS、ODT、ODP 標準效能基準](performance-standard-documents.md)。

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
| 三格式標準 | `StandardOdsBenchmarks`、`StandardOdtBenchmarks`、`StandardOdpBenchmarks` | ODS／ODT／ODP 標準讀寫、DOM 與語意檢查碼 |
| 共通封裝 | `StandardPackageOpenBenchmarks` | 分離 ZIP 封裝開啟與文件模型成本 |
| 公式 | `FormulaParseBenchmarks`、`FormulaEvaluationBenchmarks` | 公式剖析、1K／10K 全量重算、10K 線性鏈與寬 DAG、1% 交易式增量重算、大範圍及 100 × 100 陣列的配置量與延遲 |
| 協作 | `CollaborationOperationBenchmarks` | TDF JSON operation 剖析與重播 |

## 判讀規則

- 先看配置量，再看平均值；配置量突增通常比小幅平均值波動更值得追。
- 同一台機器上比較同一個 filter、同一個量測設定檔、同一個電源模式。
- 對微型基準測試結果保持保守；若 BenchmarkDotNet 提示 minimum iteration time 過短，請提高 `-IterationTime` 或資料量。
- CI 適合跑煙霧測試與回歸關卡，不適合以單一本機毫秒數作為跨平台硬門檻。
- 三格式標準工作負載與獨立子處理程序報告方法見 [ODS、ODT、ODP 標準效能基準](performance-standard-documents.md)。

公式完整矩陣以穩定設定檔執行：

```powershell
pwsh eng/Benchmark-Stable.ps1 -Filter "*FormulaEvaluationBenchmarks*"
```

BenchmarkDotNet 的 `MemoryDiagnoser` 同時記錄配置量與 Gen0／1／2；每公式配置量以
`Allocated / FormulaCount` 判讀。正式更新公式回歸基準前，必須在同一台穩定機器以
相同設定連跑三次並採中位數。時間容許 `+40%`、配置量容許 `+15%`；共享 CI runner
只執行固定小型工作負載，不把單次數值寫成公開效能宣稱。
