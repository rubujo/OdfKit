# OdfKit 底層 API 設計優化與效能重構計畫

> [!NOTE]
> **本檔為「修訂版」**：以最新網路資料（OASIS ODF 1.4 規格、LibreOffice Wholesome Encryption、.NET 10 硬體內建函式文件、Intel/Arm CRC 指令集說明）查證原計畫後，已就下列技術問題逐項修正，並在對應段落以括號標註更正理由。

## 勘誤與修正摘要 (Errata)

| # | 位置 | 原始問題 | 修正 |
| --- | --- | --- | --- |
| 1 | 決策 52、61、模組 2 CRC 段 | 宣稱以 **SSE4.2** 加速 ZIP 的 CRC32 | SSE4.2 `crc32` 僅算 CRC-32C/Castagnoli，與 ZIP 的 CRC-32/ISO-HDLC（0x04C11DB7）不相容。x86 改用 **PCLMULQDQ**；ARM64 改用 \\*\\*`Crc32`\\*\\* 類別 |
| 2 | 決策 61 | ARM64 以 **`Dp`** 指令做 CRC32 | `Dp` 為 DotProduct（點積）類別，與 CRC 無關；正確為 `System.Runtime.Intrinsics.Arm.Crc32` |
| 3 | 決策 49 | 使用 `Vector128<char>` / `Vector256<char>` | .NET 的 `Vector128<T>`／`Vector256<T>` 不支援 `char`；改用 `Vector256<byte>` 於 UTF-8 位元組層級掃描 |
| 4 | 決策 36、45、模組 2 並行解壓段 | 宣稱對「單一 content.xml 並行解壓多個 Deflate Block」 | 單一 DEFLATE 串流本質順序，無法隨機並行；改為「跨多個獨立 Entry 並行」，並註明需 flush 邊界才可單 Entry 內並行 |
| 5 | 決策 66、91 等 In-place Patch | 「以空白字元填充原位修補」未說前提 | 僅 **STORED（未壓縮）** Entry 成立；DEFLATE 壓縮的 `content.xml` 須走末尾追加 + 更新 Central Directory 路徑 |
| 6 | 前言 WARNING、決策 18/77 | ODF 1.4 加密只談 AES-GCM，漏 **Argon2** | ODF 1.4 Wholesome Encryption 標準為 **AES-256-GCM + Argon2id KDF**；AES-CBC + PBKDF2-HMAC-SHA1 僅為向後相容 Fallback |
| 7 | 決策 86 | 「`System.Numerics.Tensors` 提升百倍」 | `TensorPrimitives` 確有 SIMD 加速，但「百倍」為理論上限；改述為數倍至約一個數量級 |
| 8 | 決策 60、67 與 Direct I/O 實作 | `GC.AllocateArray<byte>(pinned: true)` 被誤認為可保證 Direct I/O 所需的 4096 位元組對齊 | `GC.AllocateArray` 僅保證固定，不保證指定對齊；`net10.0` Direct I/O 緩衝區須使用 `NativeMemory.AlignedAlloc(size, 4096)` 包裝為 `MemoryManager<byte>`，再搭配 `File.OpenHandle` 與 `RandomAccess` |
| 9 | 沙盒事務防護 | 交易日誌建立失敗只記錄警告並繼續 | 交易日誌是 rollback 正確性的前提；建立或落盤失敗時必須 fail-fast，立即拋出本地化 `IOException`，禁止降級為無 journal 交易 |
| 10 | 決策 48 與 XML parser 整合 | 自訂 parser 被描述為完全取代 `XmlReader` | .NET 目前沒有 `Span<T>` / `Memory<T>` 版 `XmlReader`；快速路徑可作為 `net10.0` 後續候選，但須保留 `XmlReader.Create(..., DtdProcessing.Prohibit)` 安全 fallback。U8XmlParser 已封存，導入前需另做維護風險評估 |

**查證為正確、未更動的主張**：`AesGcm` 在 `netstandard2.0` 不支援（以 BouncyCastle `GcmBlockCipher` fallback 可行）、.NET 10（2025-11 GA、LTS）、POH、Direct I/O 磁區對齊（512/4096）、`calcext` 條件格式命名空間。唯 `GC.AllocateArray(pinned:true)` 僅能避免 GC 移動，不保證 Direct I/O 所需對齊；`WriteFileGather`（決策 85）尚須注意每個緩衝段須恰為一個系統頁大小，文件未提，實作時請留意。

> 其餘大量未佐證的效能倍數宣稱（5\~10 倍、50%\~90% 等）保留原文，惟應視為「理論／預期」目標，須以實機 Benchmark 佐證。

---


本計畫旨在整合底層 API 友善度與效能問題，提出一套系統性的重構規劃。主要重構與優化方向包含：

## User Review Required

> [!IMPORTANT]

> **作業系統 Direct I/O 磁區對齊限制 (Sector Alignment)**

> 經網路資料與 OS API 驗證，Windows 的 `FILE_FLAG_NO_BUFFERING` 與 Linux 的 `O_DIRECT` 雖然可以繞過核心快取直接寫入，但有極其嚴格的磁區對齊限制（如寫入長度與 Offset 必須為 512 或 4096 位元組的整數倍）。在實作 `OdsStreamWriter` 與 `OdfPackage` 的雙緩衝分頁 DMA 寫入時，若資料長度不對齊，底層將拋出 I/O 異常。我們將在實作中確保緩衝區大小為 4096 的整數倍，並於檔案末尾非對齊段落自動退回常規緩衝寫入。

> [!WARNING]

> **AES-GCM 加密相容性與雙目標架構注意 (Encryption Interoperability & Multi-TFM)**

> 決策 77 中引進的 **AES-GCM 認證加密模式**，經最新 ODF 規格與 LibreOffice "Wholesome Encryption" 的最佳實踐驗證，確實提供了高強度的完整性校驗與防篡改保護。但由於其為較新的加密規範，早期版本或其他不完全相容 ODF 1.4 的辦公軟體可能無法正常開啟此模式加密的檔案。需特別說明：ODF 1.4「Wholesome Encryption」的標準安全加密實為 AES-256-GCM 搭配 Argon2id 金鑰衍生函式 (KDF)，AES-GCM 提供完整性與防篡改、Argon2id 抵禦暴力破解。本計畫將「AES-256-GCM + Argon2id」作為符合 ODF 1.4 的安全選項，並保留舊版相容的 AES-256-CBC + PBKDF2-HMAC-SHA1 作為預設 Fallback，以確保最佳向後相容性。

> 此外，由於 `System.Security.Cryptography.AesGcm` 在 `netstandard2.0` 底下並不原生支援，我們將使用條件編譯：在 `net10.0` 分支下調用 .NET 10.0 原生的硬體加速 `AesGcm`；在 `netstandard2.0` 分支下則自動 Fallback 到核心相依套件 `BouncyCastle.Cryptography` 的 `GcmBlockCipher` 實作，以確保編譯完全通過且維持跨平台正確性。

> [!NOTE]

> **calcext 條件格式化相容性 (Conditional Formatting Extensions)**

> 決策 82 引進的 Data Bars 與 Icon Sets 條件格式化，於 ODF 規格中被收錄於 `xmlns:calcext` 的擴充命名空間中。此功能與 LibreOffice 全面相容，但在不支援此延伸擴充的第三方編輯器中可能會被忽略。這符合無損 Round-Trip 的相容性預期。

1. **生成代碼拆分與建構子優化**：將 74MB 單一生成大檔拆分為多個細粒度檔案，並為所有元素提供無參數建構子與接收子節點的參數陣列建構子，以支援「函數式建構（Functional Construction）」語意。

2. **流式寫入 API 重構與混合輸出**：導入步驟式 Fluent 狀態機，並新增 `WriteNode` 方法以支援在流式寫入中直接輸出複雜的 DOM 子樹。

3. **流式寫入的高速直接輸出與異步非阻塞設計**：

    - 實作 `IAsyncDisposable` 支援 `await using`，並全面提供基於 `ValueTask` 的異步寫入方法。

    - 針對確定性 XML 標籤，繞過內建 `XmlWriter` 檢查，直接以預先編譯之 UTF-8 位元組（`byte[]`）寫入 Stream，取得極致吞吐量。

    - **流式寬高自動去重**：列寬與行高在寫入時自動快取比對並複用，壓縮產生的 XML 體積。

4. \*\*流式讀取器與資料庫批量導入 (DbDataReader 對接)\*\*：將 `OdsStreamReader` 改為實作 `System.Data.Common.DbDataReader` 介面，使其可直接作為 `SqlBulkCopy.WriteToServer` 等工具的資料源，實現零中介 Heap 分配的超高速資料導入。

5. **Sylvan 高性能 CSV 流式轉檔**：於 `OdsStreamWriter` 中整合 `Sylvan.Data.Csv`，提供 `WriteCsvStreamAsync`，直接讀取 CSV 流並零分配、非阻塞地流式寫出為 ODS 試算表，記憶體恆定小於 1MB。

6. **底層型別與列舉優化**：為開放域屬性設計「智慧列舉結構（Smart Enum）」，並為 `OdfColor` 與 `OdfLength` 提供隱式轉換與擴充方法。

7. \*\*DOM 操作終極便利性優化 (LINQ 與索引器最佳實踐)\*\*：引進 LINQ 友善之強型別尋覽擴充、試算表座標索引器（Excel-like Indexer）、強型別智慧數值屬性，以及自動樣式去重設定器。

8. \*\*樣式保留的文字替換引擎 (Format-preserving Text Replacer)\*\*：引進高級文字替換機制，在精準替換關鍵字（如 `[Company]`） the same time，自動於 XML 節點層面進行局部拆分，100% 完整保留文字原有的粗體、斜體、字色等格式。

9. \*\*DOM 延遲解析與按需載入 (Lazy Loading)\*\*：大文件載入時僅建立結構骨架，存取特定節點時才延遲解析為 DOM，節省記憶體。

10. **大批次 DOM 修改與公式延遲計算鏈**：

    - 引入批次更新模式，於區塊內暫停所有內部樣式重對照與快取重算。

    - 公式計算引入 **延遲計算鏈 (Lazy Calculation Chain)** 與 \*\*拓撲排序 (Topological Sort)\*\*，變更數值時僅傳播 Dirty 標記，讀取或存檔時才按需計算。

11. \*\*試算表 DOM 稀疏儲存與按需具現化 (Sparse Storage & On-demand Instantiation)\*\*：在 `TableTableElement` 底層，將大量單元格的原始數值與樣式改以緊湊的連續陣列儲存，只有在開發者明確透過索引器存取時，才按需具現化 Cell 節點，大幅減少數百萬個小物件產生的 GC 暫停延遲。

12. \*\*跨文件節點快速採納 (AdoptNode)\*\*：於 `OdfDocument` 中實作 `AdoptNode` 機制。跨文件轉移或合併子樹（如 Table 或 Paragraphs）時，直接更新節點的 Document 所有權與雙向指標，實現 $O(1)$ 的零分配移轉，避免 `CloneNode` 的 GC 壓力。

13. \*\*批量資料對接與高性能灌入 (Bulk Data Import)\*\*：於 `TableTableElement` 中提供 `ImportData` 方法，直接接受 `DbDataReader`、`DataTable` 與 `IEnumerable<T>`，自動高效映射並大量灌入單元格。

14. **預填充名稱表 (XmlNameTable) 優化**：於 DOM 解析器初始化時，將 ODF 1.4 標準常用節點與屬性名稱預先注入自訂的 `XmlNameTable` 中，徹底消除 XML 解析時重複標籤名稱的 String Allocation，優化 GC 效能。

15. \*\*流式範本合併引擎 (Streaming Mail Merge)\*\*：於流式寫入器中內建異步流式範本解析與替換機制，大批量套印時免除 DOM 載入，維持小於 1MB 的記憶體佔用。

16. **智慧一鍵式異步簽章與硬體加速**：提供一鍵簽章 API，底層自動計算 Zip Entry 哈希並依 ODF/XAdES 規範組裝 XML 簽章樹。於 .NET 10.0 下透過條件編譯啟用密碼學硬體加速與非同步執行。

17. \*\*高性能零分配設計 (Span & 條件編譯)\*\*：引進 `ReadOnlySpan<char>` / `ReadOnlyMemory<char>` 寫入多載，並在 `net10.0` 目標框架下透過條件編譯啟用 `ArrayPool` 快取與 Span 寫入。

18. **自動文字標記與罕見字字型解析**：文字配置自動將跳格、換行與多空白轉為 ODF 特用節點；自動偵測 CJK 增補平面與 PUA 自造字，自動補全 font-face 宣告。

19. **自動 Package 媒體與 Namespace 管理**：媒體節點支援直接綁定二進位，自動寫入 Zip Entry 並綁定 `xlink:href`；屬性操作自動匹配字首，且 Package 寫入 entry 時自動判定 MIME 並更新 manifest.xml。

20. **Debug 即時屬性值防錯驗證**：在非 Release 建置下，對不合規之屬性值進行即時規格檢查與警告。

21. **Raw Zip Entry 零重壓快取**：載入 ODF 時快取未修改 Entry 的壓縮二進位原始數據，存檔時直接二進位拷貝輸出，免除重新壓縮的 CPU 與記憶體開銷。

22. **零分配 UTF-8 直接寫入器**：針對動態屬性與 XML 節點，實作自定義寫入器，利用 `Utf8Formatter` 直寫 Span 緩衝區，避免中間字串生成。

23. **Fluent 行內富文本建構器**：於段落與單元格中提供鏈式 `InlineTextBuilder` API，支援強型別設定局部文字樣式，自動管理 `span` 節點與樣式去重。

24. **寫時複製 (COW) 樣式繼承**：複製樣式時共用定義，當局部修改時自動複製並產生新樣式，確保不影響其他共用節點，維護樣式池去重。

25. **自動欄寬估算**：流式寫入或批次導入時，底層內建無外部相依字元寬度估算器，自動依據單元格內容最大長度計算合理欄寬，防止內容被遮蔽或顯示為 `###`。

26. **DOM 結構相容性檢查器**：實作 `OdfDocument.Validate()` API，透過 Schema 規則表進行 DOM 拓撲校驗，產出帶有精準 XML 路徑的 Diagnostics 診斷報表。

27. **多執行緒流水線解析器**：基於 `System.Threading.Channels` 實作生產者-消費者流水線，並行解析單一巨大 `content.xml`，極速提升大檔案載入效能。

28. **可回收 DOM Wrapper 物件池**：引進可回收的 DOM 物件池與 `WeakReference` 機制，在高頻 DOM 尋覽與轉型中重複使用 wrapper 物件，將 GC 壓力降至零。

29. **書籤安全填值管理器**：實作 `OdfBookmarkManager`，提供 `doc.Bookmarks["Name"].Value` 安全賦值，無損替換書籤中間內容。

30. **一鍵目錄自動生成**：提供 `doc.InsertTableOfContents()`，自動遍歷所有標題節點並建立合規的目錄 XML 架構與超連結。

31. **Fluent 巢狀清單建構器**：提供 `paragraph.AppendList()` Fluent API，支援鏈式快速建構多級清單與 list-style。

32. **圖表資料自動繫結與同步**：提供 `OdfChartBuilder`，支援 `doc.AddChart()` 與 `chart.BindData()`，自動同步圖表 DOM 與其內嵌數據 ODS。

33. **Matrix3x2 幾何變換**：向量繪圖與圖片的 `Transform` 屬性直接對接 `System.Numerics.Matrix3x2` 結構，自動轉換為合規 SVG 仿射矩陣。

34. **跨文件公式引用與連結快取**：實作 `ExternalLinkManager` 快取，支援 `Func<string, OdfDocument>` 動態外部載入委派，實現跨檔案公式引用與重算。

35. **未知節點無損 Round-Trip 保障**：解析時將所有非標準/未定義節點保留為 `OdfUnknownElement`，在存檔時 100% 寫回以確保第三方擴充無損。

36. **並行隨機存取解壓**：利用 `MemoryMappedFile` 定位各壓縮 Entry 的區段，對多個獨立 Entry（如 `content.xml`、`styles.xml`、`Pictures/*`）並行解壓，將大檔案載入效能提升 2\~3 倍。（技術限制：單一 DEFLATE 串流本質順序，無法隨機並行解壓其中段；除非壓縮時預先寫入 flush 邊界並建立區塊索引，否則僅能跨 Entry 並行。）

37. **條件格式化配置器**：提供 `table.AddConditionalFormat()` 鏈式配置，底層自動產生相容的 XML 條件格式宣告並處理樣式去重。

38. **行列摺疊分組**：提供 `table.Rows.Group()`，自動建立與嵌套 `table-row-group` 節點以支援報表分組摺疊。

39. **渲染與 PDF 匯出抽象**：Core 定義 `IOdfRenderer` 介面與 `OdfRendererRegistry` 全域註冊點，允許以模組化擴充套件實現 `doc.ExportToPdf()` 與圖片渲染。

40. **SIMD 標籤高速掃描**：利用 `Vector128` / `Vector256` 硬體向量化指令比對 XML 標記，大幅提高大檔案詞法分析速度。

41. **零分配二進位轉碼快取**：直接在 `ReadOnlySpan<byte>` 階段比對常用標籤與屬性值享元快取，免除轉為新 string 的 Heap 分配。

42. **儲存格合併與覆蓋管理**：提供 `table.MergeCells()` 與 `table.UnmergeCells()` API，底層自動管理覆蓋儲存格 (`covered-table-cell`)，維護表格列二維對齊。

43. **合約修訂追蹤引擎**：提供 `doc.TrackChanges = true` 配置，底層修改自動轉為合規 `<text:tracked-changes>` 修訂標記，保留修改歷史。

44. **非受控/固定記憶體超大表格儲存**：超大表格的單元格數據儲存在 POH 或使用 NativeMemory，避免 LOH 碎裂與 GC 掃描負擔。

45. **零分配屬性二進位讀取器**：直接在 `ReadOnlySpan<byte>` 階段比對屬性名並利用 `Utf8Parser` 讀出強型別值，消除高頻 XML 屬性字串 Heap 分配。

46. **SIMD 向量化區間公式重算**：當公式引擎識別到連續儲存格區間的統計函式時，底層直接提取實體 `double` 陣列指標並利用 AVX2/AVX-512 指令集並行累加，大幅縮短評估時間。

47. **層疊樣式實質解析器與快取**：實作 ComputedStyle 層疊樣式解析，將實質最終樣式快取於 DOM 節點上，配合 Dirty 標記失效傳播，避免高頻樣式查詢時的遞迴 DOM 尋覽開銷。

48. **自訂零分配 XML 讀取器**：以超輕量、自定義的 UTF-8 XML 拉取解析器作為後續快速路徑候選，直接在 MMF Span 上拉取 Token；遇到 DTD、命名空間邊界、實體解析或未支援語法時，必須回退至 `XmlReader` 安全路徑。

49. **工作表二進位極速拷貝**：合併大表格工作表時免除 DOM 載入，直接依據二進位 Offset 區間將 `<table:table>` 段落快取拷貝至目標 ODS。

50. **Fluent 樣式混合與自動推導**：提供 `paragraph.ApplyStyle(style => style.InheritFrom(...))` 混合 API，底層享元池自動推導與生成 XML 繼承樣式。

51. **作業系統級 Direct I/O 與預讀**：Windows/Linux 平台下開啟無快取 I/O，結合 DMA 預讀，將巨型 ODF 磁碟讀寫速度推向物理極限。

52. **硬體加速 CRC32**：在 `net10.0` 目標框架下透過 x86/x64 的 `Pclmulqdq`（carry-less 乘法，即 `System.IO.Hashing.Crc32` 內部所用之加速法）與 ARM64 的 `Crc32` 硬體指令加速 Zip Entry 的 CRC-32/ISO-HDLC 計算，免除 Heap 分配並提升數倍校驗效能。（注意：x86 SSE4.2 的 `crc32` 指令僅計算 CRC-32C/Castagnoli，與 ZIP 所需的 ISO 多項式不相容，不可用於 ZIP CRC 加速。）

53. **主從/巢狀範本解算器**：範本套印支援巢狀 `#foreach` 解析，展開資料時自動執行多級行列嵌套分組、垂直儲存格合併及 SUBTOTAL 小計公式自動插入。

54. **安全範本欄位雙向提取器**：實作 `doc.ExtractFields()` API，利用跨節點字串重組狀態機，無損提取被碎裂 `<span>` 阻斷的合約欄位數據，並雙向對應 Form Fields。

---

## 實作設計決策 (依據業界最佳實踐與 /grill-me 決策)

### 1. 樣式保留的文字替換引擎 (Format-preserving Text Replacer)

* **決策**：在 ODT 文字範本中，文字會因為編輯器的不同而在底層 XML 被碎裂成多個相鄰的 Text Run 節點。如果直接使用 `Replace`，會清空子節點並直接覆寫純文字，這將無情地抹除文字原本所套用的粗體、斜體、字級與顏色等格式。

* 我們將於 `OdfDocument` 與 `OdfParagraph` 中提供 `ReplaceText(string, string)` API。內部會自動跨相鄰的 Text 節點重組字串，在偵測到關鍵字時，**於 XML 節點層面精準進行局部替換與拆分，100% 完整保留文字原本的格式與樣式結構**。

### 2. 試算表 DOM 稀疏儲存與按需具現化 (Sparse Storage & On-demand Instantiation)

* **決策**：我們將在 `TableTableElement` 底層，將大量單元格的原始資料（數值與樣式名稱）改用緊湊的連續陣列進行扁平化儲存。只有當開發者明確透過 `table["A1"]` 存取時，才按需具現化對應的強型別 Cell DOM 物件，在百萬級單元格場景下減少 80% 以上的物件數量，徹底消除 GC 掃描延遲。

### 3. OdsStreamWriter 內部流式寬高自動去重與 RLE 壓縮

* **決策**：當使用者呼叫 `WriteColumn` 或 `WriteStartRow` 頻繁定義相同寬度或高度的行列時，寫入器內部將透過輕量級快取進行去重，自動複用既有的樣式名稱定義，減少生成的重複 XML 樣式標籤。

* **RLE 壓縮**：啟用自動的單元格與列層級 RLE 壓縮，由寫入器內部自動快取單元格與資料列狀態，並在適當時機寫出 `table:number-columns-repeated` 與 `table:number-rows-repeated` 屬性。此優化對呼叫端完全透明，在大批量重複或空白儲存格匯出時可提升 50%\~90% 的寫入效能。

### 4. DOM 批量資料對接與 Off-tree 離線建置

* **決策**：在 `ImportData` 批量灌入資料時，不在實體 DOM 樹上進行高頻的逐個節點掛載。底層實作將採用「Off-tree 離線節點建置」，先在記憶體中建立獨立的稀疏單元格結構，待資料全數讀取完成後，再以一次性掛載 (Batch Attach) 方式併入實體 DOM 樹，避開 DOM 高頻連線與 GC 壓力。

### 5. 範本套印的純文字表格行與區塊循環 (Repeater)

* **決策**：在範本套印引擎中支援 `[#foreach item in Items]` 與 `[/foreach]` 標記。使用者可直接在 ODT/ODS 範本的表格單元格或段落中打字標記，引擎在解析 XML 時，會自動將這些標記所在的段落或表格行（TableRow）向上尋找，並以該元素為範本體進行動態複製與展開。此方式對範本製作者最為直覺，DX 最佳。

### 6. 公式計算的循環引用環偵測與容錯

* **決策**：當公式計算引擎遇到循環引用（Circular Reference）時，自動採用 DFS 著色法或 Kahn 演算法偵測環，系統不會直接崩潰，而是將該單元格之計算結果設為錯誤碼 `#REF!`，並於 Diagnostics 中記錄警告，確保其他無涉單元格仍能正確計算，以兼顧系統容錯性與開發偵測的便利性。

### 7. 自造字字型內嵌子集化擴充點

* **決策**：為了解決 PUA 自造字在不同裝置開啟時的豆腐字問題，核心庫提供「字型子集化介面 (IFontSubsetter)」擴充點。核心庫僅定義此介面與 XML 的 `<style:font-face>` 宣告及檔案關聯，允許開發者透過擴充套件（如 `OdfKit.Extensions.Imaging`）註冊具體的子集化抽取實作。若有註冊，存檔時會自動將 PUA 字元抽取為微型子集字型嵌入 Zip 包中，兼顧無豆腐字與檔案體積最小化。

### 8. OdsStreamReader 工作表快速跳過

* **決策**：當使用 `OdsStreamReader` 讀取特定工作表時，若掃描到非目標 `<table:table>` 節點，直接呼叫 `XmlReader.Skip()` 跳過整棵子樹。這能避開該工作表中所有儲存格的細粒度解析與 GC 分配，在多工作表大檔案場景下極速提升讀取效能。

### 9. 多文件合併的樣式衝突解決與智慧去重

* **決策**：當使用者合併多個 ODT / ODS 文件時，底層對所有併入文檔的樣式屬性集進行 Hash 計算。若內容完全一致，直接複用目標文件的樣式，減少 XML 定義冗餘；若同名但屬性衝突，自動在 `styles.xml` 中將併入樣式重新命名（如 `P1_b_1`），並更新被合併節點的關聯名稱。此機制兼顧 100% 視覺排版正確與檔案體積最小化。

### 10. 並行 Zip 封裝寫入 (Parallel Zip Package Writer)

* **決策**：當儲存包含巨大 XML 與多張圖片的大型 ODF 包時，寫入器將在背景 ThreadPool 中並行（Parallel）對多個主要 Entry（如 `content.xml`、`styles.xml` 與 `Pictures/*` 等）進行 Deflate 壓縮至從 `ArrayPool<byte>` 租用的臨時緩衝區。壓縮完成後，再由主執行緒依序極速寫出至最終的單一輸出 Stream，以充分釋放多核心 CPU 效能，大幅減少大型文件存檔耗時。

### 11. 零分配 readonly ref struct 遍歷器 (EnumerateCellViews)

* **決策**：為了在遍歷數百萬單元格進行數據分析時消除垃圾回收（GC）引起的 Stop-The-World 暫停，我們在 `TableTableElement` 引入 `EnumerateCellViews()` 方法。該遍歷器直接在底層稀疏陣列上滑動，不建立任何 `OdfElement` 物件，並以 `readonly ref struct` 提供雙向數據存取，實現真正的「零 Heap 分配」。

### 12. 零裝箱 Layout-Explicit 聯集結構 (OdfCellData struct)

* **決策**：為完全消除數值、日期與布林值存入稀疏扁平陣列時產生的裝箱 (Boxing) Heap 分配，底層設計一緊湊的 `OdfCellData` 結構體。利用 `[StructLayout(LayoutKind.Explicit)]` 讓 double 數值、long 日期 ticks 與 bool 共享同一個 8-byte 記憶體區段，字串值與樣式名則以 string 引用儲存。此方式可保證 GC 零分配，且記憶體極度緊湊。

### 13. 享元樣式池 (Flyweight Style Pool) 去重

* **決策**：為防止高頻設定儲存格或段落格式時造成 XML 樣式定義嚴重膨脹，底層引入樣式享元池。以屬性雜湊快取所有樣式組合，內容完全一致者直接複用名稱，不再重複寫入 styles.xml，對使用者完全透明，大幅減小產生的 XML 文件大小。

### 14. 二進位媒體自動去重池 (Media Deduplication Pool)

* **決策**：為解決重複圖片（如 Logo、圖章）重複寫入 Zip 包造成檔案體積暴增問題，在 `SetImageSource` 或 `AddImage` 時引入二進位媒體去重池。底層自動計算二進位資料的 SHA-256 雜湊，相同媒體資源直接複用 `xlink:href` 連結，不再重複寫入 Package。此優化對開發者完全透明，可最大化壓縮檔案體積。

### 15. 多 Entry 並行解析與解耦繫結

* **決策**：在 `LoadAsync` 開啟文件時，為釋放多核心 CPU 效能，使用 `Task.WhenAll` 並行解析 `content.xml` 與 `styles.xml`。解析 DOM 時僅保留樣式名稱字串，待各項解析任務完成後，再於最終階段進行 DOM 與實體樣式物件的關聯繫結，這能將大型文件的載入時間大幅縮短 30% \~ 50%。

### 16. 動態編譯運算式樹快取 (Compiled Expression Trees Cache)

* **決策**：在 `ImportData<T>` 批量灌入實體集合時，為完全消除反射（PropertyInfo.GetValue）所帶來的 CPU 效能瓶頸與記憶體裝箱開銷，於首次匯入類型 T 時，動態將其屬性存取編譯為強型別委派（如 `Func<T, object>`）並快取。後續百萬次灌入直接呼叫委派，效能直逼手寫，且對使用者完全透明。

### 17. 分頁式稀疏儲存 (Segmented Page Storage)

* **決策**：為完全消除百萬級試算表中呼叫 `InsertRow` 或 `DeleteRow` 進行行列增刪時，由於對連續扁平大陣列進行全表級 `Array.Copy` 所造成的停頓，底層稀疏儲存採用分頁式設計（將表格以 1024x1024 區分為多個 Page 指標表）。定址時間為 O(1)；增刪行列時只需局部移動 Page 內部元素或增刪 Page，免去全表拷貝。

### 18. 一鍵式標準 ODF 加密與並行 SIMD/AES-NI 加密加速

* **決策**：為滿足商業與公務公文安全交換需求，API 提供標準符合國際 ODF 規範的 `SaveEncryptedAsync()` 與 `LoadEncryptedAsync()`。底層在 ThreadPool 背景中並行對各個 Zip Entry（mimetype 保持未加密）進行加密，且透過 `net10.0` 條件編譯啟用 CPU AES-NI 指令集硬體加速，以解決 AES 計算所帶來的 CPU 效能瓶頸。

### 19. Memory-Mapped Files (MMF) 零拷貝載入

* **決策**：當從本機磁碟開啟大型 ODF 文件時，底層透過 `MemoryMappedFile` 將 Zip 檔直接映射至虛擬記憶體。解壓與解析時透過指標直接對接 `UnmanagedMemoryStream` 或 `Span` 進行零拷貝讀取。這能徹底消除將大型 Entry 讀入 Heap 產生的 LOH 碎裂，記憶體使用率恆定，且讀取吞吐量顯著提升。

### 20. Flyweight 1-to-1 Wrapper 繫結與 As<T>() 零分配轉換

* **決策**：為簡化底層 DOM 尋覽並徹底消除高頻轉型強型別 wrapper 時產生的 Heap 物件分配，在底層 `OdfNode` 中設計 `_wrapper` 欄位進行對應強型別實例的 1-to-1 快取。當開發者呼叫 `node.As<T>()` 時，僅進行型別轉型 (Class Cast) 即傳回既有實例，達成零 Heap 分配，並保證文檔中一個 XML 節點唯一對應一個 Wrapper 物件，避開狀態同步衝突。

### 21. AdoptNode 跨文件移轉的命名空間自動重對照與遞迴合併

* **決策**：當使用者呼叫 `AdoptNode()` 跨文件移轉子樹時，底層在執行指標移轉時會自動遞迴遍歷子樹。若目標文件尚未宣告該 Namespace URI，則自動在根節點進行 `xmlns` 補全宣告；若前綴發生衝突，則自動將子樹內節點的前綴重對照（Remap）為目標文檔標準前綴，對使用者完全透明，100% 確保生成之 XML 合規無毀損。

### 22. 二進位區塊複製與延遲解析 (Lazy Subtree Cloning)

* **決策**：當呼叫 `CloneNode(true)` 對段落或表格列等子樹進行深拷貝時，若被拷貝子樹尚未被修改，直接對其底層 XML 二進位數據進行記憶體 Block Copy，並以未解析之延遲節點掛載至新位置。只有當開發者明確讀寫其細粒度子節點時才按需解析具現化，實現大子樹複製的極速與「零 Heap 分配」。

### 23. 輕量級行內富文字標記解析器設值器 (RichText)

* **決策**：為簡化段落或儲存格中格式混排文字的設定，在 `OdfParagraph` 與 `TableTableCellElement` 提供 `RichText` 屬性。底層內建無外部相依、零分配的字元狀態機，自動解析 Markdown (如 **bold**, *italic*) 或 HTML 行內標籤，自動產生並掛載 `<text:span>`，且對接享元樣式池自動去重，提供極佳的開發體驗 (DX)。

### 24. 批次行列指標交換與 RLE 行合併

* **決策**：在 `TableTableElement` 中提供 `Rows.Insert/Move/Copy` 批次 API。當進行行列 Move 移動時，直接交換分頁式稀疏儲存（Page）的指標，免除實體儲存格數據拷貝；批次插入空行或複製重複列時，底層自動利用 ODF 的 `number-rows-repeated` 屬性將其合併為單一 XML 節點輸出，實現極速行列操作且 XML 體積降至 O(1)。

### 25. DAG 拓撲並行計算引擎 (Parallel DAG Formula Evaluator)

* **決策**：當重算包含數萬個公式的巨型試算表時，公式評估器在重算前會分析公式依賴 DAG。利用多執行緒/ThreadPool 並行計算互不相依的公式分枝（如各行小計或跨表獨立彙整），動態驅動子節點計算，充分釋放多核心 CPU 效能，大幅縮短巨型計算表的公式評估時間。

### 26. 跨文件 AdoptNode 與合併的媒體 SHA-256 智慧去重

* **決策**：在跨文件 `AdoptNode(DrawImageElement)` 或合併多個文檔時，自動對源媒體二進位進行 SHA-256 雜湊比對。若目標文件已存在相同媒體，直接修改新節點的 `xlink:href` 屬性，指向目標文檔既有路徑，完全免除圖片二進位資料的拷貝與 Zip 寫入，最大程度壓縮合併後文檔的體積，且對使用者完全透明。

### 27. 智慧數值格式設值器與享元去重

* **決策**：在儲存格與樣式 API 提供 `NumberFormat` 屬性，開發者可直接設定格式字串（如 `"$#,##0.00"`、`"yyyy-MM-dd"`）。底層自動解析格式語法，在 `styles.xml` 產生標準的 `number-style` 相關節點，並透過享元池對 `data-style-name` 進行自動去重與隱式繫結，將代碼簡化 98%。

### 28. 自定義元素 Wrapper 註冊表 (OdfWrapperRegistry)

* **決策**：為支援自定義 XML 擴充節點或屬性（如政府/企業特用元數據）的操作，核心庫提供 `OdfWrapperRegistry` 註冊表。開發者可手動註冊自訂 C# 類別 (繼承自 `OdfElement`) 對應特定標籤與 Namespace。DOM 解析器遇到時自動具現化為對應的自訂強型別 Wrappers，使開發者享受一致的強型別開發體驗。

### 29. DbDataReader 一鍵流式對接管道與 Span 格式化

* **決策**：在 `OdsStreamWriter` 提供 `WriteDataAsync(DbDataReader)` API。底層自動依據欄位結構動態編譯強型別 IL 寫出委派。遍歷時，數值與日期直接利用 `TryFormat` 寫入 Span 緩衝，完全免除 `DbDataReader.GetValue()` 的 `object` 裝箱與中間 string 分配，Web 匯出時內存恆定小於 1MB，取得極速流式處理效能。

### 30. Raw Zip Entry 零重壓快取 (Raw Zip Entry Zero-Recompression Cache)

* **決策**：在載入大型 ODF 檔案時，我們只會修改其中的特定 Entry（如 `content.xml`）。為了避免存檔時對其他未修改的 Entry（如大型圖片或 `styles.xml`）重新進行 Deflate 壓縮消耗 CPU，底層在載入時會直接將這些未修改 Entry 的壓縮二進位原始數據（Raw Compressed Data）快取至記憶體或 MMF 對映區。在存檔時，直接將快取的二進位數據 Copy 寫入輸出 Stream，實現 $O(1)$ 的極速保存。

### 31. 零分配 UTF-8 直接寫入器 (Zero-Allocation UTF-8 Writer)

* **決策**：高頻 XML 寫入時，為了消除動態屬性值與標籤（如數值、日期、自訂前綴屬性等）先轉為 C# 字串再編碼的 Heap 分配壓力，我們將設計一個自訂的零分配寫入器。該寫入器結合 `Utf8Formatter.TryFormat` 與預編譯的 UTF-8 位元組（`ReadOnlySpan<byte>`），直接將資料格式化並寫入輸出 Span 緩衝區，徹底消除字串 Allocation。

### 32. Fluent 行內富文本建構器 (InlineTextBuilder)

* **決策**：為了解決在同一個段落或單元格中設定多種不同格式文字的繁瑣操作，我們將在 `OdfParagraph` 與 `TableTableCellElement` 中引入 `AppendText()` 鏈式 API。該 API 傳回一個強型別的 `InlineTextBuilder`，支援如 `.Text("Hello").Bold().Color(Color.Red).Text("World").Underline()` 的鏈式呼叫，底層自動產生並掛載對應的 `<text:span>` 節點，並呼叫享元樣式池自動去重，提供極致便利的開發體驗 (DX)。

### 33. 寫時複製 (COW) 樣式繼承與安全修改 (Copy-on-Write Style Inheritance)

* **決策**：當呼叫 `CopyFormatFrom(source)` 複製格式時，為了避免產生大量重複的樣式定義，新節點會直接引用與源節點相同的樣式名稱。如果之後開發者呼叫 `ConfigureStyle` 局部修改該節點的樣式屬性，底層會自動執行「寫時複製 (Copy-on-Write)」機制：在樣式池中複製原樣式、套用修改、產生新樣式並與該節點重新繫結，確保不會波及其他共用同一個樣式的節點，同時兼顧效能與安全性。

### 34. 試算表自動欄寬估算 (Auto-fit Column Width)

* **決策**：當使用 `OdsStreamWriter` 流式寫入或 `TableTableElement.ImportData` 批量灌入資料時，為了防止匯出之試算表因欄寬太窄而使文字被截斷或數值顯示為 `###`，底層將內建一個不相依任何外部繪圖庫（如 GDI+）的輕量級字元寬度估算器。它能依據 CJK 與半形字元長度、字型大小自動估算最大寬度，並在存檔時自動產生或更新對應的 `style:column-width` 屬性，確保排版美觀。

### 35. DOM 結構相容性檢查器 (Structural Validation)

* **決策**：手動編輯 DOM 樹時若節點層級不合規（例如在段落底下掛載表格列），會導致檔案損毀且無從排查。我們將實作 `OdfDocument.Validate()` API，利用從 ODF Schema 產生的節點關係拓撲表，快速校驗整棵 DOM 樹。若發現結構錯誤或屬性缺失，將拋出精準帶有 XML 節點路徑的 Diagnostics 診斷警告與錯誤資訊，並對常見錯誤提供自動修復 (Auto-fix) 委派，提升強健性。

### 36. 多執行緒流水線解析器 (Channel-based XML Reader Pipeline)

* **決策**：為消除單一巨大 `content.xml` 的單執行緒 XML 解析瓶頸，底層採用 `System.Threading.Channels` 建立生產者-消費者流水線。主執行緒快速掃描並將 XML 按工作表或區段分割為 `ReadOnlyMemory<char>` 工作項發送至 Channel，多個背景 ThreadPool 線程並行解析為 DOM 子樹，最後由主執行緒完成指標合併，大幅縮短載入耗時。

### 37. 可回收 DOM Wrapper 物件池 (Recyclable DOM Wrapper Pool)

* **決策**：為消除在巨型文件遍歷與轉型中產生的 Wrapper 物件 GC 負擔，底層引入可回收物件池與 `WeakReference` 管理機制。當節點不再被外部強引用，或呼叫 `ReleaseUnusedNodes()` 時，自動將 Wrapper 物件重置並歸還物件池，待下次 `As<T>()` 時重複使用，達成高頻尋覽下的零 Heap 碎裂。

### 38. 書籤安全填值管理器 (OdfBookmarkManager)

* **決策**：在範本套印或報告產出中，提供 `OdfBookmarkManager`。開發者可透過 `doc.Bookmarks["ClientName"].Value = "Gemini"` 直覺操作，底層會自動尋找配對的 `<text:bookmark-start>` 與 `<text:bookmark-end>` 並精準安全地替換其中間內容，避免 XML 結構損毀。

### 39. 一鍵目錄自動生成 (TOC Generator)

* **決策**：提供 `OdfDocument.InsertTableOfContents(insertPoint)` API。底層自動掃描整份文檔的所有標題 `<text:h>`，並自動產生符合 ODF 1.4 標準的 `<text:table-of-contents>` 及結構宣告，完成自動目錄與超連結架構建置，免除開發者手動編寫複雜 XML 結構的痛苦。

### 40. Fluent 巢狀清單建構器 (Fluent List Builder)

* **決策**：在段落上提供 `AppendList()` 傳回 `OdfListBuilder` 鏈式 API。支援鏈式巢狀呼叫（如 `.Item().SubList().Item().Up()`），底層自動產生正確的 nested `<text:list>` 與 `<text:list-item>` 並繫結樣式，將數十行繁瑣代碼精簡至單行。

### 41. 圖表資料自動繫結與內置 ODS 同步 (OdfChartBuilder)

* **決策**：由於 ODF 圖表是內嵌的獨立子文件且內含專用 ODS 試算表，我們提供 `OdfChartBuilder` 介面。當呼叫 `doc.AddChart` 建立圖表後，透過 `chart.BindData(table["Sheet1.A1:B10"])` 可自動重組圖表 `<chart:series>` DOM，並自動將指定的資料序列同步寫入內嵌 ODS，實現圖表的一鍵高速生成。

### 42. Matrix3x2 幾何變換 (Affine Coordinate Matrix Transformation)

* **決策**：向量繪圖與圖片定位支援仿射變換。在 Shape 元素上引入 `Transform` 屬性，類型為 .NET 原生 `System.Numerics.Matrix3x2` 結構。開發者可使用矩陣乘法運算（如旋轉、縮放與平移），底層自動將其格式化為標準的 `draw:transform="matrix(...)"` 寫出。

### 43. 跨文件公式引用與連結快取 (External Cache Link Manager)

* **決策**：為了支援跨試算表檔案公式（如 `='file:///other.ods'#$Sheet1.A1`）的計算與重算，實作 `ExternalLinkManager` 快取機制。當公式重算時，優先讀取本地 XML 儲存的連結快取；同時提供載入委派，允許在重算時動態開啟並載入真實外部文檔進行即時運算。

### 44. 未知節點無損 Round-Trip 保障 (Unknown Node Preservation)

* **決策**：為確保與第三方非標準編輯器擴充資料的相容性，XML 解析時凡是未知或自定義的 XML 元素與屬性，一律解析為 `OdfUnknownElement` 並完整保留其屬性與子樹結構。在存檔時 100% 原樣寫出，確保 OdfKit 作為無損編輯器的完整性。

### 45. 並行隨機存取解壓 (Memory-Mapped Parallel Random-Access Unzip)

* **決策**：為打破單執行緒解壓大型文檔的物理瓶頸，我們利用 `MemoryMappedFile` 定位各壓縮 Entry 的二進位 offset，並在背景 ThreadPool 中對多個獨立 Entry 並行解壓。（技術限制：單一 DEFLATE 串流因滑動視窗回溯參照與 Huffman 狀態而本質順序，無法隨機 seek 至中段並行解壓；唯有在壓縮時預先寫入 flush 邊界並建立區塊索引（如 bgzf/pgzip）才能在單一 Entry 內並行。一般 ODF 不具此邊界，故僅能跨 Entry 並行。）

* **記憶體分頁 DMA 隨機寫入**：利用 P/Invoke 呼叫 `WriteFileGather` 或 `writev`，以非同步 DMA 方式直接將不連續的格式化記憶體分頁寫出至磁碟，將 CPU 與內核態 Copy 開銷降至最低。

* **二進位 XML 預編譯範本**：支援將重覆的 XML 表格/文字區段預編譯為含預留變數 offset 的二進位範本，套印時直接 Block Copy 輸出，免去 XML 序列化開銷。

* **自訂 XML 格式化設值器與 Struct 享元池**：高頻數值/貨幣格式化屬性封裝為 struct 並對接享元池，利用值類型消除高頻匯出時的 Heap 分配。

#### [MODIFY] [OdsStreamReader.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/Spreadsheet/OdsStreamReader.cs) 及 `OdtStreamReader.cs`

* \*\*實作 `System.Data.Common.DbDataReader`\*\*。

* **工作表快速跳過**：當定位至非目標工作表 `<table:table>` 節點時，直接呼叫 `XmlReader.Skip()` 跳過整棵子樹，避免 Cell 屬性解析與字串分配開銷。

* **SIMD 向量化 XML 轉義字元掃描與解碼**：以 `Vector256<byte>` 向量化加速讀取 XML 中的 `&amp;`、`&lt;` 等轉義字元，提升文字解碼速度。

* **Span-based 浮點數與整數 Fast-LUT 解析**：在載入屬性或單元格資料時，透過 Span 高速 LUT 快取解析短數值與整數字串，降低對系統 `double.Parse` 的 CPU 耗時與分配。

* **XML 屬性享元去重與常值字串表**：高頻 XML 屬性名與常值屬性值比對成功後，直接引用預快取的靜態 string 實例，減少 95% 以上字串 Heap 分配。

* **大檔案載入的執行緒親和性與優先權調度**：自訂輕量調度器，將大檔案解析/解壓線程綁定 CPU 特定核心並動態調高優先權，最大化快取局部性與 CPU 吞吐量。

* **MMF-based XML 節點二進位 Offset 索引圖隨機定位**：在讀取巨大 ODF 文件時，不解析 DOM，僅在 MMF 上掃描 `<table:table>` 節點的 XML 起始/結束 Offset 並建立索引圖，達成隨機按需讀取。
* **SIMD 命名空間字首 Checker**：以 `Vector128<byte>` 快速比對二進位字首（如 `table:` 等），省去字串 prefix 切割與查表開銷。

* **唯讀 Central Directory Zip 快速索引**：解析 Zip 尾部的中央目錄，建立各 Entry 二進位 Offset 查表，無需加載整個 Zip 結構即可進行零分配隨機唯讀訪問。

* **編譯期 Perfect Hash XML 標籤與屬性比對**：對 ODF 1.4 標準標籤與屬性名稱進行 Perfect Hash 編譯，讀取熱路徑直接比對二進位 Span 生成列舉，徹底消除 NameTable 比對字串的 CPU 開銷。

---

### 47. 行列摺疊分組 (Spreadsheet Row/Column Grouping & Outlining)
* **決策**：在 `TableTableElement` 中實作 `table.Rows.Group(startRow, endRow, collapsed)` 與列分組 API。底層自動計算巢狀層次，並產生或修改 `<table:table-row-group>` 結構以包裹目標行列，安全地維護 DOM 樹階層結構，實現一鍵摺疊與展開排版。

### 48. 渲染與 PDF 匯出抽象介面 (IOdfRenderer & Printer Export Registry)

* **決策**：保持核心庫的輕量化與零相依性，Core 中僅定義 `IOdfRenderer` 抽象介面與 `OdfRendererRegistry` 全域註冊點。允許開發者在外部擴充套件（如 `OdfKit.Extensions.Imaging`）中註冊具體的排版渲染實作，隨後可在 `OdfDocument` 直接呼叫 `doc.ExportToPdf()` 與圖片轉換，取得流暢的模組化擴充體驗。

### 49. SIMD 標籤高速掃描 (SIMD-Accelerated XML Lexing)

* **決策**：針對巨型 XML 解析，我們將在目標平台為 `net10.0` 下透過 SIMD 向量化指令（於 UTF-8 位元組層級使用 `Vector256<byte>` / `Vector128<byte>`；.NET 的 `Vector128<T>`／`Vector256<T>` 並不支援 `char` 型別）進行 XML 標記尋找與定位。在熱路徑中進行並行比對（比對 `<` 與特殊前綴），快速掃描並跳過無關資料，使 Tokenization 速度直逼磁碟頻寬極限。

### 50. 零分配轉碼與字串享元快取 (Zero-Allocation Transcoding & String Flyweight Cache)

* **決策**：高頻 XML 屬性解析時，為了消除將 UTF-8 二進位直接解碼成 UTF-16 C# string 的記憶體配置，在 `ReadOnlySpan<byte>` 階段即對常用標記與樣式名稱進行比對，直接匹配並引用預先快取的 string 享元實例，完全消除解碼產生的暫時字串，實現近乎 100% 的轉碼零 Heap 分配。

### 51. 儲存格合併與覆蓋管理 (Spreadsheet Merged & Covered Cells Manager)

* **決策**：在 `TableTableElement` 中提供 `MergeCells(string range)` 與 `UnmergeCells(string range)`。底層自動將範圍內非主儲存格安全轉換為 `<table:covered-table-cell>` 覆蓋節點，確保試算表二維網格的物理對齊，防止列資料位移錯亂，100% 確保 DOM 生成的安全性。

### 52. 合約修訂追蹤引擎 (Track Changes & Redline Engine)

* **決策**：為支援專業法律合約與公文自動化流程，Core 提供合規的修訂追蹤引擎。當 `doc.TrackChanges = true` 時，所有 DOM 節點增刪與修改皆自動轉換為 `<text:tracked-changes>` 等修訂標記節點，記錄修改者與修改時間，保留完整的文件歷史版本。

### 53. 非受控/固定記憶體超大表格儲存 (Off-Heap POH/NativeMemory Table Storage)

* **決策**：為了徹底消除超大型 ODS 試算表（數百萬至千萬單元格）載入與操作時，大陣列引起 LOH (Large Object Heap) 碎裂與頻繁 GC 掃描造成的 Stop-The-World (STW) 停頓，超大表格的底層結構體陣列將直接使用 `NativeMemory.Alloc` 進行非受控分配，或分配在 .NET 的 POH (Pinned Object Heap) 中。這能使 GC 掃描時完全忽略這部分數據，將記憶體回收延遲降至零。

### 54. 零分配屬性二進位讀取器 (Zero-Allocation UTF-8 Attribute Span Lexer)

* **決策**：高頻解析 XML 屬性值（如數值、日期等）時，為了完全免除解碼成新 string 產生的 Heap 垃圾，我們實作一個直接在 `ReadOnlySpan<byte>` 階段運作的零分配屬性讀取器。透過匹配 UTF-8 屬性名稱二進位，並直接調用 `Utf8Parser` 將屬性值直接讀出為 `double`、`DateTime` 等強型別數值，消除所有字串解碼開銷。

### 55. SIMD 向量化區間公式重算 (SIMD Vectorized Range Math for Formulas)

* **決策**：當公式引擎對大型連續儲存格區間進行公式統計（如 `=SUM(A1:A1000000)`、`AVERAGE`、`MIN`、`MAX`）時，為打破單核逐個儲存格定址累加的效能瓶頸，重算引擎將直接提取該區間稀疏分頁（Page）的實體 `double` 陣列指標，利用 **AVX2/AVX-512** 硬體向量化指令集進行並行向量累加，將統計公式的評估速度提升 5 至 10 倍。

### 56. 層疊樣式實質解析器與快取 (Computed Style Cascaded Resolver & Cache)

* **決策**：為了解決高頻查詢文字與儲存格視覺屬性（字型大小、顏色、背景）時，由於 ODF 階層樣式繼承結構（行內樣式 -> 父樣式 -> 默認樣式）所帶來的遞迴 DOM 尋覽與屬性字串查詢開銷，我們實作層疊樣式解析器。節點第一次讀取樣式時推導出「實質最終樣式結構體 (ComputedStyle)」並快取在 DOM 節點上，配合層疊 Dirty 標記失效傳播，將樣式查詢速度提高 10 倍以上。

### 57. 自訂零分配 XML 讀取器 (Zero-Allocation Custom UTF-8 Pull XML Reader)

* **決策**：為了降低大檔案 XML 解析層面的物件分配與記憶體拷貝開銷，`net10.0` 可規劃自定義 UTF-8 XML 拉取解析器作為熱路徑。該解析器直接在 MMF 的 UTF-8 位元組 Span 上進行狀態機掃描，拉取時回傳 `ReadOnlySpan<byte>` 結構體 Token；命名空間查表可評估 `FrozenDictionary.GetAlternateLookup<ReadOnlySpan<byte>>()`。但 `System.Xml.XmlReader` 目前仍是安全與相容性基準，遇到複雜 XML 語法、DTD 防禦、實體處理或快速路徑未覆蓋情境時必須回退 `XmlReader.Create(..., DtdProcessing.Prohibit)`。U8XmlParser 因上游已封存，只能列為研究候選，不直接納入 Phase 1 相依。

### 58. 工作表二進位極速拷貝 (Sheet-level Fast Clone/Adopt)

* **決策**：當在多個 ODS 試算表文件之間拷貝或合併工作表時，為了免除載入完整 DOM 樹的巨量 GC 暫停，我們實作工作表級二進位快速拷貝。在讀取時快取工作表 `<table:table>` 的 XML 原始 UTF-8 區段 offset，移轉時直接對該二進位區段進行 Block Copy 並寫入目標 Zip 包中，同步自動合併 styles.xml 樣式，將大型工作表的複製時間從數秒縮短至毫秒級，且記憶體配置為零。

### 59. Fluent 樣式混合與自動推導 (Fluent Style Mixin & Style Builder)

* **決策**：為解決 ODF 樣式繼承樹（直系樣式 -> 父樣式 -> 默認樣式）配置繁瑣的痛點，在元素與樣式 API 中引入 `ApplyStyle(Action<OdfStyleMixinBuilder> action)` 混合 API（例如 `ApplyStyle(s => s.InheritFrom("Heading").Bold().Color(Color.Red))`）。底層享元樣式池會自動比對現有繼承鏈與屬性集，若無完全相符的繼承樣式則自動於 `styles.xml` 中產生正確的繼承樣式節點並套用，免除開發者手動進行樣式宣告與名稱綁定的負擔。

### 60. 作業系統級 Direct I/O 與預讀 (OS-Level Direct I/O & DMA Pre-fetching)

* **決策**：當讀寫數百 MB 至 GB 級的超大型 ODF 文件時，傳統的 `FileStream` 磁碟快取與記憶體轉拷容易導致 Cache Thrashing 與系統卡頓。當平台為 Windows 且目標框架為 `net10.0` 時，底層利用 `FILE_FLAG_NO_BUFFERING` 開啟檔案，並以 `NativeMemory.AlignedAlloc(size, 4096)` 建立對齊緩衝區，再搭配 `File.OpenHandle` 與 `RandomAccess.Read` / `RandomAccess.Write` 進行 Direct I/O；尾端非 4096 對齊資料與舊 TFM 一律回退常規 `FileStream`。

### 61. 硬體加速 CRC32 (Hardware-Accelerated CRC32)

* **決策**：Zip 包每個 Entry 寫出前必須計算 CRC32 校驗碼。在大檔案保存時，CRC 計算佔用了大量的 CPU 運算資源。在 `net10.0` 目標框架下，我們將透過 x86/x64 的 `Pclmulqdq`（carry-less 乘法）與 ARM64 的 `System.Runtime.Intrinsics.Arm.Crc32` 原生 CPU 指令，實作零 Heap 分配的硬體加速 CRC-32/ISO-HDLC 計算器。（重要更正：x86 SSE4.2 的 `Sse42.Crc32` 僅支援 CRC-32C/Castagnoli 多項式，與 ZIP 不相容；ZIP 標準 CRC 在 x86 須以 PCLMULQDQ 加速。ARM64 的 CRC32 指令位於 `Crc32` 類別且支援 ISO 多項式，並非 `Dp`/DotProduct 類別。）

### 62. 主從/巢狀範本解算器 (Hierarchical Template Engine)

* **決策**：針對企業財務、銷售報表中常見的「主從/樹狀嵌套」數據套印，範本套印引擎支援巢狀的 `#foreach` 標記。解算器在解析展開數據時，會自動在表格中進行「多級行列摺疊分組」、「重複父級儲存格自動垂直/水平合併（Vertical/Horizontal Merge）」，並自動插入「小計與匯總公式」（如 `=SUBTOTAL(...)`），提供一鍵式主從報表自動套印。

### 63. 安全範本欄位雙向提取器 (Safe Template Field Extractor)

* **決策**：為了從使用者上傳的合約文檔中精準反向提取變數值（如合約中 `[甲方名稱]` 佔位符所填內容），我們提供 `doc.ExtractFields("[", "]")` API。底層利用跨節點字串重組狀態機，定位並無損提取被破碎 `<span>` 節點阻斷的欄位數據，並自動對應綁定至 Bookmarks 書籤或 Form Fields，防止提取失敗並確保雙向數據綁定的高強健性。

### 64. 流式寫入的雙緩衝非同步流水線 (Double Buffering Async Pipeline for OdsStreamWriter)

* **決策**：為消除流式寫出大量資料時由磁碟/網路 I/O 所帶來的執行緒阻塞，我們將引入雙 `ReadOnlyMemory<byte>` 輪替緩衝區。當主執行緒格式化好緩衝區 A 並觸發非同步寫出時，立即切換至緩衝區 B 繼續寫入，實現 CPU 格式化與物理 I/O 寫出的並行流水線化，進一步壓低匯出時間。

### 65. MMF-based XML 節點二進位 Offset 索引圖隨機定位 (XML Node Byte Offset Indexing Map)

* **決策**：在開啟數百 MB 的巨大 ODS/ODT 時，傳統 DOM 解析必須一次性加載所有內容。我們將在 MMF 讀取時進行極速輕量狀態機掃描，僅記錄每個工作表（`<table:table>`）或主要區段的 XML 起始與結束 Byte Offset。建立扁平的二進位索引映射表後，當開發者存取 `doc.Sheets["Sheet3"]` 時，底層僅精準讀取並解析該 Offset 區間的二進位，其餘工作表節點完全不載入，實現巨型檔案的隨機按需讀取。

### 66. 直接 In-place Zip 二進位 Patch 修改 (In-place Zip Struct Patching)

* **決策**：當使用者僅微調文檔中的少數文字（如套印變數）或少量單元格，且修改後 XML 的位元組大小不超過原有 Entry 的預留空間時，底層 Patch 引擎將直接以 MMF 寫入 Zip 檔案的對應二進位區段，並將剩餘位元組以 XML 空白字元填充，更新 Local File Header 的 CRC。這能實現 $O(1)$ 的極速儲存，完全避開重壓整個 Zip 封裝與寫出的大量 I/O。（重要前提：此原位填充修補僅對 STORED（未壓縮）的 Entry 成立。`content.xml` 通常為 DEFLATE 壓縮，於壓縮串流中填充空白無法保持有效，且 CRC-32 係針對未壓縮資料計算，故壓縮 Entry 須改走「末尾追加新 sector + 更新 Central Directory offset」的路徑，見決策 91。）

### 67. Pinned Array Buffer 與非受控並行壓縮 (Pinned Buffer Pools for Parallel Compression)

* **決策**：多 Entry 並行 Deflate 壓縮時，背景執行緒常因為大緩衝區頻繁分配與被 GC 移動而增加系統拷貝開銷。Pinned 陣列可用於避免 GC 移動，但不保證 4096 位元組對齊；若要對接 Direct I/O，`net10.0` 須使用 `NativeMemory.AlignedAlloc` 建立對齊非受控緩衝區。一般壓縮暫存緩衝則可繼續使用 `ArrayPool<byte>` 或 pinned 陣列。

### 68. SIMD 向量化 XML 轉義字元掃描與解碼 (SIMD-Accelerated XML Entity Escape Decoder)

* **決策**：在讀取 content.xml 的文字常值時，必須進行 `&amp;`、`&lt;` 等轉義比對。我們利用 `Vector256<byte>` 向量化比對尋找 `&` 的 ASCII 值，一次性掃描 32 個位元組，若無轉義字元則直接 Block Copy 輸出，將字元解碼速度提升 5 倍以上。

### 69. HTML/Markdown 行內富文本一鍵解析渲染 (HTML/Markdown to Rich ODF DOM Renderer)

* **決策**：為簡化從 Web 端或 Rich Text 編輯器匯入格式的開發繁瑣度，在 `OdfParagraph` 與 `TableTableCellElement` 提供 `AppendHtml(string)` 與 `AppendMarkdown(string)`。底層內建零分配的 Markdown/HTML 行內語法解析器，自動識別 `<b>`, `<i>`, `<span style="...">` 標籤並轉為強型別 `<text:span>` DOM 節點，大幅提昇 DX。

### 70. Excel-like 區域選取與批次格式化 API (Cell Range Operations API)

* **決策**：為提供極致的表格操作 DX，我們提供 `table.Cells["A1:D100"]` 選取範圍物件。支援 Fluent 批次設定，例如 `.SetValue(100)`、`.SetStyle(s => s.Bold().Color(Color.Blue))`，並在底層直接對分頁式稀疏儲存（Page）進行批次更新，避免逐個儲存格操作的代碼冗餘與效能開銷。

### 71. 自訂自動篩選與排序定義 (Spreadsheet AutoFilter & Column Sorting)

* **決策**：在 `TableTableElement` 提供 `table.AutoFilter("A1:D100")` 與 `table.Sort("A2:A100", SortOrder.Descending)` 強型別 API。底層自動產生符合 ODF 1.4 與 LibreOffice 規格的 `<table:database-range>` 與 `<table:filter>` 相關 XML 節點，維護表格數據篩選與排序結構。

### 72. 影像仿射裁剪與進階特效 API (Image Affine Cropping & Draw Effects)

* **決策**：為 `DrawImageElement` 提供 `Crop(top, bottom, left, right)` 與 `SetEffects(e => e.SoftEdge(...).Shadow(...))` API。底層自動計算幾何變換矩陣，與 `System.Numerics.Matrix3x2` 整合，並正確寫入 SVG 裁剪與濾鏡屬性，提供完善的圖形特效支援。

### 73. Span-based 浮點數與整數 Fast-LUT 解析 (Fast Span-based Number Parser)

* **決策**：在載入大表格時，屬性或儲存格中高頻出現數字字串（如 `"0"`, `"1.5"`）。底層維護一個 Span-based Fast-LUT (Look-Up Table) 快取，對常見的短數字字串直接在二進位 Span 階段進行快速比對，避免高頻調用系統 `double.Parse` 的 CPU 分配與計算耗時。

### 74. 主動樹枝化剪裁與 Wrapper 物件銷毀 (Active DOM Tree Pruning & Manual De-registration)

* **決策**：當處理完超大型 ODS/ODT 的特定工作表或章節後，為防止巨型 DOM 樹在 GC 存活過久而進入 Gen 2，開發者可呼叫 `sheet.PruneAndCollect()`。底層會主動將該子樹從主 DOM 樹斷開，並深度清除其關聯的 Wrapper 快取、將所有 Wrapper 物件歸還物件池，並強制釋放其非受控/POH 表格記憶體，主動控制 GC 壓力。

### 75. Fluent 統計公式建構器 (Fluent Formula Builder)

* **決策**：為避免手寫公式字串拼錯或語意錯誤，提供 `Formula` 靜態類別，支援如 `table["C1"].Formula = Formula.Sum("A1:A10").Multiply(Formula.Cell("B1"))` 強型別鏈式建構。底層自動將其編譯為 ODF 符合的 `of:=SUM([.A1:.A10])*[.B1]` 標準語意字串，兼顧強型別防錯與便利性。

### 76. 流式寫入多工作表動態隨機切換 (Multi-Sheet Interleaved StreamWriter)

* **決策**：為解決流式寫入器（StreamWriter）必須依序寫出工作表的限制，我們在 `OdsStreamWriter` 提供「交錯流式寫入」：寫入器在內部為各工作表建立獨立的流式暫存緩衝（利用 MMF 虛擬分區或租用記憶體），開發者可隨時呼叫 `writer.SwitchToSheet("Sheet2")` 交錯寫出。直到呼叫 `Dispose` 時，才將各工作表緩衝以零拷貝方式彙整輸出到 content.xml 中，解決了多工作表流式寫出的 DX 痛點。

### 77. AES-GCM 認證加密與硬體加速 (AES-GCM Authenticated Encryption for ODF)

* **決策**：在 `SaveEncryptedAsync` 與 `LoadEncryptedAsync` 時，除了相容舊版的 AES-256-CBC（搭配 PBKDF2-HMAC-SHA1）外，追加支援符合 ODF 1.4「Wholesome Encryption」標準的 AES-256-GCM 認證加密，並以 Argon2id 作為金鑰衍生函式 (KDF)。利用 .NET 10.0 的 `AesGcm` 類別，在支援 AES-NI 指令集的 CPU 上直接以硬體加速運算，防止檔案內容被惡意篡改與重放攻擊。（註：`netstandard2.0` 不支援 `AesGcm`，將 Fallback 至 BouncyCastle 的 `GcmBlockCipher`；Argon2id 於雙平台均以 BouncyCastle 實作。）

### 78. XML 屬性享元去重與常值字串表 (XML Attribute Flyweight & String Pool)

* **決策**：在解析 XML 屬性名稱與屬性值（如 `office:value-type="float"` 等）時，底層維護一個輕量常值字串池。將高頻出現的字首與屬性值對照到唯讀靜態 string 實例，在 DOM 讀取熱路徑中減少 95% 以上的 String 執行個體建立。

### 79. 大檔案載入的執行緒親和性與優先權調度 (Thread Affinity & Priority Scheduler for Big Files)

* **決策**：當載入數百 MB 級的試算表時，為了防止多執行緒並行解析與解壓時 CPU 核心頻繁切換（Context Switch）降低 CPU L1/L2 快取命中率，我們自訂一個輕量級工作調度器。將解析線程綁定（Affinitize）到特定的 CPU 物理核心，並動態調高工作優先權，確保高密度的 SIMD/XML 解析工作獲得最大快取局部性與 CPU 執行吞吐量。

### 80. 一鍵強型別數據透視表生成 (One-Click Strong-Typed Pivot Table Generator)

* **決策**：提供 `table.CreatePivotTable(sourceRange, targetCell, config => config.RowFields("Region").ColumnFields("Year").DataFields("Sales", Aggregation.Sum))`，底層自動構建與維護 ODF 規範中極為繁瑣的 `<table:data-pilot-table>` 等十餘種 nested XML 節點結構，免除手動構造 XML 的痛苦。

### 81. 文字段落大綱與多級清單自動化編號 (Paragraph Outlining & Autonumbering)

* **決策**：提供 `paragraph.SetOutlineLevel(2).EnableAutoNumbering(styleName)` API，底層自動在 `styles.xml` 與 `content.xml` 之間建立和維護 `text:list-style` 的層級關係，對使用者隱藏多級編號的複雜度。

### 82. 條件格式化圖示集與進階視覺化 (Conditional Formatting Icon Sets & Data Bars)

* **決策**：擴充條件格式化 API，提供 `table.AddDataBar(range, color)` 與 `table.AddIconSet(range, IconSetType.ThreeArrows)`。底層自動產生與 `styles.xml` 對應的條狀、色階與圖標集 XML 配置，一鍵增強試算表的可讀性。

### 83. 一鍵 PDF 數位簽章嵌入 (One-Click PDF Digital Signature Integration)

* **決策**：在 Core 轉檔抽象中，允許 `ExportToPdf` 接收 `X509Certificate2`。當渲染引擎輸出 PDF 時，會自動在 PDF 二進位結構中嵌入合規的數位簽章，簡化公文與商業合同的安全交換流程。

### 84. 大 XML 常值的分塊讀取與二進位直寫 (Chunked XML Read & Binary Stream-through)

* **決策**：對於含有巨型文字或內嵌 Base64 媒體的 XML 節點，自訂 XML 拉取解析器提供 `reader.ReadValueChunk(Span<byte> buffer)` 分塊讀取 API。讀取時直接將數據流分塊寫入 Zip 目標 Entries，杜絕將大型字串或位元組陣列載入 Heap 引起的 LOH 記憶體分配。

### 85. 記憶體分頁 DMA 隨機寫入 (Paged DMA Page Cache for StreamWriter)

* **決策**：在流式寫入超大型試算表時，寫入器內部將資料格式化至 Pinned 虛擬記憶體分頁，並透過 P/Invoke 調用 Windows 的 `WriteFileGather` 或 Linux 的 `writev`。以非同步 DMA 方式將多個不連續的記憶體分頁直接一次性發送至磁碟，將 CPU 與核心態 I/O 拷貝降到物理極限。

### 86. SIMD 向量化矩陣與多維公式重算 (SIMD-Accelerated Matrix Formula Evaluation)

* **決策**：對於試算表中的矩陣運算公式（如 `MMULT`、`SUMPRODUCT` 等），重算引擎直接提取單元格對應的 Pinned 稀疏記憶體 Span，調用 `System.Numerics.Tensors` 的 `TensorPrimitives` 向量化運算，免除 CPU 迴圈遍歷，依資料規模與硬體可顯著提升多維矩陣運算效能（實測量級通常為數倍至約一個數量級，「百倍」屬理論上限，非一般可達）。

### 87. 唯讀 Central Directory Zip 快速索引 (Read-Only Zip Central Directory Indexer)

* **決策**：對於唯讀存取或按需讀取特定 Entry（如只讀取 meta.xml 或是特定圖片），解析器直接讀取並解析 Zip 尾部的 Central Directory（中央目錄），建立各 Entry 的 offset 快速查表。無需初始化整個 `ZipArchive` 或建立 Entry 物件，實現零分配的瞬時唯讀定址。

### 88. 編譯期 Perfect Hash XML 標籤與屬性比對 (Perfect Hashing for Namespace, Tags, and Attributes)

* **決策**：針對 ODF 1.4 標準中上千個標籤與屬性名稱，在編譯期產生 Perfect Hash Function。在讀取 Span 時，直接對 UTF-8 二進位 Span 進行 Perfect Hash 雜湊計算，直接映射到對應的 C# 標籤列舉，完全免除 XML 解析時的字串生成與 NameTable 查表比對開銷。

### 89. ODT 樣式表自動精簡與未使用樣式垃圾回收 (Style Garbage Collection & Pruning)

* **決策**：在長期編輯的大型 ODT/ODS 文件中，會累積大量已無任何節點引用的樣式。提供 `doc.Styles.GC()` API，底層自動遍歷 DOM 樹與 styles.xml，找出所有未被任何節點或 parent 樣式引用的定義，一鍵將其垃圾回收，以精簡檔案大小與載入記憶體。

### 90. 試算表凍結窗格與視窗分割 API (Spreadsheet Freeze Panes & Window Splits)

* **決策**：提供 `table.FreezePanes(int rows, int cols)` 與 `table.SplitWindow(x, y)`，底層自動產生與 LibreOffice/MS Excel 相容的 `<table:views>` 與 `<table:table-view-configuration>` 複雜 XML 結構，提昇大試算表的檢視 DX。

### 91. 高效二進位 Patch 的寫時複製區段 (In-place COW Zip Sectors)

* **決策**：在 In-place Zip Patch 中，若修改後 XML 的長度超出原有預留空間，底層不再重寫整份 Zip，而是自動在 Zip 末尾寫入新增的 sector，並僅修改 Central Directory 中的對應 offset 指標。這能確保檔案大小僅有微量增加的前提下，依然維持 $O(1)$ 的儲存速度。

### 92. 二進位 XML 預編譯範本 (Pre-compiled Binary XML Templates)

* **決策**：針對大批量重覆格式的表格或文字套印，支援將 XML 區段預先編譯為含預留變數 offset 的二進位範本。套印時直接使用 `Block Copy` 並直接以 `Utf8Formatter` 寫入變數，免除任何 XML 解析與標籤生成，達到近乎記憶體拷貝的套印極限。

### 93. 雙平台 PDF 匯出無相依字型對照表 (Font Match Table for Render)

* **決策**：為了解決跨平台（Windows / Linux）渲染時字型缺失導致的排版位移，核心庫提供一組無相依之「標準字型替代對照表」。當在 Linux 容器中找不到 `Calibri` 或 `新細明體` 時，自動依表對照為 `Carlito` 或 `DejaVu Sans`，並自動計算字元寬度位移，確保 100% 的排版一致性。

### 94. 公式重算的非同步非阻塞異步通道 (Async Channel Formula Pipeline)

* **決策**：當公式重算與使用者寫入操作並行時，重算任務透過 `System.Threading.Channels` 以非同步非阻塞管道進行。變更單元格時僅將更新發送至重算 Channel，由背景執行緒進行 DAG 重算與數值發佈，前端維持 100% 響應度且零 UI 卡頓。

### 95. 自訂 XML 格式化設值器與 Struct 享元池 (Struct-Flyweight Formatter Pool)

* **決策**：針對高頻格式化數值（如貨幣 `$#,##0.00`），我們將其格式化屬性包裝為唯讀的 struct 並使用 struct 享元池。利用 Struct 避免 GC Heap 分配，並透過 Hash 比對快速複用，保證高頻資料導出時 100% 的零 Heap 分配。

### 96. 樣式 COW 複製時的繼承鏈深層拷貝防護 (Style COW Deep Copy Inheritance Guard)

* **決策**：在執行樣式寫時複製 (COW) 時，複製程序不僅複製當前屬性，還必須遞迴/深層複製其 `parent-style-name`、`style:class` 及樣式家族屬性，確保複製出的新樣式不會因繼承鏈斷裂而失去上級樣式的視覺外觀。

### 97. 跨文件採納 (Adopt) 的預設樣式屬性扁平化合併 (Default Style Flattening for Adopted Nodes)

* **決策**：當採納依賴來源文件預設樣式的節點時，移轉程序會自動將來源文件之預設樣式中的實質屬性「扁平化解析並合併」寫入被移轉樣式中，使其在目標文件中保持視覺外觀不因目標文件預設樣式不同而變更。

### 98. 非受控記憶體 (POH/NativeMemory) 生命週期 SafeHandle 封裝與 Finalizer 安全網 (Unmanaged Memory Lifecycle SafeHandle & Finalizer)

* **決策**：表格底層非受控記憶體分配必須封裝於自訂的 `SafeHandle` 衍生類或透過託管的 Finalizer 析構函數作為記憶體回收的最後防線，避免文件未顯式 Dispose 時產生嚴重的記憶體洩漏。遍歷器與 Prune 釋放操作之間必須有狀態鎖同步，防止 Use-After-Free 造成的作業系統級 Crash。

### 99. MMF 隨機寫入檔案動態擴展機制 (Dynamic File Expansion for MMF Patching)

* **決策**：當 In-place Patch 導致檔案大小增加且超出當前 MMF 映射邊界時，寫入引擎會安全關閉當前 MMF，擴展檔案物理長度，隨後重新映射更大容量 of MMF 進行尾部區段追加，防止記憶體越界寫入異常。

### 100. Perfect Hash 標籤比對的二進位二次驗證防護 (Double-Verification Guard for Perfect Hash Lexing)
* **決策**：Perfect Hash 雜湊計算對應至列舉後，底層解析器必須將對應出的列舉常值與實際 Span 字元進行一次二進位二次比對。若不一致則代表是未知或非標準擴充標籤，自動 fallback 回 `OdfUnknownElement` 處理，避免誤判。

### 101. 儲存格多行富文本與自動換行 API (Cell RichText Auto-Wrap & Multi-Style Builder)
* **決策**：提供 `cell.RichText` Fluent API。底層自動將 `\n` 轉化為 `<text:line-break>`，自動配置 `<text:p>` 與 `<text:span>` 結構，並自動啟用單元格樣式的 `style:wrap-option="wrap"`，提供完美的單元格多格式與換行 DX。

### 102. 表單控制項與欄位一鍵安全填充器 (Form Field & Control Value Binder)
* **決策**：提供 `doc.FormFields["FieldKey"].Value = "Value"` 等強型別 API。底層自動遍歷與修改 XML 中複雜的 `<text:text-input>` 或 `<form:input>` 表單結構，滿足公文/申請書範本套印的安全填充。

### 103. Excel-like 大範圍 Fluent 邊框與網格管理器 (Fluent Grid Border & Alignment Manager)
* **決策**：提供 `range.Borders` 鏈式設定 API（如 `.SetOuterBorder(Border.Medium)`）。底層自動識別 Range 內邊角（Corners）、內部（Interior）與邊界（Boundaries）在 ODF 中複雜的 XML 邊框屬性差異並智慧去重，簡化表格樣式設計。

### 104. 影像自動 DPI 壓縮與 WebP 轉換優化 (Media Auto-DPI Optimizer & WebP Transcoder)
* **決策**：提供 `doc.OptimizeMedia(maxDpi, jpegQuality)` API。底層自動壓縮文檔中所有大圖至排版 DPI 尺寸，且自動將 PNG/BMP 轉為 WebP 格式，重新寫入 Zip 包並更新 XML 節點，實現體積的極致壓縮。

### 105. 多工作表並行寫入的 Lock-Free Disruptor 管道 (Lock-Free Parallel Multi-Sheet Pipeline)
* **決策**：並行寫入 ODS 時，各工作表寫入線程將格式化的 XML 二進位區段直接發送至各自獨立的 Lock-Free RingBuffer (Disruptor 模式)，由單一 I/O 背景線程以連續 DMA 方式寫入磁碟，消除鎖競爭與執行緒阻塞。

### 106. SIMD 向量化 XML 命名空間字首並行 Lexing (SIMD-Accelerated Namespace Prefix Checker)
* **決策**：使用 `Vector128<byte>` 快速識別並校驗例如 `table:`、`text:`、`draw:` 等命名空間字首與節點類型，省去任何字串 prefix 截取與 Namespace 查表的開銷。

### 107. 零記憶體 Central Directory 與 EOCD 直接修改器 (Zero-Allocation EOCD & Central Directory Writer)
* **決策**：在 In-place Zip Patch 追加區段時，底層直接在 MMF 檔案尾端對 Zip Central Directory 與 EOCD 進行二進位覆寫與 offset 更新，無 Heap 物件分配。

### 108. 表格稀疏儲存的冷熱數據分離 (Cold-Hot Separation for Spreadsheet Storage)
* **決策**：將試算表底層稀疏分頁（Page）進行冷熱分離。含公式之單元格（熱數據）儲存於 Pinned 且連續的 Unmanaged 記憶體中以便於 SIMD 高速定址；常值單元格（冷數據）則儲存在壓縮分頁中，極大提升 CPU 快取命中率。

### 109. 文字文檔節點主動預綁定 (Paragraph Node Pre-binding)
* **決策**：為避免高頻建立段落時產生的 Wrapper 物件分配，提供一個可重複使用的 `OdfParagraph` 享元實例，僅修改其內部的 Span 引用與屬性指針，實現段落寫出時的「零 Wrapper 分配」。

### 110. SVG 幾何圖形路徑二進位快速解析器 (Fast Binary Parser for SVG Paths)
* **決策**：針對圖形中的 `<draw:path d="..." />`，實作一個 Span-based 二進位 SVG 路徑解析狀態機，直接在 UTF-8 位元組中解析坐標與幾何命令，免除字串分割與裝箱開銷。

### 111. 圖表數據離線快取與按需延遲載入 (Lazy Chart Data Cache)
* **決策**：在讀取圖表時僅解析 XML 結構，只有當使用者顯式要求修改圖表數據或進行重算時，才載入並解析其內嵌的 ODS 試算表，縮短載入巨型圖表的等待。

### 112. 自適應執行緒調度與系統 CPU 核心預留 (Dynamic Thread Scheduler & CPU Core Reservation)
* **決策**：在並行解析或壓縮巨大文件時，調度器允許設定 `ReservationRatio`。自動預留特定比例的 CPU 物理核心不參與計算，維持宿主伺服器的總體可用度與響應度。

### 113. 樣式衝突的語意智慧解決 (Semantic Style Merge Resolver)
* **決策**：在合併多文件時，對屬性進行智慧語意分析。若僅是微小差異，自動轉為繼承樣式；若實質衝突，則重新命名並更新 DOM，確保在最小的 XML 增長下維持視覺正確。

### 114. 自訂 XML 屬性 Span 快速查表 (Span-based Fast-LUT for Custom Attributes)
* **決策**：針對使用者註冊的自定義 XML 屬性，解析器在讀取屬性 Span 時，利用 Span-based Fast-LUT 進行快速狀態機查表比對，直接跳過無關屬性，提升讀取效能。

### 115. 二進位 XML 區段的 Dirty 標記局部序列化 (Incremental XML Chunk Serialization)
* **決策**：在 DOM 節點上引入 Dirty 區段標記，存檔時只對被修改的 Chunk 進行 XML 序列化，其餘未修改區段直接 Block Copy 原始二進位，實現大型文件極速存檔。

### 116. 一鍵 HTML 公文樣式對應器 (HTML Document to ODT Stylesheet Mapper)
* **決策**：提供 `doc.ImportHtmlStyles(cssString)` API，自動將 HTML/CSS 樣式表語意映射並寫入 ODT 的 `styles.xml` 中，供 HTML 轉 ODF DOM 一鍵調用。

---

## Proposed Changes

本計畫將重構分為五個主要元件，並在過程中同步修正與執行測試。

### 1. 生成代碼拆分與建構子模組 (OdfSchemaGenerator & DOM)

將現有 74MB 的 `GeneratedDomWrappers.g.cs` 進行命名空間或模組層級的拆分，並優化元素初始化 API。

#### [MODIFY] [DomWrappersCSharpWriter.cs](file:///d:/Dev/Project/Application/OdfKit/tools/OdfSchemaGenerator/DomWrappersCSharpWriter.cs)

* 修改程式碼生成邏輯。將其依據 Schema 命名空間（如 `office`、`table` , `text`、`draw`）分別建立獨立 we `StreamWriter`。

* **新增雙重建構子生成**：無參數建構子與函數式參數陣列建構子。

* 輸出多個細粒度檔案至 `OdfKit/DOM/Generated/` 目錄。

#### [MODIFY] [oasis-odf14-dom-wrappers.json](file:///d:/Dev/Project/Application/OdfKit/tools/OdfSchemaGenerator/oasis-odf14-dom-wrappers.json)

* 將原本的 `"outputPath": "OdfKit/DOM/Generated/GeneratedDomWrappers.g.cs"` 修改為目錄設定 `"outputDirectory": "OdfKit/DOM/Generated/"`。

#### [DELETE] [GeneratedDomWrappers.g.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/DOM/Generated/GeneratedDomWrappers.g.cs)

* 刪除此單一巨大檔案，改由多個產生的 `OdfElementWrappers.*.g.cs` 替代。

---

### 2. 流式讀寫 API 與混合輸出模組 (Spreadsheet & Text)

為流式讀寫 API 設計狀態安全與支援 DOM 子樹寫入的混合功能，並引入 Span 零分配、IAsyncDisposable 異步非阻塞寫入、高速直接輸出、流式範本解析、Sylvan CSV 流式對接與 DbDataReader 介面實作。

#### [MODIFY] [OdsStreamWriter.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/Spreadsheet/OdsStreamWriter.cs) 與 `OdtStreamWriter.cs`

* 引入步驟式 Fluent Builder 介面。

* \*\*實作 `IAsyncDisposable`\*\*。

* **新增 `WriteNode(OdfNode node)` 方法**。

* **`ReadOnlySpan<char>` 與異步 `ValueTask` 寫入優化**。

* **流式寫入的雙緩衝非同步流水線**：內部採用雙 `ReadOnlyMemory<byte>` 緩衝機制輪替寫出，CPU 格式化與物理 I/O 寫出完全並行非阻塞。

* **交錯流式寫入多工作表**：支援 `SwitchToSheet(string)` 交錯寫入，底層利用租用記憶體/MMF 虛擬分區快取各 Sheet XML 區段，並於 `Dispose` 時一併輸出。

* **內建流式範本合併引擎**：新增 `ApplyTemplateAsync(Stream templateStream, IDictionary<string, object> data)`，並支援 `[#foreach item in Items]` 與 `[/foreach]` 表格列與段落循環套印，內部會自動依據標記切割並複製 DOM 結構進行動態展開。

* **流式 RLE 壓縮與高速直接 XML 寫入**：於寫入熱路徑中，針對確定性標籤，使用預先定義的 `ReadOnlySpan<byte>` 直接寫入 Stream，繞過 `XmlWriter`；寫入器內部自動快取單元格與 Row 狀態，並在適當時機寫出 `table:number-columns-repeated` 與 `table:number-rows-repeated` 屬性進行 RLE 壓縮。

* **Sylvan CSV 高性能流式對接**：新增 `WriteCsvStreamAsync` 支援 CSV 流非阻塞式灌入。

* **自動欄寬計算支援**：流式寫入時自動累計儲存格的最大字元長度，並在儲存前產生或套用對應的列寬定義。

* **零分配 UTF-8 直接寫入**：結合 `Utf8Formatter` 直寫輸出 `Span`，免除屬性與節點名稱演示字串分配。

* **`IBufferWriter<byte>` 零拷貝寫入管道**：支援直接對接並寫入 `IBufferWriter<byte>` 或 `PipeWriter` 進行零拷貝的 XML 動態寫出，消除中介 Stream 的轉拷開銷。

* \*\*二進位分段串流 (Incremental HTTP Streaming)\*\*：新增 `ToAsyncEnumerable()` API，傳回 `IAsyncEnumerable<ReadOnlyMemory<byte>>`，支援與 Web 伺服器 Response 串流對接進行分段 Chunked 傳輸。

* **多執行緒流水線解析**：整合 `System.Threading.Channels` 於大檔案流式載入，多線程並行解析 DOM 子樹。

* **並行隨機存取解壓**：底層整合 `MemoryMappedFile`，對大型 `content.xml` 的壓縮 Entry 進行多執行緒隨機分塊 Deflate 解壓。

* **SIMD 詞法掃描與零分配轉碼**：於 XML 讀取熱路徑中，針對 `net10.0` 啟用 SIMD 加速字元比對，並配合 `transcoding cache` 享元快取，消除重複解碼為 C# 字串的 Heap 開銷。

* **零分配屬性二進位讀取**：底層在解析屬性時，直接對接 `ReadOnlySpan<byte>` 比對名稱並呼叫 `Utf8Parser` 直讀強型別值。

* **自訂零分配拉取解析器**：新增自訂 UTF-8 拉取解析器（\*\*[NEW] [OdfUtf8XmlReader.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/DOM/OdfUtf8XmlReader.cs)\*\*），實現熱路徑無物件分配解析。

* **作業系統 I/O 與硬體 CRC32**：流式儲存對接 Windows/Linux 的 Direct I/O，並啟用 PCLMULQDQ (x86) / ARM64 Crc32 硬體加速 CRC-32/ISO-HDLC 計算。

#### [MODIFY] [OdsStreamReader.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/Spreadsheet/OdsStreamReader.cs) 及 `OdtStreamReader.cs`

* \*\*實作 `System.Data.Common.DbDataReader`\*\*。

* **工作表快速跳過**：當定位至非目標工作表 `<table:table>` 節點時，直接呼叫 `XmlReader.Skip()` 跳過整棵子樹，避免 Cell 屬性解析與字串分配開銷。

* **SIMD 向量化 XML 轉義字元掃描與解碼**：以 `Vector256<byte>` 向量化加速讀取 XML 中的 `&amp;`、`&lt;` 等轉義字元，提升文字解碼速度。

* **Span-based 浮點數與整數 Fast-LUT 解析**：在載入屬性或單元格資料時，透過 Span 高速 LUT 快取解析短數值與整數字串，降低對系統 `double.Parse` 的 CPU 耗時與分配。

* **XML 屬性享元去重與常值字串表**：高頻 XML 屬性名與常值屬性值比對成功後，直接引用預快取的靜態 string 實例，減少 95% 以上字串 Heap 分配。

* **大檔案載入的執行緒親和性與優先權調度**：自訂輕量調度器，將大檔案解析/解壓線程綁定 CPU 特定核心並動態調高優先權，最大化快取局部性與 CPU 吞吐量。

* **MMF-based XML 節點二進位 Offset 索引圖隨機定位**：在讀取巨大 ODF 文件時，不解析 DOM，僅在 MMF 上掃描 `<table:table>` 節點的 XML 起始/結束 Offset 並建立索引圖，達成隨機按需讀取。
* **SIMD 命名空間字首 Checker**：以 `Vector128<byte>` 快速比對二進位字首（如 `table:` 等），省去字串 prefix 切割與查表開銷。

---

### 3. 底層型別與列舉優化模組 (DOM & Core)

優化型別轉換，減少代碼雜訊，增強開放域屬性的擴充彈性。

#### [MODIFY] [OdfColor.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/DOM/OdfColor.cs) 與 `OdfLength.cs`

* 於 `OdfColor` 中新增 `public static implicit operator OdfColor(System.Drawing.Color color)`。

* 於 `OdfColor` 中新增 `public static implicit operator OdfColor(string htmlColor)`，支援十六進位字串隱式轉換。

* 於 `OdfLength` 所屬命名空間新增靜態擴充方法類別 `OdfUnitExtensions`，為 `double` 與 `int` 注入 `.Cm()`、`.Pt()`、`.Mm()` 等擴充方法。

#### [MODIFY] [OdfPresentationTransitionType.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/DOM/OdfPresentationTransitionType.cs)

* 將原 `enum OdfPresentationTransitionType` 重構為唯讀結構 `public readonly struct OdfPresentationTransitionType`，包含靜態屬性 `Manual`、`Automatic`、`SemiAutomatic`，並支援透過 `Custom(string)` 傳入自訂特效名稱字串。

---

### 4. DOM 操作與延遲載入優化模組 (DOM)

為開發者提供更快速的節點尋覽、強型別屬性與樣式管理機制，並引入延遲載入、批次更新、延遲公式計算、批量資料灌入、單元格稀疏儲存與樣式保留文字替換。

#### [MODIFY] [OdfNode.TextContent.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/DOM/OdfNode.TextContent.cs) 與 `OdfElement.cs`

* **自動解析特用字元**：於 `TextContent` 設值器中加入過濾器。若偵測到 `\t`、`\n` 或多於一個空白，API 在內部將其分割並組合成 `OdfNode`。

* **罕見字與自造字字型補全與子集化**：當寫入文字時，底層 API 自動掃描並補全對應的 `<style:font-face>` 宣告。提供 `IFontSubsetter` 介面擴充點，允許擴充套件註冊字型子集化實作，在存檔時將 PUA 自造字抽取為微型子集內嵌寫入 OdfPackage。

* **新增 LINQ 友善的強型別尋覽擴充**：新增 `Children<T>()` 與 `Descendants<T>()`。

* **支援鏈結式 (Fluent) DOM 建立**：新增 `Append(params OdfNode[] children)` 傳回此元素本身。

* **DOM 延遲解析（Lazy Loading）機制**。

* **批次更新（BeginUpdate / EndUpdate）**。

* **公式延遲計算鏈 (Lazy Calculation Chain) 與 DAG 並行計算**：重構試算表 DOM 更新機制，新增 `OdfFormulaEvaluator`；使用 DFS/Kahn 拓撲排序，當偵測到公式循環引用時，不直接崩潰，將結果設為 `#REF!` 並記錄 Diagnostics 警告；重算時分析依賴 DAG，於 ThreadPool 中並行評估無相依的公式分枝，充分釋放多核心 CPU 效能。

* **樣式保留的文字替換**：新增 `ReplaceText(string oldValue, string newValue)` 擴充方法，內部自動跨 Text 節點重組字串，在 XML 節點層面精準進行局部替換， 100% 完整保留原有格式。

* **強型別 Flyweight 轉型與零分配**：於 `OdfNode` 中提供 `As<T>() where T : OdfElement` 方法。底層以私有 `_wrapper` 欄位快取強型別對應實例，實現轉型時零 Heap 物件分配，並確保整份文件中一個 XML 節點唯一繫結同一個實體 Wrapper 實例以防狀態衝突。

* **二進位區塊深拷貝與延遲解析**：優化 `CloneNode(true)`。若子樹未修改，直接對底層 XML 二進位數據 Block Copy 並掛載為延遲節點，讀取時才具現化，實現大子樹複製的極速與「零 Heap 分配」。

* **智慧行內富文字設值與狀態機解析**：於 `OdfParagraph` 與 `TableTableCellElement` 提供 `RichText` 屬性。內部實作一個零分配的 Markdown/HTML行內標籤解析狀態機，自動解析如粗體、斜體、字色並掛載 `<text:span>` 節點，且自動調用享元池複用樣式名稱。

* **自定義元素強型別註冊與繫結**：設計全域 `OdfWrapperRegistry` 註冊表。允許開發者註冊自定義強型別 Element class，底層解析器在遇上非標準 XML 標籤與 Namespace 時，自動具現化為該強型別 Wrapper，使擴充標籤操作享受與標準 DOM 一致的強型別 API 開發體驗。

* **鏈式行內富文本建構**：提供 `AppendText()` 傳回 `InlineTextBuilder`（\*\*[NEW] [InlineTextBuilder.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/DOM/InlineTextBuilder.cs)\*\*），實現強型別的鏈式富文本建構。

* **DOM 結構相容性檢查與診斷**：新增 `Validate()` 於 `OdfDocument`，利用從 Schema 產生的節點關係拓撲表（\*\*[NEW] [OdfDocumentValidator.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/DOM/OdfDocumentValidator.cs)\*\*）進行快速 DOM 校驗。

* **記憶體分配過載與反模式操作警示**：於偵錯或特定監控模式下，若單次載入節點數超標、發生 LOH 碎裂風險或高頻 Boxing 裝箱等反模式操作時，自動在 Diagnostics 輸出詳細警告與效能優化建議。

* **可回收 Wrapper 物件池**：在 `OdfNode` 底層加入可回收 Wrapper 的 Pool 機制，回收 `WeakReference` 物件。

* **書籤與目錄生成 API**：新增 `OdfBookmarkManager`（\*\*[NEW] [OdfBookmarkManager.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/DOM/OdfBookmarkManager.cs)\*\*）與 `InsertTableOfContents` 方法。

* **Fluent 巢狀清單建構**：於段落新增 `AppendList()` 傳回 `OdfListBuilder`（\*\*[NEW] [OdfListBuilder.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/DOM/OdfListBuilder.cs)\*\*）。

* **圖表資料繫結與幾何變換**：新增 `OdfChartBuilder`（\*\*[NEW] [OdfChartBuilder.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/DOM/OdfChartBuilder.cs)\*\*）以支援 `AddChart` 與資料繫結；向量 Shape 新增 `Transform` 屬性以支援 `System.Numerics.Matrix3x2` 仿射矩陣。

* **未知節點保留**：解析器遇未知標籤時，自動解析為 `OdfUnknownElement`，存檔時 100% 寫回以保證 Round-Trip 無損。

* **條件格式化配置與行列摺疊分組**：提供 `AddConditionalFormat()` 條件格式引擎，以及 `table.Rows.Group()` 巢狀分組機制。

* **渲染與 PDF 匯出抽象**：Core 內建 `IOdfRenderer` 抽象（\*\*[NEW] [IOdfRenderer.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/Core/IOdfRenderer.cs)\*\*）與 `OdfRendererRegistry`，支援動態載入 PDF 轉檔功能。

* **儲存格合併與覆蓋管理**：提供 `MergeCells` 與 `UnmergeCells`，自動插入 covered 節點對齊。

* **合約修訂追蹤引擎**：提供 `doc.TrackChanges` 引擎，將 DOM 修改轉為標準修訂節點記錄。

* **非受控儲存與層疊樣式快取**：表格底層改為非受控的 POH/NativeMemory 表格儲存，公式計算新增 SIMD 區間重算加速，且 DOM 樣式存取實作 `ComputedStyle`（\*\*[NEW] [ComputedStyle.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/Core/ComputedStyle.cs)\*\*）解析器與層疊快取。

* **工作表二進位極速拷貝**：於 `TableTableElement` 實作 `AdoptSheet` 方法，以 Block Copy 快取拷貝原始 UTF-8 區段，繞過 DOM 樹解析。

* **Fluent 樣式混合與自動繼承**：新增 `ApplyStyle()` 鏈式 API 與 `OdfStyleMixinBuilder`，自動管理 XML 繼承樣式去重。

* **主從巢狀套印與安全欄位提取**：新增巢狀 `#foreach` 報表展開（含 Vertical Merge 與 SUBTOTAL 插入），並實作 `ExtractFields()` 合約欄位無損重組與雙向對照。

* **HTML/Markdown 行內富文本一鍵解析渲染**：於段落或單元格中提供 `AppendHtml(string)` 與 `AppendMarkdown(string)`，直接解析為 `<text:span>` 強型別子樹。

* **Excel-like 區域選取與批次格式化 API**：提供 `table.Cells[string range]`，支援 Fluent 批次設定 `.SetValue()` 與格式化。

* **自訂自動篩選與排序定義**：提供 `table.AutoFilter(string range)` 與 `table.Sort(...)` 一鍵產生符合規格的 XML 篩選與排序配置。

* **影像仿射裁剪與進階特效 API**：為 `DrawImageElement` 提供 `Crop` 與 `SetEffects`，支援幾何變換與陰影/圓角/濾鏡 XML 生成。

* **主動樹枝化剪裁與 Wrapper 物件銷毀**：提供 `PruneAndCollect()` 以主動將子樹斷開、清空快取並回收 POH/NativeMemory 空間，降低 GC 的 Gen 2 堆積。

* **Fluent 統計公式建構器**：提供強型別的 `Formula` 靜態語法生成，避免手寫 ODF 公式字串出錯。
* **單元格多行富文本與自動換行 API**：提供 `cell.RichText` 流暢設定單元格內局部字體樣式與換行（自動產生 `<text:line-break>` 與 style wrap 配置）。
* **表單一鍵安全填充器**：提供 `doc.FormFields` 機制，一鍵安全更新 `<text:text-input>` 等表單欄位。
* **Excel-like 大範圍 Fluent 邊框管理器**：支援選取範圍鏈式設定內外框線，底層自動計算邊界/內部 XML 樣式差異並去重。
* **影像自動 DPI 壓縮與 WebP 轉換**：自動壓縮大圖並將 PNG/BMP 轉為 WebP，大幅降低 Zip 文件體積。
* **表格稀疏儲存的冷熱數據分離**：將熱數據單元格儲存於 Pinned Unmanaged 記憶體中供 SIMD 高速累算，冷數據儲存於壓縮分頁。
* **文字文檔段落節點預綁定**：大批量產生段落時複用同一 `OdfParagraph` 享元，僅修改內部 Span 指針，實現「零 Wrapper 物件分配」。
* **SVG 幾何路徑二進位快速解析**：直接於 UTF-8 位元組中狀態機解析路徑，免除字串分割與裝箱。
* **圖表數據離線快取與按需延遲載入**：唯有在公式重算或修改時才解析圖表內嵌 ODS。
* **樣式衝突的語意智慧解決**：在合併多文件時，對同名樣式進行語意分析與繼承化重組，以最小 XML 增長維持視覺正確。
* **自訂屬性 Span 快速查表**：利用 Span-based Fast-LUT 快速過濾自訂 XML 屬性。
* **二進位 XML 區段的 Dirty 標記局部序列化**：只對 Dirty 標記的區段進行 XML 序列化，未修改區段 Block Copy 原始二進位。
* **一鍵 HTML 公文樣式對應器**：提供 `doc.ImportHtmlStyles(css)` 自動將 CSS 映射寫入 styles.xml。
* \*\*效能遙測與遠端測量 (Performance Telemetry & ActivitySource)\*\*：引入 `System.Diagnostics.ActivitySource` 與 `System.Diagnostics.Meter`，支援開發者透過 OpenTelemetry 監控 Zip 解壓、XML 解析、樣式享元池與非受控記憶體佔用等關鍵效能指標。
* \*\*記憶體分配過載與反模式操作警示 (Anti-Pattern Diagnostics)\*\*：於偵錯模式或特定監控配置下，當偵測到 LOH 碎裂風險、單次載入節點數過多或高頻迴圈中進行 Boxing 裝箱等反模式操作時，自動在 Diagnostics 輸出警告與程式碼最佳化建議。
* \*\*`IBufferWriter<byte>` 零拷貝寫入管道 (Zero-Copy PipeWriter Integration)\*\*：使 `OdsStreamWriter` 與 `OdfPackage` 的底層寫入器對接 `IBufferWriter<byte>`，實現將產生的二進位直寫 Web 輸出管道，免去中間 `MemoryStream` 轉拷開銷。
* **低階二進位操作的「沙盒事務防護 (Low-level Sandbox Transaction)」**：引入 `OdfTransaction` 區塊，當進行極低階之 MMF 原位二進位 Patch 修改或 EOCD 覆寫時，若中途拋出例外會自動進行 Rollback，保護物理檔案結構不致毀損。
* \*\*虛擬檔案系統偵錯視圖 (VFS Debug Visualizer)\*\*：提供 `DumpVfsLayout()` 方法與 IDE 整合之 `[DebuggerTypeProxy]` 偵錯屬性，將 Zip 封裝內部各 Entry 的物理 Offset、壓縮大小、加密算法、Dirty 狀態以結構化表格呈現，提昇底層 Debug 效率。
* **寫時複製樣式的動態歸併與冷回收**：在存檔或背景閒置時，自動比對並歸併屬性完全相同的 COW 樣式，並回收無效樣式，對使用者完全透明。
* \*\*二進位分段串流 (Incremental HTTP Streaming)\*\*：為 `OdsStreamWriter` 新增 `ToAsyncEnumerable()` API，支援與 ASP.NET Core 的 Response Stream 對接，以 Chunked 方式邊壓縮邊串流輸出，將記憶體保留量限制在幾 KB 內。
* \*\*未受控記憶體安全生命週期追蹤器 (Unmanaged Memory Leak Tracker)\*\*：實作 `OdfMemoryTracker` 全域診斷點，追蹤所有 POH 鎖定與 NativeMemory 分配的 Stack Trace，若有洩漏則於終結器中發出警告。
* \*\*零拷貝 Entry 位元組直改器 (Zero-Copy Native Entry Patcher)\*\*：提供 `package.RawEntryPatch()` API，直接傳入 Entry 的原始 UTF-8 唯讀 Span 進行特徵比對與 Patch，免除 DOM 解析與 Heap 物件分配。

* **一鍵強型別數據透視表生成**：於 `TableTableElement` 提供 `CreatePivotTable`，自動管理極其繁瑣的 Pivot Table XML 節點結構與對應欄位映射。

* **文字段落大綱與多級清單自動化編號**：段落提供 `SetOutlineLevel` 與自動清單編號，底層自動維護 `text:list-style` 的複雜對應關係。

* **條件格式化圖示集與進階視覺化**：支援一鍵套用 `AddDataBar` 與 `AddIconSet` 條件格式化視覺效果與 XML 配置生成。

* **ODT 樣式表自動精簡與未使用樣式垃圾回收**：提供 `doc.Styles.GC()` API，一鍵遍歷並回收未引用的樣式，以精簡 XML 文件體積與載入開銷。

* **試算表凍結窗格與視窗分割 API**：提供 `FreezePanes` 與 `SplitWindow` API，自動生成與 LibreOffice/Excel 相容的 `<table:views>` 配置。

* **雙平台 PDF 匯出無相依字型對照表**：核心庫提供無相依的跨平台字型替代對照表與字元寬度位移計算，確保 Linux/Windows 排版 100% 一致。

* **公式重算的非同步非阻塞異步通道**：重算引擎透過 `System.Threading.Channels` 與背景執行緒解耦，變更時非同步重算並發佈，前端零卡頓。

#### [MODIFY] [DomWrappersCSharpWriter.cs](file:///d:/Dev/Project/Application/OdfKit/tools/OdfSchemaGenerator/DomWrappersCSharpWriter.cs)

* **新增強型別智慧屬性**：為特定元素一併產生對應的 `double? FloatValue`、`DateTime? DateValue`、`bool? BooleanValue`。

* **新增試算表二維索引器 (Excel-like Indexer) 與分頁式零裝箱儲存**：為 `TableTableElement` 產生 `public TableTableCellElement this[string address]` 與 `public TableTableCellElement this[int row, int col]` 屬性。底層稀疏儲存採用以 1024x1024 為 Page（區塊）的分頁式 `OdfCellData` 緊湊結構體指標表，利用 `[StructLayout(LayoutKind.Explicit)]` 避免數值裝箱。定址複雜度為 O(1)，且行列增刪時只需挪動局部 Page，完全避免全表級 `Array.Copy` 停頓。

* **批量資料對接 (Bulk Data Import) 與 Off-tree 離線建置與運算式樹快取**：於 `TableTableElement` 生成類別中新增 `ImportData(DbDataReader reader)` , `ImportData(DataTable table)` 與 `ImportData<T>(IEnumerable<T> collection)`。底層實作「Off-tree 離線節點建置」與一次性掛載；針對實體集合匯入，內部實作「動態編譯運算式樹快取」，在首次載入型別 T 時動態將其屬性存取編譯為強型別 IL 委派並快取，完全消除 PropertyInfo 反射開銷與裝箱壓力。

* **零分配儲存格遍歷器**：為 `TableTableElement` 產生 `EnumerateCellViews()` 方法，傳回 `readonly ref struct` 迭代器，支援在底層稀疏儲存上直接滑動並提供讀寫功能，實現數百萬儲存格遍歷的「零 Heap 分配」。

* **批次行列操作 API 與分頁指標交換**：為 `TableTableElement` 生成 `Rows` 與 `Columns` 管理屬性，提供 `Insert/Move/Copy` 批次方法。底層於 `Move` 時直接交換 Page 記憶體指標以避開資料拷貝；於 `Insert` 批次空行或複製時，自動在 XML 端套用 `number-rows-repeated` 合併，使 XML 記憶體佔用降至 O(1)。

#### [MODIFY] [OdfDocument.Styles.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/Core/OdfDocument.Styles.cs) 與樣式配置 API

* **新增自動樣式設定器 (Fluent Style Configurator) 與享元樣式池**：為 DOM 元素提供 `ConfigureStyle(Action<OdfStyleBuilder> action)`，底層對接 `Flyweight Style Pool` 進行屬性 Hash 智慧比對與複用，防止 XML 樣式定義膨脹。

* **智慧數值格式設值器與去重**：為儲存格與樣式提供 `NumberFormat` 屬性。底層內建格式字串解析狀態機，自動識別貨幣、日期、百分比等自訂格式字串，於 styles.xml 產生標準 XML 節點，並利用享元池對 `data-style-name` 進行自動去重與隱式繫結。

* **寫時複製 (COW) 樣式繼承**：在 `CopyFormatFrom` 時直接複用樣式名，修改時才執行 COW 以免影響原節點。
* **樣式自動歸併與冷回收**：自動分析樣式池中屬性完全相同的 COW 樣式，重對照 DOM 名稱並進行隱式歸併；自動回收無效與未引用的孤立樣式。

---

### 5. Package 與 Namespace 自動化模組 (Core & DOM)

簡化底層節點寫入時 Namespace 與 Package 媒體宣告管理，並提供一鍵式異步硬體加速簽章與無分配名稱解析。

#### [MODIFY] [OdfNode.Attributes.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/DOM/OdfNode.Attributes.cs)

* 修改 `SetAttribute` 方法。當使用者未指定 `prefix` 時，API 內部自動匹配對應 Namespace URI 的標準字首。

* \*\*即時屬性值驗證 (Debug 模式)\*\*：在 Debug 下對寫入的屬性值進行規格合規性比對，若不合規則丟出 Warn 或拋出 `ArgumentException`。

#### [MODIFY] [OdfPackage.PublicApi.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/Core/OdfPackage.PublicApi.cs) 與 `OdfSigner.cs`

* **自動管理 manifest.xml**：當使用者呼叫 `AddEntry` 或 `WriteEntry` 時，API 自動對照檔名副檔名與對應的 MIME-type，並同步將其寫入 `META-INF/manifest.xml`。

* **二進位快速拷貝、並行 Zip 儲存與 MMF 零拷貝加載**：在 `OdfPackage` 的儲存機制中，對未修改之 Lazy Entry 進行零分配直接二進位快速拷貝；對需 Deflate 壓縮之 Entry 於 ThreadPool 背景並行壓縮至 ArrayPool 暫時記憶體，再由主執行緒依序寫出；提供 `SaveEncryptedAsync` 與 `LoadEncryptedAsync` 加密存取並啟用 AES-NI 加速。當從本機磁碟載入大檔案時，底層自動使用 `MemoryMappedFile` 映射至虛擬記憶體，配合指針對接 `UnmanagedMemoryStream` 進行零 Heap 分配解析，徹底避開 LOH 碎裂。

* **直接 In-place Zip 二進位 Patch 修改**：在進行微量文字或儲存格資料更新且未超出 Entry 預留長度時，底層利用 MMF 直接原位 (In-place) 覆寫 Zip 的對應二進位區段，免除整包重壓的 I/O 開銷。

* **高效二進位 Patch 的寫時複製區段**：在 In-place Patch 時若 XML 長度超出預留空間，自動在 Zip 末尾追加寫入新增 sector，並修改 Central Directory 中的對應 offset，以 O(1) 速度完成局部寫入且維持檔案結構最小化。

* **一鍵 PDF 數位簽章嵌入**：在 Core 轉檔渲染中，支援於 PDF 輸出時接收證書並在 PDF 二進位結構中一鍵嵌入合規的數位簽章，便於合同合同安全流轉。

* **Pinned Array Buffer 與非受控並行壓縮**：在並行 Deflate 壓縮緩衝區時，Pinned 陣列僅用於避免 GC 移動；若需對接 Direct I/O，必須改用 `NativeMemory.AlignedAlloc` 建立 4096 位元組對齊緩衝區，避免把未對齊 managed array 傳入無快取 I/O。

* **AES-GCM 認證加密與硬體加速**：文檔加解密擴充支援 AES-GCM 認證加密模式，利用 .NET 10.0 的 `AesGcm` 在 AES-NI 硬體指令集上並行計算，增強防篡改與防重放保護。
* **零記憶體 Central Directory 與 EOCD 直接修改**：In-place Patch 時直接於 MMF 尾端二進位覆寫 Zip 中央目錄與 EOCD，零 Heap 分配。
* **自適應執行緒調度與系統 CPU 核心預留**：為並行計算設定核心預留比例，防止 OdfKit 跑滿 CPU 導至伺服器響應卡頓。
* \*\*效能遙測與遠端測量 (ActivitySource / Meter)**：實作 `OdfPerformanceTelemetry`（**[NEW] [OdfPerformanceTelemetry.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/Core/OdfPerformanceTelemetry.cs)\*\*），對接標準 `ActivitySource` 與 `Meter` 以監控 Zip/XML/GC 效能。
* \*\*沙盒事務防護 (Sandbox Transaction)**：實作 `OdfTransaction`（**[NEW] [OdfTransaction.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/Core/OdfTransaction.cs)\*\*），支援低階 In-place Patch 時的 rollback 機械，防範物理毀損。
* \*\*虛擬檔案系統偵錯視圖 (VFS Debug Visualizer)\*\*：提供 `DumpVfsLayout()` API 與在 `OdfPackage` 宣告自訂 `DebuggerTypeProxy` 偵錯包。
* \*\*未受控記憶體安全生命週期追蹤器 (Unmanaged Memory Leak Tracker)**：實作 `OdfMemoryTracker`（**[NEW] [OdfMemoryTracker.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/Core/OdfMemoryTracker.cs)\*\*）全域診斷點，自動追蹤 POH 與 NativeMemory 分配的 Stack Trace，若有洩漏則於終結器中輸出警告。
* \*\*零拷貝 Entry 位元組直改器 (Zero-Copy Native Entry Patcher)\*\*：提供 `RawEntryPatch(string entryName, Func<ReadOnlySpan<byte>, IBufferWriter<byte>, bool> patcher)` API，直接傳入原始二進位進行快速 Patch 覆寫。

* \*\*一鍵式異步簽章 (SignDocumentAsync)\*\*：提供 `OdfDocument.SignDocumentAsync(X509Certificate2 cert)` 擴充方法，內部自動遍歷 Zip 包計算雜湊，並產生 XML 數位簽章。於 `net10.0` 下透過條件編譯啟用高性能 SHA256/ECDSA 硬體加速。

* **跨文件節點採納 (AdoptNode) 與多文件智慧合併**：於 `OdfDocument` 實作 `AdoptNode(OdfNode node)`，在移轉子樹時直接更改 Document 所有權與鏈結指針，實現 $O(1)$ 的零拷貝轉移。內部實作「命名空間自動重對照與遞迴合併」，自動補全 `xmlns` 與 remap 衝突前綴。於合併文檔 API 中實作「樣式屬性 Hash 比對與自動命名去重」；同時，在移轉媒體或合併時，自動對媒體二進位進行 SHA-256 雜湊比對，若目標文件已存在相同圖片，直接重對照 `xlink:href` 連結指向目標文檔既有路徑，完全免除圖片二進位資料的拷貝與寫入，最大程度壓縮合併後的檔案體積。

* **Raw Zip Entry 零重壓快取與直接輸出**：在讀取 ODF 時快取未修改 Entry 的壓縮後二進位原始數據，並在儲存時直接 Copy 到 Zip 輸出串流。

* **跨文件公式連結快取管理**：新增 `ExternalLinkManager` 於試算表，支援動態讀取外部文件進行公式計算。

#### [MODIFY] [OdfXmlReader.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit/DOM/OdfXmlReader.cs) 與名稱配置層

* \*\*預填充名稱表 (XmlNameTable)\*\*：建立 `OdfXmlNameTable` 實例，於 DOM 載入階段將 ODF 所有標準名稱注入其中，並將其套用至 `XmlReaderSettings.NameTable`，消除解析時的重複字串分配。

* **多 Entry並行載入與解析**：於 `OdfDocument.LoadAsync` 階段，透過 `Task.WhenAll` 並行解析 `content.xml` 與 `styles.xml`。DOM 樹解析時僅保留樣式字串標記，在所有 Entry 解析完畢後統一執行 DOM 與實體樣式物件的連結綁定，提升載入效能。

#### [MODIFY]強型別媒體元素（如 `DrawImageElement`）與對應生成屬性

* **自動化媒體綁定與二進位媒體去重池**：提供 `SetImageSource(byte[] bytes, string fileName)` 方法。內部自動將二進位資料計算 SHA-256 雜湊，相同媒體資源自動複用 Package 既有路徑與 `xlink:href` 屬性，不再重複寫入相同二進位，實現極佳的體積優化。

---

## 本次增補實作範圍（2026-06）

### Workstream B：主格式能力樣板抽象化

* **範圍**：`TextDocument`、`SpreadsheetDocument`、`PresentationDocument`、`DrawingDocument` 的 `Load/LoadAsync` 四組多載，統一收斂至 `OdfDocumentVariantSupport`。
* **做法**：在 `OdfDocumentVariantSupport` 新增泛型 `Load` / `LoadAsync` helper（支援 path 與 stream），內部統一執行 `DocumentKind` 驗證與失敗釋放，主格式類別僅保留一行轉呼叫。
* **不納入本波**：`Create()`、`Builder()`、`CreateFromTemplate()` 與各變體類別 `Load` 樣板碼維持現狀（投入成本高於收益）。
* **目標**：不變更任何公開 API 簽章，降低未來新增主格式時的重複與漏改風險。

### 第 7 節延伸項目納入

1. **Chart - Legend 統一可編輯模型**
    - 新增 `OdfChartLegend`，集中 `IsVisible`、`Position`、`Alignment`、`StyleName` 與 `Style`。
    - `OdfChartDocument.Legend` 作為新入口。
    - `LegendPosition`、`LegendAlignment`、`LegendStyle` 保留，並委派到新模型以維持相容。

2. **Chart - Fluent Builder API**
    - 新增 `ChartDocument.Builder()`。
    - 新增 `ChartDocumentBuilder`、`ChartAxisBuilder`、`ChartSeriesBuilder`，支援 `WithType`、`WithDataRange`、`WithLegend`、`WithAxis`、`ConfigureSeries` 等鏈式設定。
    - 底層直接重用既有 `OdfChartDocument` / `OdfChartSeries` / `OdfChartStyle` 能力，不重寫資料模型。

3. **Formula - 語意編輯 Helper（查詢 - 修改 - 更新）**
    - `OdfMathToken` 新增 `FindFirst`、`FindAll` 與不可變 `WithChild`。
    - `OdfFormulaDocument` 新增 `ReplaceFirst`，可對既有公式樹做局部替換而非整棵重建。
    - 設計維持 immutable token 風格，避免引入可變狀態模型。

---

## Verification Plan

在變更實施過程中與完成後，必須執行完整測試以確保所有代碼功能未受破壞。

### Automated Tests

* **Parity 測試修正**：更新 [TypedDomParityTests.cs](file:///d:/Dev/Project/Application/OdfKit/OdfKit.Tests/TypedDomParityTests.cs)，適配拆分後的 DOM wrappers 結構。

* **建置與單元測試**：在每一次模組變更後，於終端機執行 `dotnet test`，確保所有現有測試與新增 API 的單元測試全部通過。

### Manual Verification

* **編譯效能評估**：對比大檔拆分前後，Visual Studio 或 Rider 的背景偵錯載入時間與記憶體耗用。
