# ASP.NET Core WebFont Sample

本範例以受控動態產字為主，預產生資產及 durable cache 作為暖機與 fallback。字型路徑只來自
部署設定，不接受 HTTP request 提供任意路徑；啟動時會比對完整來源檔 SHA-256。

1. 依授權合法取得字型，放在 `Fonts` 或部署端受保護目錄。範例不散布全字庫字型。
2. 將 `appsettings.WebFont.example.json` 複製為 `appsettings.Development.json`，依實際檔案更新
   `FontPath`、`SourceSha256`、`FontSourceId`、`FaceIndex` 與版本化 `ProfileId`。
3. 以標準 .NET 組態提供至少 32 bytes 的高熵 API key，再啟動網站。正式環境建議使用
   Secret Manager、環境變數或 Key Vault provider；不要把實際 secret 提交至 `appsettings.json`：

```powershell
$env:OdfKit__WebFonts__ApiKey = '<deployment-secret-at-least-32-random-bytes>'
dotnet run --project samples/WebFonts.AspNetCore --urls http://127.0.0.1:5080
```

受信任後端使用相同 key 要求產生內容定址 WOFF2；公開瀏覽器只 GET 產物，不取得 key，也不在
request 中提供實體字型路徑：

```powershell
$headers = @{ 'X-OdfKit-WebFont-Key' = $env:OdfKit__WebFonts__ApiKey }
$body = @{
  fontSourceId = 'cns-ext-b'
  faceIndex = 0
  profileId = 'cns11643-euc-tw-2026-05-05'
  fontFamily = 'OdfKit Dynamic CNS'
  sequences = @('邉󠄐', '𠀀', '󰀀', 'العربية', 'हिन्दी')
  formats = @('Woff2')
} | ConvertTo-Json
Invoke-RestMethod `
  -Uri 'http://127.0.0.1:5080/_odf-fonts/generate' `
  -Method Post `
  -Headers $headers `
  -ContentType 'application/json' `
  -Body $body
```

endpoint 具 API key authorization、固定窗口 rate limit、來源／face／Profile／format allowlist、
有界 queue、single-flight、三分鐘工作上限及檔案 durable cache。空的 `AssetRoot` 可直接冷啟動；
產生後回傳的 hash URL 使用一年 `immutable`。受信任後端應依 POST 回傳 manifest 建立頁面所需的
`@font-face`，瀏覽器不應持有 API key。預產生的 `webfonts.json` 與 CSS 也可放入相同
`AssetRoot`；這類穩定 alias 維持 `no-cache`，並以實際傳輸 bytes 的 SHA-256 ETag 支援
GET／HEAD 與 304 重驗證。指紋 CSS 與字型資產使用一年 `immutable`。generation Handler 的
成功與輸入錯誤回應使用 `no-store, no-cache`；sample 也在 authentication／rate limiter 前對
generation POST 套用相同政策，使 401／429 不可被 IIS、WAF 或 CDN 快取。

`OdfKit:WebFonts:ApiKey` 可直接放在未提交且受保護的 `appsettings.{Environment}.json`，也可由
`OdfKit__WebFonts__ApiKey` 環境變數覆寫；舊的 `ODFKIT_WEBFONT_API_KEY` 僅保留為找不到標準組態
鍵時的相容 fallback。完整產製、保管與輪替流程見 [`docs/webfonts.md`](../../docs/webfonts.md)。

Windows CI 可用官方 CNS 字型，透過 IIS Express 與 ANCM 實際驗證 In-Process／Out-of-Process：

```powershell
pwsh eng/Test-WebFontAspNetCoreIisExpressSmoke.ps1 `
  -FontPath <TW-Sung-Ext-B-98_1.ttf> `
  -SourceSha256 eb3f27d9c58e05d23a292e59371fb6afb8d9c5da28d592b18671f1f28d7c8583
```

Smoke 另對 In-Process／Out-of-Process 的 WOFF2 資產執行 16 路、256～1,024 次有界 GET 負載，
逐一重算 SHA-256 並記錄 CPU 與 initial／peak working set。Out-of-Process 同時計入 IIS Express
proxy 與 Kestrel `dotnet` 子程序；這些數值是 CI 回歸預算，不是正式容量承諾或長時間 soak。

若由 CDN 提供資產，設定 `OdfKit__WebFonts__PublicBaseUrl`；跨來源部署須另外設定精確的
`OdfKit__WebFonts__AllowedOrigin`。正式 CSP、CORS、WAF 與 CDN 說明見
[`docs/webfonts.md`](../../docs/webfonts.md)。
