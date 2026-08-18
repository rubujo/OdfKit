# ASP.NET Web Forms WebFont Sample

1. 安裝 `OdfKit.WebFonts.Hosting.SystemWeb`。
2. 從[政府資料開放平臺的全字庫資料集](https://data.gov.tw/dataset/5961)下載官方宋體或楷體
   封存檔。PUA 造字範例使用 `TW-Sung-Plus-98_1.ttf`；也可改用
   `TW-Kai-Plus-98_1.ttf`，但必須重新計算雜湊。只將 TTF 解壓到網站私有的
   `App_Data/Fonts`，不要安裝至 Windows 字型目錄或寫入字型登錄。範例不散布全字庫字型。
3. 執行 `Get-FileHash .\App_Data\Fonts\TW-Sung-Plus-98_1.ttf -Algorithm SHA256`，以實際
   SHA-256 更新
   `webfonts.dynamic.example.json` 後另存為 `App_Data/webfonts.dynamic.json`。
4. 以應用程式集區環境變數 `ODFKIT_WEBFONT_API_KEY`，或受 Protected Configuration 保護的
   `Web.config` 鍵 `OdfKit.WebFonts.ApiKey` 提供高熵 API key；不得把明文 secret 提交至原始碼。
5. 將 `OdfKit.WebFonts.SampleInternalGenerateUrl` 設為同一個 IIS 站台的明確 loopback 動態
   端點，例如 `http://localhost:5095/_odf-fonts/generate`。範例 BFF 只接受 loopback URL，
   不會依不受信任的 `Host` header 組合轉送目的地。
6. 將 `Default.aspx`、`WebFontGenerate.ashx`、`webfont-autosubset.js`、
   `webfont-sample.js`、`webfont-sample.css` 與 `Web.config` 放入 .NET Framework 4.8
   Web Forms 應用程式。

範例 `Web.config` 包含目前套件相依組件的 binding redirects；若由專案系統安裝或升級套件，應以
該次建置產生的 redirects 為準，不要沿用不同套件版本的值。

受信任後端以 `POST /_odf-fonts/generate` 與 `X-OdfKit-WebFont-Key` 要求動態產字；公開頁面只
GET 回傳 manifest 中的內容定址資產。Handler 使用有界並行、face／Profile／format allowlist，
並在相同路徑保留預產生 manifest／CSS fallback。`net48` 的處理程序內引擎明確只產生
TTF／WOFF，不會靜默把 WOFF2 改成其它格式；需要 request-time WOFF2 時，可改用
`webfonts.dynamic.sidecar.example.json`，連線至不需安裝 .NET Runtime 的 NativeAOT sidecar。
Sidecar token 優先讀取 `ODFKIT_WEBFONT_SIDECAR_TOKEN`；未設定時可由
`tokenAppSettingName` 指向受 Protected Configuration 保護的
`OdfKit.WebFonts.SidecarToken`。IIS 與 Host 必須取得相同 token；授權失敗是 503，不是缺字
的 204。
正式部署與 JSON 本文範例見 [`docs/webfonts.md`](../../docs/webfonts.md)。
動態 POST 的成功、授權、格式、限流與暫時失敗回應皆禁止快取；manifest、CSS 與內容定址字型
支援 GET／HEAD、SHA-256 ETag 及 304。Handler 逐 byte 傳送已驗證的 UTF-8 CSS，避免轉碼造成
manifest hash 與 CDN 實際內容不一致。

`WebFontGenerate.ashx` 是最小同源 BFF 範例：它限制 POST、JSON 與 64 KiB 本文，只將
`sidecar`／`managed` 診斷選擇轉成受限 header，並在伺服器端取得 API key。公開腳本及 HTML
不包含 key。正式應用程式仍須在 BFF 前套用既有登入、授權、稽核與組織要求的 CSRF 防護。
互動頁會顯示完整輸入內容及 800 個宋／楷 Plus 共通 PUA，並可獨立切換字型、Sidecar 與
WOFF2／WOFF／TTF。

本 sample 以全字庫 Plus 的 PUA 造字區為主，預設驗證
[CNS 17-2174／U+FFAE0](https://www.cns11643.gov.tw/wordView.jsp?ID=1122676)；Ext-B 只作
補充平面對照。PUA 沒有跨字型版本的固定語意，必須讓頁面碼位、Profile、Plus 字型版本與
SHA-256 成套部署。`webfont-autosubset.js` 會以 grapheme cluster 掃描全 Unicode 候選範圍，
先排除系統字型已有的字，再只把缺字交給 Plus 字型；CSS 順序必須是「具名系統字型、
OdfKit Plus、generic family」。不可把 `serif`、`sans-serif` 或 `system-ui` 放在 Plus 前面，
否則 generic family 的缺字方框可能阻止後續 fallback。普通文字與系統已有的 Ext-B 因此維持
本機字型，真正缺字才落到 Plus。IVS、ZWJ、combining mark 與區域指示符號不會被拆開。它會
監看後續 DOM 與 open
shadow root 與可見表單值、分批要求，並以 `maximumConcurrentRoutes`（預設 2）限制來源平行數；
重疊 route 交由各來源的實際 `cmap` 判定且失敗後可重試。可用 `data-odf-ignore` 排除不應送出的文字。應用程式須
以既有登入身分在受信任後端實作
`window.odfKitRequestWebFonts(route, sequences)`；API key 不得進入瀏覽器。Handler 仍會依來源
字型 `cmap` 做第二次篩選，因此惡意或錯誤送入的混排文字不會拖垮服務；該來源沒有任何可用
glyph 時回 204，後續合法要求仍可正常處理。

`createSystemGlyphDetector` 是 Canvas 字形指紋的 best-effort detector，因為瀏覽器沒有標準
API 可直接回報每個 cluster 最後使用的本機 fallback face。已知測試機未安裝 EUDC／PUA 字型
時，可設定 `assumePrivateUseMissing: true` 加速 500～1,000 個 PUA；可能安裝企業 EUDC 的
環境必須關閉此捷徑，或提供權威的 `isSystemGlyphAvailable` callback。

helper 本身可掃描其它語言的 grapheme cluster，但本 sample 只配置 CNS Plus。其它語言缺字
必須另加具有合法嵌入授權、實際涵蓋該 script 的來源字型與 route；不能期待 CNS Plus 自動補齊
Arabic、Indic 或東南亞文字。

楷宋切換驗收應另取兩個實際來源 `cmap` 的非零 glyph 交集。專案的實機案例使用
U+F04E1～U+F0800 共 800 個 PUA，依序驗證宋體 Plus → 楷體 Plus → 宋體 Plus，每一步都要求
新的 WOFF2 manifest 並儲存截圖。此測試使用的 `TW-Kai-Plus-98_1.ttf` 不含 U+FFAE0 的非零
glyph，所以 CNS 17-2174 只驗證宋體；不可因同一段文字中的其它字成功渲染，就把 U+FFAE0
在楷體中的 fallback 方框判為成功。

Sidecar 開關與輸出格式選擇必須分開。停用 WOFF2 不代表停用 Sidecar；呼叫端仍可要求 WOFF
或 TrueType。若測試頁需要切換引擎，可在受信任設定啟用 `allowManagedFallback: true`，並於要求
加入 `X-OdfKit-WebFont-Backend: managed`。取消 Sidecar 後，managed 引擎仍會產生 WOFF 或
TrueType；若原要求為 WOFF2，Handler 會自動降級成 WOFF。範例網站的格式驗收會執行
WOFF2 → WOFF → TrueType → WOFF2，每次切換都卸載舊 `FontFace`、要求新 manifest，並確認
實際格式與 PUA 像素。正式環境不需要公開這個診斷開關。

只要 `App_Data/webfonts.dynamic.json` 含有效 `sidecar` 區段，System.Web Handler 就會自動選用
Sidecar，不需要前端開關啟用。測試頁的開關只能作故障演練。VS／IIS Express 本機開發可另外設定
`sidecar.autoStart: true` 與 `sidecar.hostExecutablePath`；Handler 會先做具名 pipe health check，
只有服務不存在時才以隱藏子程序啟動 Host，並透過子程序環境變數傳遞權杖。搭配
`stopWithApplicationProcess: true` 時，Host 會在 IIS Express 結束後退出。

`hostExecutablePath` 指向 `eng/Publish-WebFontSidecar.ps1` 或 GitHub Release 產生的同版本、
同架構 self-contained Host；`OdfKit.WebFonts.Sidecar` NuGet 是具名 pipe client，不包含 NativeAOT
執行檔。正式 IIS、web garden 或多站台環境應保留 `autoStart: false`，使用 Release 內的
`Manage-WebFontSidecarService.ps1` 安裝原生 Windows Service，或由部署平台在網站前啟動 Host，
避免 worker recycle 造成不明確的服務生命週期。完整平台決策、
參數表、帳號／ACL、監控與升級方式見
[`docs/webfont-sidecar-deployment.md`](../../docs/webfont-sidecar-deployment.md)。

Sidecar 無法連線或啟動逾時時，Handler 回傳 `503 Service Unavailable`；Sidecar 工作佇列已滿時
回傳 `429 Too Many Requests` 與 `Retry-After`，不再把部署問題籠統回報成 `500`。

helper 可在不啟用 `unsafe-inline`、`unsafe-eval`、`data:` 或 `blob:` 的嚴格 CSP 下執行。
sample 的 `web.config` 只允許同源外部腳本、連線、樣式與字型，並啟用 Trusted Types
限制；應用程式提供的 callback 也必須放在 CSP 允許的外部腳本中。若字型資產改由 CDN 提供，
必須將明確來源加入 `font-src`，不得放寬成萬用來源。

`document.fonts.check()` 只表示瀏覽器可用某個 family 或 fallback 完成排版，不能證明目標造字
已由該 WebFont 畫出。驗收時應呼叫
`OdfKitWebFontAutoSubset.verifyGlyphRendering(fontFamily, text)`，並儲存預覽區截圖；此 helper
會先要求目標 face 載入，再比較目標字型與 fallback 的 canvas 像素。部署新版
`webfont-autosubset.js` 時，必須同步更新檔名指紋或查詢版本，否則既有頁面可能繼續執行瀏覽器
快取中的舊腳本。

Windows 開發機或 CI 可用官方 CNS 字型執行真實 IIS Express smoke。下列既有閘門以 Ext-B
驗證一般補充平面；PUA 驗收另應以 Plus 字型與 U+FFAE0 執行網站端 WOFF2 測試：

```powershell
pwsh eng/Test-WebFontIisExpressSmoke.ps1 `
  -Pipeline Integrated `
  -FontPath <TW-Sung-Ext-B-98_1.ttf> `
  -SourceSha256 a0ddaf5ba5ea1823e853f82514819cf27e6512ef2865ad562c0bba3e879242a5
pwsh eng/Test-WebFontIisExpressSmoke.ps1 `
  -Pipeline Classic `
  -Destination artifacts/webfont-iis-express-classic-smoke `
  -FontPath <TW-Sung-Ext-B-98_1.ttf> `
  -SourceSha256 a0ddaf5ba5ea1823e853f82514819cf27e6512ef2865ad562c0bba3e879242a5
```

此測試會將隨機 key 寫入隔離站台的 `web.config`、清除環境變數、實際編譯頁面、經 HTTP 動態
產生 TTF／WOFF，並驗證未授權回應、GET／HEAD、內容 SHA-256、ETag 與 304。兩次執行分別以
`Clr4IntegratedAppPool` 與 `Clr4ClassicAppPool` 啟動隔離站台；它們不等同客戶正式 IIS、WAF／CDN
或組織安全設定的驗收。每個 pipeline 另對 WOFF 執行 16 路、256～1,024 次有界 GET 負載，逐一
驗證 SHA-256 並將 CPU 與 initial／peak working set 寫入 `evidence.json`；這不等同長時間 soak。
