# OdfKit 基準測試

本專案使用 BenchmarkDotNet 量測 OdfKit 的效能與記憶體行為。

## 協作操作基準測試

`CollaborationOperationBenchmarks` 覆蓋：

- `Parse_10kOperations`
- `Serialize_10kOperations`
- `Replay_10kTextOperations`
- `Replay_LongParagraphRangeFormatting`
- `Replay_FixedSizeLargeTable`

執行範例：

```powershell
dotnet run -c Release --project OdfKit.Benchmarks -- --filter *CollaborationOperationBenchmarks*
```

## ODT 來回讀寫基準測試

`OdtRoundTripBenchmarks` 覆蓋大型 ODT 建立、儲存與載入。

## 試算表儲存格存取基準測試

`OdfTableSheetCellAccessBenchmarks` 覆蓋 `OdfTableSheet.GetCell` 逐格填值情境（例如以巢狀
迴圈依列／欄索引逐一建立儲存格），用於驗證 `OdfTableSheetDomAccessEngine` 的列／儲存格節點
存取快取是否維持攤提低複雜度，避免每次呼叫都重新掃描整表。

執行範例：

```powershell
dotnet run -c Release --project OdfKit.Benchmarks -- --filter *OdfTableSheetCellAccessBenchmarks*
```

## 其他基準測試類別

- `DomInsertBenchmarks`：DOM 節點循序插入效能。
- `DomTextContentBenchmarks`：文字內容讀取與寫入緩衝區效能。
- `FormulaParseBenchmarks`：公式剖析器（簡單與複雜運算式）效能。
- `OdfPackageLoadBenchmarks`：ODF 封裝載入效能。
- `OdsStreamWriterBenchmarks`：`OdsStreamWriter` 大量列寫入效能，見類別內建 XML 文件說明。

## 穩定本機量測設定檔

正式比較效能變更時，優先使用較長、以時間為導向的量測設定檔：

```powershell
pwsh eng/Benchmark-Stable.ps1 -Filter "*OdsStreamWriter*"
```

此量測設定檔預設使用 BenchmarkDotNet `Medium` job、`250 ms` iteration time、`9` 到 `15`
次量測迭代，並開啟記憶體與例外欄位。它比煙霧測試指令更慢，但較能避免極短迭代
造成的量測雜訊。

## 基準線報告

需要產生可附在 pull request 或發行說明的 Markdown 摘要時，使用：

```powershell
pwsh eng/Benchmark-BaselineReport.ps1 -Filter "*OdsStreamWriter*"
```

報告會包含穩定量測設定檔指令、目前回歸關卡基準線，以及 BenchmarkDotNet
GitHub 摘要。長期基準政策請見 [效能基準線](../docs/performance-baselines.md)。

## 結果政策

Benchmark 結果受 CPU、記憶體、磁碟、OS、電源設定與 JIT 狀態影響，不提交本機輸出作為固定
SLA。CI 只應執行煙霧測試，驗證大型協作操作可完成且報告狀態正確，不應以
固定毫秒或記憶體門檻作為單元測試條件。
