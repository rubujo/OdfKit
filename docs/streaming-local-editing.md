# 既有 ODF 文件的有界串流局部修改

OdfKit 將「建立新文件的串流輸出」與「修改既有文件的局部串流轉換」視為不同能力。
前者由 `OdsStreamWriter`／`OdtStreamWriter` 提供；後者必須同時處理 ZIP、未知 XML、重複
列欄、資源上限與失敗復原。

## 格式判斷

| 格式 | 局部修改粒度 | 現況與決策 |
| --- | --- | --- |
| ODS | 工作表＋列欄座標 | `OdsSparseEditor` 已提供。只具現化命中的單一實體 row subtree，未命中的 XML 與 ZIP entry 以串流複製。 |
| ODT | 書籤、欄位、文字標記 | `OdfStreamingMailMerge` 已對 `content.xml`／`styles.xml` 作安全 SAX-style 轉換。座標式文字修改不適合，因文字可跨 span、欄位與 tracked-change 邊界；應以穩定書籤或範本標記定位。 |
| ODP | 投影片／shape | 不提供仿 ODS 的 cell patch。投影片本身是合理的最小一致性單位，細粒度重寫還必須同步 animation、event 與 style 參照，效益不足以抵銷損毀風險。 |
| ODG | page／shape | 與 ODP 相同；使用 DOM 修改具識別碼的物件，或以整頁為串流替換單位。 |

這個判斷不是說 ODP／ODG 不能串流複製，而是它們沒有像 ODS 列欄重複結構那樣清楚、
可獨立驗證的細粒度一致性邊界。

## ODS 安全與效能契約

- `ApplyAsync` 要求不同的來源與目的串流；`ApplyFileAsync` 使用同目錄隨機暫存檔，成功後才
  原子取代目的檔。
- 禁止 DTD 與外部實體，並套用 ZIP entry 數、單 entry、總解壓大小及 XML 字元數上限。
- 拒絕重複 ZIP entry、重疊 cell patch、加密 entry、直接修改 covered cell 與不存在的座標，
  不會靜默產生可能損毀的輸出。
- patch 先依 sheet／row 建立有界索引；掃描複雜度為實體 XML 節點數加 patch 數，不會對
  每一列重新掃描全部 patch。
- `table:number-rows-repeated` 與 `table:number-columns-repeated` 只在命中座標時拆分；
  其餘重複區間維持壓縮表示。
- 公式在開始寫出前完成語法驗證，輸出時清除過期 cached value，由相容的試算表應用程式
  重新計算；不會在串流熱路徑重複剖析公式。
- 樣式 patch 可引用 `styles.xml` 或 `content.xml` 中已宣告的 `table-cell` style，也可建立
  具型別屬性的 automatic cell style。名稱、宣告數、字串長度、父樣式存在性與父鏈循環
  都在寫出前驗證；同名定義不得衝突。
- 批註可新增、取代或移除；作者、文字及日期都受單項與批次總字元預算限制。
- 合併格建立、調整與解除都會在寫出前展開成具總數上限的座標索引。新增 covered 區域
  必須是空白普通儲存格，且不得與其他合併或 patch 衝突；不會為了改變合併範圍而靜默
  刪除資料。
- 可透過 `CancellationToken` 中止；串流 API 的目的串流可能已有部分輸出，需交易語意時
  使用檔案 API 或由呼叫端提供暫存目的串流。

目前 cell patch 支援文字、公式、既有或新建 automatic cell style、批註，以及建立、調整
與解除合併區域。它刻意不提供任意 XML 注入或任意樣式屬性字典；這些操作缺乏可預先驗證
的安全邊界，應改用 DOM 並由呼叫端承擔完整參照一致性。
