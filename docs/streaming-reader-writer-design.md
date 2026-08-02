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

## 後續量測原則

Reader 基準至少分開量測第一張表、後段工作表、完整 metadata 掃描、同步與非同步讀取、寬列、
大量小段落及大型文字控制。Writer 則分開量測順序寫入、明確多工作表緩衝與非同步資料來源。
每項記錄 elapsed time、GC allocated bytes、peak working set 與語意 checksum；不得以不同格式、
不同資料或缺少語意驗證的數字直接排名。

[odf14-packages]: https://docs.oasis-open.org/office/OpenDocument/v1.4/os/part2-packages/OpenDocument-v1.4-os-part2-packages.html
[xml-reader-settings]: https://learn.microsoft.com/dotnet/fundamentals/runtime-libraries/system-xml-xmlreadersettings
[owasp-xxe]: https://cheatsheetseries.owasp.org/cheatsheets/XML_External_Entity_Prevention_Cheat_Sheet.html
[zip-dispose]: https://learn.microsoft.com/dotnet/api/system.io.compression.ziparchive.dispose
[deflux]: https://github.com/daniilvaino/Deflux
