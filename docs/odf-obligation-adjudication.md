# ODF 規範性義務逐句裁決

日期：2026-10-15。完整逐句裁決**尚未完成**；本頁與機器可讀資料不宣稱完成或百分之百合規。
條款索引、句子候選清單、個別義務裁決及 SDK 合規是四種不同成果。

後續先完成[全量分類](odf-obligation-classification.md)，再依分類裁決與安排驗證缺口修正。
官方章節分組只建立工作佇列；SDK 責任、規範性與豁免仍需個別語意證據。
處理單位改為規範群組：一起核對共同要求、版本差異與條件例外，再共用修正與回歸；
保留各來源句子作為證據，不將每個來源單位視為獨立實作工作。

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

[odf-obligation-decisions.json](odf-obligation-decisions.json) 目前記錄 38 個裁決及 356 個精確句子
對應，涵蓋 document／package conformance、QName datatype，以及封裝 IRI、RDF、加密、簽章、
preview 與檢視模式的條件義務。這不是全部來源的完成比例。
每個對應同時鎖定 unit ID 與 sentence hash；來源或分句改變時，工具拒絕沿用舊裁決。

| 裁決 | 結果 |
| --- | --- |
| QName／namespacedToken | 修正 data 節點未檢查 prefix binding 及 XML whitespace collapse 的缺陷；三版 native chart／series、繼承宣告與內建 xml prefix 有回歸證據。整句仍為 partial，literal QName 相等比較等未證明。 |
| mimetype | 區分一般 package 的 should 與存在根 manifest entry 後的 shall；SDK 文件種類 profile 的額外必填限制不能視為一般 package 全部合規證據。 |
| ZIP compression | 1.2～1.4 所有 entry 的方法須為 STORED／DEFLATED；新增 lazy binary payload 的跨載入路徑回歸，無法取得中繼資料時明示警告。外部 ZIP 方言與 AOT 證據仍未完成。 |
| Extended inline foreign content | 依版本與允許 character data 的祖先鏈驗證保留內容，避免刪除 wrapper 時隱藏非法 native 元素；保留原始 QName scope。舊版 process-content 與完整 consumer fallback 仍未完成。 |
| ZIP local header | 補上 mimetype local extra field 檢查，包含非第一筆的組合；以實體順序取代中央目錄排序；1.0／1.1 的第一筆是 should，回報警告，1.2～1.4 的 shall 回報錯誤。 |
| mimetype 原始位元組 | 補上 ASCII 精確值檢查；讀取器相容移除 BOM／修剪空白，不再隱藏原始輸入的違規。完整 media type RFC 語法另列依賴。 |
| Manifest payload | 1.2～1.4 非排除 payload 缺少 entry 回報錯誤；META-INF 可不列入，移除簽章檔額外必列規則。 |
| 簽章檔 | META-INF 的巢狀名稱同樣驗證 dsig grammar；補上唯一 Id、KeyInfo 及封裝檔案參照 transform 的 prose 檢查。這不是完整 XMLDSig、憑證信任或密碼學驗證證明。 |
| Strict／Extended | Extended 只豁免逐項列出的條件，不能一起略過 manifest、XML、MathML 或 foreign-content 處理。 |
| OpenFormula | prefix 綁定、1.4 無 prefix 的預設語意、語法與計算語意仍屬 SDK；Safe Large corpus 不等於全部 Part 4 義務已完成。 |
| XML-ID | 補上文件與封裝中繼資料的 NCName／XML whitespace 與每 XML 文件唯一性檢查，包含被排除的 foreign element。仍未證明完整 processor type assignment 與資訊集回報，依據 [W3C XML-ID](https://www.w3.org/TR/xml-id/#processing)。 |
| XML consumer | DTD、外部實體及資源限制是明示的安全政策限制，並非 OASIS 授予的豁免；保留防護，不以放寬安全處理換取無限制 XML processor 宣稱。 |
| Producer | conforming 模式、所有 implementation-defined 值的說明及 foreign extension 登錄各自需要證據；不能以一份樣本通過取代所有輸出組合。 |
| Flat XML | 修正解碼 binary-data 後再次前進而跳過下一節點的缺陷；完整測試原已驗證影像標題在 flat／package 轉換後保留。 |
| Database | 官方 1.2～1.4 RNG 已定義 office-database；更正先前誤判並移除豁免，合法 datasource、缺少 datasource 及非法子節點有回歸案例。SQL 執行與資料庫伺服器仍是不同角色。 |

`reviewed` 只表示對該句及列明子義務作出裁決，不表示 fulfilled。`partial` 需要繼續補證據；
`validator-gap` 是已辨識的 SDK 檢查缺口；`evidence-gap` 不直接等於程式缺陷。
`security-policy-limit` 與 `product-profile-limit` 不能當成規範豁免，也不計為 fulfilled。
未審閱的來源單位保留 `unreviewed`／`unreviewed-object`，不自動排除於 SDK。

新增的局部回歸證據為 [ZIP mimetype](../OdfKit.Tests/OdfZipMimeTypeConformanceTests.cs)
與 [簽章／manifest](../OdfKit.Tests/OdfPackageSignatureConformanceTests.cs)。每筆新增子義務另記錄
角色、強度、條件與目前處置；已有實作不等於該筆裁決包含的全部外部標準依賴已閉合。

Package §3.7 的 IRI 演算法、全部子文件 RDF metadata、PNG preview 與加密／簽章兩種操作
順序仍缺逐分支證據。繪製 preview 或開啟 slide-show 的宿主互動不能用來豁免 SDK 的封裝
位置、資料格式、QName binding 或資料保留責任。`preferred-view-mode` 的 only applicable
描述適用語意，不直接改寫成禁止其他 entry 存在該屬性的錯誤規則。

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
