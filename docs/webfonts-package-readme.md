# OdfKit WebFonts

OdfKit WebFonts 是供 C#／.NET 使用的多國罕用字 WebFont 動態產生、預產生與安全託管套件組。

- ASP.NET Core 與 ASP.NET Web Forms 均提供須經授權、有界且以內容定址的動態產生路徑；
  CLI／MSBuild 預產生作為暖機與 fallback。
- 接受 TTF／OTF／TTC／OTC 指定 face、`.tte`、WOFF，以及 net10 standalone WOFF2 null／
  `glyf`／`loca`／`hmtx` transform；net10 WOFF2 collection 指定 face 為 experimental 輸入，輸出瀏覽器部署用
  WOFF2／WOFF／TTF／OTF；支援 Unicode、Big5、明確 Big5E 與版本化 PUA Profile；TrueType
  Variable Fonts、standalone／OTC face 的 CID-keyed 靜態 CFF 1.0、含 VariationStore 的 CFF2
  variable `OTTO` 與不含 VariationStore 的非變動 CFF2 為 experimental；名稱式 CFF、缺少
  VariationStore 卻使用 `vsindex`／`blend` 的 CFF2 與直接 collection 輸出明確拒絕；color font
  採保留完整 glyph ID 空間的 experimental 路徑。
- Arabic／Devanagari 採保留完整 glyph ID、`cmap`、GDEF／GPOS／GSUB 的 correctness-first
  路徑；其它 complex script 必須先有合法 corpus 與三瀏覽器差分證據。
- 提供 CLI／MSBuild 自動內容掃描、CSP/CDN URL、精確 CORS allowlist 與有界背景 Worker。
- 核心不綁定 ADO.NET、Dapper、EF Core 或其它 ORM。

快速開始、套件選型、安全界線與完整範例請參閱
[WebFont 多國罕用字套件文件](https://github.com/rubujo/OdfKit/blob/main/docs/webfonts.md)。

使用任何字型前，採用者必須確認該字型允許修改、子集化及 Web 散布。本套件不會替使用者推定
或授予第三方字型授權。
