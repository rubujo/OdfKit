# OdfKit Benchmarks

本專案使用 BenchmarkDotNet 量測 OdfKit 的效能與記憶體行為。

## Collaboration benchmarks

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

## ODT round-trip benchmarks

`OdtRoundTripBenchmarks` 覆蓋大型 ODT 建立、儲存與載入。

## Table sheet cell access benchmarks

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

## Result policy

Benchmark 結果受 CPU、記憶體、磁碟、OS、電源設定與 JIT 狀態影響，不提交本機輸出作為固定
SLA。CI 只應執行 smoke tests，驗證大型 collaboration operations 可完成且報告狀態正確，不應以
固定毫秒或記憶體門檻作為單元測試條件。
