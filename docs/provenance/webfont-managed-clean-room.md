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
- 尚未通過的人工作業：由維護者進行第三方結構相異性審查；完成前能力維持 experimental。

### 2026-07-17 靜態 CFF 1.0

- 參與者：Codex agent；實作依據僅限 Adobe CFF TN #5176、Adobe Type 2 TN #5177、Microsoft
  OpenType 1.9.1 與 W3C WOFF／WOFF2；未讀取、搜尋或改寫任何禁止來源。
- 原創範圍：CID-keyed CFF INDEX／DICT／FDSelect／charset／Private／Subrs 有界驗證、相同長度
  Type 2 空 outline 程式、精確單次 CFF table 配置與格式／outline 一致性閘門。
- 真實黑箱 corpus：Adobe Source Han Sans 2.005R `SourceHanSansTC-Regular.otf`，SHA-256 為
  `10e6d832bc73650840aa7fbfec4e10c527f8136ae2aec71c3e1c13a67475c24a`，只進 CI cache／artifact。
- Chromium、Firefox 與 WebKit 對來源 OTF 與 managed WOFF2 的三組中文逐 RGBA byte 及文字
  metrics 相同；名稱式 CFF、OTC、CFF2 與 color font 仍明確拒絕。
- 尚未通過的人工作業：第三方結構相異性與惡意 CFF 安全審查；能力維持 experimental。
