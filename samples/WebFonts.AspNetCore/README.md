# ASP.NET Core WebFont Sample

先使用具備 Web 散布權的字型產生資產：

```powershell
dotnet run --project OdfKit.WebFonts.Build -- build `
  --font Fonts/licensed.ttf `
  --content-root samples/WebFonts.AspNetCore `
  --output samples/WebFonts.AspNetCore/wwwroot/_odf-fonts `
  --profile sample-v1 `
  --formats woff2

dotnet run --project samples/WebFonts.AspNetCore
```

若由 CDN 提供資產，設定 `OdfKit__WebFonts__PublicBaseUrl`；跨來源開發測試可另外設定
`OdfKit__WebFonts__AllowedOrigin`。正式 CSP、CORS 與 CDN 說明見
[`docs/webfonts.md`](../../docs/webfonts.md)。
