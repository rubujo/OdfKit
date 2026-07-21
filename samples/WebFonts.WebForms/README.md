# ASP.NET Web Forms WebFont Sample

1. 安裝 `OdfKit.WebFonts.Hosting.SystemWeb`。
2. 依授權合法取得字型，放在 `App_Data/Fonts`，並以實際 SHA-256 更新
   `webfonts.dynamic.example.json` 後另存為 `App_Data/webfonts.dynamic.json`。
3. 以應用程式集區環境變數 `ODFKIT_WEBFONT_API_KEY`，或受 Protected Configuration 保護的
   `Web.config` 鍵 `OdfKit.WebFonts.ApiKey` 提供高熵 API key；不得把明文 secret 提交至原始碼。
4. 將 `Default.aspx` 與 `Web.config` 放入 .NET Framework 4.8 Web Forms 應用程式。

範例 `Web.config` 包含目前套件相依組件的 binding redirects；若由專案系統安裝或升級套件，應以
該次建置產生的 redirects 為準，不要沿用不同套件版本的值。

受信任後端以 `POST /_odf-fonts/generate` 與 `X-OdfKit-WebFont-Key` 要求動態產字；公開頁面只
GET 回傳 manifest 中的內容定址資產。Handler 使用有界並行、face／Profile／format allowlist，
並在相同路徑保留預產生 manifest／CSS fallback。`net48` 明確只產生 TTF／WOFF，不會靜默把
WOFF2 改成其它格式。正式部署與 JSON 本文範例見 [`docs/webfonts.md`](../../docs/webfonts.md)。
動態 POST 的成功、授權、格式、限流與暫時失敗回應皆禁止快取；manifest、CSS 與內容定址字型
支援 GET／HEAD、SHA-256 ETag 及 304。Handler 逐 byte 傳送已驗證的 UTF-8 CSS，避免轉碼造成
manifest hash 與 CDN 實際內容不一致。

`webfont-autosubset.js` 會以 grapheme cluster 掃描頁面並只收集 Plane 2 難字，一般字維持頁面
預設字型；IVS、ZWJ、combining mark 與區域指示符號不會被拆開。它會監看後續 DOM 與 open
shadow root 與可見表單值、分批要求，並以 `maximumConcurrentRoutes`（預設 2）限制來源平行數；
重疊 route 交由各來源的實際 `cmap` 判定且失敗後可重試。可用 `data-odf-ignore` 排除不應送出的文字。應用程式須
以既有登入身分在受信任後端實作
`window.odfKitRequestWebFonts(route, sequences)`；API key 不得進入瀏覽器。Handler 仍會依來源
字型 `cmap` 做第二次篩選，因此惡意或錯誤送入的混排文字不會拖垮服務；該來源沒有任何可用
glyph 時回 204，後續合法要求仍可正常處理。

Windows 開發機或 CI 可用官方 CNS 字型執行真實 IIS Express smoke：

```powershell
pwsh eng/Test-WebFontIisExpressSmoke.ps1 `
  -Pipeline Integrated `
  -FontPath <TW-Sung-Ext-B-98_1.ttf> `
  -SourceSha256 eb3f27d9c58e05d23a292e59371fb6afb8d9c5da28d592b18671f1f28d7c8583
pwsh eng/Test-WebFontIisExpressSmoke.ps1 `
  -Pipeline Classic `
  -Destination artifacts/webfont-iis-express-classic-smoke `
  -FontPath <TW-Sung-Ext-B-98_1.ttf> `
  -SourceSha256 eb3f27d9c58e05d23a292e59371fb6afb8d9c5da28d592b18671f1f28d7c8583
```

此測試會將隨機 key 寫入隔離站台的 `web.config`、清除環境變數、實際編譯頁面、經 HTTP 動態
產生 TTF／WOFF，並驗證未授權回應、GET／HEAD、內容 SHA-256、ETag 與 304。兩次執行分別以
`Clr4IntegratedAppPool` 與 `Clr4ClassicAppPool` 啟動隔離站台；它們不等同客戶正式 IIS、WAF／CDN
或組織安全設定的驗收。每個 pipeline 另對 WOFF 執行 16 路、256～1,024 次有界 GET 負載，逐一
驗證 SHA-256 並將 CPU 與 initial／peak working set 寫入 `evidence.json`；這不等同長時間 soak。
