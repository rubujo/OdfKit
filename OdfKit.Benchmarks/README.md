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

## Result policy

Benchmark 結果受 CPU、記憶體、磁碟、OS、電源設定與 JIT 狀態影響，不提交本機輸出作為固定
SLA。CI 只應執行 smoke tests，驗證大型 collaboration operations 可完成且報告狀態正確，不應以
固定毫秒或記憶體門檻作為單元測試條件。
