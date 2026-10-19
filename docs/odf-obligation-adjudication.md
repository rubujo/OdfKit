# ODF 規範性義務逐句裁決

日期：2026-10-16。完整逐句裁決**尚未完成**；本頁與機器可讀資料不宣稱完成或百分之百合規。
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

群組語意分類分為 [逐組審閱紀錄](odf-semantic-reviews.json) 與
[完整句型批次分類](odf-semantic-batches/groups.json)。待辦、已分類與尚待分類數量由
`eng/Test-OdfSemanticReviews.py` 依目前來源成員即時計算。已無待辦的來源背景分類保留
歷史紀錄，但不重複抵減現行待辦。
每組保留來源成員 hash、實際審閱版本、規範語意、適用條件、
SDK 責任與豁免判定；只有標題、明示非規範性的指引及明確的歷史背景被判為不獨立施加義務，
沒有因 deprecated、圖形、表單、資料庫或外部標準引用而整組排除 SDK 責任。

語意分類與實作裁決分開計數；已分類群組的實作裁決仍為 pending，未結案群組仍需
後續裁決及適用的修正／回歸證據。`eng/Test-OdfSemanticReviews.py` 檢查群組成員完整覆蓋、
版本與判定欄位，`--require-complete` 在尚有未分類群組時失敗；通過欄位契約不代表
判定語意或 OdfKit 的完整合規已獲證明。先完成其餘群組語意分類，再安排批次修正。

批次分類只接受固定規範正文內、整句符合已審閱句型且整組所有成員均可解釋的來源。
屬性值域保留 datatype 聯集分支；條件、否定、modal、版本與參照不因共用句型而合併。
舊版 RELAX NG 片段須具有官方指定的程式碼段落樣式；一般 XML 範例不能因此列為
規範 grammar。document、manifest 與 strict suffix 各自保留 profile。
同組含任何未辨識文字、表格或物件即保留待審；不以大類名稱授予 SDK 範圍外豁免。

ODF 1.1 的固定勘誤完整版本仍含編輯刪除線。
[生效文字政策](odf-effective-text-policy.json) 逐項鎖定已核對的編輯樣式，
[差異證據](odf-effective-text/odf-1.1-spec.json) 保留原始 ID、hash 與刪除範圍。
全部 27,434 個文字區塊已核對原始擷取一致性，162 個區塊受編輯刪除影響，
其中 37 個生效文字為空。普通文件排版用刪除線仍保留；這份投影尚未完成各條勘誤的
規範性義務裁決，也沒有結案非文字物件。批次分類先排除受編輯變更影響的群組，避免
把已刪除文字與生效文字一起當成現行義務。

ODF 1.0 的 [PDF grammar 政策](odf-pdf-grammar-policy.json) 與
[重建證據](odf-pdf-grammar-evidence.json) 涵蓋第 33 至 683 頁的 651 個實體頁面，
只抽取灰底且有行號的 document grammar。完整樹與 namespace scope 的 11,037 個節點
與專案內官方 RNG 相符；7 處 pattern 換行按個別來源／目標 hash 核對，2 處註解結尾換行
另外保留，不刪除 regex 的空白。未編號的 enhanced geometry／animation BNF 不是因此
被豁免，而是不能混入 RELAX NG 樹。此證據不證明 SDK matcher 等價，也未結案勘誤、
manifest、strict suffix、prose 或註解承載的義務。

[出版物物件來源](odf-publication-objects.json) 保留全部 2,591 個原始 ID、來源 ZIP 與
payload hash，其中 1,233 個可解析為 MathML，另有 1,233 個來源 frame 宣告的替代圖。
可解析不等於已審閱；只有逐組紀錄實際列入的物件才計入語意分類，且審閱另綁定 payload、
archive、MathML 樹及 rendition 證據 hash，不能只沿用未變的 xlink:href。
來源 ODT 的 office:version 與所述規範版本分開；依實際容器版本核對 frame 選用規則。
選用首選 MathML 不代表替代二進位圖已視覺比對，也不將數學定義改成 SDK 範圍外。

引言的目錄、致謝、出版／權利政策與技術來源規則分開。1.3／1.4 所述獨立 normative
機器可讀檔優先序仍是 SDK 必須追蹤的技術依賴，不能因位於前言而略過。出版授權及
IPR 通報不屬資料驗證器的裁決功能，但第三方權利及發布義務沒有因此被免除。
ISNA 的 Syntax 寫成 ISERR、CSC 的冗詞及 FACTDOUBLE 圖式標記等來源差異已保留於
逐組紀錄；它們不自動等於 SDK 缺陷，也不是修改 SDK 去複製矛盾敘述的理由。

公式章節的結構角色以完整 Summary／Syntax／Returns／Constraints／Semantics 序列核對，
不把任意段落或空的 See also 當成已理解的行為。這類角色分類獨立計數，不扣除語意待辦；
必須另有逐組、逐版的語意審閱才可列入已分類群組。CORREL 的算式括號、IMSQRT 的
sin／cos 項及 ISODD 摘要等上游矛盾保留，不能靠複製錯誤式修補 SDK。

純文字 hash 無法保留公式的上標／下標。`docs/odf-formula-semantic-batches.json` 另外保存
原段落 XML hash、非基線文字及樣式繼承：例如 DEC2HEX 的範圍是 −2^39～2^39−1，
不是擷取結果中的 −239～239−1。帶有此類格式的語意審閱須綁定其獨立證據 hash；
保留來源格式不等於數學公式或 SDK 實作已裁決正確。

DDE、INFO、CELL 的表格定義保留逐列的來源單位、實際 locator 與文字 hash，並檢查
同一語意列不能混入其他列／表。宿主環境資料與可選 UI 動作分開，不能將完整函式
一律排除 SDK。所有 `outside-sdk` 豁免另綁定所引用產品政策／實作的內容 hash；
政策或實作改變後必須重審，單純換行差異不影響該憑證。

版本差異也適用正式解讀慣例：例如 ODF 1.1 的 Notation 將義務關鍵字的粗體字形
列為條件，不能僅依純文字 modal 掃描裁決 SHALL／SHOULD／MAY 的強度。
FLOOR、DAYS、MINUTE、SECOND 等跨版本行為差異必須保留，不能用最新版的單一
演算法宣稱所有舊版也已符合。SDK 的 HYPERLINK 已改為保留第二引數的求值型別，
TRIM 已改為只處理規範列出的 TAB／LF／CR／SPACE；相關回歸案例與來源簽章誤植、
缺少公式或矛盾敘述分開記錄，測試通過也不代表其他函式或全部 SDK 義務已完成。

ODF 1.3 Part 2 的 `dsig:version` 正文要求 `1.2`，但其
[官方數位簽章 schema](https://docs.oasis-open.org/office/OpenDocument/v1.3/os/schemas/OpenDocument-v1.3-dsig-schema.rng)
的 `dsig-document-signatures-attlist` 明定 `1.3`。依 1.3／1.4 Part 2 前置條款
（原始 ODT 的 B00046），另行發布且 declared Normative 的機器可讀定義與文件顯示
內容有差異時，以獨立 plain text 檔案為準。本次對此重疊的屬性值定義採官方 schema，
並以 1.2／1.3／1.4 的交叉版本案例保護；正文殘留仍記錄為來源差異。此裁決不免除
grammar 未表達的其他正文條件，也不擅自改寫官方 schema 或產生碼。

PBKDF2 的簡短名稱與 `urn:oasis:names:tc:opendocument:xmlns:manifest:1.0#pbkdf2`
是規範要求 consumer 支援的等價名稱。SDK 的解密與 manifest iteration 檢查已共用
此辨識邏輯；回歸測試涵蓋兩者的解密、IRI 精確比對、運算次數上限與零次運算拒絕。
ODF 1.4 的 key-derivation-name 末句稱唯一值為 PGP，但同節仍要求 PBKDF2，官方
manifest schema 也包含 PBKDF2 分支。本次同樣依機器定義優先規則採官方 grammar
的分支結構，保留正文顯示差異，不用來取消既有 PBKDF2 義務或將整個加密功能宣稱
為已完成裁決。

`docs/odf-package-informative-contexts.json` 記錄 package 非規範書目與 ZIP 圖解的
實際起迄標記、原始節點種類、來源檔案 hash，以及圖像所在的 body 順序；圖像另綁定
完整 payload 證據。來源把附錄標題放在一般段落時不升為 `text:h`，混合群組內的
namespacedToken 與 schema／ontology 依賴仍保留規範責任。這份範圍檔案本身也由
審閱紀錄的 hash 綁定，不能只修改邊界就將其他正文或物件納入豁免。

`docs/odf-reviewed-examples.json` 另保留明示 Example 的精確範圍：1.1 Sample Style
的完整示範 XML，以及 DDE topic 的 OpenOffice.org 實例。示範 style 名稱及 margin
不升為必填值，DDE 範例也不使所有 topic 都必須是檔案名稱；同章的正式資料與 grammar
義務仍獨立保留。

`docs/odf-reviewed-note-contexts.json` 保留已核對的明示 Note 段落、緊接的下一段邊界與
來源 hash。Note 的延續句即使沒有再次出現標籤，也屬同一段說明；不能只豁免第一句，
再把同段第二句升為獨立義務。這個判定只涵蓋已登錄的完整段落，不將所引用的正文、
型別或相鄰段落一併視為非規範性內容。

加密 file-entry 的 `manifest:size` 是原始未壓縮、未加密的位元組數。SDK 的加密
中繼資料檢查已補上缺漏及負值診斷，並保留零大小為合法邊界；未加密項目不因這條
條件式要求而被強制加上 size。此檢查只處理欄位存在與非負性，未解密時不能據此
宣稱已核實宣告大小與實際明文內容一致。解密路徑現在於取代項目內容前，比對已宣告的
大小與實際明文位元組數；不符時保留該項目的密文與加密中繼資料並回報在地化錯誤。
這項檢查共用於內建與自訂提供者，不改以密文或壓縮後大小判斷。自訂提供者的寫入路徑
也由 SDK 按實際明文長度記錄 size，不沿用提供者漏填或錯填的大小。金鑰大小的公開文件也改為實際使用的
位元組單位，不更改演算法參數。

空的章節號快取也不等於沒有來源目標。`docs/odf-empty-reference-evidence.json` 保留
ODT 的 `text:ref-name`、reference／bookmark／sequence 各自的錨點空間、原始 XML
及段落 hash；例如 1.1 的 Image Data／Text Anchor 可由原始命名錨點定位。
同段落的非空參照也保留，因為快取數字可能指向另一個父節。此證據不重新計算章節
編號，不把被刪除或歧義的目標自動當成有效來源，也不單獨關閉 SDK 義務。

`number:am-pm` 出現在日期或時間樣式時，小時顯示須採 1 至 12；這不改變底層日期時間值。
數字格式引擎已修正午夜、正午及下午的小時轉換，並讓日期樣式依子元素順序輸出時間欄位。
回歸案例涵蓋兩種樣式、長短小時格式、標記前後順序、不同 XML 前綴、外來命名空間與
`DateTime` 邊界。外來同名標記不啟用十二小時制。這些呈現回歸不單獨證明該群組的
父子模型、空內容限制或所有語系義務已完成，整組仍保留實作待裁決。

ODF 1.2～1.4 的 `text:measure` 除了 inline grammar，還受正文「只能位於尺寸標註繪圖
物件文字內」的限制。規範檢查已補上 `ODF3117`：先依 Extended 規則處理外來內容，
再檢查是否有原生 `draw:measure` 祖先；遇到 inline `office:document` 即停止向外尋找，
不讓內嵌文件借用外層的尺寸標註。封裝、單一 XML 及內嵌檔案共用此檢查。這項必要
位置條件不外推為已完成尺寸值計算、所有樣式與舊版正文的實作裁決。

ODF 1.4 的共用 `writing-mode` 列舉也不能取代上下文檢查：`ODF3118` 檢查
`style:page-layout-properties` 不得使用 `page`，其他合法屬性位置仍可使用此值；
`ODF3119` 檢查同一 graphic style 的 graphic／paragraph properties 若都有明確值，
兩值須一致。這些條件只套用於已核對的 1.4 要求，不改寫官方 schema 或移除其他
位置的合法列舉。型別的 XML whitespace 正規化、default style、外來內容、內嵌文件
及單一 XML 路徑均有分開的控制案例；方向繼承及完整排版仍另行裁決。

`docs/odf-implementation-reviews.json` 與語意分類紀錄分開，登錄直接義務的實作裁決。
2026-10-16 最近一次完整本機核驗檢查點為 **101 組直接裁決、5,761 組待裁決**；
其中 79 組具有執行期實作與測試證據，22 組逐項確認沒有 SDK 執行期義務。
此檢查點的兩框架各 4,891 項回歸、格式與靜態閘門、資源壓力、公開 API 基線及
17 語系在地化閘門均通過，建置與分析器沒有警告；
249 項裁決工具契約測試通過；收據檢查在單次執行共用 TRX 解析結果，仍逐組驗證
案例、hash 與計數，且不同執行之間不沿用快取。後續批次仍在實作；
目前有效數字與來源是否失效，以裁決驗證器重算為準，
不能將此檢查點視為全部規範已完成。Actions 留待全部本機裁決與實作完成後統一驗證。

樣式批次已核對 27 組、324 個來源單位，共 28 種屬性與承載元素組合，另以 Jing 與
三版官方 RNG 驗證 84 份完整正例模型。[樣式回歸](../OdfKit.Tests/OdfStyleAttributeConformanceTests.cs)
覆蓋型別正反例、可選屬性、錯誤承載元素、兩條讀寫路徑及版本化 DOM 存取。
`OdfLength` 的有限數值輸出改用可往返的十進位詞彙，避免極小／極大值產生不合法的
科學記號長度。`ODF3143` 補上 marker／stroke-dash 名稱參照的目標種類與存在性，
允許空參照，支援 content.xml 與 styles.xml 之間的合法參照，並分隔封裝目錄與行內文件。
對角線邊框仍依規範宣告的 string 保存，不擅自加入 CSS 語法限制；text-combine 的字元
也不因停用呈現效果就被禁止。資料型別全集與其他相依條款仍獨立追蹤，共 31 組相依
尚未完成，沒有新增 SDK 範圍豁免或實體渲染完成宣稱。

儲存格目前值批次（ODF 1.1 的 office:value-type 與 boolean／string／date／time／
numeric／currency 目前值，共 7 組）已核對核准勘誤的生效文字；原始抽取中的刪除片段不作為規範，
七組均保留 SDK 驗證、產生與消費責任，沒有豁免。實作補上 `MoveCell` 與拒絕追蹤修訂的
複製／清除清單先前漏列的 `time-value`、`string-value`、`currency`，並由
[值轉移回歸](../OdfKit.Tests/OdfCellValueTransferConformanceTests.cs)與目前值、純量回歸涵蓋。
該批在兩框架各 4,998 項回歸通過後，直接裁決升為 **108 組、5,754 組待裁決**
（86 組具執行期實作與測試證據）；被引用的 W3C 型別族及其他儲存格屬性仍為獨立相依，
不宣稱完整合規。

作者與寄件人欄位批次（ODF 1.1 的 Sender／Author Fields 與 1.2～1.4 的 Subsequent
Author／Author Fields，共 37 組、17 種 text:author-*／text:sender-* 元素）以
[欄位回歸](../OdfKit.Tests/OdfAuthorSenderFieldConformanceTests.cs)逐版核對：
各文件列出的父元素均可使用，字元資料經兩種讀取器與寫出路徑保留，text:fixed 缺省、
true、false 皆合法，其他值、未知原生屬性、原生子元素、錯誤位置及外來同名元素被拒絕。
SDK 沒有自動更新作者欄位的功能，測試另證明載入與儲存不改寫 fixed 與非 fixed 欄位的值。
這批沒有發現 SDK 缺陷；使用者資訊對話框屬宿主 UI，不是核心責任，text:fixed 的布林型別
另為獨立相依。兩框架各 5,017 項回歸通過，直接裁決累計 **145 組、5,717 組待裁決**
（123 組具執行期實作與測試證據）。

文字欄位元素批次（ODF 1.2～1.4 的 66 個 text／number／presentation 欄位與相關元素）以
[元素回歸](../OdfKit.Tests/OdfFieldElementConformanceTests.cs)與
[產生器回歸](../OdfKit.Tests/OdfFieldProducerConformanceTests.cs)逐版核對：
最小實例、每個屬性的資料型別正反例、必要屬性缺漏、未知屬性、子元素、錯誤位置、外來同名元素、
字元資料保存及載入後儲存的屬性不變。這批發現並修正兩個產生器缺陷：`AddChapterField`
漏寫必要的 `text:display` 與 `text:outline-level`，`AddVariableSetField` 漏寫必要的
`office:value-type`，兩者先前輸出都不符 schema。SDK 不對欄位求值，僅保存資料。
另有 4 組（`number:fill-character`、`text:hidden-text`、`text:page-number`、`text:script`）
尚無語意分類，未列入。兩框架各 5,038 項回歸通過，直接裁決累計 **211 組、5,651 組待裁決**
（189 組具執行期實作與測試證據）。

屬性定義批次（ODF 1.2～1.4 的 537 個「General Attributes」群組）以官方 RELAX NG schema
為每個列出的載體元素合成最小實例，先由 Jing 獨立判定，再要求驗證器在 Strict 與 Extended
給出相同結果：Jing 接受的正例驗證器也接受，資料型別可構造的無效值 Jing 拒絕者驗證器同樣拒絕，
省略屬性的判定與官方 schema 一致（可選就可缺省，缺省本身不是錯誤；規範文字另有條件時驗證器
可再以其他規則編號拒絕，但不得出現 schema 錯誤），列舉屬性的其餘定義值也逐一驗證。
固定資料為 [OdfAttributeCases.jsonl.gz](../OdfKit.Tests/TestData/OdfAttributeCases.jsonl.gz)
（5,979 個案例、1,058 個群組），以[差異測試](../OdfKit.Tests/OdfAttributeDefinitionConformanceTests.cs)
逐群組逐版本執行，並可用 `eng/Test-OdfAttributeFixture.py` 單獨以 Jing 重驗全部 16,405 份文件。
這批發現並修正兩個驗證器缺陷：一、XML Schema 正則的 `\i`、`\c` 名稱字元跳脫沒有翻譯成 .NET
正則，導致 CURIE／SafeCURIE 樣式整個失敗，Strict 設定檔誤拒 `xhtml:about`、`xhtml:property`
等屬性；二、Extended 剝除外來屬性時連 ODF schema 定義的四個 RDFa 屬性一併剝除，使只寫一半的
RDFa 屬性組被接受，現改為保留。`draw:z-index` 在 dr3d 場景子層與 `text:measure` 的祖先限制
未合成而不列入；含條件義務或其他子句的群組保留待個別裁決。兩框架各 17,447 項回歸通過，
直接裁決累計 **748 組、5,114 組待裁決**（726 組具執行期實作與測試證據）。

表單型別批次已修正並完整核驗兩項不同的驗證缺口，三個型別子節已計入直接裁決：
CLR 的 `XmlConvert` 會接受 `Infinity`、`nan` 等 XML Schema 不允許的特殊詞彙，
因此 float／double 轉換前增加線性 ASCII 詞彙檢查，保留合法的指數與 `INF`、`-INF`、`NaN`。
另外，1.2～1.4 的官方 `form:value-range` schema 透過 `common-value-attlist` 使用 string，
但各版 `form:value` 數值子節明定 double；完整合規檢查以 `ODF3141` 補足正文限制，
不改寫官方 RNG。[型別回歸](../OdfKit.Tests/OdfFormTypedValueConformanceTests.cs) 分開驗證
schema 接受與正文拒絕、原生命名空間、缺省、日期／時間邊界及兩條讀寫路徑。
便利屬性的 CLR 型別容量與原始屬性資料保存是不同覆蓋項目；本批不宣稱所有便利 API
都能表示 XML Schema 的完整值域，也不將這項容量限制豁免為 SDK 範圍以外。
詞彙依據為 [XML Schema 1.0 Part 2 的 float 與 double 規則](https://www.w3.org/TR/xmlschema-2/#double)，
數值承載元素依據為各版本已鎖定來源的 `form:value` 子節。

接續的 duration／metadata 批次已修正並通過完整閘門，兩組 metadata 直接義務已計入裁決：
`duration` 不再以 CLR `TimeSpan` 容量判斷詞彙合法性，改為依序掃描年、月、日及時間分量，
並要求秒數小數點後有數字；十萬位分量也有正反例。float／double 詞彙檢查同樣不再依賴
多餘的 CLR 數值轉換。[時間長度回歸](../OdfKit.Tests/OdfSchemaDurationConformanceTests.cs)
包含原生中繼資料及兩條讀寫路徑，並以 XML 字元參照保留 CR，避免把 fixture 正規化誤判為 SDK 問題。
[中繼資料型別回歸](../OdfKit.Tests/OdfMetadataValueConformanceTests.cs) 涵蓋五種旗標、缺省 string、
父元素、子元素、屬性與型別限制；產生器改用標準浮點詞彙，標準無限大不再解碼成零，
合法型別 token 的 XML 空白也不再造成錯誤字串 fallback。這些修正不連帶完成 duration 的
排序／facet、整個 XML Schema 型別族或所有 CLR 便利 API 的容量覆蓋。
後續[未型別化日期 API 回歸](../OdfKit.Tests/OdfMetadataDateValueConformanceTests.cs) 已修正並
重新核驗 BCE／超大年份被替換為最小日期，以及高精度小數秒被四捨五入的資料損失；
無法精確解析的值維持原字串，可精確表示的值維持既有 `DateTime` 結果。
[設定型別回歸](../OdfKit.Tests/OdfConfigDatatypeConformanceTests.cs) 驗證八種 `config:type`，
以 `ODF3142` 補足官方 text-only RNG 沒有表達的內容型別限制；Base64 另外檢查未使用位元，
使用線性掃描而不配置解碼緩衝區。
[設定產生器回歸](../OdfKit.Tests/OdfSettingProducerConformanceTests.cs) 確保宣告的 int／short
範圍在修改前檢查、更新既有值時同步型別，且 XML 整數讀取不受目前文化特性影響。
這批 41 份來源變更均經精確差異審計；新增在地化訊息後共有 653 鍵，字典皆由 JSON 重產。

先前 schema 批次新增註解作者縮寫、簡報頁首／頁尾宣告及 `presentation:name` 四組直接裁決，
並核對公開 API 基線。
[原生文字宣告回歸](../OdfKit.Tests/OdfNativeTextDeclarationConformanceTests.cs) 分別驗證
版本適用性、原生父元素與內容模型、必要名稱、字串型別及資料保存。
共用讀取器改用官方 schema 推導的字元內容判定，並補上 XML 屬性字面空白正規化。
另修正 RELAX NG 名稱 token 空白、常值文字保留、子樹前綴脈絡，以及無命名空間名稱遭丟棄的問題；
所有受影響 schema 均由產生器重產，舊版統計屬性有合法值與負值拒絕的回歸證據。
這些共用修正沒有連帶關閉其餘文字、格式計算、`xml:space` 或表格框架條件群組。

此後續批次已累積完成表格、固定文字及表單變更，再統一核驗完整格式與兩框架回歸。
[表格描述回歸](../OdfKit.Tests/OdfTableDescriptionConformanceTests.cs) 補上直接框架內容表格的
標題／描述禁止條件，保留文字流程中的表格及框架 SVG 描述。
[固定文字回歸](../OdfKit.Tests/OdfNumberLiteralTextConformanceTests.cs) 驗證七種資料樣式及 1.4 清單格式的
文字保存，並修正既有格式引擎的明確空常值及布林／字串前後文字遺失。
[XML 空白脈絡回歸](../OdfKit.Tests/OdfXmlSpaceConformanceTests.cs) 修正繼承、覆寫、小型子樹重讀及延遲複本的
資料保存，同時核驗字元預算；這項共用補強沒有額外計入完整群組裁決。
[表單項目回歸](../OdfKit.Tests/OdfFormItemConformanceTests.cs) 核驗 `form:item`／`form:option` 的
資料與結構，屬性完整語意另列明確相依。
[選取互斥回歸](../OdfKit.Tests/OdfRadioSelectionConformanceTests.cs) 及
[選取屬性回歸](../OdfKit.Tests/OdfFormSelectionAttributeConformanceTests.cs) 補上同表單具名選項按鈕的
預設／目前選取各自互斥，並更正設計器只寫目前狀態而漏寫參數所承諾預設狀態的問題。
`form:selected` 的 30 個來源單位逐句分類；僅宿主互動初始化／重設的三句另列 SDK 範圍，
資料保存、缺省值、型別、產生器契約與群組一致性均保留為 SDK 義務。
目前仍有 30 個未完成的相依群組，直接裁決數不能解讀為其所有參照功能均已完成。

先前的 [索引層級回歸](../OdfKit.Tests/OdfIndexOutlineLevelConformanceTests.cs)
驗證無界正整數、數值等價重複鍵與來源範圍；
[圖形文字回歸](../OdfKit.Tests/OdfGraphicDescriptionConformanceTests.cs) 則修正純空白、
XML 字面換行、CR 參照、CDATA 及註解／處理指令混入容器文字的資料保留缺口。
每一組仍以其列明的直接義務結案，沒有連帶關閉其他索引、無障礙或 XML 處理義務。

每組須涵蓋原始全部成員與每一語意規則，並綁定語意紀錄。直接實作裁決另須綁定
程式碼、回歸案例與跨 TFM 執行結果。整組僅含來源背景或具體 SDK 範圍外義務時，
以 `no-sdk-runtime-obligations` 單獨結案，不虛構執行測試；任何未豁免的規範規則都會
阻止此結案方式，也不藉此關閉參照群組。標題、SDK 外的宿主動作及參照依賴不會偽裝成已實作功能；參照群組仍各自
待裁決。HYPERLINK／TRIM 的紀錄保留共通函式語法與轉型前提，不由這兩組的直接
義務通過宣稱所有 evaluator 語意均已驗證。

表單的 `form:list-property`／`form:list-value` 兩組另有直接義務的實作裁決：官方三版 schema
將父層 `office:value-type` 與對應的子值放在同一個選擇分支；matcher 先前將屬性與內容分開
投影，可能接受互不相容的分支。現以[屬性衍生規則](https://relaxng.org/jclark/derivative.html)
保留剩餘內容的分支關聯，並以原生文件和自訂 schema 回歸綁定證據。一般 office 值屬性及
型別定義仍列為獨立依賴；兩組通過不結案完整表單執行器或 RELAX NG 引擎的其他差異。

人工語意紀錄與句型分組都可作為實作裁決來源。句型分組的紀錄另綁定每一原始單位的
family、參數、來源 hash，以及編譯規則、有效文字與來源範圍政策；缺少成員、來源重複
或參數變動都會使實作證據失效。這條連結只允許獨立審閱，不因語意分組存在而自動結案。

`Test-OdfImplementationReviews.py --check-local-receipts` 另核對本機 TRX 的內容 hash、
成功數與實際回歸案例。離線 CI 檢查追蹤紀錄及檔案是否失效，實際 CI 測試仍須執行；
symbol 存在或 hash 一致本身不是語意正確的證明。來源、語意、程式碼或案例改變時須
重新審閱並取得適用執行證據，不可盲目更新 hash。語意檔內的 pending 不主張實作結案，
整體實作計數則由這份獨立紀錄計算；完整合規宣稱仍保持 false。

ODF 1.0 的 PDF grammar 語意審閱另以實體頁碼與整份重建證據 hash 綁定，並核對來源
block 相同；目前已逐頁查看的純 grammar 頁仍保留跨頁 pattern、註解與核准勘誤依賴。
頁面只有灰底程式碼不等於沒有義務，重建模型一致也不等於 SDK matcher 或註解義務已驗證。
1.1 人工紀錄若涉及編輯刪除，則另綁定該 block 的刪除範圍及生效文字證據，不用原始
重疊文字直接取代生效要求。

完整裁決需要全部規範性文字具有有效版本、上下文、適用角色、前置條件、義務強度、
獨立義務拆分、scope 裁決、實作／測試證據或具體缺口。參照其他標準的要求須有依賴追蹤；
不屬 SDK 的互動操作須有逐項理由，不能用 display、formula 或 database 等字詞直接整章排除。

資料庫表單產生器的本機修正涵蓋 `office:forms` 容器、控制項識別碼、原生值屬性、
清單項目標籤，以及 ODB 的內嵌文字表單與對應元件。內嵌文件須在載入 XML 前取得
自身路徑，不能先載入父文件再設定路徑；文件種類及 MathML 根元素例外也依自身媒體
型別判定。回歸另涵蓋多種內建文件、範本、反射建構失敗及父文件保存往返。
時間詞法驗證接受合法的 `24:00:00`，拒絕該時刻的非零分鐘、秒或小數秒及非 ASCII
數字。這些修正不結案所有表單、版面參照或 MathML 義務；完整 MathML schema 與
一般樣式參照的語意驗證仍須獨立審閱，不能由產生器測試通過推論完整覆蓋。

樣式語意檢查另補上兩項 grammar 未表達的要求：`layout-grid-ruby-height` 必須與
同一 `page-layout-properties` 上明示為 `false` 的 `layout-grid-standard-mode` 共現，
而四種 `draw:visible-area-*` 屬性只能指定於自動樣式。檢查依命名空間與直接樣式容器
判定，不用外來同名屬性滿足原生條件。ODF boolean 的 `true`／`false` 詞法與 XML Schema
boolean 的 `0`／`1` 不同。條件區段的 `text:condition` 存在性已由官方 schema 表達，
保留控制案例而不新增重複語意規則；不能把所有跨屬性條款都推定為 SDK 缺口。

圖表的 `chart:regression-period` 從 ODF 1.3 起另有最小值 2，不能只以 schema 的
`positiveInteger` 接受值 1。語意檢查保留任意大小合法正整數，並處理正號、前導零及
XML 空白；原生屬性值域與是否採用移動平均呈現分開判定，不把外來同名屬性納入。
原有大綱層級檢查共用同一正整數正規化程序，回歸同時檢查索引限制未改變。

ODF 1.1 的分類另核對原始字重：該版 Notation 只賦予粗體規範用語正式強度，
因此設定名稱等一般字重敘述與 metadata 編輯建議的粗體 `should` 分開記錄。
版本屬性與語言標籤段落的核准編輯刪除，則綁定既有生效文字證據並保留原始單位 ID。
正文的 `*-del` 與同組／官方 grammar 的 `*-decl` 拼寫差異，以及時間調整範例中的
`PTM15`／`-PTH1`，均記錄為上游文字不一致；範例錯字不新增 SDK 的接受義務，
此對照也未宣稱 OASIS 已核准額外勘誤。明示範例與 Note 只按精確來源界線排除，
純註腳範例組的結案不涵蓋其他組的註腳結構或呈現義務。

資料型別的本機修正另涵蓋 XML Schema 長度單位與日期詞法。字串長度以 Unicode 字元
計算，補充平面代理對不拆成兩個字元；二進位長度以解碼位元組、清單以項目計算。
日期與日期時間不再以 CLR DateTime 的範圍代替規範值域，並檢查完整詞法、曆日、
BCE 年份與時區邊界。`OdfCharacter` 使用單一合法 XML 字元，不把多字元顯示字素
當成一個字元；有界 `OdfPercent` 先檢查完整詞法，再提供受 decimal 精確度限制的
數值檢視。一般 `percent` 與 1.4 的 `nonNegativePercent` 不套用 0～100 的上限，
原始詞法由字串與通用 DOM 保存。型別定義的裁決不外推使用處的預設值、樣式引用、
計算或排版義務；`styleName` 也不能只憑 NCName 正反例就宣告引用語意已完成。

`relativeLength` 的官方 grammar 接受 `0*`，但 1.2～1.4 正文要求正整數後接星號。
本機語意檢查因此在兩個原生使用處拒絕全零值：`style:column/@style:rel-width`
及 `style:table-column-properties/@style:rel-column-width`。前導零正值與任意大正值
仍可保存；外來命名空間的同名屬性不套用此規則。這項正文補充不修改官方 schema，
也不宣稱相對欄寬的整體版面分配演算法已完成裁決。

角度的補充驗證沿三版實際引用的 SVG 1.1（2003）核對，涵蓋官方 schema 中全部
13 個角度屬性與其中的清單項目；不因基礎型別是 string 就接受任意文字。原生 OASIS
profile 的補充問題碼為 `ODF3136`，官方 grammar 保持原樣。`OdfAngle` 同時檢查
數字與單位詞法，度數檢視支援 deg、grad、rad 與指數形式，並說明 decimal 精確度及
弧度轉換的圓周率近似。點清單的連續空格分隔另已修正，但其 Int32 座標檢視的責任
與表示範圍仍待裁決，不能由這個局部修正宣告 `points` 整組完成。

現代字母索引鍵的補充檢查以 `ODF3137` 表達 1.2～1.4 的單鍵要求：存在 `text:key2`
時也須存在 `text:key1`，外來同名屬性不算入。空字串仍是合法 string，與屬性省略
分開處理；SDK 的可選鍵設為 null 時會移除屬性，新增標記的組合參數不合法則在修改
段落前拒絕。既有可編輯 DOM 仍可經過暫時不完整的狀態，由最終 OASIS profile 驗證。
此處不將舊版一般本文的 must 直接升格成相同強度的錯誤規則，舊版來源與義務強度
保留待裁決；其餘索引、phonetic 排序與呈現也不由這項修正推論完成。

目前尚有 PDF 1.0 表格／schema panel 的精確重建、1.0 勘誤生效文字覆蓋、informative／example
與規範引用的逐項區分、非文字公式／圖表的內容審閱，以及其餘句子的語意與證據裁決。
這些是未完成工作，不能用候選清單的完整列舉或新的測試通過率替代。

重建工具為 `eng/Build-OdfObligationInventory.py`。`--verify` 比對固定來源重建結果；
`--check-contracts` 可離線檢查資料一致性；加上 `--require-complete` 時會因尚有未裁決單位而
失敗，防止把一致性閘門的綠燈解讀為完整裁決已完成。擷取與錯誤證據傳遞的回歸案例在
`eng/testdata/Test-OdfObligationInventory.py`。
