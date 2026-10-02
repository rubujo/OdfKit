# 效能基準線

本文件說明 OdfKit 的效能基準線、穩定量測設定檔與報告產生方式。基準測試結果受 CPU、記憶體、磁碟、作業系統、電源設定與 JIT 狀態影響，因此本文件記錄「如何量測」與「目前關卡」，不把單一本機輸出宣告為跨機器服務等級承諾。

ODS／ODT 串流 I/O 的維護決策、隨機存取邊界與安全依據見
[ODS／ODT 串流 Reader／Writer 設計與最佳化準則](streaming-reader-writer-design.md)。

## 目前回歸關卡

`eng/Benchmark-Regression.ps1` 會執行 `DomInsertBenchmarks.SequentialInsertAfter` 與
`OdsStreamWriterBenchmarks.WriteRows`，並與 `eng/baselines/performance-baselines.json` 比對。

| 基準測試 | 基準線平均值 | 容許範圍 | 用途 |
|-----------|---------------|-----------|------|
| `DomInsertBenchmarks.SequentialInsertAfter` | `123.9 us` | `+40%` | 偵測 DOM 循序插入效能的重大回歸 |
| `OdsStreamWriterBenchmarks.WriteRows` | 以 `eng/baselines/performance-baselines.json` 為準 | 時間 `+40%`、配置量 `+15%` | 保護 200,000 列雙欄 ODS 串流寫入的時間與配置量 |
| `OdfPackageLoadBenchmarks.LoadFileMmf`（128 KB） | 以 `eng/baselines/performance-baselines.json` 為準 | 時間 `+40%`、配置量 `+15%` | 保護主要檔案路徑封裝載入入口 |
| `FormulaEvaluationBenchmarks.FullRecalculation10000` | `223.9 ms`／`98.83 MB` | 時間 `+40%`、配置量 `+15%` | 保護 10K 獨立公式交易式全量重算 |
| `FormulaEvaluationBenchmarks.IncrementalOnePercentRecalculation10000` | `2.058 ms`／`1.82 MB` | 時間 `+40%`、配置量 `+15%` | 保護 10K 公式中 1% 受影響子圖的交易式增量重算 |

執行：

```powershell
pwsh eng/Benchmark-Regression.ps1 -Configuration Release
```

排程 CI 另傳入 `-ReportTimingRegression`：共享 runner 的耗時超標會保留明確 notice，
配置量超標仍為硬性失敗。本機穩定環境省略此參數時，耗時與配置量都維持硬閘門。

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

## 資源限制壓力測試

[載入與串流讀取安全限制](security-limits.md) 記錄的限制與「不得崩潰」的行為，由
`OdfKit.Benchmarks` 的壓力測試執行器轉為可執行檢查。BenchmarkDotNet 無法量測「壞了就整個
處理程序崩潰」的情境（堆疊溢位、記憶體耗盡），因此每個情境都在獨立子處理程序執行，崩潰即失敗。

```powershell
pwsh eng/Benchmark-Stress.ps1
```

| 情境 | 判定方式 | 保護的行為 |
|------|----------|------------|
| 逐列建立工作表（8,000、16,000、32,000 列） | 資料量放大 4 倍，耗時比值不得超過 `8.0`（線性約 4，二次方約 16） | 逐列附加不得每列全表重掃；修正前 8,000 列約 9.5 秒 |
| 運算子連鎖公式（4,000 與 16,000 項，256 KB 與 128 KB 堆疊執行緒） | 處理程序不得崩潰，且只能成功或被上限乾淨拒絕 | 公式遞迴堆疊餘量與運算子節點上限 |
| 100 萬個連續前置運算子 | 必須被乾淨拒絕 | 公式剖析巢狀深度上限 |
| 以引數為迴圈上限的公式（`CRITBINOM`、`COMBIN`、`FACT` 等 8 條） | 每條在 10 秒內結束 | 迴圈上限與溢位提早離開 |

縮放比與「不得崩潰」不受機器速度影響，因此這組檢查不需要基準線 JSON；絕對耗時只列印供參考。
它只在 `performance-benchmark.yml` 的排程與手動觸發時執行，不進入 PR 必跑的 CI。

跨套件寫入與讀取對比（MiniExcel、ClosedXML；[效能對比](performance-comparison.md)）耗時與記憶體都高，
只能在 `performance-benchmark.yml` 手動觸發並勾選 `run_competitive_comparison` 時執行，會在 Ubuntu 與
Windows 各跑一次完整的一百萬列寫入與讀取，並把輸出存為 `competitive-comparison-<os>` 產物（保留 14 天）。
讀取對比含內容檢查碼，不符即失敗。

## Schema 驗證耗時

`OdfPackageValidator` 對 ODF 1.4 schema 的驗證耗時隨工作表列數成長。`table:table` 的內容模型含巢狀重複
`oneOrMore(oneOrMore(table-row))`，外層每個起點的展開都會回傳與列數同階的位置集合。先前的實作對每個起點
重新驗證整個子樹，並反覆走過整棵語法樹，耗時隨列數呈平方成長：同一台機器上 2,000 列約 72 秒，依平方成長
推估 5,000 列超過 7 分鐘。

現在的實作以位元集合表示位置、記憶「由某位置起重複比對可到達的位置」與各節點的比對結果，並由大到小處理
前緣位置，使巢狀重複每個位置只做一次集合聯集：

| 列數 | 耗時（含約 3 秒的首次啟動成本） | 說明 |
|------|--------------------------------|------|
| 2,000 | 約 3.5 秒 | |
| 5,000 | 約 5 秒 | |
| 20,000 | 約 19 秒 | 峰值工作集約 380 MB |

數字是一般開發機（Intel Core i7-9750H）上的單次量測，不是基準線；CI 開啟覆蓋率並使用較慢的執行器，
實測慢十倍以上。剩餘的成長略高於線性，更大的工作表建議使用串流讀取器而不是完整驗證，完整驗證也會把
整份 DOM 載入記憶體。`RealWorldFidelityTests.SchemaValidationOfManyRowsCompletesInReasonableTime` 以寬鬆的
上限（4,000 列在 120 秒內）防止回到平方成長。

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
| 樞紐 | `OdfPivotCalculatedBenchmarks` | 10K 列一般彙總、計算欄位、數值分組、列百分比與雙軸總計的時間及配置量 |
| 協作 | `CollaborationOperationBenchmarks` | TDF JSON operation 剖析與重播 |

### ODS 可編輯 DOM 載入分層

`StandardOdsBenchmarks` 將一般 `SpreadsheetDocument.Load` 拆成三個不可混為單一
「開檔時間」的情境：

- `LoadComplexDomAndEnumerateSheets`：只建立外層 DOM 與列舉工作表中繼資料，工作表列維持 lazy。
- `LoadComplexDomAndReadFirstSheet`：載入後首次具現化第一張工作表。
- `LoadComplexDomAndReadLastSheet`：驗證定位最後一張工作表不會連帶具現化前面的工作表。

核心 XML entry 已完成 ZIP 大小與 CRC 驗證後，直接以既有 UTF-8 記憶體交給 span parser；
大型 `table:table` 保留來源 memory slice，避免 `ReadInnerXml()` 建立大型 UTF-16 字串後再
複製成 UTF-8 陣列。載入期的 LibreOffice 擴充屬性正規化延後至該 lazy subtree 首次
具現化，且多執行緒首次存取只允許單次具現化。完整儲存對未觸碰的 lazy subtree 會直接傳遞
原始 UTF-8；若 subtree 已具現化或需要 sparse cell 序列化，仍會走訪必要內容。`LoadOnly` 與
`LoadAndSaveUntouchedLazyDom` 必須分別量測，不能把前者宣稱為 round-trip 成本。

此設計借鑑 [Deflux](https://github.com/daniilvaino/Deflux) 將工作表發現與內容讀取分離的
產品邏輯，但不引用其實作，也不加入 Checkpoint／DEFLATE 狀態引擎。安全與資源邊界依
[Microsoft .NET ZIP 最佳實踐](https://learn.microsoft.com/dotnet/standard/io/zip-tar-best-practices)、
[Microsoft XML Reader 安全設定](https://learn.microsoft.com/dotnet/fundamentals/runtime-libraries/system-xml-xmlreadersettings)
及 [OASIS ODF 1.4 Package 標準](https://docs.oasis-open.org/office/OpenDocument/v1.4/part2-packages/OpenDocument-v1.4-os-part2-packages.html)
維持 entry 數量／大小、解壓總量、DTD、resolver、XML 字元數、巢狀深度與路徑防護。

相同核心路徑也服務 ODT、ODP 與 ODG：大型 `text:p`／`text:list`、內嵌表格及
styles／meta／settings subtree 都保留 UTF-8 lazy slice。`StandardOdtBenchmarks` 另以
`LoadLargeParagraphAndEnumerateParagraphs` 與 `LoadLargeParagraphAndReadText` 分離 ODT
外層載入和首次段落內容存取；ODP／ODG 的投影片／頁面索引只列舉外層 `draw:page`，不會
因建立集合而讀取其大型段落內容。四格式共同的快速 parser 必須拒絕 DTD／其他 XML markup
declaration，並將 lazy payload 計入呼叫端指定的 `MaxXmlCharactersInDocument`；首次具現化
沿用原始 `StrictXmlParsing` 與字元上限，不可退回較寬鬆的預設值。

執行緒邊界維持「單一文件不保證無鎖並行讀寫」：不同文件可平行處理，同一 lazy subtree、
ODS worksheet facade 與 row/cell sparse cache 的首次發布具備同步保護；同一文件的多個儲存
操作會序列化，但 DOM Children／Attributes、投影片／繪圖頁面集合仍不得與修改或儲存並行。

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
