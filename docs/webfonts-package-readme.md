# OdfKit WebFonts

OdfKit WebFonts 是供 C#／.NET 使用的多國罕用字 WebFont 動態產生、預產生與安全託管套件組。

- ASP.NET Core 與 ASP.NET Web Forms 均提供須經授權、有界且以內容定址的動態產生路徑；
  CLI／MSBuild 預產生作為暖機與 fallback。
- 支援 WOFF2／WOFF／TTF、Unicode、Big5、明確 Big5E 與版本化 PUA Profile；不支援的
  CFF／CFF2、variable、color font 會明確拒絕。
- 提供 CLI／MSBuild 自動內容掃描、CSP/CDN URL、精確 CORS allowlist 與有界背景 Worker。
- 核心不綁定 ADO.NET、Dapper、EF Core 或其它 ORM。

快速開始、套件選型、安全界線與完整範例請參閱
[WebFont 多國罕用字套件文件](https://github.com/rubujo/OdfKit/blob/main/docs/webfonts.md)。

使用任何字型前，採用者必須確認該字型允許修改、子集化及 Web 散布。本套件不會替使用者推定
或授予第三方字型授權。
