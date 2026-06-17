# OdfKit 效能優化計畫

本檔案記錄效能走查結論與可落地優化項目，與
[`OdfKit-Completeness-Plan.md`](OdfKit-Completeness-Plan.md) 並列維護。

## 走查結論（已驗證 ✅）

| 項目 | 判定 | 證據 |
|------|------|------|
| 公式 `ref struct` + `ReadOnlySpan<char>` 剖析 | ✅ 正確 | `FormulaParser`、`Tokenizer`、`FormulaParserToken` |
| `OdfXmlReader` 使用 `StringPool` 共用標籤字串 | ✅ 正確 | `OdfXmlReader.cs` |
| `OdsStreamWriter` 流式寫入 + `NonSeekableStreamWrapper` | ✅ 正確 | `OdsStreamWriter.cs`、`NonSeekableStreamWrapper.cs` |
| ZIP / XML 安全上限（Zip bomb、深度、XXE） | ✅ 正確 | `OdfPackageZipLoader`、`OdfXmlReader` |
| `InsertBefore`/`InsertAfter` 使用 `List.IndexOf` 為 O(N) | ✅ 正確 | `OdfNode.Tree.cs` |
| `OdfAttributeName.GetHashCode` 使用 XOR | ✅ 正確（可改進） | `OdfNodeType.cs` |
| `TextContent` 讀取使用 `StringBuilder` 串接 | ✅ 正確 | `OdfNode.cs` |
| ZIP entry 讀取 `MemoryStream` + `ToArray()` 雙重配置 | ✅ 正確 | `OdfPackageZipLoader.cs` |

**備註**：報告中 StringPool「減少 80% 記憶體」為質性估計，repo 內無基準測試數據；測試數量以 `net8.0` 為 1265 通過 + 5 略過（與 net10.0 相近）。

## Phase PERF-1 — 低風險快速優化（✅ 本輪）

| ID | 變更 | 狀態 |
|----|------|------|
| PERF-1a | `OdfAttributeName` 改用 `OdfHashing.Combine`（跨 TFM 安全雜湊） | ✅ |
| PERF-1b | `TextContent` 單一／零子節點快速路徑 | ✅ |
| PERF-1c | ZIP entry 直接讀入 `byte[]`，避免 `MemoryStream.ToArray()` 額外拷貝 | ✅ |
| PERF-1d | 子節點 `SiblingIndex` 快取，`InsertBefore`/`InsertAfter` 常見情況 O(1) | ✅ |

驗收：`dotnet test` 全綠；`OdfNodePerformanceTests`。

## Phase PERF-2 — 中期（planned）

| ID | 變更 | 說明 |
|----|------|------|
| PERF-2a | `TextContent` 提供 `TryWriteTextContent(IBufferWriter<char>)` | 高頻讀取零配置 API |
| PERF-2b | 大型 DOM 改雙向鏈結或 `LinkedList<OdfNode>` | 需全面評估與 schema 產生器相容性 |
| PERF-2c | ZIP 讀取 `ArrayPool<byte>` 過渡緩衝 | 當 entry 必須複製為自有陣列時減少 LOH 暫存 |

## Phase PERF-3 — 基準與監控（planned）

- 新增 `OdfKit.Benchmarks` 或 `eng/Benchmark-*.ps1`（公式剖析、XML 載入、百萬列 `OdsStreamWriter`）
- CI 可選效能回歸門檻（非阻擋性）

## 全程約束

- 雙 TFM `net10.0` + `netstandard2.0` 須通過
- 安全上限（`MaxZipEntries`、`MaxElementDepth` 等）不可為效能讓步
- 提交前 `pwsh eng/Format-Safe.ps1`