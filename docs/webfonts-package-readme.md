# OdfKit WebFonts

OdfKit WebFonts 是供 C#／.NET 使用的多國罕用字 WebFont 動態產生、預產生與安全託管套件組。

- ASP.NET Core 與 ASP.NET Web Forms 均提供須經授權、有界且以內容定址的動態產生路徑；
  CLI／MSBuild 預產生作為暖機與 fallback。
- 支援 WOFF2／WOFF／TTF／OTF、Unicode、Big5、明確 Big5E 與版本化 PUA Profile；TrueType
  Variable Fonts 與 standalone CID-keyed 靜態 CFF 1.0 為 experimental，OTC、CFF2、
  PostScript variable 與 color font 明確拒絕。
- Arabic／Devanagari 採保留完整 glyph ID、`cmap`、GDEF／GPOS／GSUB 的 correctness-first
  路徑；其它 complex script 必須先有合法 corpus 與三瀏覽器差分證據。
- 提供 CLI／MSBuild 自動內容掃描、CSP/CDN URL、精確 CORS allowlist 與有界背景 Worker。
- 核心不綁定 ADO.NET、Dapper、EF Core 或其它 ORM。

快速開始、套件選型、安全界線與完整範例請參閱
[WebFont 多國罕用字套件文件](https://github.com/rubujo/OdfKit/blob/main/docs/webfonts.md)。

使用任何字型前，採用者必須確認該字型允許修改、子集化及 Web 散布。本套件不會替使用者推定
或授予第三方字型授權。
