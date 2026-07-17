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

Windows 開發機或 CI 可用官方 CNS 字型執行真實 IIS Express smoke：

```powershell
pwsh eng/Test-WebFontIisExpressSmoke.ps1 `
  -FontPath <TW-Sung-Ext-B-98_1.ttf> `
  -SourceSha256 eb3f27d9c58e05d23a292e59371fb6afb8d9c5da28d592b18671f1f28d7c8583
```

此測試會將隨機 key 寫入隔離站台的 `web.config`、清除環境變數、實際編譯頁面、經 HTTP 動態
產生 TTF／WOFF，並驗證未授權回應、
GET／HEAD、內容 SHA-256、ETag 與 304；它證明 IIS Express 的 Integrated pipeline，不等同完整
IIS Classic mode 或客戶 WAF／CDN 驗收。
