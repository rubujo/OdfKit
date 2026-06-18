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

## Phase PERF-2 — 中期（✅）

| ID | 變更 | 狀態 |
|----|------|------|
| PERF-2a | `TryWriteTextContent(IBufferWriter<char>)` + `WriteTextContentTo` 共用邏輯 | ✅ |
| PERF-2b | `OdfNodeChildList` 雙向鏈結子節點；插入／移除 O(1) + 延遲索引快取 | ✅（見 PERF-5a） |
| PERF-2c | ZIP 讀取 `ArrayPool<byte>` 過渡緩衝 + 可成長讀取 | ✅ |

## Phase PERF-3 — 基準與監控（✅）

| ID | 變更 | 狀態 |
|----|------|------|
| PERF-3a | `eng/Benchmark-Performance.ps1` 測試子集計時 | ✅ |
| PERF-3b | `OdfKit.Benchmarks`（BenchmarkDotNet） | ✅ |
| PERF-3c | `eng/Benchmark-Regression.ps1` + 基準線 JSON；CI `performance-benchmark.yml`（非阻擋） | ✅ |

## Phase PERF-5 — 深度最佳化路線圖（2026-06-18 評估報告）

> **效能結論（報告驗證 ✅）**：1277 項測試約 1 分 20 s 全綠，核心庫效能已屬健康；先前 PERF-4 關鍵瓶頸已根除。

| ID | 報告項 | 正確性判定 | 實作策略 | 狀態 |
|----|--------|------------|----------|------|
| **5a** | A. 雙向鏈結 DOM（PERF-2b） | ✅ 正確：`List.Insert` 仍為 O(N)；`SiblingIndex` 僅加速定位 | `OdfNodeChildList` + `FirstChild`/`NextSibling` 指標；`IList` + `Find` 相容 | ✅ |
| **5b** | B. JIT `AggressiveInlining` | ✅ 正確但效益質性（約 5–10% 微觀）；過度標註會膨脹 IL | 僅標註 `OdfHashing.Combine`、`TryCoerceDouble` 等極熱路徑 | ✅ |
| **5c** | C. 執行緒專屬 `StringPool` | ✅ 正確：單次 `Parse` 內池化有效，跨檔案需執行緒池 | `ThreadLocal` + 4096 次重置，避免無界成長 | ✅ |
| **5d** | D. SIMD 統計累加 | ⚠️ 部分正確：僅連續 `double[]`/`object[,]` 純數值陣列可向量化的；混合型別仍走原路徑 | `SUM`/`AVERAGE`/`COUNT` + `SUMIF`/`COUNTIF` 純數值等號快速路徑 | ✅ |
| **5e** | E. Native AOT 裁剪相容 | ✅ 方向正確；完整 AOT 需大量後續（Source Generator、trim root） | `IsTrimmable`、`TrimSmoke`、`BouncyCastle` AOT 標記、工廠註冊表 | 🔶 深化中 |

**5a 取捨**：隨機索引 `Children[i]` 在快取失效後需 O(n) 重建；插入／刪除為 O(1)。試算表連續 `InsertAfter` 為主要受益場景（`DomInsertBenchmarks`）。

**5a 基準**（`DomInsertBenchmarks.SequentialInsertAfter`，2000 列、`Release`）：**~124 µs／次**、配置 ~641 KB／次（2026-06-18 本機）。

**5d 限制**：`SUMIF`/`COUNTIF` 快速路徑僅涵蓋純數值矩陣 + 等號條件；比較運算子與字串條件仍走 `CriteriaMatcher`。

**5e 後續**：`PublishAot` 全量驗證；未註冊類型反射後備；XML Source Generator；BouncyCastle 靜態演算法註冊或整包保留。

驗收：`dotnet test` 全綠；`OdfNodePerformanceTests`、`DomInsertBenchmarks`。

## Phase PERF-4 — 全案掃描報告驗證與修正（✅ 完成）

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
| C. `OdfToXlsxConverter.ScanCellValues` 全表字典 | ✅ 已修正 | `EnumerateSheetCells` 流式寫入 | **4g** ✅ |
| D. `OdfToDocxConverter.LoadStyles` 全樹 Descendants | ✅ 已修正 | 限縮 `office:styles` 區段 | **4h** ✅ |
| E. `OdfAttributeName` XOR 雜湊 | ❌ 報告過時 | 已為 `OdfHashing.Combine` | — |

### 3. 架構冗餘與雙重 DOM

| 報告項 | 驗證結果 | 現況 | PERF-4 ID |
|--------|----------|------|-----------|
| A. `OdfHtmlExporter` AngleSharp 雙重 DOM | ✅ 問題成立 | 匯出仍建立完整 AngleSharp 樹 | **4e** 改 `StringBuilder` 直寫 |
| B. `OdfChartRenderer` ToList/ToArray | ✅ 已修正 | `EnumerateDrawFrames`、預先配置陣列 | **4f** ✅ |

### 4. 穩健性與安全

| 報告項 | 驗證結果 | 現況 | PERF-4 ID |
|--------|----------|------|-----------|
| A. `OdfXmlWriter.WriteNode` 遞迴 StackOverflow | ✅ 問題成立 | 讀取有 `MaxElementDepth=256`，寫入無對稱防護 | **4i** 寫入深度限制 |
| B. `CollectNamespaces` 遞迴 StackOverflow 風險 | ✅ 已修正 | 迭代式 `Stack` 收集 + 根元素批次宣告 xmlns | **4j** ✅ |
| C. Argon2id `argon2P=4` 執行緒飢餓 | ✅ 已緩解 | `SemaphoreSlim` 閘道 + 平行度上限 | **4k** ✅ |

### PERF-4 實作優先序

| 優先 | ID | 變更 | 狀態 |
|------|-----|------|------|
| 1 | 4e | HTML 匯出移除 AngleSharp，改直寫 + `HtmlEncode` | ✅ |
| 2 | 4a | `FlattenValues` Stack 迭代（單一狀態機） | ✅ |
| 2 | 4b | GCM 解密單次配置 | ✅ |
| 2 | 4c | 點陣圖匯出略過 `OdfCell` 配置 | ✅ |
| 3 | 4d | 樣式屬性 Rebuild 展平快取 | ✅ |
| 4 | 4i | `OdfXmlWriter` 深度限制 | ✅ |
| 6 | 4f | 圖表 `EnumerateDrawFrames`、預先配置陣列 | ✅ |
| 6 | 4j | XmlWriter 迭代式命名空間收集、根元素批次 xmlns | ✅ |
| 6 | 4k | Argon2 `SemaphoreSlim` 併發閘道 | ✅ |
| 5 | 4g | ODS→XLSX `EnumerateSheetCells` 流式寫入；樞紐表 `TryGetCellAt` | ✅ |
| 5 | 4h | ODT→DOCX 限縮 `office:automatic-styles`／`office:styles` 走訪 | ✅ |

驗收：`dotnet test` 全綠；`HtmlExportTests`、`EncryptionTests`、`OdfNodePerformanceTests`。

## 全程約束

- 雙 TFM `net10.0` + `netstandard2.0` 須通過
- 安全上限（`MaxZipEntries`、`MaxElementDepth` 等）不可為效能讓步
- 提交前 `pwsh eng/Format-Safe.ps1`