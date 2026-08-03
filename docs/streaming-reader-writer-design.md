# ODS／ODT 串流 Reader／Writer 設計與最佳化準則

本文件定義 OdfKit 的 ODS／ODT 串流 I/O 維護方向。目標是在不建立完整 DOM 的前提下，
維持可預測的常駐記憶體、安全的 XML／ZIP 邊界，以及容易跨 .NET 目標框架維護的實作。

## 權威基準

- ODF 1.4 package 只允許 ZIP entry 使用 `STORED` 或 `DEFLATED`，且 `mimetype` 應為第一個、
  不壓縮且不含 extra field 的 entry。見 [OASIS ODF 1.4 Packages][odf14-packages]。
- 不可信 XML 使用 `XmlReaderSettings` 明確禁止 DTD、停用 resolver，並設定文件字元上限；
  見 [Microsoft XmlReaderSettings 安全注意事項][xml-reader-settings] 與
  [OWASP XXE Prevention Cheat Sheet][owasp-xxe]。
- `ZipArchive.Dispose()` 會完成 archive 與中央目錄，因此 Writer 不把半完成 ZIP 描述為
  可安全恢復的文件。見 [Microsoft ZipArchive.Dispose][zip-dispose]。
- ZIP instance 不在多執行緒間共享；大型不可信封裝避免 `Update`，並維持 entry／總解壓量限制。
  見 [Microsoft .NET ZIP／TAR 最佳實踐][zip-best-practices]。
- 真正的非同步檔案 I/O 由 Stream async API 與 cancellation token 承擔；大型 parser backing
  可使用作業系統虛擬記憶體，而不是宣稱所有 CPU DOM preparation 都是非同步。見
  [Microsoft 非同步檔案 I/O][async-file-io] 與 [Memory-mapped files][memory-mapped-files]。
- DEFLATE 格式本身沒有原生隨機存取。稀疏 access point 可以改善特定工作負載，但需要
  額外儲存壓縮與 parser 狀態，並不屬於一般 forward-only Reader 的必要條件。

上述來源於 2026-08-02 重新核對。專案公開效能數值仍只使用可重現的 OdfKit benchmark，
不以外部專案數字代替同機、同資料、同語意驗證。

## 核心決策

### Reader

1. 預設採單趟 forward-only pull parsing，不預先建立完整文件索引。
2. Metadata 採延遲載入；只有呼叫端要求完整工作表名稱時才掃描完整 ODS `content.xml`。
3. 同步與非同步路徑共用相同資料與安全語意。非同步路徑不得為方便而將完整資料列先轉成
   XML 字串再解析一次。
4. 所有文字、repeat、列、欄及 XML 文件限制都必須在大量配置或展開前檢查。
5. 使用 BCL `ZipArchive` 與 `XmlReader`，不維護自有 DEFLATE 或 XML parser。

### Writer

1. 維持嚴格順序寫入；預設熱路徑不儲存完整工作表或文件 DOM。
2. `mimetype` 先寫且不壓縮；大量 `content.xml` 使用偏重吞吐量的壓縮設定，小型 metadata
   entry 可使用較高壓縮率。
3. `FlushAsync` 只保證已產生 XML 傳遞到底層 entry，不代表 ZIP 已完成；只有完成／處置
   archive 後才是完整 ODF package。
4. 長時間輸出若需要失敗復原，由呼叫端儲存資料來源 cursor，寫入暫存檔，完整關閉與驗證後
   再替換正式檔案；不序列化半完成 `ZipArchive`、壓縮器或 `XmlWriter` 狀態。

## 隨機存取與 Checkpoint

[`daniilvaino/Deflux`][deflux] 展示了以可序列化壓縮／XML 狀態快速重新開啟工作表的
替代設計，適合高頻工作表定位
或跨程序續讀。OdfKit 文件保留這項生態系觀察，但目前不引用其實作、不新增套件相依，也不在
核心實作 Checkpoint 引擎。

這項決策的理由如下：

- ODS／ODT 的主要串流情境是單次循序匯入、匯出與文字擷取；Checkpoint 對首次完整讀取
  沒有普遍收益。
- BCL 的 DEFLATE stream 不支援 seek；可恢復實作必須自行儲存 dictionary、bit offset、
  XML stack 與 namespace，顯著擴大安全與相容測試矩陣。
- 自有 parser 會繞過目前 `XmlReaderSettings` 提供的 DTD、resolver 與字元預算契約。
- Reader 的高頻熱資料重複查詢應由上層有界 cache 或 DOM 模型處理，不應改變 forward-only
  cursor 的記憶體契約。

若未來有實際採用證據，隨機存取應先以獨立 extension 或 adapter 進行同機 benchmark、corpus、
fuzzing 與威脅模型驗證；核心 Reader 不為尚未驗證的需求預先加入 provider abstraction。

## 已採用的最佳化

| 範圍 | 措施 | 效益 |
|------|------|------|
| ODS Reader | 建構時不再無條件掃描工作表名稱 | 直接讀第一張表時避免額外完整解壓與 XML 掃描 |
| ODS Reader | `SheetNames` 延遲掃描並於 Reader 生命週期內快取 | 只有需要 metadata 的呼叫端支付成本 |
| ODS Reader | async 路徑直接走 row subtree | 避免 `ReadOuterXmlAsync`、整列字串與第二次 XML 解析 |
| ODS Reader | 儲存格段落長度使用累計值 | 避免每個段落重算既有集合 |
| ODT Reader | 文字與 `text:s` 在 append 前檢查上限 | 防止先大量配置、事後才拒絕惡意輸入 |
| ODS／ODT Writer | 保留 sequential raw XML 熱路徑 | 維持低常駐與可理解的封裝生命週期 |

## 一般 DOM 載入／儲存的六項最佳化

一般 `SpreadsheetDocument`／`TextDocument` 需要可編輯 DOM，與 forward-only Reader 的契約不同；
因此採用下列可局部驗證、無 Checkpoint 狀態引擎的最佳化：

1. 未具現化的 lazy subtree 儲存時直接複製既有 UTF-8 payload；文件統計與媒體清理遇到未知
   lazy 內容時採保守策略，不為附帶最佳化強迫展開或誤刪引用。
2. XML 字元預算先走 ASCII 快速路徑，遇到非 ASCII 才執行 UTF-8 字元計數；安全上仍以
   `MaxXmlCharactersInDocument` 限制完整 lazy payload，而不是以 byte 數冒充字元數。
3. parser 僅在元素實際宣告 namespace 時複製 scope dictionary，否則安全共用父 scope；writer
   保留來源 URI／prefix 對應，擴充 namespace 在 lazy 與具現化 round-trip 都不遺失。
4. 常見 ODF qualified name 使用既有 bounded hash switch 與靜態 namespace/prefix；未知擴充名稱
   只在目前節點解析，不加入程序級無界 intern/cache，避免不可信名稱造成記憶體常駐。
5. 大型壓縮核心 XML 在完成 entry 大小、總解壓量與 CRC 驗證後，以匿名 MMF 作 parser backing；
   小型 entry 保留 byte array，內容覆寫會先釋放舊映射，避免陳舊指標與大型 LOH 常駐。
6. ODS 隨機 cell access 沿用 worksheet 範圍內的 row/cell sparse cache，並以細粒度 lock 保護首次
   發布與失效；同一 worksheet facade 的並行 `GetCell` 不會建立重複 DOM 節點。

非同步儲存會以文件範圍 semaphore 序列化同一 `OdfDocument` 的 package/DOM snapshot，再把
cancellation token 傳入非同步 I/O；這不代表可一邊修改 DOM 一邊儲存。不同文件可平行，單一
文件的 mutation 與 save/dispose 仍應由呼叫端安排明確生命週期。`ZipArchive` 本身不視為可並行
共享物件，符合 Microsoft 對 archive instance 避免跨執行緒共用的建議。

## 後續量測原則

Reader 基準至少分開量測第一張表、後段工作表、完整 metadata 掃描、同步與非同步讀取、寬列、
大量小段落及大型文字控制。Writer 則分開量測順序寫入、明確多工作表緩衝與非同步資料來源。
每項記錄 elapsed time、GC allocated bytes、peak working set 與語意 checksum；不得以不同格式、
不同資料或缺少語意驗證的數字直接排名。

[odf14-packages]: https://docs.oasis-open.org/office/OpenDocument/v1.4/os/part2-packages/OpenDocument-v1.4-os-part2-packages.html
[xml-reader-settings]: https://learn.microsoft.com/dotnet/fundamentals/runtime-libraries/system-xml-xmlreadersettings
[owasp-xxe]: https://cheatsheetseries.owasp.org/cheatsheets/XML_External_Entity_Prevention_Cheat_Sheet.html
[zip-dispose]: https://learn.microsoft.com/dotnet/api/system.io.compression.ziparchive.dispose
[zip-best-practices]: https://learn.microsoft.com/dotnet/standard/io/zip-tar-best-practices
[async-file-io]: https://learn.microsoft.com/dotnet/standard/io/asynchronous-file-i-o
[memory-mapped-files]: https://learn.microsoft.com/dotnet/standard/io/memory-mapped-files
[deflux]: https://github.com/daniilvaino/Deflux
