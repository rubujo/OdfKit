# WebFont managed 引擎 Clean-room 來源紀錄

> 建立日期：2026-07-16
>
> 適用範圍：`OdfKit.WebFonts.OpenType` 的 sfnt／TTC parser、TrueType／CFF 1.0 subset、
> `cmap`、GSUB closure、TTF／OTF／WOFF／WOFF2 writer 與 verifier。

## 實作契約

本引擎只依公開標準的欄位定義、演算法要求與互通行為重新撰寫。實作者不得閱讀後再翻譯、移植、
改寫或反編譯 FontTools、FreeType、HarfBuzz、SixLabors.Fonts、OpenFontSharp、LayoutFarm
Typography、OTS 或瀏覽器字型引擎的實作程式碼。

允許的設計來源：

- [Microsoft OpenType 1.9.1](https://learn.microsoft.com/en-us/typography/opentype/spec/)
- [OpenType font file 與 checksum](https://learn.microsoft.com/en-us/typography/opentype/spec/otff)
- [OpenType `cmap`](https://learn.microsoft.com/en-us/typography/opentype/spec/cmap)
- [OpenType `glyf`](https://learn.microsoft.com/en-us/typography/opentype/spec/glyf)
- [OpenType `fvar`](https://learn.microsoft.com/en-us/typography/opentype/spec/fvar)
- [OpenType `gvar`](https://learn.microsoft.com/en-us/typography/opentype/spec/gvar)
- [OpenType GSUB](https://learn.microsoft.com/en-us/typography/opentype/spec/gsub)
- [OpenType `OS/2.fsType`](https://learn.microsoft.com/en-us/typography/opentype/spec/os2)
- [W3C WOFF 1.0](https://www.w3.org/TR/WOFF/)
- [W3C WOFF 2.0](https://www.w3.org/TR/WOFF2/)
- [Adobe CFF TN #5176](https://adobe-type-tools.github.io/font-tech-notes/pdfs/5176.CFF.pdf)
- [Adobe Type 2 TN #5177](https://adobe-type-tools.github.io/font-tech-notes/pdfs/5177.Type2.pdf)
- [Unicode 17.0.0](https://www.unicode.org/versions/Unicode17.0.0/)
- [Unicode Ideographic Variation Database](https://www.unicode.org/ivd/)
- [.NET 公開 `BrotliEncoder` API](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.brotliencoder?view=net-10.0)

上述規格內的常數、table tag、bit flag、欄位順序、checksum 常數及標準演算法屬互通所需的規範
事實。程式結構、命名、例外策略、資源上限、資料模型與測試 fixture 均由 OdfKit 重新設計。

## 禁止來源與隔離

第三方實作可在獨立 CI job 中作黑箱 oracle，但須符合下列限制：

1. 產品程式碼撰寫及審查不得開啟第三方實作原始碼。
2. oracle 只接收 OdfKit 已產生的 bytes，回報能否解析、table 摘要或瀏覽器呈現結果。
3. 不得由 oracle 產生 C#、修補檔、table writer 或演算法片段。
4. oracle package、Python、Node、native binary 與下載腳本不得進入產品 nupkg、build consumer 或
   runtime dependency graph。
5. 發現互通差異時，先回到規格重新確認欄位與要求；不得以第三方實作程式碼作修正來源。

## 測試資料

- 最小結構 fixture 由 C# 測試 builder 原創產生，採 OdfKit 的 CC0-1.0。
- 真實字型只使用允許測試／修改的版本，下載 URI、版本、SHA-256 與授權均須鎖定。
- 沒有再散布權的字型不得提交 repository 或放入 nupkg；CI cache／artifact 也依授權與保存期限處理。
- 瀏覽器、FontTools 或其它 validator 的輸出不作為原創程式碼來源。

## 變更稽核

任何修改 `OdfKit.WebFonts.OpenType` parser／writer 的 PR 必須在說明中列出所依規格章節、加入正向
與負向 fixture，並確認未參考禁止來源。第三方程式碼相似性或授權疑慮未釐清前不得發布套件。

### 2026-07-17 TrueType Variable Fonts

- 參與者：Codex agent；提示範圍為純 C#／.NET、Clean Room、安全、效能、retain-GIDs、CFF 與
  Variable Fonts 的分階段規劃與實作。
- 實作依據僅限 Microsoft OpenType 1.9.1 的 `fvar`、`gvar` 與 W3C WOFF2；未讀取、搜尋或改寫
  FontTools、HarfBuzz、FreeType 或其它 subset compiler 原始碼。
- 原創範圍：`gvar` short／long offset 有界解析、未選 GID 的零長度資料重建、精確單次配置及
  synthetic 正負向 fixture。
- 真實黑箱 corpus：Adobe Source Han Sans 2.005R 與鎖定版本 Noto Arabic／Devanagari，皆為
  OFL-1.1；只下載到 CI cache／本機 artifacts，不納入 repository 或 nupkg。
- 採用者外部驗收：可另由第三方進行結構相異性審查；此項不計入工程完成條件。

### 2026-07-17 靜態 CFF 1.0

- 參與者：Codex agent；實作依據僅限 Adobe CFF TN #5176、Adobe Type 2 TN #5177、Microsoft
  OpenType 1.9.1 與 W3C WOFF／WOFF2；未讀取、搜尋或改寫任何禁止來源。
- 原創範圍：CID-keyed CFF INDEX／DICT／FDSelect／charset／Private／Subrs 有界驗證、相同長度
  Type 2 空 outline 程式、精確單次 CFF table 配置與格式／outline 一致性閘門。
- 真實黑箱 corpus：Adobe Source Han Sans 2.005R `SourceHanSansTC-Regular.otf`，SHA-256 為
  `10e6d832bc73650840aa7fbfec4e10c527f8136ae2aec71c3e1c13a67475c24a`，只進 CI cache／artifact。
- Chromium、Firefox 與 WebKit 對來源 OTF 與 managed WOFF2 的三組中文逐 RGBA byte 及文字
  metrics 相同；此階段名稱式 CFF、OTC 與 color font 仍明確拒絕。
- 採用者外部驗收：第三方結構相異性與惡意 CFF 安全審查；此項不計入工程完成條件。

### 2026-07-19 名稱式 CFF 1.0

- 參與者：Codex agent；實作依據限於先前已記錄的 Adobe CFF TN #5176、Adobe Type 2 TN
  #5177 與 Microsoft OpenType 1.9.1；本階段未讀取、搜尋或改寫第三方 parser、subsetter 或
  fuzz framework 原始碼。
- 原創範圍：依 Top DICT 是否含 ROS 區分 CID-keyed 與名稱式 CFF；名稱式路徑禁止混入
  FDArray／FDSelect，接受 ISOAdobe／Expert／ExpertSubset 預定義 charset 或既有有界自訂
  charset，並允許省略 Private DICT。CharStrings 繼續使用相同長度的 retain-GIDs 空 outline。
- 安全與效能：CFF 弱參照解析快取新增 glyph count context 核對，避免相同 table bytes 在不同
  face metadata 下誤用；預定義 charset 依各自固定 glyph 上限拒絕超界輸入。名稱式 `seac`
  依 Adobe #5177 從 `endchar` 取得 StandardEncoding base／accent code，再透過 #5176 charset
  SID 找回 GID；解析結果沿用弱參照快取。代碼非整數或超過 0～255、SID 缺漏、重複 charset SID、
  CID-keyed `seac` 與規格禁止的巢狀組字均明確拒絕。
- corpus：[Adobe 官方 Source Code Pro](https://github.com/adobe-fonts/source-code-pro/releases/tag/2.042R-u/1.062R-i/1.026R-vf)
  `2.042R-u/1.062R-i/1.026R-vf` OTF release，授權為
  [OFL-1.1](https://github.com/adobe-fonts/source-code-pro/blob/2.042R-u/1.062R-i/1.026R-vf/LICENSE.md)；ZIP SHA-256 為
  `754a2e3ebb945ae905d720ac5896b3b34acc9546dd6551ef9536869788629dae`，
  `SourceCodePro-Regular.otf` SHA-256 為
  `9f9664e2edf6f045c11e774f9bd0be6993971f2544a39061a5ce478b96b051f8`。字型只在測試時下載，
  不進入 repository 或 nupkg。
- 證據：純 C# 最小二進位 fixture 驗證合法名稱式 CFF、等長輸出、預定義 charset 上限、跨
  glyph count 快取隔離，以及 ISOAdobe／Expert／ExpertSubset／自訂 charset 的 `seac` closure；
  負向案例涵蓋缺漏元件、巢狀組字與非法元件代碼。官方 OTF 實際產生 OTF／WOFF／WOFF2，所有
  輸出由 managed verifier 逐 glyph 驗證，另以 64 組固定種子來源 mutation 驗證明確拒絕。
- 瀏覽器證據：format matrix 產生的 WOFF2 與官方 OTF 由 Chromium／Firefox／WebKit 比較三組
  Latin 文字的 Canvas RGBA bytes 與文字 metrics，並輸出每瀏覽器 JSON 及截圖 artifact。
- 尚缺的工程證據：可再散布的真實 `seac` 三瀏覽器 corpus；完成前能力維持 experimental。
  Compact rewrite 已由下節完成。第三方結構相異性與惡意字型安全審查只屬採用者外部驗收。

### 2026-07-19 CFF 1.0 Compact Rewrite

- 實作依據限於 Adobe CFF TN #5176、Adobe Type 2 TN #5177 與 Microsoft OpenType 1.9.1；
  未讀取、搜尋或改寫第三方 parser、subsetter 或字型工具原始碼。
- 原創範圍：未選 CharString 縮為單一 `endchar`，以固定 32-bit DICT operand 建立 Top DICT、
  Font DICT 與 Private DICT 的兩趟 relocation，重算 CharStrings／charset／Encoding／FDArray／
  FDSelect／Private 絕對 offset 與 local Subrs 相對 offset；GID、charset、FDSelect 與 subroutine
  bytes 保持不變。
- 安全與效能：替換區必須互不重疊，offset 不得指向替換區內部，所有 checked overflow 統一轉為
  有界 `InvalidDataException`；未選 glyph 共用單一 `endchar` bytes，避免為大型 CJK 字型建立
  數萬個相同的小陣列。
- 證據：規格 fixture 驗證名稱式預定義／自訂 charset、Private／local Subrs relocation、輸出
  縮小與重建冪等；25 案例真實矩陣以 Source Han Sans CID-keyed CFF、Noto CJK OTC 與 Adobe
  Source Code Pro 名稱式 CFF 通過 OTF／WOFF／WOFF2 managed verifier 與三瀏覽器路徑。
- 尺寸：Source Han Sans OTF 由 16,528,276 降至 2,312,096 bytes，WOFF 為 1,565,276 bytes、
  WOFF2 為 1,170,684 bytes；Source Code Pro OTF 由 131,128 降至 63,368 bytes，WOFF 為
  40,404 bytes、WOFF2 為 31,496 bytes。數值只適用鎖定 corpus 與目前測試字串。

### 2026-07-17 CFF2 Variable

- 參與者：Codex agent；實作依據僅限 Microsoft OpenType 1.9.1 CFF2／Font Variations、Adobe
  CFF／Type 2 技術文件與 W3C WOFF／WOFF2；未讀取、搜尋或改寫禁止來源。
- 原創範圍：32-bit INDEX、CFF2 DICT、FDSelect 0／3／4、Item Variation Store、`vsindex`、
  `blend`、隱含 subroutine return 與等長零位移 CharString 的純 C# 有界實作。
- 真實黑箱 corpus：Adobe Source Han Sans 2.005R `SourceHanSansTW-VF.otf`，SHA-256 為
  `e66bca1da93f068521f3ab10dc7fa0c6691a37c64a0ccfdb6bb3a2ee879deb77`，只進 CI cache／artifact。
- Chromium、Firefox 與 WebKit 對 300／500／700 三個 `wght` 座標的來源／subset DOM 截圖
  bytes 相同；managed verifier 另逐一驗證所有輸出 glyph CharString。
- 尚缺的工程證據：更廣多軸 corpus；完成前能力維持 experimental。第三方結構相異性與惡意
  CFF2 安全審查只屬採用者外部驗收。

### 2026-07-19 CFF2 Compact Rewrite

- 實作依據限於 Adobe CFF／Type 2 技術文件與 Microsoft OpenType 1.9.1 CFF2／Font
  Variations；未讀取、搜尋或改寫第三方 parser、subsetter 或字型工具原始碼。
- 原創範圍：未選 CharString 縮為規格允許的零長度資料，以固定 32-bit DICT operand 建立
  Top／Font／Private DICT 與 32-bit INDEX 的兩趟 relocation，回填 Header Top DICT length、
  VariationStore／CharStrings／FDArray／FDSelect 絕對 offset 與 local Subrs 相對 offset。
- `vsindex`／`blend`：Private DICT tokenizer 維持 active variation index 與 blend stack 消耗，
  非 offset 條目逐 bytes 保留；GID、VariationStore、subroutine bytes 與 variation metadata
  不剪枝。
- 安全與效能：沿用 CFF 1.0 的非重疊替換區、禁止 interior offset、checked overflow 正規化與
  共用空 CharString bytes；Top DICT 長度超過 16-bit 時明確拒絕。
- 證據：fixture 驗證 32-bit INDEX、Private／local Subrs relocation、縮小與冪等；25 案例真實
  矩陣以 Source Han Sans CFF2 variable 與其 OTC face 通過 OTF／WOFF／WOFF2 managed verifier、
  deterministic 第二次建置及三瀏覽器路徑。
- 尺寸：Source Han Sans CFF2 OTF 由 10,495,320 降至 343,400 bytes，WOFF 為 72,324 bytes、
  WOFF2 為 54,736 bytes。輸出 SHA-256 依序為
  `9edba222ee24ffd2a4fa6f7abde6fc548f140bdf10f20e198be2ef829784b9f3`、
  `423bb1c93ce7728530b5b7f8268479e79c519b2c65ec13ad402b98fa8c2da346` 與
  `ff08ef2e1c506e7b88a8cded8ebe949043edd38991ffd10238c2a024c87c8b20`。數值只適用鎖定 corpus
  與目前測試字串。Firefox sanitizer 曾拒絕無 stem 的 `hintmask`；依 OpenType 1.9.1 改用零長度
  CharString 後，Chromium／Firefox／WebKit 均通過像素差分。

### 2026-07-18 非變動 CFF2

- 參與者：Codex agent；實作依據僅限 Microsoft OpenType 1.9.1
  [CFF2](https://learn.microsoft.com/en-us/typography/opentype/spec/cff2)。該規格明定不支援 Font
  Variations 的字型必須省略 VariationStore；未讀取、搜尋或改寫 FontTools、FreeType、HarfBuzz
  或其它第三方 parser／subsetter 實作。
- 原創範圍：允許省略 VariationStore／`fvar` 的 CFF2 結構，並以空 variation context 驗證
  Top／Font／Private DICT、INDEX、CharString 與 subroutine；缺少 store 的 `vsindex`／`blend`
  仍以有界 `InvalidDataException` 明確拒絕。
- 安全與效能：CFF2 弱參照解析快取除來源 bytes 外，同時核對 glyph count 與 variation axis
  count；上下文不同時重新解析，避免跨 face／metadata 誤用快取結果，來源淘汰後仍可回收。
- 證據：以官方規格欄位建立的最小二進位 fixture 驗證合法非變動 CFF2、retain-GIDs、非法
  `vsindex`／`blend` 與跨 glyph count 快取隔離。fixture 是結構測試，不冒充真實字型 corpus。
- 尚缺的工程證據：截至 2026-07-18 尚未找到授權可追溯、可鎖定 SHA-256 且能在
  Chromium／Firefox／WebKit 重現的真實非變動 CFF2；能力維持 experimental。

### 2026-07-18 OpenType Collection CFF／CFF2

- 參與者：Codex agent；實作依據僅限 Microsoft OpenType 1.9.1 Font Collections、Adobe
  CFF／Type 2 技術文件與 W3C WOFF／WOFF2；未讀取、搜尋或改寫禁止來源。
- 原創範圍：沿用有界 `ttcf` header 與 collection 起點絕對 table offset 驗證，將指定 CFF／CFF2
  face 複製至既有 immutable table model，再由 standalone writer 重建 checksum；不重用或翻譯
  FontTools、HarfBuzz、FreeType 的 collection 程式碼。
- 真實黑箱 corpus：Noto Sans CJK `Sans2.004` OTC face 4，SHA-256
  `b76b0433203017ca80401b2ee0dd69350349871c4b19d504c34dbdd80541690a`；另以鎖定的 Source Han
  Sans 2.005R CFF2 variable 建立規格合法的單 face OTC。兩者皆 deterministic 產生並由 managed
  verifier 驗證 OTF／WOFF／WOFF2，不納入 repository 或 nupkg。
- 瀏覽器證據：Chromium 直接比較 raw OTC 與 managed WOFF2；Firefox／WebKit 不接受 raw OTC，
  改以同一 face 的 managed standalone OTF 與 WOFF2 逐 RGBA byte 比較。三者皆驗證可部署輸出，
  但 raw `format(collection)` 能力只由 Chromium 證實。
- 尚缺的工程證據：共享 CFF／CFF2 table 的多 face corpus；來源 face 支援維持 experimental。
  直接 collection writer 不在產品輸出契約內。第三方惡意 collection 安全審查只屬採用者外部
  驗收。

### 2026-07-18 WOFF 輸入與 Color Fonts

- 參與者：Codex agent；實作依據僅限 W3C WOFF／WOFF2 與 Microsoft OpenType 1.9.1 的
  COLR、CPAL、CBDT、CBLC、EBDT、EBLC、`sbix` 與 `SVG ` 規格；未讀取、搜尋或改寫禁止來源。
- 原創範圍：WOFF zlib 與 null-transform WOFF2 的有界 sfnt 正規化；color table 成對關係、版本、
  計數、offset、strike／document／glyph range 驗證；color 輸入保留完整 GID 的
  correctness-first 路徑。
- 真實 corpus：Google Noto Emoji v2.047 `NotoColorEmoji.ttf`，SHA-256
  `39ee3c587e10e89669b9ff32703261d10d5f9c4dd5ad147b6b5a1c5200591817`；同 tag
  `Noto-COLRv1.ttf`，SHA-256
  `23549f29b5ad741fcb4c025b8dc44652ff0f459892467ebcccec1e6bbe839b44`。兩者採 OFL-1.1，
  僅下載到 CI cache／artifact，不納入 repository 或 nupkg。
- 後續已完成 COLR v0／v1 全部 paint graph、`sbix dupe` glyph closure、SVG document 主動內容
  安全檢查，以及 CBDT／CBLC、EBDT／EBLC 資料範圍驗證。分格式 aggressive color pruning 是
  選用效能優化，不列為工程完成條件；第三方惡意 color font 稽核仍是未取得的額外證據，因此
  能力維持 experimental。

### 2026-07-19 sbix 與 OpenType SVG 真實 corpus

- 參與者：Codex agent；產品實作仍只依 Microsoft OpenType 1.9.1 規格。Google Color Fonts
  repository 只讀取 README、Apache-2.0 LICENSE、兩個 TOML corpus metadata 與產生後字型；未讀取、
  搜尋、翻譯或改寫其產生器、FontTools、HarfBuzz、FreeType、OTS 或瀏覽器原始碼。
- 黑箱 corpus：commit `0046ea4c3b69e9fbbe464c2594816894e3aa5e4b` 的
  `samples-sbix.ttf`，SHA-256
  `0fd0a23379b0e982db8bef5f9a50cf7960a6ee3504778a9a9c039bde4d2f573d`；
  `samples-picosvg.ttf`，SHA-256
  `c55758a47ce0c0493eed2ba4a7ec131eed44649ab38a22ce318371427f841470`。兩者採
  Apache-2.0，只下載至 CI cache／artifact，不納入 repository 或 nupkg。
- 證據邊界：兩者通過 deterministic TTF／WOFF／WOFF2 managed verifier。瀏覽器測試要求來源與
  managed WOFF2 逐 RGBA byte 相同且至少一個非灰階像素；Chromium 驗證 `sbix`，Firefox 驗證
  OpenType SVG。其它不渲染組合明確記錄為 `browser-unavailable`，不以空白畫布算成功，也不宣稱
  已實作 color table 跨格式轉碼。

### 2026-07-18 WOFF2 transformed tables decoder

- 參與者：Codex agent；原創實作只依 W3C WOFF 2.0 Recommendation 的 255UInt16、Table
  Directory、5.1 transformed `glyf`、5.2 triplet、5.3 `loca` 與 5.4 `hmtx` 規範文字。未讀取、
  搜尋、翻譯或改寫 FontTools、Google woff2、FreeType、HarfBuzz、OTS 或瀏覽器解碼器原始碼。
- 原創範圍：collection directory、255UInt16 face／table index、共享 transformed table 配對、
  七個 glyf substream 的精確切分與消耗、simple／composite glyph、bbox／overlap
  bitmap、instructions、四類 triplet、short／long `loca`、`hmtx` bearing 重建，以及展開大小、
  glyph／point／component、offset、reserved flag 與尾端資料防禦。
- 正向 corpus：W3C `woff2-compiled-tests` commit
  `1fd8cd583645618f4df36c65a297479840ad5510` 的 WOFF2／TTF pair；Google Fonts production
  Noto Sans v42 Latin WOFF2 SHA-256
  `09aee8065d25508f23a4c3d92cd777ac869c52d93fd868a88f025d888a7937d6` 與 Devanagari WOFF2
  SHA-256 `1ccb720178c307d17a30f2f8eda43c2f9ffa831c02cb7f7d9d7b8708bcbaf43c`。Google 字型為
  OFL-1.1；所有 corpus 只下載至 CI cache／artifact，不納入 repository 或 nupkg。
- W3C 與 production corpus 只作黑箱資料／結果驗證，不作程式碼來源。synthetic C# fixture
  驗證 `hmtx` 三種合法 omission 組合、simple triplet、composite、short／long `loca` 與負向拒絕；
  兩個 production WOFF2 各執行 64 組固定種子 byte mutation，限制結果只能是有效解析或明確的
  `InvalidDataException`／`NotSupportedException`。
- decoder 與 synthetic fixture 完成後，為釐清 2019 compiled corpus 的 `hmtx` reference bytes
  差異，曾只讀檢視 W3C `woff2-tests` 的 fixture generator 中 `transformHmtx`／`makeLSB1` 測試資料
  建構段落；未檢視其 glyf decoder、Google woff2 或 FontTools 內部實作，亦未依該段落修改產品
  演算法。W3C pair 因此只比較非 transform tables 並驗證重建 sfnt 結構；`hmtx` bytes 的精確
  正向契約由獨立 C# synthetic fixture 三種 flags 覆蓋。此 post-implementation 檢視須納入人工
  clean-room 審閱，不得隱匿為完全未接觸測試 generator。
- WOFF2 collection 以規格建構的負向 fixture、SHA-256 鎖定的官方 CNS 宋體 Ext-B／PUA
  真實 sfnt face 所建立之 null-transform collection，以及同一 W3C commit 的 DSIG 移除／face-order
  transformed collection 驗證。兩個 W3C collection 各有 3 face，含 `glyf`／`loca` v0 與
  `hmtx` v1；每個輸出 face 的非重建表均逐 byte 比對官方 TTC reference，重建表通過 managed
  結構驗證，另驗證越界 face 拒絕。直接
  collection 輸出不是產品目標；第三方惡意 WOFF2 安全稽核與 coverage-guided fuzz 也不屬於
  套件完成或 CI 條件，只能作外部額外證據。整體 engine 仍因其它格式 corpus 缺口維持
  experimental。
