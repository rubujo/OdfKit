# 變更紀錄

本檔案依 [Keep a Changelog](https://keepachangelog.com/) 慣例，記錄 OdfKit 對外可見的重大里程碑。

## 0.0.1 - 2026-10-02

以 LibreOffice 26.2.4.2 的實機輸出再驗證一輪（Portable 版 LibreOffice 把測試用的 XLSX、DOCX、HTML 轉成真正的 ODS／ODT，再由 OdfKit 讀取；反向則讓 LibreOffice 開啟 OdfKit 的輸出並匯出成 XLSX 與文字）。OdfKit 轉換出的 ODS 經 LibreOffice 匯出為 XLSX 後，1,143 個儲存格與合併範圍全部與原始真值一致；ODT 的空白、定位字元、換行、超連結、表格與巢狀表格也都正確。同時抓到下列四個先前沒有被發現的缺陷，其中第一個會讓 LibreOffice 儲存的試算表在載入後資料全部遺失。

- **修正載入 LibreOffice 文件時資料靜默遺失（嚴重）**：`OdfNode` 對 8 KB 以上的 `table:table`、`text:p`、`text:list` 等子樹採延遲具現化，具現化時用只宣告七個前綴（office、text、table、style、draw、fo、xlink）的外殼重新解析原始 XML。LibreOffice 在每個儲存格寫 `calcext:value-type`，該前綴只宣告在文件根元素，外殼未宣告使解析失敗，寬鬆模式（預設）又把失敗「搶救」成空的子樹，只留一則診斷警告：LibreOffice 儲存的 ODS 只要有一張工作表超過約 8 KB，`SpreadsheetDocument.Load` 後該表的儲存格全部讀不到，修改後再儲存就把資料永久清掉（`OdsStreamReader` 不受影響，所以兩種讀取器的結果不一致）。現在具現化時帶入該節點當時在作用域內的全部命名空間宣告。OdfKit 自己產生的文件只用到這七個前綴，因此既有測試都沒有發現。
- **修正 DOM 載入時段落內元素之間的空白遺失**：UTF-8 快速解析器在每個標記前跳過所有空白，使文字節點的開頭空白與僅含空白的文字節點全部遺失，結果與 `XmlReader` 路徑不同。LibreOffice 的 ODT 載入後再儲存，會把「粗體 與 斜體」（兩個 `text:span` 之間為單一空格）變成「粗體與 斜體」。現在保留段落內容元素（`text:p`、`text:h`、`text:span`、`text:a`、`text:meta`、`text:ruby-base`、`text:ruby-text`）中僅含空白的文字；結構性元素之間的縮排空白仍然略過；`XmlReader` 路徑（Flat XML 與延遲具現化的子樹）原本保留所有縮排空白，現在與快速路徑採用相同規則，延遲具現化的外殼也依子樹是段落內容或結構性元素區分。
- 修正 schema 驗證器對屬性值為空字串的處理：`<empty/>` 模式在屬性值上未被支援，使規格允許的空值（`styleNameRef` 為 NCName 或 empty，LibreOffice 寫出 `style:list-style-name=""`）被判為不符；另外 `XmlConvert.VerifyName`、`VerifyNCName`、`VerifyNMTOKEN` 對空字串擲出 `ArgumentException` 而非 `XmlException`，驗證 LibreOffice 的 ODT 時整個驗證器因此中止。
- 修正 `TextDocument.AddListWithStyle` 寫出的清單層級樣式屬性命名空間：編號格式、前綴與後綴寫成 `fo:num-format`、`text:num-prefix`、`text:num-suffix`，而 ODF 規定為 `style:num-format`、`style:num-prefix`、`style:num-suffix`，不符 schema，且 LibreOffice 會忽略這些屬性（`a)`、`(I)` 都顯示成 `1`，經 LibreOffice 實測確認）。大綱編號樣式的 `num-suffix` 同樣更正。既有測試鎖定的是錯誤的屬性名稱，已一併更新。
- 修正 `OdfPageSetup` 的頁首頁尾區域順序：ODF 規定 `style:master-page` 的子元素依 `style:header`、`style:header-left`、`style:header-first`、`style:footer`、`style:footer-left`、`style:footer-first` 排列，區域原本一律附加在最後，先設定首頁頁首或頁尾再設定預設頁首就會使文件不符合 schema。現在依順序插入。
- 修正 `AddPageCountField` 寫出的總頁數欄位：原本寫成 `text:page-number text:select-page="last"`，而 schema 只允許 `previous`、`current`、`next`，LibreOffice 也把它當成一般頁碼（以 LibreOffice 匯出 DOCX 確認，頁碼與總頁數都變成 `PAGE`，總頁數顯示成目前頁碼）。現在寫出 `text:page-count`。既有測試鎖定的是 `select-page="last"`，已一併更新。
- 新增 `LibreOfficeInteropTests.RealDocuments.cs` 六個以真實 LibreOffice 執行的互通測試（日期、合併儲存格與稀疏資料、清單與註腳與分頁與頁尾欄位、空白字元與編號格式、OdfKit 載入 LibreOffice 儲存的試算表與文字文件；沒有 LibreOffice 時略過），把這一輪人工探針的發現變成可重複的驗收；互通矩陣與新增的 [OOXML 轉換涵蓋範圍](docs/ooxml-conversion.md) 同步更新。
- 驗證器對 LibreOffice 輸出仍會回報一項真正的規格不符，並非 OdfKit 的缺陷：LibreOffice 的 HTML 匯入寫出 `style:border-line-width-bottom="0cm 0.004cm 0.002cm"`，而官方 ODF schema 的 `positiveLength` 要求嚴格為正，`0cm` 不符。

以官方 ODFDOM 範例與 Office 風格的 XLSX／DOCX（含樣式、合併儲存格、清單、巢狀表格、超連結、註腳與稀疏資料）做了一輪真實性驗證，並以產生器已知的真值逐格、逐段對照。官方範例本身內容極少（試算表與文字範例皆無資料），驗證力有限；真正的發現來自後者。

- 修正段落內空白字元的編碼（ODF 1.3 Part 1 §6.1.2）：消費端會把 `text:p`、`text:h` 及其行內元素中的定位字元與換行視為空格、移除開頭與結尾空格、並把連續空格折疊為單一空格，只有 `text:s`、`text:tab`、`text:line-break` 保留有意義的空白。先前 `TextDocument.AddParagraph(...)`、`OdfParagraph.AddTextRun(...)`、`OdfNode.TextContent` 等路徑把含連續空格、定位字元或換行的文字原樣寫成單一文字節點，LibreOffice 等符合規範的消費端會把 `a    b` 讀成 `a b`；OdfKit 自己的讀取器不折疊空白，因此來回讀寫看不出問題。現在 `text:p`／`text:h`／`text:span`／`text:a` 等段落內容元素的 `TextContent` 在文字含這類空白時會寫成 `text:s`／`text:tab`／`text:line-break`，不含時維持單一文字節點。儲存格文字開頭與結尾的空格原本寫成「字面空格＋`text:s`」，開頭那一個字面空格同樣會被消費端移除，現在整段以 `text:s` 表示。
- 修正 `OdfParagraph.AddHyperlink` 與 HTML 片段匯入寫出的 `text:a` 缺少 schema 必要的 `xlink:type="simple"`。
- 修正存取試算表遠端位址時建立的中間列沒有任何儲存格：ODF schema 規定 `table:table-row` 至少含一個 `table:table-cell` 或 `table:covered-table-cell`，空列使文件不符合 schema。新建的列現在預先含一個空白儲存格。
- 修正 ODF schema 驗證的耗時隨工作表列數呈平方成長：模式比對每次都用新的比對內容檢查子元素，快取形同虛設，同一列會被重複驗證與列數成正比的次數（2,000 列約 72 秒，依平方成長推估 5,000 列超過 7 分鐘）。現在元素比對結果依「模式節點對元素」快取；`table:table` 的內容模型含巢狀重複（`oneOrMore(oneOrMore(table-row))`），每個起點的展開都回傳與列數同階的位置集合，因此再以位元集合取代雜湊集合、記憶「由某位置起重複比對可到達的位置」與各節點的比對結果，並由大到小處理前緣位置，使每個位置只做一次集合聯集。5,000 列由超過 7 分鐘降為約 5 秒、20,000 列約 19 秒（先前約 340 秒），峰值記憶體約 380 MB；仍略高於線性成長，更大的工作表建議改用串流讀取器而非完整驗證。接著又找出剩下的平方項：序列中的重複節點之後若還有其他節點，每個起點都要把後續節點套用到與列數同階的位置集合上，並且每次列舉都配置一個迭代器物件（20,000 列約 14 秒，其中多數是配置）。現在記住序列每個節點最近一次的輸入與輸出，起點依遞減順序詢問時只計算新增的位置，並以結構列舉器取代迭代器：20,000 列約 4 秒、40,000 列約 10 秒、80,000 列約 30 秒（先前約 125 秒）。記憶體成長較快（20,000 列峰值約 440 MB、40,000 列約 1.3 GB），更大的工作表仍建議改用串流讀取器。
- 修正 XLSX → ODS 轉換：（1）日期儲存格被當作當地時間轉成 UTC 並加上 `Z`（`2017-09-23` 寫成 `2017-09-22T16:00:00Z`，UTC+8 機器上整批日期前一天），Excel 的日期序號沒有時區，現在寫成不含時區的 `2017-09-23T00:00:00`；（2）走訪整個已使用範圍並為每個空白儲存格建立節點，位於 `AO5000` 的兩個儲存格就產生約 20 萬個空白儲存格與 9 MB 的 `content.xml`，位址更遠的小檔案可使轉換無法完成，現在只走訪實際存在的儲存格；（3）合併儲存格完全沒有轉換，現在轉為 `number-columns-spanned`、`number-rows-spanned` 與 `covered-table-cell`（每個工作表的被覆蓋儲存格上限 1,000,000，超過的範圍略過，避免 `A1:XFD1048576` 這類極小輸入展開成數十億個節點）。
- 修正 DOCX → ODT 轉換遺失內容：（1）段落中的超連結（`w:hyperlink`）整個被丟棄，現在轉為 `text:a`，目標為 `javascript:`、`vbscript:`、`data:` 時只保留連結文字；（2）定位字元、換行與不斷行連字號被丟棄，且連續空格與前後空格未編碼，現在保留；（3）內容控制項（`w:sdt`）、簡單欄位與自訂 XML 內的文字被丟棄，現在展開；（4）表格以儲存格順序當作欄索引，水平合併（`w:gridSpan`）之後的欄位整排錯位，垂直合併（`w:vMerge`）也未處理，現在依格線位置放置並轉為 `number-columns-spanned`／`number-rows-spanned`（欄數上限 1,024）；（5）儲存格內的巢狀表格整個被丟棄，現在轉為巢狀 `table:table` 並保留前後段落順序。
- DOCX → ODT 轉換補上先前列為未處理的三項（以 LibreOffice 26.2.4.2 開啟輸出確認）：（1）清單：`w:numPr`（含段落樣式帶入的編號）轉成巢狀的 `text:list`／`text:list-item`，並依編號定義建立 `text:list-style`（專案符號字元、`style:num-format`、前綴後綴、起始值、縮排與 `w:suff`；`taiwaneseCounting`、`ideographLegalTraditional` 對應 ODF 的「一, 二, 三, ...」與「壹, 貳, 參, ...」），被其他內容打斷後回到同一份編號時加上 `text:continue-numbering`，Symbol／Wingdings 私用區的專案符號改為標準字元；（2）註腳與章節附註：`w:footnoteReference`、`w:endnoteReference` 轉成 `text:note`，引用標記依序編號，多段內文各自成為 `text:p`；（3）分頁符號：`w:br w:type="page"` 與 `w:pageBreakBefore` 轉成 `fo:break-before="page"`，只承載分頁符號的段落不產生空白段落，段落中間的分頁符號把段落切成兩段。另外，轉換時建立的自動樣式原本以附加方式建在 `office:body` 之後，違反 `office:document-content` 的子元素順序（`office:automatic-styles` 必須在 `office:body` 之前）；`TextDocumentDomHelper.FindOrCreateChild` 現在依 ODF schema 的順序插入 `office:document-content` 與 `office:document-styles` 的子元素。表格儲存格內的段落也改走與本文相同的段落轉換（原本壓成純文字）：樣式、標題階層、定位字元與換行、超連結、清單、註腳與圖片都保留。頁首與頁尾也改走相同的轉換（原本只取純文字串接，且把首頁與偶數頁的頁首全部串在一起）：樣式、清單、表格、超連結與圖片保留，依 `w:titlePg` 與 `w:evenAndOddHeaders` 建立 `style:header-first` 與 `style:header-left`，沒有實際內容的預設頁首頁尾不建立區域；頁碼與總頁數（複雜欄位 `PAGE`、`NUMPAGES` 與簡單欄位）轉成 `text:page-number` 與 `text:page-count`，略過 Word 儲存的結果文字（否則會變成固定的「1」）。以 LibreOffice 開啟後再匯出 DOCX，頁尾還原為 `PAGE` 與 `NUMPAGES` 欄位。多個章節各自的頁首頁尾與頁面設定也已轉換（原本只採用文件最後一個章節的設定）：第一個章節使用預設主頁面，之後頁面大小、邊界、頁首或頁尾不同的章節各建立一個主頁面（`DocxSection{n}`）並套用在該章節的第一個區塊，沒有自己參照的章節沿用前一個章節的頁首頁尾，設定相同的章節共用主頁面。另外補上：超連結內的分頁符號（連結在分頁處切成兩段並各自保留目標）、`HYPERLINK` 複雜欄位與簡單欄位轉成 `text:a`、`DATE`、`TIME` 與 `TITLE`、`SUBJECT`、`AUTHOR`、`FILENAME`、`NUMWORDS`、`NUMCHARS` 轉成對應的 ODF 欄位（原本只留下結果文字）。以真實 LibreOffice 驗證：直向與橫向章節輸出為兩頁且頁面大小各自正確，匯出的 DOCX 含兩個章節的頁首與超連結目標。仍未轉換：連續章節的頁面設定變更、頁首頁尾距離、分欄、頁碼重新起算，以及上列以外的欄位（保留結果文字）。

- 修正 `OdtStreamReader` 的文字擷取：（1）兩個行內元素（例如 `text:span`）之間只隔單一空白時，空白被丟掉而使字黏在一起（`Hello world` 讀成 `Helloworld`），現在保留段落內的空白節點；（2）清單項目與表格儲存格內的多個段落原本直接相接（`A`、`B` 讀成 `AB`），現在以換行分隔；（3）巢狀清單與巢狀表格的內層項目原本被併入外層節點，現在外層節點只含自己的段落，遇到巢狀 `text:list` 或 `table:table` 即結束，內層清單項目與儲存格各自成為獨立節點（文件順序不變）；（4）段落內的 `office:annotation` 與 `text:note`（註解與註腳）內文原本混入段落文字（`AnoteB`），現在略過。標準 ODT 資料集（10 萬個結構節點）的語意檢查碼與修正前完全相同；同時因不再為每個元素建立 `XmlSubtreeReader`，該情境的讀取耗時約 250 ms 降為約 209 ms、GC 配置量約 106 MB 降為約 37 MB。由 `OdtStreamReaderBehaviorTests` 涵蓋同步與非同步兩條路徑。
- 修正 `OdsStreamReader` 的三個資料正確性缺陷，並大幅降低讀取配置量。**缺陷**：（1）多段落儲存格會遺失每隔一個的段落（`A`、`B` 讀成 `A`；讀取一個段落後多讀了一個節點，略過緊接的下一個 `text:p`）；（2）段落內含 `text:span`、`text:a`、`text:s`（連續空白）、`text:tab` 或 `text:line-break` 會擲出 `XmlException`，現在依 ODF 語意讀出顯示文字（`text:s` 為 `text:c` 個空白、`text:tab` 為定位字元、`text:line-break` 為換行，`office:annotation` 與 `text:note` 不計入），並在累積時就套用 `MaxCellTextCharacters`，避免 `text:c` 宣告極大值時配置巨量字串；（3）第一列為空列時，同步的 `Read()` 直接回傳 `false` 而讀不到任何列（非同步路徑正常），現在與非同步路徑一致。（4）空白列區塊（`table:number-rows-repeated`）只回傳一列，但後續列的 `RowIndex` 原本沒有計入被略過的空白列而與實際列號不一致，現在後續列的 `RowIndex` 會加上略過的列數（空白區塊本身的 `RowIndex` 是區塊的第一列；標準 ODS 資料集的語意檢查碼不變）。**最佳化**：不再對每列與每格建立 `XmlSubtreeReader`、屬性改為一次走訪、重用每列暫存並共用空白儲存格實例；100 萬列 × 10 欄的讀取耗時由約 25 至 30 秒降為約 7.7 秒、GC 配置量由約 13.5 GB 降為約 1.8 GB（同機器的 MiniExcel 約 20 至 23 秒、19.7 GB），見 `docs/performance-comparison.md` 第 7 節。由 `OdsStreamReaderCharacterizationTests` 鎖定同步與非同步兩條路徑的逐格結果。
- 修正稽核發現的封裝與公式邊界：Flat XML 不再套用 ZIP 封裝的 `mimetype` 規則；CSV 匯出會先完成索引驗證才建立目標檔；Stream 載入失敗會釋放整個 package 並維持 `leaveOpen` 契約；損壞的選用 `settings.xml` 不再中止儲存；統計彙總溢位統一回報 `#NUM!`。另補齊 MMF 降級處理、Direct I/O 建構、ZIP 收尾與底層串流覆寫失敗時的資源及資料安全，並避免非陣列 `ReadOnlyMemory<byte>` 經 `Stream.WriteAsync` 基底橋接產生額外配置。
- **破壞性**：修正封裝加密與 ODF 1.0～1.4 Part 2 §4.16 的落差，並使 OdfKit 能讀取 LibreOffice／OpenOffice 的傳統加密文件。`manifest:checksum-type` 由非規範的 `SHA256`（完整未壓縮明文）改為 `urn:oasis:names:tc:opendocument:xmlns:manifest:1.0#sha256-1k`（壓縮後未加密資料前 1024 位元組），並支援讀取 `SHA1/1K`／`#sha1-1k`；Blowfish 由 CBC＋PKCS#7 改為 `Blowfish CFB`，演算法宣告改為 `urn:oasis:names:tc:opendocument:xmlns:manifest:1.0#blowfish`（回饋寬度依 OpenOffice.org 以來的實作採 64 位元區塊，而非規範字面的 8 位元）；AES-256-CBC 解密改依 W3C XML Encryption §5.2 移除填補（PKCS#7 為其特例）；`manifest:start-key-generation` 缺席時採規範預設 SHA-1、`manifest:key-size` 缺席時依演算法推導；PBKDF2 反覆運算上限由 50,000 放寬至 10,000,000（LibreOffice 寫入 100,000），OdfKit 自身寫入的預設值同步由 50,000 提高為 100,000。`encryption-data` 不再輸出金鑰衍生擴充屬性，子元素改為 algorithm →〔start-key-generation〕→ key-derivation。Argon2id 的 `key-derivation-name` 與 `loext:argon2-iterations`／`-memory`／`-lanes` 對標 LibreOffice manifest schema，並停止輸出該分支不允許的 `manifest:iteration-count`。另補上 ODF Part 2 §3.4.1 要求的 `manifest:size`（加密項目的原始未壓縮未加密大小），Blowfish 的檢查碼型別改用同世代的 `SHA1/1K`、演算法宣告改用簡短名稱 `Blowfish CFB`，並省略等同規範預設的 `start-key-generation`。加密項目改以 ZIP `STORED` 寫出（內容在加密前已 deflate，密文不可再壓縮），並修正 `PBKDF2` 的虛擬亂數函式：規範 §4.16.7 固定為 HMAC-SHA-1，與 `start-key-generation-name` 無關，先前 AES 路徑誤用 HMAC-SHA-256；解密端保留舊 PRF 後備路徑，既有 OdfKit 加密檔仍可讀取。此二項修正後，**OdfKit 與 LibreOffice 26.2 在 Blowfish CFB 與 AES-256-CBC 兩種傳統加密上達成雙向互通**。解密端相容早期 OdfKit 版本寫出的舊形狀（完整摘要、CBC Blowfish 宣告、`argon2-t`／`-m`／`-p`），但**以舊版寫出的 Blowfish 密文需以舊版解密**。驗證：AES-256-CBC 與 Blowfish 的 manifest 以 Jing 對官方 `OpenDocument-v1.4-manifest-schema.rng` 通過、Argon2id manifest 對 LibreOffice schema 通過，LibreOffice 26.2 產生的傳統加密 ODT 已可逐項目正確解密，且 OdfKit 產出的密文可由獨立實作依規範流程解開。另新增 LibreOffice 24.8+ 預設的整包加密（wholesome encryption）雙向支援：載入時偵測單一 `encrypted-package` 項目，以 `Argon2id(SHA-256(密碼), salt)` 衍生金鑰、解開 `IV(12) ‖ AES-256-GCM 密文 ‖ tag(16)`，再 inflate 展開內層完整封裝；儲存時選用 `OdfEncryptionAlgorithm.Aes256Gcm` 會先建立內層 ODF ZIP、整體 deflate 與加密，再寫出 LibreOffice 相容的三項外層封裝。CLI `sanitize --encryption aes256-gcm` 亦使用此路徑。三種形狀的 LibreOffice 實機素材已納入 `tests/fixtures/encryption-interop/` 並由 `EncryptionInteropCorpusTests` 在主 CI 驗證。邊界記於 [odf-format-support.md](docs/odf-format-support.md)。
- Wholesome 寫入的外部驗證改為持續守門：每週 LibreOffice 雙 TFM 工作流程以 Python UNO 開啟 OdfKit 產出並核對本文；外部 baseline 則以固定 SHA-256 的 LibreOffice 26.2.4.2 extended manifest schema 與 Jing 驗證由目前 CLI 即時產生的 manifest。
- `BE_Government_ODF` profile 由 `Normative`／`VerifiedOfficial` 降為 `Draft`／`NeedsActiveSource`：ODF 義務源自 2006-06-23 部長會議決議與 2007 年聯邦備忘錄，但 BOSA 站內查無直接列出 ODF 的現行條目，`dt.bosa.be` 的開放標準頁目前不可達。
- ASP.NET Core WebFont sample 的 Static File middleware 明確略過 `/_odf-fonts`；先前動態 `AssetRoot` 位於 `wwwroot` 時，IIS InProcess 會由 StaticFileMiddleware 搶先回應並以檔案時間／長度 ETag 取代內容 SHA-256，使安全標頭與條件式端點皆遭繞過。
- OdfKit 字型分段改以 grapheme cluster 為不可拆單位，增補平面基底字不再與 IVS、combining mark、ZWJ 或區域指示符號分到不同 ODF span。`OdfWebFontRequirementCollector.CollectSupportedAsync` 新增依實際 `cmap` 覆蓋與有序來源優先權的多來源收集，未命中文字維持預設字型。
- WebFont 引擎、verifier 與耐久快取改共用 `WebFontUnicodePolicy`，修正空白、variation selector 與 default-ignorable 的 glyph 判定漂移；來源有對應的空白會保留 glyph 與 advance width，但不加入供 CSS 遞補的 `unicode-range`，兼顧排版忠實度與「只套用難字」策略。此問題先後讓真實 CFF 矩陣在 `VerifyContainsSequences` 與 `VerifyRetainsGlyphIds` 誤判合法產物，放寬後又由三瀏覽器像素差分抓到名稱式 CFF 空白 metrics 遺失，並可能讓 IVS／ZWJ 耐久快取回 500。
- ASP.NET Core 與 System.Web 現在只在 coverage filter 判定「無可補 glyph」時回 204；若要求已通過語法與 allowlist 驗證、但引擎後續仍拋出 `ArgumentException`，兩個託管配接器一律視為伺服器端失敗並回 500。客戶端中止改為 499。兩個瀏覽器 helper 支援重疊來源後端覆蓋判定、有界來源平行、可見表單值、屬性變更與完整 `FontFace` descriptor 去重，並忽略無效 UTF-16 cluster。
- Spreadsheet：當 `OdfCell.CellValue` 可將 `office:date-value` 依 round-trip 語意解析時，現在會回傳 `DateTime`（先前為原始字串）。連帶地，`GetValue<DateTime>()` 會保留解析後的時間點，`GetValue<string>()` 則會把該 `DateTime` 以 `InvariantCulture` 固定格式輸出，而非回傳原始 ISO 8601 文字；若解析失敗，仍會退回原始字串。**`Kind` 只在 `Z`（UTC）與無時區形式下保留**：XSD `dateTime` 允許 `+05:30` 這類明確位移，而 `DateTimeStyles.RoundtripKind` 無法在 `DateTime` 中表示，那些值仍會轉為當地時間並標記 `Local`——時間點不變，但結果隨機器時區而異且原始位移遺失，需要原始字面形式時請改讀 `office:date-value`。這是 0.x 未發佈期間的公開行為調整。
- Spreadsheet：`OdfCell` 新增 `CurrencyCode` 與 `SetCurrencyValue(...)`，可直接寫出 `office:value-type="currency"`、`office:value`、`office:currency` 與顯示文字；覆寫既有儲存格時也會一併清掉殘留的貨幣屬性。`CurrencyCode` 讀取時也會正規化為去空白的大寫 ISO 4217 代碼，與 setter 行為一致。寫入時驗證 ISO 4217 的**形狀**（恰好三個 ASCII 字母）並正規化為大寫，但不比對現行代碼清單——那會拒絕歷史代碼、`XTS` 測試代碼與日後配發的代碼；讀取則刻意寬容，已存在於文件中的值只做正規化而不拒絕，確保其他產生器寫出的文件仍可讀取。
- 修正五處例外路徑的資源與資料安全缺陷。**儲存**：`OdfPackageSaver` 覆寫底層串流時原本先 `SetLength(0)` 才複製，複製途中的取消或 I/O 失敗會留下已清空且無法復原的檔案；改為複製完成後才截斷到實際寫入長度。**串流寫入器**：`OdtStreamWriter` 的同步收尾與 `CompleteAsync`、`OdsStreamWriter.Dispose` 的收尾寫入都未包在 `try/finally` 內，中間步驟一旦拋出就跳過 `ZipArchive.Dispose()`——而那是寫出 ZIP 中央目錄的唯一時機，結果是任何工具都打不開的殘骸；`CompleteAsync` 另因開頭即設定 `_disposed`，取消後 `DisposeAsync` 會直接返回而永久洩漏控制代碼。**Direct I/O**：`OdfDirectIoReadableStream` 建構子在第二個對齊緩衝區配置或 `FileInfo.Length` 拋出時會洩漏已配置的原生記憶體（以不存在的路徑建構即可觸發，每次 128 KiB）；由於 `AlignedNativeBuffer` 依 CA2015 刻意不提供 GC 備援釋放，建構子必須自行清理後再重新擲出。
- Worker 耐久快取新增 `MaxDurableAssetBytes` 與 `DurableAssetMaxIdle`：只在跨處理程序 cleanup lease 內清除超出軟性預算、未受保留 manifest 參照且已超過安全期的內容定址資產，避免不同文字組合使磁碟永久單調成長。
- 補齊套件／Flat XML 驗證路徑（`OdfPackageValidator`、`OdfFlatDocumentValidator`、profile 規則掃描）的 `MaxCharactersInDocument`，與核心載入共用 `OdfLoadOptions.MaxXmlCharactersInDocument` 資源預算；簽章寫入／TSA 路徑的 `XmlDocument` 顯式 `XmlResolver = null`。同步更新 `docs/security-limits.md`。
- 釐清公開 API 可選參數與 `CancellationToken` 政策：公開允許尾端 `= default` 或明確多載鏈兩種等價形狀，內部應必填並傳遞；`eng/Expand-OptionalParameters.py` 略過僅 CT 的 `= default`，避免機械拆除 .NET SDK 慣用便利形狀（見 `docs/public-api-optional-parameters.md`）。
- WebFont 子集化修正 `cmap` format 4 建構與解析。建構端新增連續範圍合併（`idDelta` 相同的相鄰碼位併為單一 segment），解除先前「每字元一個 segment」導致的 8,188 個 BMP 字元硬性上限——該上限低於各層設定允許的 65,536，且遠低於 Big5 常用字集（13,053 字）與 CNS 11643 第 1、2 面（約 13,000 字），亦即完整繁中字集子集在修正前必定失敗。極端稀疏字集改為依 OpenType 1.9.1「format 12 存在時 format 4 為相容性選配」省略 format 4，不再中止產生。encoding record 改為規格要求的 platformID／encodingID 排序（`(0,3)`、`(0,5)`、`(3,1)`、`(3,10)`）。解析端以 subtable 宣告的 `length` 約束 `segCount`，並強制 segment 依 `endCode` 遞增且不重疊，將惡意 `cmap` 的展開迴圈上界由約 21 億次降至 0x10000 次。
- 新增 `cmap` 規模路徑的三瀏覽器實機閘門（`eng/Test-WebFontCmapScaleBrowserProof.ps1`／`tests/OdfKit.WebFontCmapScaleProof`）：以鎖定的 Adobe Source Han Sans TC `2.005R` 產生 12,000 字單片子集與 9,000 字稀疏子集（後者依規格省略 format 4，僅保留 `(3,10)`），在 Chromium／Firefox／WebKit 驗證 `FontFace` 實際載入與取樣字元的 canvas 墨跡。同批資產截斷後的負向對照在三個引擎均正確遭拒，用以證明量測不是在觀察 fallback。字型與證據皆不納入 repository。
- WebFont 產字熱路徑全面貫穿 `CancellationToken`（`SfntFont.Parse`／`CreateSubset`、`cmap` 各 format 解析、GSUB closure、composite closure、CFF／CFF2 subsetter 與 compactor、`gvar`、WOFF2 `glyf` 重建）。修正前 `WebFontGenerationWorker.JobTimeout` 雖會觸發卻無人觀察，單一惡意或損毀字型即可永久占住 consumer 執行緒，佇滿後動態產生端點需重啟處理程序才能恢復；架構文件宣稱的「工作逾時」界限至此才實際成立。`ColorFontValidator` 依其巡訪次數已由位元組範圍檢查隱含限制，僅在各色彩技術階段之間檢查取消，此取捨記於該方法 `<remarks>`。
- WebFont 託管層修正：ASP.NET Core 與 System.Web 將要求語法／allowlist 錯誤分為 400、無可補 glyph 分為 204、不支援的格式或技術分為 422、產物資料不一致分為 500、暫時性基礎設施失敗分為 503。`Vary: Origin` 在來源允許清單非空時無條件輸出，避免 `immutable` 長快取讓共享快取將缺少 `Access-Control-Allow-Origin` 的回應餵給合法跨來源請求。動態產生的資產索引新增 `MaxGeneratedAssetCount` 上限（先前隨每次產生單調成長）。
- WebFont worker 修正單飛競態：`_inflight` 移除改為連同 value 比對，先前可能誤刪其他呼叫端新登記的項目而讓同鍵工作重複執行；併修正 `TryCacheCompleted` 因短路而遞減未曾遞增之計數器的失衡。
- WebFont 安全強化：System.Web 資產路徑補上 reparse point（符號連結／junction）檢查，與 ASP.NET Core 端一致；移除會永久快取例外的 `Lazy` 設定載入（暫時性失敗先前會固定回 503 直到 AppDomain 回收）；`WindowsEudcFontSourceResolver` 限制解析結果須位於系統字型目錄內（`HKEY_CURRENT_USER` 對使用者可寫，不應成為任意路徑來源）；建置期 CSS 的字型家族名稱改用標準十六進位逸出，涵蓋換行與 `<` 等可跳出字串或 `<style>` 的字元。
- WebFont 效能：WOFF／WOFF2 標頭的 `totalSfntSize` 改為算術計算，同時要求 ttf／woff／woff2 時省去兩次完整 TTF 序列化；Adler-32 改採 zlib NMAX 分塊取模；輸出 CharString 全量驗證改為 `ManagedOpenTypeWebFontEngineOptions.VerifyEveryOutputCharString` 可設定（建置期建議維持啟用，動態端點可關閉以降延遲）；語料掃描改以純量配對判重，消除每個語料字元一次的字串配置；`ManagedOpenTypeWebFontVerifier` 與 writer 共用單一靜態 WOFF2 已知 tag 表。
- WebFont 驗證器新增 `Verify`／`VerifyContainsScalars` 的位元組上限與取消權杖多載，修正先前硬編碼 32 MiB 與 `MaxOutputBytes` 脫鉤導致合法大型輸出被誤判的問題；WOFF 解碼的 `totalSfntSize` 由嚴格相等放寬為不等式，避免誤拒表格排列與填充方式不同的合法字型（WOFF2 的同名欄位維持既有的「建議值」政策，理由記於程式碼）。
- 新增 `OdfCodePointMappingTable` 通用碼位對照表輔助：`ParseDelimitedHex` 可直接解析 unicode.org 官方對照檔（TAB／分號分隔、`0x`／`U+` 前綴、`#` 註解）、`Parse(lineParser)` 委派擴充點支援任意行格式、`Join` 通用字串鍵聯結（`JoinOnCns` 改為轉呼叫別名）。碼位遷移鏈至此格式中立，同樣適用日本 MJ 外字、GB 18030-2022 PUA 重指派與 MUFI 等遷移場景。解析入口比照 security-limits 原則施行文件化資源預算（行長 4,096／筆數 2,000,000、負值十六進位拒絕、例外訊息截斷與控制字元清洗），CNS 特化解析器同步套用。
- samples 第 9 節新增 CNS 11643 正面示例（全字庫遞補分段、自訂罕字字型情境、PUA 碼位遷移、Big5E 編碼 CSV 匯出）；`OdfTextFontFallbackOptions.Custom` 新增 `fontContext` 三參數多載，修補「自訂 font-face 清單與自訂字型情境無法同時使用」的 API 組合縫隙。
- 效能：`SegmentText` 導入每呼叫平面字型快取，將逐字元的內建規則鏈評估攤提為每平面一次；新增 `SegmentTextBenchmarks` 的 125,000 個 UTF-16 碼元混排工作負載，可用 `pwsh eng/Benchmark-Stable.ps1 -Filter "*SegmentTextBenchmarks*"` 重現目前版本的時間與配置量。原始開發環境曾觀察到 fall-through 家族最高約減少 97% 執行時間，但比例取決於硬體、執行階段與基線 commit，不作為固定保證；純 BMP 路徑不配置平面快取。
- 新增全字庫（CNS 11643 open data）整合：`OdfCns11643MappingTable` 官方對照表解析與聯結、`OdfBig5EEncoding` 資料驅動 Big5E 編碼（可餵入 CSV 匯入匯出）、`OdfDocument.MigrateTextCodePoints` 文件碼位遷移（舊版全字庫 PUA 自造字 → 新版 Unicode 正式碼位，回傳統計報告）。維持「機制內建、資料外部」：對照表由使用者自政府資料開放平臺下載，倉庫不內建資料。新增 cns11643-baseline CI workflow 以釘選版本官方資料驗收 10.4 萬碼位的平面路由、CP950 差異白名單（2 字）與 Big5E 全碼位往返。
- CNS 11643 字型遞補入口擴及圖表與簡報嵌入表格：新增 `OdfChartDocument.SetChartTitle(title, options)`／`SetAxisTitle(dimension, title, options)`（含 `ChartDocumentBuilder`／`ChartAxisBuilder.WithTitle(title, options)`）與 `OdfEmbeddedTable.SetCellText(row, column, text, options)` 多載，重用同一套分段與 font-face 宣告基礎；至此所有承載 ODF 文字 run 的高階入口皆支援字型遞補選項（頁首頁尾經由 `OdfParagraph` 既有多載涵蓋，MathML 公式內容不適用）。
- 字型子系統重構為 `OdfFontContext` 單一實例模型（實例為核心＋靜態 `Default` 單例，對齊 `JsonSerializerOptions.Default` 業界慣例）：字型註冊、替代對照、平面對應、子集化器與警告快取全數由情境執行個體承載；**移除** `OdfFontResolver` 與 `OdfFontSegmenter` 靜態類別（0.x 未發佈期間之刻意破壞性重整）。隔離注入點兩層：`OdfDocument.FontContext`（文件層級，含存檔時字型內嵌）與 `OdfTextFontFallbackOptions.FontContext`（單次呼叫層級），優先序「選項 → 文件 → Default」。已知限制：PDF 匯出因 PDFsharp 全域字型解析器為處理程序層級，一律使用 `Default`。
- 新增自訂罕字字型擴充點：`OdfFontContext.RegisterSupplementaryPlaneFontMapping` 可註冊「基礎字型 → Unicode 平面（1–16）→ 字型名稱」對應（優先於內建規則、`IDisposable` 還原、無鎖讀取熱路徑）；`OdfFontFaceInfo` 公開化為 `sealed record` 並新增 `OdfTextFontFallbackOptions.Custom` 工廠，讓使用者不修改 OdfKit 即可接上自備的 Ext B–J 罕字字型（如黑體系補字字型）。核心維持字型中立，不內建任何第三方字型名稱。
- CLI `convert-csv --encoding` 開放 IANA 編碼名稱與字碼頁編號（如 `big5`、`shift_jis`、`gb18030`、`950`），支援舊系統傳統編碼 CSV；UTF-7 維持 .NET 預設封鎖。
- `docs/odf-format-support.md` 新增 Unicode 版本相容性聲明（平面路由與版本無關，Unicode 17.0 Ext J 自動歸入 Plane 3）與內建對應表 Plane 3 覆蓋現況。
- 完成第二階段 API 人體工學與效能精修：新增 `OdfDiagnostic` 統一診斷模型（八個 report 類別加掛強型別 `Diagnostics` 檢視，見 `docs/reference/diagnostics.md`）、`ImportRecordsAsync`／`ReadRecordsAsync` 非同步物件繫結、`OdfTextMatch.Paragraph`／`ParagraphOffset` 段落定位資訊與搜尋取代單次 traversal 重構、HTML／PDF 匯出低緩衝寫入；同步擴大效能基準與 CI 迴歸閘門（find/replace、物件繫結、export 記憶體）。
- 補齊少數 XML 讀取點的 `MaxCharactersInDocument` 上限（Flat XML 二次解析、串流套印範本、簽章檔載入、混合 PDF XMP 中繼資料），使 XXE／DoS 防禦姿態全庫一致。
- 完成 ODT／ODS／ODP／ODG 高階 facade 的一致 CRUD 生命週期契約，加入逐 topic semantic coverage、隨機 mutation、重複儲存載入、clean-room provenance 與 Office 修改另存驗證；同步更新 Public API 基線及破壞性重整遷移指南。
- 新增 `net48` Windows CLR consumer smoke，從本機 NuGet 套件驗證四主格式 round-trip、binding redirect、native imaging 與 7 個 extensions 最小執行入口。
- 新增 `OdfVersionCompatibilityReport` 與 `AnalyzeVersionCompatibility`，在 ODF 1.4 語意降版至 1.1～1.3 前後提供元素／屬性、命名空間及 DOM 路徑的結構化診斷；儲存仍保留無法映射與 foreign namespace 內容，不捏造等價語意。
- 新增 ODS／ODT 串流 Reader 資源限制選項與真正非同步讀取；repeat、列欄、節點及文字超限時改為失敗，不再靜默截斷。
- 修正 `OdsStreamReader.GetValue` 的 `DbDataReader` 語意：空值回傳 `DBNull.Value`，公式儲存格回傳已儲存快取值；新增 `GetCell` 保留公式、值類型、貨幣及顯示文字。這是 1.0 前的刻意破壞性修正。
- ODS／ODT Writer 新增非同步 flush／complete 路徑；ZIP 中央目錄提交因 BCL `ZipArchive` 限制仍為同步步驟。
- 新增效能預算、能力 claims、證據索引及 GitHub Pages API reference 建置流程；站台初始提供 12 語系，後續擴充為目前的 17 語系。
- API 文件站台重構為 DocFX 站內多語系結構（根層導覽與首頁、初始 12 語系入口改為站內內容頁，後續擴充為 17 語系）：修復模板 logo 全站 404、搜尋框不可見與語系入口孤立問題；移除指向未渲染 `OdfKit.DOM.*` 頁面的失效連結；建置腳本新增語系契約驗證與站內連結健檢閘門（見 `docs/api-docs-site.md`）。
- API 文件站升級至 DocFX 2.78.5 modern 模板，初始加入 12 語系原生 TOC，後續擴充為目前的 17 語系；同時加入站內權威聲明、sitemap、共用 footer 及 modern 輸出驗證。
- API 文件站新增自訂 404 頁（DocFX 內容頁）：建置時注入站台根 `<base>` 使任意深度缺失路徑下樣式與導覽正常、自 sitemap 移除 404 條目，並新增對應建置閘門。

## [0.0.1] - 持續維護

`v0.0.1` 是持續完滿的產品身分，不以升版作為補齊必要功能、文件或品質債務的手段。
GitHub Release 資產若建立，只代表特定提交的交付快照；目前未發佈至 nuget.org。

### 新增

- **核心 ODF 支援**：24 種主要 ODF extension（ODT/ODS/ODP/ODG 及其範本、母片、Flat XML、次格式變體）之偵測、建立、載入、儲存、驗證與來回讀寫。
- **四主格式高階 API**：ODT、ODS、ODP、ODG 已達 `complete` 分級，涵蓋常用建立、編輯、樣式、公式、加密、追蹤修訂、條件格式、樞紐分析表等場景。
- **規範可信度**：ODF 1.1/1.2/1.3/1.4 官方 RELAX NG 衍生的版本化 schema metadata／pattern 驗證、profile 規則（OASIS Strict/Extended、ISO/IEC 26300、EU、ROC Taiwan）、266 筆 corpus fixtures，以及由獨立 CI 以固定版本與 SHA-256 執行的外部 ODF Validator baseline。
- **安全性**：PBKDF2（≤ 50,000 次迭代）、Argon2id、OpenPGP（RSA/ElGamal/ECDH X25519 與傳統曲線）加密；XAdES 數位簽章與時間戳記驗證；XXE／Zip Slip／OOM DoS 防禦。
- **轉換與互通**：
  - OOXML：ODT↔DOCX、ODS↔XLSX（含具名段落／字元樣式、公式、圖表）。
  - PresentationML：ODP↔PPTX（投影片、主題色票、表格、動畫時間軸與 build list）。
  - Managed-first 淨室轉換：ODT↔Markdown／RTF、ODG→SVG，LibreOffice 降為 fallback。
  - LibreOffice headless 互通矩陣（26.x）、OOXML 視覺 golden file 比對。
- **協作格式**：ODT ↔ JSON operations 雙向轉換（對標 ODF Toolkit CLI，`OdfKit.Extensions.Collaboration`）。
- **RDF／中繼資料**：`manifest.rdf` triple CRUD 與 SPARQL 查詢橋接（`OdfKit.Extensions.Rdf`）。
- **效能**：`OdsStreamWriter` 以串流寫入降低常駐記憶體（公開跨套件對比見 `docs/performance-comparison.md`；勿與 GC 累積配置量混淆）；公式剖析採 `ref struct` + `ReadOnlySpan<char>` 低配置設計；XML 標籤字串池化；ZIP 載入 `ArrayPool` 緩衝。
- **泛型物件序列匯出**：新增 `ObjectDataReader<T>`（將任意 `IEnumerable<T>`／`IAsyncEnumerable<T>` 轉接為 `DbDataReader`）與對應的 `OdsStreamWriter.WriteDataAsync<T>` 多載，可將任意物件序列（例如 Entity Framework Core `IQueryable<T>.AsNoTracking().Select(...).AsAsyncEnumerable()` 查詢投影）低記憶體串流匯出成 ODS，亦可與 `SqlBulkCopy` 等外部 `DbDataReader` 消費者互通；核心不因此新增任何外部 ORM 或資料庫套件相依。
- **實務相容性檢查器**：新增 `OdfPracticalCompatibilityValidator`，依 `OdfPracticalCompatibilityProfile`（LibreOffice 現行版本、Microsoft Office ODF、跨辦公軟體可攜編輯）掃描封裝、內容、內嵌圖表與影像，回報 `OdfPracticalCompatibilityReport`／`OdfPracticalCompatibilityIssue` 常見跨工具編輯風險（含 Microsoft Word ODT 復原風險提示）。
- **圖表深度 API**：新增 `OdfChartPreset` 任務導向預設（長條、折線、圓餅、面積、散佈等）、泡泡圖與股價圖系列（`OdfBubbleChartSeriesInfo`／`OdfBubbleChartSeriesRequest`、`OdfStockChartSeriesInfo`／`OdfStockChartSeriesRequest`）與 `OdfChart3DOptions`（投影模式、角度偏移、雙面光照、光源清單），補齊圖表建立與樣式高階 API 深度。
- **TemplateBinder 情境強化**：擴充文字、試算表、簡報、影像與繪圖等文件類型的占位符繫結情境涵蓋範圍，並補上對應 cookbook 範例。
- **ODF 1.4 coverage 契約**：新增 `OdfCoverageContractTests` 等測試鎖定 ODF 1.4 規格覆蓋契約與 typed DOM audit 入口，明確區分規格覆蓋、package lifecycle、high-level facade 與 interop behavior 四個持續追蹤層次。
- **套件與發行**：此里程碑當時包含 8 個套件（`OdfKit` 核心 + 7 個 `OdfKit.Extensions.*`）雙 TFM（`net10.0` + `netstandard2.0`）NuGet 封裝，後續已擴充；目前完整套件與 TFM 清單以 [`docs/package-catalog.md`](docs/package-catalog.md) 為準。GitHub Release 資產不發布至 nuget.org。
- **串流寫入熱路徑（ODS／ODT）**：將批次原始 XML 組裝與字元防線抽至共用 `OdfRawXmlWriter`／`OdfXmlCharacterGuard`（`OdfKit.Core`），`OdsStreamWriter` 與 `OdtStreamWriter` 段落／標題／清單／儲存格熱路徑共用；關閉 `XmlWriter.CheckCharacters` 後仍以 `Err_OdfStreamWriter_InvalidXmlCharacter` 快速失敗；補齊 ODS／ODT fast-path 與字元邊界測試。`docs/performance-comparison.md` 於 2026-07-09 重跑 ODS 百萬列對比（第 2 次：約 4.96 s／472 MB 配置／38 MB 峰值，與 MiniExcel 耗時接近持平）。
- **合規文件**：新增 `docs/ip-compliance.md`（複合授權、AI 產製、clean-room、DCO、採用者盡職調查）；README 補強「何時使用／不使用」與效能敘事對齊。
- **可維護性**：`OdfLocalizer.Exceptions` 初始按 12 語系拆檔，後續擴充為目前的 17 語系；新增 `docs/maintainability.md`、產生碼目錄 README、`eng/Test-OneLineXmlSummary.ps1`；歷史 `Split-*`／`Merge-*` 等腳本移至 `eng/historical-refactor/`；合併弱 partial（`OdfAnimation`、簽章 `Common`）並移除空殼 partial 根檔。
- **公開 API 形狀與文件完滿基線（v0.0.1）**：
  - 手寫公開 API 將 RS0026／RS0027 升為 **error**；產生 DOM／schema 目錄覆寫為 none，且禁止手改 `.g.cs`。
  - 單一尾端可選參數改明確多載鏈；多可選高頻面改 **options 物件**（`OdfRichTextRunOptions`、`OdsRowWriteOptions`、`OdfValidationOptions`、`OdfFlatXmlWriteOptions`、`OdfSchemaRegistrationOptions`）；其餘展開為短多載轉呼叫（`Expand-OptionalParameters.py --dry-run` = 0）。0.x **不**保留舊多可選相容層。
  - PublicApiAnalyzers 雙 TFM Unshipped 基線與 Package Validation；在地化 JSON 產線初始涵蓋 12 語系，後續擴充為目前的 17 語系與鍵對等閘門。
  - 雙語 XML **missing** 清零；`Test-BilingualXmlDocs.ps1` 基線 `TOTAL=0`／`FILES=0`。
  - 高頻 API（`OdfDocumentFactory`、`OdfPackage`、`OdfDocument`、`OdfValidator`、`OdsStreamWriter` 等）便利多載摘要差異化（`eng/Rewrite-ConvenienceSummaries.py`）。
  - 產品品質分層入口見 `docs/product-quality-gates.md`（提交前 A／PR 與 main 之 B／外部環境與穩定量測之 C）。
  - God-class 採人機 KEEP 準則與協作者地圖（`docs/human-agent-maintainability.md`、`docs/architecture-collaborators.md`），禁止為行數機械切檔。
  - 多版官方 schema（1.1～1.4）內建為**產品選擇**（封存／存量流通）；為瘦身拆成可選 NuGet 是版本無關的**永久非目標**。

### 架構

- 採用協作者抽取模式拆分上帝類別；大型 façade 維持領域 partial／engine 邊界（見協作者地圖）。
- 所有公開 `*Async` 方法統一帶 `CancellationToken cancellationToken = default`。
- 測試套件依分層命名規則整理，移除歷史開發階段命名與重複測試檔。
- 可選參數與 options 表面對齊 `docs/public-api-optional-parameters.md`；新增公開 API 須更新 PublicAPI 基線。

### 修正

- 修正 `OdsStreamWriter.SwitchToSheet` 緩衝寫入路徑產生結構錯誤 `content.xml` 的問題（`<office:spreadsheet` 起始標籤未透過同一個 `XmlWriter` 正確關閉即被後續原始位元組覆蓋，導致無法被嚴格 XML 剖析器讀回）；改為統一透過 `XmlWriter.WriteRaw` 寫入緩衝工作表片段，並補上以 `OdsStreamReader` 嚴格剖析回讀的迴歸測試。
- 修正 `OdfDirectIoReadableStream.Dispose` 未等待背景預讀工作（`_prefetchTask`）完成即釋放原生檔案控制代碼／對齊緩衝區的資源生命週期競爭，改為先取出並等待該工作，再釋放底層資源。
- 修正 `OdfTableSheetRepeatSplitEngine.GetRepeatCount` 未對 `number-rows-repeated`／`number-columns-repeated` 設上限的問題，改為與 `OdsStreamReader` 一致地截斷至 1,048,576／16,384，避免文件宣告超大重複計數被呼叫端當成迴圈上限而放大為阻斷服務風險。
- 修正 `FormulaParser.ParsePower` 對 `^` 運算子左結合與優先序的處理，讓其符合 OpenFormula 規範（`2^3^2` 應為 `64`，且 `-2^2` 應視為 `(-2)^2 = 4`）；並修正連續前置一元運算子（如 `--2`）因遞迴解析誤改而無法剖析的問題。
- 修正 `OdfPackageEntry.SetContent(byte[])` 未釋放先前指派之 `Stream` 內容的資源洩漏問題，改為與 `SetContent(Stream)` 一致，於覆蓋內容前先行釋放。
- 修正 `OdfPackageFlatXmlLoader`／`OdfStreamingMailMerge` 於修正整數溢位風險時，意外將 `MaxTotalUncompressedSize = 0` 的語意由「拒絕任何非空內容」改為「不限制」，與 `OdfPackageZipLoader` 及既有慣例不一致的問題。
- 修正 `FormulaParser.ParseFactor` 解析 `*`／`/` 右運算元時未延伸至乘方層級，導致 `^` 出現在因數運算右側時剖析失敗（如 `2*3^2`）的問題。
- 修正 `FormulaStringFunctionHandlers.EvaluateSubstitute` 於搜尋文字為空字串時，未帶出現次數引數會擲出未處理的 `System.ArgumentException`、帶出現次數引數則計數邏輯失真的問題，改為直接回傳 `#VALUE!` 診斷。
- 修正 `OdfTableSheetVisibilityEngine.IsRowVisible`／`IsColumnVisible` 未透過 `OdfTableSheetRepeatSplitEngine.GetRepeatCount` 截斷重複計數上限的問題，避免惡意宣告超大重複計數導致索引整數溢位。
- 修正 `OdfPackageEntry.OpenReader()` 對以 `Stream` 支援內容的專案直接回傳內部共用資料流本體，導致該資料流於呼叫端 `using` 區塊結束後即被釋放、往後任何存取皆擲出 `ObjectDisposedException` 的問題，改為回傳不會連動釋放底層資料流的包裝資料流。
- 統一簽章描述檔路徑 `META-INF/documentsignatures.xml` 參照至既有的 `OdfSignerConstants.SignaturePath` 常數，避免多處獨立硬編碼字面值於日後路徑調整時各自失步。
- 修正 `OdfBouncyCastleOpenPgpProvider` 兩處硬編碼中文例外訊息未透過 `OdfLocalizer.GetMessage` 在地化的問題。
- 修正 `OdfChartDocument.GetPositiveRepeatCount` 未截斷嵌入圖表內嵌資料表重複計數上限的問題，改為與 `OdfSpreadsheetLimits.CsvMaxRepeat`／`FormulaMaxRepeat` 一致地截斷至 10,000。
- 修正 `OdfDrawPageShapeReadEngine.CollectGroupsRecursive` 遞迴走訪 `draw:g` 群組無深度上限的問題，比照 `OdfDatabaseDocument` 既有的巢狀深度防護慣例，於超過 64 層時擲出可攔截的例外，避免惡意或損毀文件觸發 `StackOverflowException`。
- 修正 `SpreadsheetDocumentEmbeddedChartReadEngine.TryReadChartMetadata` 於任何大小限制生效前即以 `ReadToEnd()` 無界讀入嵌入圖表 `content.xml` 的問題，改為透過 `OdfBoundedStreamReader` 以 `OdfLoadOptions.MaxEntrySize` 為上限邊界複製。
- 修正 `PptxToOdpConverter.ConvertGraphicFrame` 未檢查 PPTX 表格儲存格自帶的 `RowSpan`／`GridSpan` 是否與實際表格列欄數一致，格式不一致時會擲出 `ArgumentOutOfRangeException` 中止整個轉換的問題，改為依實際表格邊界夾限合併範圍。
- 統一媒體項目路徑前綴 `"Pictures/"` 參照至既有的 `OdfMediaManager.PicturesEntryPrefix` 常數，並修正多處大小寫比對不一致（`StringComparison.OrdinalIgnoreCase` 與 `Ordinal` 混用）的問題。
- 修正 `OdfToDocxConverter.LoadStylesEntry` 未設定 `MaxCharactersInDocument` 的問題，與同專案內 `OdfToXlsxConverter` 保持一致。
- 修正 `OdfComment.FromXmlNodeSingle` 在節點具有 `dc:date` 屬性時，會跳過解析 `dc:creator`／`text:p` 子節點導致註解作者與內容遺失的問題。
- 修正 `OdtStreamReader.CaptureCurrentElement` 一律以 Text 命名空間讀取 `style-name` 屬性，導致表格儲存格（`table:table-cell`）樣式名稱讀取失敗的問題，改為依節點型別選用 Table 或 Text 命名空間。
- 修正 `OdfPackageEntryAccessEngine.ExtractObjectStream` 於內嵌物件名稱含結尾斜線時，串接出雙斜線路徑（如 `Object 1//content.xml`）導致無法讀取內嵌物件內容的問題。
- 修正 OpenPGP PKESK 封包解析在惡意／毀損輸入下擲出型別不一致例外的問題，並新增隨機化邊界測試取代外部模糊測試工具鏈。
- 修正 `OdfKit.Extensions.Rendering`／`OdfKit.Extensions.Imaging`／`OdfKit.Extensions.Rdf` 等擴充套件之 REST 重試緩衝重用、SKTypeface 資源釋放、RDF 相對 IRI 解析等缺陷。
- 修正資料庫（`OdfKit.Extensions.*` Database 相關）表單元件遞迴深度未設上限與重複鍵檢查缺失的問題。
- 修正 DOM 註解／CDATA 節點掃描邏輯，改依終止符掃描避免誤判；修正合規性掃描器（Compliance）略過規則時未回報、及掃描後未還原串流位置的問題。
- 修正批次套印（Mail Merge）改為真正非同步執行並修正首筆資料遺失的問題；修正 OOXML 公式翻譯過程誤改寫字串常數內容的問題。
- 強化 `OdfSignatureSigner`／`OdfSignatureVerifier` 之 OpenPGP 金鑰抹除與 XAdES 節點走訪安全性；強化封裝（`OdfPackage`）輸入安全與完整性、排序驗證。
- 修正 CSV 匯出（`OdfCsvExporter`）未防範 CSV 公式注入（CSV Injection）的問題，依 OWASP 建議對以 `=`、`+`、`-`、`@` 開頭之文字值加上單引號前綴（新增 `OdfCsvOptions.SanitizeFormulas`，預設啟用）。
- 修正 `OdfPdfExporter` 未釋放 `PdfDocumentRenderer.PdfDocument` 造成資源洩漏的問題。
- 修正 `OdfSlide.AddEmbeddedObject` 未將反斜線正規化為正斜線，導致內嵌物件 `href` 不符合 ODF 封裝路徑規範的問題。
- 修正 `OdfChartDocument` 讀取圖表序列（series）時，若缺少 `values-cell-range-address` 屬性即整筆略過，導致採用內嵌圖表資料的序列完全遺失的問題（`OdfChartSeriesInfo.ValuesCellRangeAddress` 隨之改為可為 `null`）。
- 修正 `AdvancedSecurityTests` 中 5 個測試方法直接對 `ErrorMessage` 斷言英文子字串、未強制文化特性，導致系統語系為 zh-TW 等非英文環境時測試失敗的問題，比照既有 `SecurityComplianceTests` 慣例暫時切換至 `en-US` 文化特性；另發現並修正僅切換 `Thread.CurrentThread.CurrentCulture`／`CurrentUICulture` 於完整測試套件中仍不穩定的問題——`OdfLocalizer.GetMessage` 實際優先採用靜態的 `OdfLocalizer.DefaultCulture`（會被 `EncryptionTests`／`OdfValidationReportTests` 等其他測試類別設定後即不再還原），因此改為同時暫存並還原 `OdfLocalizer.DefaultCulture`，確保不受其他測試執行順序影響。
- 修正 `CliTests`／`DomTest`／`EncryptionTests`／`LibreOfficeRendererBoundaryTests`／`LibreOfficeRendererDiagnosticsTests`／`OdfValidationReportTests`／`PresentationAndRenderingTests` 共 7 個測試類別於建構子設定全域靜態的 `OdfLocalizer.DefaultCulture` 後從未還原、污染同一測試處理程序後續測試的問題，改為實作 `IDisposable`，暫存原始值並於 `Dispose()` 還原。
- 統一 XAdES 命名空間 URI `http://uri.etsi.org/01903/v1.3.2#`（原於 `OdfSignatureSigner`／`OdfSignatureX509Utilities`／`OdfSignatureVerifier` 共 23 處硬編碼字面值）至 `OdfNamespaces.Xades` 常數。
- 合併 `OdfPackageFlatXmlLoader`／`OdfStreamingMailMerge`／`XlsxToOdfConverter`／`UnoserverRestBackend` 各自獨立實作的溢位安全大小檢查邏輯，統一改為呼叫 `OdfBoundedStreamReader.AddBytes`／`EnsureInitialBytes` 的既有多載或新增的 `exceptionFactory` 多載。
- 移除 `OdfDatabaseFormDesigner` 與 `OdfNamespaces` 重複宣告的 5 個命名空間常數，改為直接參照 `OdfNamespaces` 既有常數。
- 修正 `OdfBorder.Parse` 遇到格式不正確的 `#RRGGBB` 色彩片段時靜默退回黑色、掩蓋損毀樣式資料的問題，改用 `OdfColor.TryParse` 驗證格式，格式不正確時記錄診斷警告並略過該色彩片段。
- 重構 `OdfMediaManager.DetectImageFormat` 由循序 if-else 鏈改為資料驅動的 magic bytes 比對表格，行為不變。
- 合併 `OdfSignatureVerifier`（`.Dsig.cs`／`.Revocation.cs`／`.Timestamp.cs`）中重複的「設定 ErrorCode／ErrorMessage／Warnings 並回傳 false」錯誤處理樣式為共用私有輔助方法，並保留控制流程互異（`throw`、迴圈 `break`、條件式覆寫）的呼叫點不變。
- 於 `OdfLength` 新增 `FromEmu`／`ToEmu` 與 `EmusPerInch` 常數，集中 OOXML EMU 單位換算的推導來源；`DocxToOdtConverter`／`PptxToOdpConverter`／`OdpToPptxConverter`／`OdfToDocxConverter` 中原各自獨立推導、數學上等價的 EMU 換算常數與運算，統一改為參照此單一來源。
- 抽取 `OdfFormulaLatexConverter.AppendAtom` 中 `munderover`／`munder`／`mover`／`msubsup`／`msub`／`msup` 六個分支重複的「將子節點包入 `<mrow>` 並輸出標籤對」邏輯為區域函式，判斷樹與 MathML 輸出語意不變。
- 拆分 `OdfPackageFlatXmlLoader.Initialize`：將巢狀內嵌文件抽取邏輯抽出為 `ExtractNestedDocuments`，將 content／styles／meta／settings 四棵 `XElement` 樹的切分邏輯抽出為 `SplitDocumentSections`，核心 XML 串流剖析迴圈維持不變。
- 修正 `OdfPackageFlatXmlLoader.ExtractNestedDocuments` 巢狀內嵌文件 `objectId` 僅做 `TrimStart`／`TrimEnd` 而未呼叫既有的 `OdfPackage.SanitizeEntryName`，導致惡意 `xlink:href` 中的 `..` 片段未被清除、可能覆寫封裝內任意項目的 Zip Slip 變體問題。
- 修正 `OdfPackageEntryNameSanitizer.Sanitize` 逐段比對 `".."` 時，未考慮 Windows 會靜默去除路徑片段尾端句點與空白，導致 `".. "` 等片段可繞過 Zip Slip 防禦的問題，改為比對前先 `TrimEnd('.', ' ')`。
- 修正 `OdfPackageMacroSanitizer.Sanitize` 於 XML 項目淨化失敗時僅記錄警告、原始未淨化內容原樣寫回封裝的問題，改為收集失敗項目並於處理完畢後擲出 `InvalidDataException`，讓呼叫端可感知淨化未完整成功。
- 修正 `OdfMmfZipInfo` 記憶體映射快速路徑解析 ZIP 中央目錄時，未驗證壓縮資料偏移量與大小是否超出實體檔案長度，導致損毀或惡意 ZIP 延後至 `OpenStream` 建立記憶體對應檢視時才擲出未經處理例外的問題，改為於解析階段即驗證並略過越界項目。
- 修正 `OdfUtf8XmlReader` 解析未閉合的 Processing Instruction（截斷於 `<?xml ...` 無 `?>`）時，掃描迴圈已觸及緩衝區尾端仍無條件前進兩個位元組，導致切片越界擲出 `ArgumentOutOfRangeException` 的問題，改為切片前以 `Math.Min` 夾限至緩衝區長度。
- 修正 `OdfNodeChildList.Unlink` 每次移除子節點皆從串列頭重新索引全部節點、違反其宣稱之 O(1) 移除複雜度（實為 O(N²)）的問題，改為僅從被移除節點的原位置往後重新索引。
- 修正 `OdfNode.CloneNode`／`OdfElement.CloneNode`（及間接使用其結果的 `OdfNode.ImportNode`）遞迴複製子節點無深度上限，與 `OdfXmlReader.MaxElementDepth` 剖析路徑防護不一致，透過純 DOM API 疊出的極深巢狀樹可能觸發 `StackOverflowException` 的問題，改為以執行緒個別遞迴深度計數器比照剖析器上限攔截。
- 修正 `OdfElementContentModel.Table.SetSparseCellValue` 將儲存格值設為 `null` 時，未清除該儲存格既有的 `FormulaPtr`／`StyleNamePtr`，導致透過 `ImportData` 覆寫為 `null` 的儲存格仍會殘留並輸出舊公式／樣式的問題。
- 修正 `OdfDrawPageShapeReadEngine` 中 `WalkDrawingNodes`／`FindImageHref`／`ExtractTextBoxContent`／`ContainsDescendant` 遞迴走訪缺少與 `CollectGroupsRecursive` 一致的巢狀深度上限的問題，避免深巢狀 `draw:g` 觸發 `StackOverflowException`。
- 修正 `OdfChartRenderer` 具體化圖表資料範圍為陣列（`GetRangeStrings`／`GetRangeDoubles`）前未限制範圍總儲存格數的問題，惡意圖表範圍（如指向整欄）可能嘗試配置巨量陣列造成記憶體耗盡，改為新增 `OdfSpreadsheetLimits.ChartRenderMaxCells` 上限並於超出時視為無效範圍。
- 為公式引擎多個函式的無界迭代加上與 `OdfSpreadsheetLimits` 一致的上限：`OFFSET` 之 `height`／`width`、`IPMT` 之期數、`WORKDAY`／`NETWORKDAYS` 之日期跨距（新增 `OdfSpreadsheetLimits.FormulaMaxDateSpanDays`），避免惡意公式引數觸發近乎無限迴圈的阻斷服務風險。
- 修正 `OdfSignatureVerifier.Revocation` 中一段永遠無法觸發的死碼判斷（先前的例外處理路徑必定已提前 `return`），並修正線上 CRL 多個下載位址逐一嘗試失敗時僅保留「最後一個」例外訊息、其餘失敗原因遺失不利除錯稽核的問題，改為保留並回報所有失敗訊息。
- 修正 `OdfSchemaPatternValidator.MatchAttributeValueNode` 之 `Ref` 分支為唯一跳過循環參照防護（`EnterReference`／`LeaveReference`）的參照解析路徑，自我遞迴的屬性值 pattern 可能觸發 `StackOverflowException` 的問題，比照其餘解析路徑補上防護。
- 修正 `OdfSchemaPatternValidator` 之 `pattern` facet 使用 `Regex.IsMatch` 未設定逾時的問題，由於 `OdfSchemaRegistry.RegisterSchema` 為公開 API，不受信任來源的 pattern facet 存在 ReDoS 風險，改為附加 2 秒逾時並攔截 `RegexMatchTimeoutException`。
- 修正 `OdfDesignTheme.GetAccentFillColor`／`OdfStyleSet.GetChartPaletteColor` 以 `Math.Abs(index) % length` 正規化索引，當 `index` 為 `int.MinValue` 時會擲出 `OverflowException` 的問題，改為使用不依賴 `Math.Abs` 的正規化運算。
- 修正 `OdfXmlStringPools` 之 `ThreadLocal<PoolHolder>` 誤用 `trackAllValues: true`（實際未使用該追蹤功能），導致伺服器情境下執行緒集區churn 時舊執行緒的字串池於程序生命週期內持續被強引用而緩慢洩漏記憶體的問題。
- 修正 `FormulaLookupFunctionHandlers.EvaluateIndex` 於範圍為真正二維矩陣（多列且多欄）卻僅提供單一索引引數時，未依規範回傳 `#REF!`、而是靜默將索引當作列號並預設欄號為 1 的問題。
- 修正 `OoxmlUnitConverter.TryParseOdfLengthToTwips` 將換算後的 twip 值轉為 `int` 前未檢查是否超出 `int` 可表示範圍的問題，異常巨大的 ODF 長度字串換算後可能產生無意義的極端值。
- 修正 `TtfFontNameReader` 於 TrueType Collection（TTC）中單一子字型名稱表損毀時，因僅有單一外層 `catch` 包覆整個方法而中止其餘子字型名稱擷取的問題，改為逐一子字型獨立捕捉例外並記錄診斷警告；並將原本完全靜默的頂層例外壓制改為透過 `OdfKitDiagnostics.Warn` 留下可追蹤紀錄。
- 修正 `OdfMediaManager.ScanExistingMedia` 建構時整檔載入既有 `Pictures/` 媒體項目計算 SHA-256 卻未套用大小上限的問題，改為透過 `OdfBoundedStreamReader` 以 `OdfLoadOptions.MaxEntrySize` 為界複製，超出上限的項目會記錄警告並略過。
- 修正 `FormulaDateTimeFunctionHandlers.EvaluateWeekNum` 未實作 ISO 8601 週數規則（`type=21`）、原本一律套用美式簡化公式的問題，改為改用符合 ISO 8601「第一週須包含該年第一個星期四」規則的手動計算（netstandard2.0 無 `System.Globalization.ISOWeek` 可用）。
- 修正 `XlsxToOdfConverter.ReadOpenXmlCellValue` 於 SharedString 索引越界時靜默回傳 `null`、與同檔案 `CopyCharts`／`CopyPivotTables` 皆會記錄診斷警告的慣例不一致的問題，改為透過 `OdfKitDiagnostics.Warn` 留下可追蹤紀錄。
- 修正 `DocxToOdtConverter.AppendDrawing` 讀取 DOCX 內嵌 `ImagePart` 前未套用大小上限的問題，改為透過 `OdfBoundedStreamReader` 以 `OdfLoadOptions.MaxEntrySize` 為界複製，避免超大內嵌圖片造成記憶體放大風險。
- 修正 `TableTableElement` 稀疏儲存格頁面配置（`EnsurePageAllocated`／`GetOrCreateCell`）未對列／欄索引設定上限的問題，改為與 ODF 試算表格線規格（1,048,576 列／16,384 欄）一致地拒絕越界索引，避免透過 `ImportData` 匯入異常寬/高的資料來源時觸發無界原生記憶體配置。
- 修正 `TryWriteOverride` 儲存每一列時固定掃描至 ODF 規範上限 16,384 欄以尋找該列已用欄位範圍的問題，改為改用序列化前一次性掃描得出的整表最大已用欄位索引，稀疏且列數龐大的表格可大幅減少不必要的掃描次數。
- 修正 `OdfPackageArchiveWriter` 合併 `content.xml`／`styles.xml` 自動樣式時，以 `Elements().FirstOrDefault(...)` 線性掃描既有樣式名稱去重、隨樣式數量呈 O(n²) 成長的問題，改用 `HashSet<string>` 追蹤已加入的樣式名稱。
- 修正 `OdfSchemaPatternValidator` 之 `MatchAttributePatternReference`／`MatchListReference` 於偵測到同名參照已在作用中堆疊時直接判定為循環並拒絕比對、未比照 `Content.Sequence`／`ElementMatching` 既有慣例改用 `CreateRecursiveContext` 建立巢狀內容繼續比對的問題，導致合法的巢狀或跨分支共用具名 pattern 參照可能被誤判為循環而驗證失敗。
- 修正 `OdfTransformHelper.ParseTransform` 只要字串中任何位置出現 `matrix(...)`，即直接以該矩陣做為結果並略過其餘變換函式（如 `"rotate(0.5) matrix(...)"` 會遺失 `rotate` 部分）的問題，改為僅當整個（去除頭尾空白後的）字串恰為單一 `matrix(...)` 呼叫時才套用此快速路徑。
- 修正 `OdfElementContentModel.Table.AllocatePageMemory` 於 netstandard2.0 分支逐位元組迴圈歸零新配置頁面（約 655,360 bytes）的問題，改用 `Span<byte>.Clear()` 批次歸零。
- 修正 `OdfElementComplexAttributeAccess.GetDateTime` 僅接受精確的 `yyyy-MM-ddTHH:mm:ss`（或附加字面 `Z`）格式，導致含次秒精度或數值時區偏移（如 `+08:00`）等合法 xsd:dateTime 寫法解析失敗、中繼資料時間戳記靜默遺失的問題，改為依序嘗試含次秒精度與數值時區偏移的格式組合。
- 修正 `OdfNode.MigrateMediaReferences` 搬移內嵌物件（非 `Pictures/` 媒體）子項目時，若部分項目搬移失敗仍會繼續將 `href` 改指向該不完整資料夾並儲存 manifest 的問題，改為失敗時移除已寫入的殘缺項目、保留原始參照不變；並將內嵌物件資料夾隨機後綴由 `Guid.NewGuid().ToString("N").Substring(0,8)`（32 位元碰撞空間）改為完整 32 位元十六進位 GUID，降低高併發匯入下的檔名碰撞風險。
- 修正 `OdsStreamWriter.Dispose` 對 `WriteStyles()` 失敗僅記錄警告後吞掉例外的問題，導致輸出封裝可能實際缺少 manifest 已宣告的 `styles.xml` 卻不被呼叫端察覺，改為讓例外傳播（並以 `finally` 確保 `_zip` 資源仍會釋放）。
- 修正 `OdfFontResolver._warnedMissingFonts`／`OdfNumberFormatter.Parsing.FormatInfoPool` 兩處僅供效能／診斷用途的靜態快取無上限成長的問題，長時間執行的轉換服務處理大量不重複字型名稱或格式字串時會緩慢洩漏記憶體，改為加上大小上限，超過時清空重來。
- 修正 `OdfProfileRuleValidator.ValidateMacroOrScriptAttributes` 對文件中每個元素的每個屬性值都做 `Contains("vnd.sun.star.script:")` 全字串掃描的問題，改為限縮至 `href` 屬性，該巨集 URI 僅可能出現於連結類屬性中。
- 為 `DrawImageElement.Crop` 補充 `<remarks>` 說明：方法簽章引數順序 (top, bottom, left, right) 與依 CSS `rect()` 語法規範輸出的 `fo:clip` 屬性值順序 (top, right, bottom, left) 不同屬預期行為，避免呼叫端誤解為缺陷。
- 統一 `OdfSchemaPatternValidator` 三處各自獨立實作、行為曾經分歧過的 RELAX NG `zeroOrMore`／`oneOrMore` 重複比對「frontier（前緣）狀態展開」演算法（內容模型依子元素索引、清單語彙依 token 索引、屬性模式依已消耗屬性位元遮罩）至共用泛型輔助方法 `OdfSchemaPatternFrontierMatcher.ExpandRepeated`。
- 將 `OdfSchemaPatternValidator` 屬性模式比對（`Attributes.Matching`）的「已消耗屬性」狀態，由逗號分隔字串（每次比對節點皆需 `Split`／`int.TryParse`／排序／重新組字串）改為 `BigInteger` 位元遮罩，避免屬性數量較多時的字串配置開銷；`BigInteger` 不像 `ulong` 受 64 位元限制，任意屬性數量皆可正確表示。
- 為 `OdfStyleEngine` 補充 `<remarks>`，明確標示其內部快取（一般 `Dictionary`）非執行緒安全，若需並行處理應為每份文件建立獨立執行個體。
