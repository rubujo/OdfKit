---
title: 套件選型 / Package selection
---

# 套件選型 / Package selection

- ODF 建立、讀寫與驗證：`OdfKit`。
- HTML、PDF、OOXML、影像、RDF、協作或文件巨集管理：選擇對應的 `OdfKit.Extensions.*`。
- ODF 1.0～1.4 指令碼與 LibreOffice Basic／Python 文件巨集 CRUD：
  `OdfKit.Extensions.Scripting`；套件不執行巨集或重新簽章。
- ASP.NET Core 動態 WebFont：`OdfKit.WebFonts.Hosting.AspNetCore`。
- ASP.NET Web Forms／System.Web 動態 WebFont：`OdfKit.WebFonts.Hosting.SystemWeb`。
- net48 request-time WOFF2：另加入 `OdfKit.WebFonts.Sidecar`，並部署 NativeAOT Host。
- 預產生工具：`OdfKit.WebFonts.Build`；底層純 managed 子集引擎：
  `OdfKit.WebFonts.OpenType`。

Hosting 套件已整合授權、限流、內容定址與快取邊界。一般網站應從 Hosting 套件開始，
不要只安裝底層契約後自行重建安全邊界。

## 全字庫 Plus 與缺字路由

一般文字以及作業系統已有的 Ext-B 字形應維持使用具名系統字型；只有實際缺字的
grapheme cluster 才交給動態 WebFont。CNS 造字區應使用全字庫封存檔內版本相符的
`TW-Sung-Plus-98_1.ttf` 或 `TW-Kai-Plus-98_1.ttf`，放在網站私有目錄或唯讀部署掛載，
不要安裝到開發機或伺服器的系統字型環境。頁面碼位、PUA Profile、字型版本及來源
SHA-256 必須成套管理；不同 Plus 字型不可共用雜湊。

System.Web 的設定含有效 `sidecar` 區段時，Handler 會自動使用 Sidecar。本機
IIS Express 可設定 `sidecar.autoStart: true`；正式 IIS 建議由 Windows Service 或部署平台
管理 Host。Sidecar 選擇與輸出格式互相獨立：WOFF2 不可用時仍可產生 WOFF 或 TrueType，
不應以豆腐字作為停用 WOFF2 或診斷 Sidecar 的結果。

範例 helper 支援不含 `unsafe-inline`、`unsafe-eval`、`data:` 或 `blob:` 的嚴格 CSP。
正式部署仍須將實際 API 與字型來源分別列入 `connect-src` 與 `font-src`。

- [ASP.NET Core 完整範例](../../samples/WebFonts.AspNetCore/README.md)
- [ASP.NET Web Forms 完整範例](../../samples/WebFonts.WebForms/README.md)
- [Web Forms CSP 相容 helper](/OdfKit/samples/WebFonts.WebForms/webfont-autosubset.js)
- [ASP.NET Core CSP 相容 helper](/OdfKit/samples/WebFonts.AspNetCore/wwwroot/webfont-autosubset.js)

- [完整套件目錄與 TFM](https://github.com/rubujo/OdfKit/blob/main/docs/package-catalog.md)
- [NuGet 相容矩陣](https://github.com/rubujo/OdfKit/blob/main/docs/nuget-compatibility-matrix.md)
- [WebFont 完整文件](../../docs/webfonts.md)
