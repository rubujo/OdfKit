---
title: 套件選型 / Package selection
---

# 套件選型 / Package selection

- ODF 建立、讀寫與驗證：`OdfKit`。
- HTML、PDF、OOXML、影像、RDF 或協作功能：選擇對應的 `OdfKit.Extensions.*`。
- ASP.NET Core 動態 WebFont：`OdfKit.WebFonts.Hosting.AspNetCore`。
- ASP.NET Web Forms／System.Web 動態 WebFont：`OdfKit.WebFonts.Hosting.SystemWeb`。
- 預產生工具：`OdfKit.WebFonts.Build`；底層純 managed 子集引擎：
  `OdfKit.WebFonts.OpenType`。

Hosting 套件已整合授權、限流、內容定址與快取邊界。一般網站應從 Hosting 套件開始，
不要只安裝底層契約後自行重建安全邊界。

- [完整套件目錄與 TFM](https://github.com/rubujo/OdfKit/blob/main/docs/package-catalog.md)
- [NuGet 相容矩陣](https://github.com/rubujo/OdfKit/blob/main/docs/nuget-compatibility-matrix.md)
- [WebFont 完整文件](../../docs/webfonts.md)
