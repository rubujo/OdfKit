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
| `InsertBefore`/`InsertAfter` 子節點定位 | ⚠️ 已緩解 | `SiblingIndex` 快取（PERF-1d）；`List.Insert` 仍為 O(N) |
| `OdfAttributeName.GetHashCode` XOR 碰撞 | ✅ 已修正 | PERF-1a 改用 `OdfHashing.Combine` |
| ZIP entry `MemoryStream.ToArray()` 雙重配置 | ✅ 已修正 | PERF-1c/2c 直接 `byte[]` + `ArrayPool` 緩衝 |

**備註**：報告中 StringPool「減少 80% 記憶體」為質性估計，repo 內無基準測試數據。

## Phase PERF-1 — 低風險快速優化（✅）

| ID | 變更 | 狀態 |
|----|------|------|
| PERF-1a | `OdfAttributeName` 改用 `OdfHashing.Combine`（跨 TFM 安全雜湊） | ✅ |
| PERF-1b | `TextContent` 單一／零子節點快速路徑 | ✅ |
| PERF-1c | ZIP entry 直接讀入 `byte[]`，避免 `MemoryStream.ToArray()` 額外拷貝 | ✅ |
| PERF-1d | 子節點 `SiblingIndex` 快取，`InsertBefore`/`InsertAfter` 常見情況 O(1) 定位 | ✅ |

驗收：`dotnet test` 全綠；`OdfNodePerformanceTests`。

## Phase PERF-2 — 中期（✅，2b 延後）

| ID | 變更 | 狀態 |
|----|------|------|
| PERF-2a | `TryWriteTextContent(IBufferWriter<char>)` + `WriteTextContentTo` 共用邏輯 | ✅ |
| PERF-2b | 大型 DOM 改雙向鏈結或 `LinkedList<OdfNode>` | ⏸️ 延後（PERF-1d 已涵蓋常見路徑） |
| PERF-2c | ZIP 讀取 `ArrayPool<byte>` 過渡緩衝 + 可成長讀取 | ✅ |

## Phase PERF-3 — 基準與監控（✅）

| ID | 變更 | 狀態 |
|----|------|------|
| PERF-3a | `eng/Benchmark-Performance.ps1` 測試子集計時 | ✅ |
| PERF-3b | `OdfKit.Benchmarks`（BenchmarkDotNet） | ✅ |

後續可選：CI 非阻擋性回歸門檻（比對上次基準輸出）。

## Phase PERF-4 — 全案掃描報告驗證與修正（✅ 本輪，4g 等延後）

以下對照「終極完滿版」掃描報告逐項驗證現況（2026-06-18）。

### 1. 記憶體管理與 GC

| 報告項 | 驗證結果 | 現況 | PERF-4 ID |
|--------|----------|------|-----------|
| A. `FlattenValues` 巢狀 yield | ✅ 問題成立 | 遞迴 `yield` 仍會配置多層狀態機 | **4a** 改 Stack 迭代 |
| B. AES-GCM 解密雙重配置 | ✅ 問題成立 | `output` + `decrypted` 兩次配置 | **4b** 單次配置並裁切 |
| C. `OdfImageExporter` 每格 `new OdfCell` | ✅ 問題成立 | 雙重迴圈仍配置包裝器 | **4c** 直接讀節點 |
| D. ZIP `MemoryStream.ToArray()` | ❌ 報告過時 | PERF-1c/2c 已改 `ReadEntryBytes` + `ArrayPool` | — |

### 2. 演算法與時間複雜度

| 報告項 | 驗證結果 | 現況 | PERF-4 ID |
|--------|----------|------|-----------|
| A. `OdfStyleEngine.FindPropertyInStyleNode` O(N) | ✅ 問題成立 | 每次查詢遍歷子節點 | **4d** Rebuild 時展平快取 |
| B. `InsertBefore`/`InsertAfter` IndexOf | ⚠️ 部分成立 | PERF-1d 快取索引；`List.Insert` 仍 O(N) | 2b 延後 |
| C. `OdfToXlsxConverter.ScanCellValues` 全表字典 | ✅ 問題成立 | 仍一次載入 `Dictionary<(Row,Col),CellData>` | **4g** 延後（大改） |
| D. `OdfToDocxConverter.LoadStyles` 全樹 Descendants | ✅ 問題成立 | 三輪 `root.Descendants()` | **4h** 延後（限縮走訪區段） |
| E. `OdfAttributeName` XOR 雜湊 | ❌ 報告過時 | 已為 `OdfHashing.Combine` | — |

### 3. 架構冗餘與雙重 DOM

| 報告項 | 驗證結果 | 現況 | PERF-4 ID |
|--------|----------|------|-----------|
| A. `OdfHtmlExporter` AngleSharp 雙重 DOM | ✅ 問題成立 | 匯出仍建立完整 AngleSharp 樹 | **4e** 改 `StringBuilder` 直寫 |
| B. `OdfChartRenderer` ToList/ToArray | ✅ 問題成立 | `Descendants().ToList()`、多處 `.ToArray()` | **4f** 延後（ScottPlot API 限制） |

### 4. 穩健性與安全

| 報告項 | 驗證結果 | 現況 | PERF-4 ID |
|--------|----------|------|-----------|
| A. `OdfXmlWriter.WriteNode` 遞迴 StackOverflow | ✅ 問題成立 | 讀取有 `MaxElementDepth=256`，寫入無對稱防護 | **4i** 寫入深度限制 |
| B. `CollectNamespaces` 額外全樹走訪 | ✅ 問題成立 | 寫入前仍預掃 namespace | 4j 延後（動態宣告） |
| C. Argon2id `argon2P=4` 執行緒飢餓 | ⚠️ 情境成立 | 預設來自檔案 metadata；高併發解密可能競爭 ThreadPool | 4k 延後（併發閘道） |

### PERF-4 實作優先序

| 優先 | ID | 變更 | 狀態 |
|------|-----|------|------|
| 1 | 4e | HTML 匯出移除 AngleSharp，改直寫 + `HtmlEncode` | ✅ |
| 2 | 4a | `FlattenValues` Stack 迭代（單一狀態機） | ✅ |
| 2 | 4b | GCM 解密單次配置 | ✅ |
| 2 | 4c | 點陣圖匯出略過 `OdfCell` 配置 | ✅ |
| 3 | 4d | 樣式屬性 Rebuild 展平快取 | ✅ |
| 4 | 4i | `OdfXmlWriter` 深度限制 | ✅ |
| — | 4f/4j/4k | 圖表陣列、動態 xmlns、Argon2 閘道 | 延後 |
| 5 | 4g | ODS→XLSX `EnumerateSheetCells` 流式寫入；樞紐表 `TryGetCellAt` | ✅ |
| 5 | 4h | ODT→DOCX 限縮 `office:automatic-styles`／`office:styles` 走訪 | ✅ |

驗收：`dotnet test` 全綠；`HtmlExportTests`、`EncryptionTests`、`OdfNodePerformanceTests`。

## 全程約束

- 雙 TFM `net10.0` + `netstandard2.0` 須通過
- 安全上限（`MaxZipEntries`、`MaxElementDepth` 等）不可為效能讓步
- 提交前 `pwsh eng/Format-Safe.ps1`