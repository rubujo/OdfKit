# ASP.NET Core WebFont Sample

本範例以受控動態產字為主，預產生資產及 durable cache 作為暖機與 fallback。字型路徑只來自
部署設定，不接受 HTTP request 提供任意路徑；啟動時會比對完整來源檔 SHA-256。

1. 從[政府資料開放平臺的全字庫資料集](https://data.gov.tw/dataset/5961)下載官方宋體或楷體
   封存檔。PUA 造字範例使用 `TW-Sung-Plus-98_1.ttf`；也可改用
   `TW-Kai-Plus-98_1.ttf`。只將 TTF 解壓到專案私有的 `Fonts` 或部署端受保護目錄，不要安裝
   至作業系統。範例與 NuGet 套件都不散布全字庫字型。
2. 將 `appsettings.WebFont.example.json` 複製為 `appsettings.Development.json`，依實際檔案更新
   `FontSources` 內宋體與楷體 Plus 的 `Path`、`SourceSha256`、`Id`、`FontFamily`、
   `FaceIndex`，以及版本化 `ProfileId`。每個來源都必須分別執行 `Get-FileHash`；不得共用
   雜湊。舊的單一 `FontPath`／`FontSourceId` 設定仍可作為相容 fallback。
3. 以標準 .NET 組態提供至少 32 bytes 的高熵 API key，再啟動網站。正式環境建議使用
   Secret Manager、環境變數或 Key Vault provider；不要把實際 secret 提交至 `appsettings.json`：

```powershell
$env:OdfKit__WebFonts__ApiKey = '<deployment-secret-at-least-32-random-bytes>'
dotnet run --project samples/WebFonts.AspNetCore --urls http://127.0.0.1:5080
```

受信任後端使用相同 key 要求產生內容定址 WOFF2；公開瀏覽器只 GET 產物，不取得 key，也不在
request 中提供實體字型路徑。sample 頁面的 `/sample/generate` 是限速、固定來源、固定
Profile，且只允許 WOFF2、WOFF、TrueType 的示範 BFF endpoint，不會把 key 傳到瀏覽器：

```powershell
$headers = @{ 'X-OdfKit-WebFont-Key' = $env:OdfKit__WebFonts__ApiKey }
$body = @{
  fontSourceId = 'cns-sung-plus'
  faceIndex = 0
  profileId = 'cns11643-euc-tw-2026-08-05'
  fontFamily = 'OdfKit Dynamic CNS'
  sequences = @([char]::ConvertFromUtf32(0xFFAE0))
  formats = @('Woff2')
} | ConvertTo-Json
Invoke-RestMethod `
  -Uri 'http://127.0.0.1:5080/_odf-fonts/generate' `
  -Method Post `
  -Headers $headers `
  -ContentType 'application/json' `
  -Body $body
```

本 sample 以全字庫 Plus 的 PUA 造字區為主，預設驗證
[CNS 17-2174／U+FFAE0](https://www.cns11643.gov.tw/wordView.jsp?ID=1122676)；Ext-B 只作
補充平面對照。PUA 沒有跨字型版本的固定語意，頁面碼位、Profile、Plus 字型版本與 SHA-256
必須成套部署。頁面可載入 `wwwroot/webfont-autosubset.js`，它會以 grapheme cluster 掃描全
Unicode 候選 route，先排除系統字型已有的字，再只將缺字送往 Plus 字型；IVS、ZWJ、
combining mark 與區域指示符號不會被拆開。CSS 應依序放「具名系統字型、OdfKit Plus、
generic family」；不可把 `serif`、`sans-serif` 或 `system-ui` 放在 Plus 前面，否則瀏覽器可能
在 generic family 畫出缺字方框後停止 fallback。這個順序可確保系統已有的普通文字與 Ext-B
不被 WebFont 取代，缺字才落到 Plus。

`createSystemGlyphDetector` 是以 Canvas 字形指紋實作的 best-effort detector；瀏覽器沒有標準
API 可直接回報最後選到的本機 fallback face。已知測試機未安裝 EUDC／PUA 字型時，sample
使用 `assumePrivateUseMissing: true` 加速 800 個 PUA；可能安裝企業 EUDC 的網站應關閉此捷徑，
或提供有權威覆蓋資料的 `isSystemGlyphAvailable` callback。

這套缺字路由可用於其它語言，但本 sample 只配置 CNS 宋體／楷體 Plus，所以不會自動補齊其它
script。要支援其它語言，必須再加入具有該 glyph 與合法嵌入授權的來源字型、獨立
`fontSourceId` 與 route；後端 `cmap` 篩選仍是最後防線。

範例頁會以真實 WOFF2 在執行期切換宋體 Plus 與楷體 Plus，並以兩個來源 `cmap` 共同具有
非零 glyph 的 U+F04E1～U+F0800 共 800 個 PUA 作批次、切換與截圖驗收。此測試使用的
`TW-Kai-Plus-98_1.ttf` 不含 U+FFAE0 的非零 glyph，因此 CNS 17-2174 只列入宋體案例；
楷體案例不得把 fallback 方框誤報為支援。`verifyGlyphRendering` 會逐 grapheme cluster 比較
像素，任一 cluster fallback 即判定失敗。造字與難字即時輸入框的完整內容會追加到同一個
動態預覽區；瀏覽器 smoke 會比較表單值與預覽文字，再儲存截圖，避免只驗證固定測試字串。
頁面另會執行 WOFF2 → WOFF → TrueType → WOFF2 格式切換；不使用 WOFF2 時仍由相同 Sidecar
產生 WOFF 或 TTF，不得改以系統 fallback 或豆腐字冒充格式切換。
瀏覽器 smoke 會輸出每次切換的端到端毫秒數、字型傳輸 bytes 與系統字型排除的 scalar 數；
這些是當次機器、冷暖 cache 與來源字型的測試值，不是固定 SLA。
它也會監看後續 DOM、可見表單值與 open shadow root、按 scalar 數與 UTF-8 bytes 分批，並以
`maximumConcurrentRoutes`（預設 2）限制來源平行數；重疊 route 會交由各來源的實際 `cmap`
做最後判定，失敗後可重試。可用
`data-odf-ignore` 排除不應送出的文字。應用程式必須提供
`window.odfKitRequestWebFonts(route, sequences)`，由已驗證的同源後端代送要求；不得把上述 API
key 暴露給瀏覽器。即使呼叫端錯送混排或純一般字，Handler 也會按來源字型 `cmap` 再篩選；無
可用 glyph 時回 204，而不是讓低階引擎例外擴散成 400／503。

helper 可在不啟用 `unsafe-inline`、`unsafe-eval`、`data:` 或 `blob:` 的嚴格 CSP 下執行。
sample 以外部同源腳本載入 helper，並只允許同源 `script-src`、`connect-src` 與 `font-src`；
應用程式提供的 callback 也必須放在 CSP 允許的外部腳本中。若字型資產改由 CDN 提供，必須將
明確來源加入 `font-src`，不得放寬成萬用來源。

`document.fonts.check()` 可能因 fallback 可排版而回傳 `true`，不能單獨作為目標造字已渲染的
證據。驗收時應呼叫
`OdfKitWebFontAutoSubset.verifyGlyphRendering(fontFamily, text)`，並儲存預覽區截圖；部署新版
helper 時也必須更新檔名指紋或查詢版本，避免瀏覽器沿用舊腳本。

endpoint 具 API key authorization、固定窗口 rate limit、來源／face／Profile／format allowlist、
有界 queue、single-flight、三分鐘工作上限及檔案 durable cache。空的 `AssetRoot` 可直接冷啟動；
產生後回傳的 hash URL 使用一年 `immutable`。受信任後端應依 POST 回傳 manifest 建立頁面所需的
`@font-face`，瀏覽器不應持有 API key。預產生的 `webfonts.json` 與 CSS 也可放入相同
`AssetRoot`；這類穩定 alias 維持 `no-cache`，並以實際傳輸 bytes 的 SHA-256 ETag 支援
GET／HEAD 與 304 重驗證。指紋 CSS 與字型資產使用一年 `immutable`。generation Handler 的
成功與輸入錯誤回應使用 `no-store, no-cache`；sample 也在 authentication／rate limiter 前對
generation POST 套用相同政策，使 401／429 不可被 IIS、WAF 或 CDN 快取。
sample 的 Static File middleware 明確略過 `/_odf-fonts`，即使 `AssetRoot` 位於 `wwwroot`，
資產仍由 WebFont endpoint 套用內容 SHA-256 ETag、安全標頭與條件式要求，不會遭靜態檔案處理器攔截。

多個 ASP.NET Core 節點共用同一個資產目錄時，任一節點可依內容 hash 驗證並提供其它節點已
產生的檔案；產生工作本身仍需外部協調。Worker 的 durable manifest cache 具有條目、bytes 與
閒置期限，字型資產本體則交由共用儲存體或 CDN 的生命週期政策治理。

`OdfKit:WebFonts:ApiKey` 可直接放在未提交且受保護的 `appsettings.{Environment}.json`，也可由
`OdfKit__WebFonts__ApiKey` 環境變數覆寫；舊的 `ODFKIT_WEBFONT_API_KEY` 僅保留為找不到標準組態
鍵時的相容 fallback。完整產製、保管與輪替流程見 [`docs/webfonts.md`](../../docs/webfonts.md)。

Windows CI 可用官方 CNS 字型，透過 IIS Express 與 ANCM 實際驗證 In-Process／Out-of-Process：

```powershell
pwsh eng/Test-WebFontAspNetCoreIisExpressSmoke.ps1 `
  -FontPath <TW-Sung-Ext-B-98_1.ttf> `
  -SourceSha256 a0ddaf5ba5ea1823e853f82514819cf27e6512ef2865ad562c0bba3e879242a5
```

Smoke 另對 In-Process／Out-of-Process 的 WOFF2 資產執行 16 路、256～1,024 次有界 GET 負載，
逐一重算 SHA-256 並記錄 CPU 與 initial／peak working set。Out-of-Process 同時計入 IIS Express
proxy 與 Kestrel `dotnet` 子程序；這些數值是 CI 回歸預算，不是正式容量承諾或長時間 soak。

若由 CDN 提供資產，設定 `OdfKit__WebFonts__PublicBaseUrl`；跨來源部署須另外設定精確的
`OdfKit__WebFonts__AllowedOrigin`。正式 CSP、CORS、WAF 與 CDN 說明見
[`docs/webfonts.md`](../../docs/webfonts.md)。
