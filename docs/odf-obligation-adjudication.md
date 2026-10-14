# ODF 規範性義務逐句裁決

日期：2026-10-14。完整逐句裁決**尚未完成**；本頁與機器可讀資料不宣稱完成或百分之百合規。
條款索引、句子候選清單、個別義務裁決及 SDK 合規是四種不同成果。

## 來源與追蹤

以 [條款矩陣](odf-conformance-matrix.md) 的定案出版物為基礎，1.2～1.4 另讀取出版物指定的
authoritative ODT，保留精確的 `content.xml` 節點位置。1.0 使用官方 PDF 本文，1.1 改用
已納入 Approved Errata 的 authoritative 完整版；原版條款索引仍保留原來源，兩者不能混同。

本次另外納入先前未列的兩份核准勘誤：

- [ODF 1.0 Errata 02](https://docs.oasis-open.org/office/v1.0/errata/os/OpenDocument-v1.0-errata-02-os.html)，
  2010-09-20；它已包含 Errata 01。latest 檔名仍含 `errata-01`，不可據檔名誤判版本。
- [ODF 1.1 Errata 01](https://docs.oasis-open.org/office/v1.1/errata01/os/OpenDocument-v1.1-errata01-os.html)，
  2013-07-22；修訂指令與刪除文字不能直接當成生效義務，須以整合後的版本核對。

[來源鎖定與狀態統計](odf-obligation-inventory/sources.json) 包含 16 份來源的 URL、SHA-256、
文字區塊數、非文字物件數及排除區塊的理由與 hash。第三方全文留在 ignored artifacts；
提交的資料只含標題、位置、hash、技術 token 與自行撰寫的裁決。

| 版本 | 來源定位清單 |
| --- | --- |
| 1.0 | [odf-1.0.csv](odf-obligation-inventory/odf-1.0.csv) |
| 1.1 | [odf-1.1.csv](odf-obligation-inventory/odf-1.1.csv) |
| 1.2 | [odf-1.2.csv](odf-obligation-inventory/odf-1.2.csv) |
| 1.3 | [odf-1.3.csv](odf-obligation-inventory/odf-1.3.csv) |
| 1.4 | [odf-1.4.csv](odf-obligation-inventory/odf-1.4.csv) |

清單涵蓋無 shall／must 的宣告句、條件片段、表格儲存格及非文字物件，**不是義務總數**。
一個句子可能包含多個義務；單一表格儲存格或 PDF 頁面片段也可能無法獨立解釋。
沒有以 modal-word 篩選刪除其他文字，沒有以 QName 名稱存在或來源相似度推導整句合規。

## 已裁決範圍與結果

[odf-obligation-decisions.json](odf-obligation-decisions.json) 目前記錄 24 個裁決及 121 個精確句子
對應，以 ODF 1.4 document／package conformance 及 1.2～1.4 QName datatype 為優先範圍。
每個對應同時鎖定 unit ID 與 sentence hash；來源或分句改變時，工具拒絕沿用舊裁決。

| 裁決 | 結果 |
| --- | --- |
| QName／namespacedToken | 修正 data 節點未檢查 prefix binding 及 XML whitespace collapse 的缺陷；三版 native chart／series、繼承宣告與內建 xml prefix 有回歸證據。整句仍為 partial，literal QName 相等比較等未證明。 |
| mimetype | 區分一般 package 的 should 與存在根 manifest entry 後的 shall；SDK 文件種類 profile 的額外必填限制不能視為一般 package 全部合規證據。 |
| ZIP local header | 第一筆與 STORED 已檢查；mimetype 的 extra field 長度仍缺驗證。 |
| Strict／Extended | Extended 只豁免逐項列出的條件，不能一起略過 manifest、XML、MathML 或 foreign-content 處理。 |
| OpenFormula | prefix 綁定、1.4 無 prefix 的預設語意、語法與計算語意仍屬 SDK；Safe Large corpus 不等於全部 Part 4 義務已完成。 |
| XML consumer | DTD、外部實體及資源限制是明示的安全政策限制，並非 OASIS 授予的豁免；保留防護，不以放寬安全處理換取無限制 XML processor 宣稱。 |
| Producer | conforming 模式、所有 implementation-defined 值的說明及 foreign extension 登錄各自需要證據；不能以一份樣本通過取代所有輸出組合。 |
| Flat XML | 修正解碼 binary-data 後再次前進而跳過下一節點的缺陷；完整測試原已驗證影像標題在 flat／package 轉換後保留。 |
| Database | 官方 1.2～1.4 RNG 已定義 office-database；更正先前誤判並移除豁免，合法 datasource、缺少 datasource 及非法子節點有回歸案例。SQL 執行與資料庫伺服器仍是不同角色。 |

`reviewed` 只表示對該句及列明子義務作出裁決，不表示 fulfilled。`partial` 需要繼續補證據；
`validator-gap` 是已辨識的 SDK 檢查缺口；`evidence-gap` 不直接等於程式缺陷。
`security-policy-limit` 與 `product-profile-limit` 不能當成規範豁免，也不計為 fulfilled。
未審閱的來源單位保留 `unreviewed`／`unreviewed-object`，不自動排除於 SDK。

## SDK 資料表命令擴充

ODF 的 `db:command` 不能用於 `db:table-representation`。SDK 的 `AddTable`／`UpdateTable`
命令參數因此以 `urn:odfkit:database:compatibility` 命名空間的 `command` 屬性保留；
偏好前綴為 `odfkitdb`，語意以 namespace URI 與 local name 判斷，與前綴無關。
值是選擇性的原始字串，不承諾 OASIS SQL 語意或執行。這是可移除的 foreign metadata，
含此屬性的文件須採 Extended conformance；未提供命令時不需要此擴充。

讀取器優先讀取此 SDK 屬性，並相容讀取舊版錯置的 `db:command`。只讀取舊文件不會掩蓋
原始輸入的驗證錯誤；呼叫 `UpdateTable` 時才移除舊屬性並寫入新命名空間，空白值移除命令。
合法 query 的 `db:command` 維持 OASIS 定義。Strict 驗證不先刪除此擴充讓文件通過。

## 完成條件

完整裁決需要全部規範性文字具有有效版本、上下文、適用角色、前置條件、義務強度、
獨立義務拆分、scope 裁決、實作／測試證據或具體缺口。參照其他標準的要求須有依賴追蹤；
不屬 SDK 的互動操作須有逐項理由，不能用 display、formula 或 database 等字詞直接整章排除。

目前尚有 PDF 1.0 表格／schema panel 的精確重建、1.0 勘誤生效文字覆蓋、informative／example
與規範引用的逐項區分、非文字公式／圖表的內容審閱，以及其餘句子的語意與證據裁決。
這些是未完成工作，不能用候選清單的完整列舉或新的測試通過率替代。

重建工具為 `eng/Build-OdfObligationInventory.py`。`--verify` 比對固定來源重建結果；
`--check-contracts` 可離線檢查資料一致性；加上 `--require-complete` 時會因尚有未裁決單位而
失敗，防止把一致性閘門的綠燈解讀為完整裁決已完成。擷取與錯誤證據傳遞的回歸案例在
`eng/testdata/Test-OdfObligationInventory.py`。
