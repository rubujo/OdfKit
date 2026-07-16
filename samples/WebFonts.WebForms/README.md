# ASP.NET Web Forms WebFont Sample

1. 安裝 `OdfKit.WebFonts.Hosting.SystemWeb`。
2. 將 build-time 產生的 `webfonts.json`、CSS 與 SHA-256 目錄部署到
   `App_Data/OdfWebFonts`，或把 `PublicBaseUrl` 改成 CDN URL。
3. 將 `Default.aspx` 與 `Web.config` 放入 .NET Framework 4.8 Web Forms 應用程式。

IIS 行程只讀取由純 .NET 引擎預產生的資產，不在 HTTP request 中動態產字。正式部署說明見
[`docs/webfonts.md`](../../docs/webfonts.md)。
