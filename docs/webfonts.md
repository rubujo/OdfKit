# WebFont 多國罕用字套件

> 目前狀態：純 C#／.NET TrueType 子集引擎、TTF／WOFF／WOFF2、Build、ASP.NET Core 與
> System.Web 動態端點，以及單機 durable Worker 已有可執行實作。官方 CNS Ext-B 真字型已通過 managed verifier、
> Chromium、Firefox 與 WebKit；真實 TTC／IVS／PUA 與不支援格式矩陣亦已進入 CI。完整多國
> complex-script shaping、惡意來源字型 fuzz、跨節點 store
> 與外部安全／客戶驗收仍未完成，因此整套產品仍標示 experimental。權威實作邊界見
> [WebFont 純 .NET 架構契約](webfont-managed-architecture.md)。

OdfKit WebFonts 的主要產品情境，是 ASP.NET Core 與 ASP.NET Web Forms 在執行期遇到 CNS
11643、IVS、PUA 或其它多國 Unicode sequence 時，快速取得只含所需 glyph 的 WebFont。核心
不綁定全字庫；CNS 是可追溯的內建 Profile／mapping provider，自訂 JSON Profile 與 C#
provider 使用同一套中性契約。

## 產品模型

動態內容是主線，預產生是必要的暖機及 fallback：

1. 應用程式把受信任文字轉成 canonical sequence request。
2. 先以來源字型 SHA-256、face、Profile、sequence 與格式計算 canonical hash。
3. durable cache 命中時直接回傳內容定址資產；未命中才送入有界 managed Worker。
4. 相同 hash 以 single-flight 合併，完成資產放入 object storage／CDN。
5. 公開 HTTP 只取得不可變資產；動態 generation endpoint 必須選擇啟用、授權、限流與 allowlist。

首頁、常用介面與已知 corpus 應在 build／publish 預產生。這能降低冷啟動延遲，並在 Worker
故障時維持既有難字顯示。未完成 object store、跨節點協調、租戶配額及失敗接手前，單機動態
Worker 不構成大規模 production 承諾。

## 純 .NET 邊界

產品套件不得啟動 FontTools、Python、Node.js 或其它外部程序，也不得包含 native runtime asset
或在 build／request time 下載工具。乾淨 consumer 只能依賴受支援 .NET SDK／Runtime 與 NuGet
還原內容。

CI 的 clean consumer 會從同一批 `0.0.1` nupkg 安裝 library 與 `OdfKit.WebFonts.Build` dotnet
tool，再以鎖定的 CNS 真字型產生 TTF／WOFF／WOFF2；consumer build 與 run 使用
`--no-restore`，不以 project reference 或開發環境工具代替套件內容。

第一個可交付 engine 支援 TrueType outline、Unicode scalar、
Supplementary Plane、PUA、IVS、TTF／WOFF 輸出，並在 `net10.0` 增加 WOFF2。TrueType
Variable Fonts 的 retain-GIDs／`gvar` 重建，以及 standalone CID-keyed 靜態 CFF 1.0 的
retain-GIDs 路徑目前為 experimental。靜態 CFF OTC face 可依 `faceIndex` 抽出 standalone
OTF／WOFF／WOFF2；含 `fvar`／VariationStore 的 standalone 或 OTC CFF2 variable `OTTO`
亦有 experimental retain-GIDs 路徑。輸入容器另接受 TTC／OTC 指定 face、Windows `.tte`、WOFF，
以及 `net10.0` 由本引擎產生的 null-transform WOFF2；輸出只產生瀏覽器部署用的獨立
TTF／OTF／WOFF／WOFF2，不輸出 collection。名稱式 CFF、無 VariationStore 的 CFF2 與未知
color table 版本必須明確拒絕，不能刪表或 fallback。Arabic／Devanagari 可使用
下述 correctness-first 模式；其它尚未具合法 corpus 與三瀏覽器差分證據的 complex script
不得據此推定為已支援。

設定來源 SHA-256 時，engine 的有界 source cache 同時保留已驗證 bytes 與依 face 解析的 immutable
sfnt 模型；相同來源／face 的後續動態請求不再重新複製所有 table。CFF／CFF2 的 INDEX、DICT、
VariationStore 與 subroutine 結構使用以 table byte array 為生命週期的弱參照快取；來源 cache
淘汰後解析模型可一併回收，不建立跨來源的無界靜態字典。輸出 bytes、選字 closure 與 verifier
仍依每個 canonical request 重新產生及驗證。

WOFF2 的 .NET `BrotliEncoder` API 由 Runtime 提供，但官方 Runtime 原始碼顯示底層使用 native
encoder。因此正確宣稱是「OdfKit 不帶入額外 native 相依」，不是「Brotli 演算法由純 managed
C# 實作」。`net48` 第一階段使用 TTF／WOFF，不為 WOFF2 引入 native 套件。

## 預定最短使用方式

以下介面已由 repository smoke 實際執行；正式發布仍受證據矩陣與人工閘門約束：

```powershell
dotnet tool install OdfKit.WebFonts.Build
odfkit-webfonts build `
  --font Fonts/licensed.ttf `
  --content-root . `
  --content-extensions .cshtml,.razor,.resx,.html,.txt `
  --output wwwroot/_odf-fonts `
  --profile cns11643-euc-tw-2026-05-05 `
  --formats woff2
```

大型 corpus 可選擇固定 Unicode bucket 切片；bucket 邊界由 scalar 數值決定，新增一個字只會使
對應 bucket 的內容 hash 改變，不會推移後續切片。切片並非預設，必須依實際頁面 corpus、資產
數與 cache hit ratio 決定，例如：

```powershell
odfkit-webfonts build `
  --font Fonts/licensed.ttf `
  --content-root . `
  --output wwwroot/_odf-fonts `
  --formats woff2 `
  --slice-size 256 `
  --max-slices 512 `
  --font-display optional
```

產生器會把相鄰 scalar 合併為 canonical `unicode-range` 區間。IVS 的 base scalar 與 variation
selector 保持在同一個 slice。預設只產生 WOFF2；需要舊瀏覽器或 `net48` 部署時可明確要求
WOFF／TTF。WOFF writer 會逐 table 產生 zlib stream，只有壓縮後較小時才採用壓縮內容。

`pwsh eng/Test-WebFontFormatMatrix.ps1` 使用 SHA-256
`eb3f27d9c58e05d23a292e59371fb6afb8d9c5da28d592b18671f1f28d7c8583` 的官方 CNS Ext-B
TrueType 字型與 `A𠆩` 真實子集重現：TTF 1,044,104 bytes、WOFF 297,692 bytes、WOFF2
138,660 bytes。該案例的 WOFF 比 TTF 小約 71.5%；這是鎖定 corpus 的證據，不是所有字型的固定
壓縮承諾。

Arabic／Devanagari 等需要 GSUB／GPOS 的文字會進入 correctness-first 模式：輸出保留來源的完整
glyph ID space、`cmap`、GDEF、GPOS 與 GSUB，不嘗試重寫 layout lookup。這能由瀏覽器維持塑形
正確性，但檔案通常只獲得 WOFF／WOFF2 壓縮效益，不應宣稱是 aggressive subset。實際驗證以
`pwsh eng/Test-WebFontLayoutBrowserSmoke.ps1` 比較來源 TTF 與 managed WOFF2 的逐像素結果；
2026-07-17 的遠端 CI 已在 Chromium、Firefox 與 WebKit 通過六組 Arabic／Devanagari 字串的
RGBA bytes 與文字 metrics 差分。

Color font 採相同 correctness-first 原則：COLR／CPAL、CBDT／CBLC、EBDT／EBLC、`sbix` 與
`SVG ` 先做有界結構驗證，保留完整 glyph ID 空間與 color tables，再縮減外部 `cmap`。鎖定的
Noto Color Emoji v2.047 bitmap-only 與 COLRv1 字型用於 managed 正向矩陣；COLRv1 來源與 managed
WOFF2 已在 Chromium／Firefox／WebKit 通過逐 RGBA byte 差分，且測試要求非灰階像素。CBDT
bitmap-only 可作輸入，但 Firefox WebFont sanitizer 不接受沒有 outline 的來源／輸出，因此不能
宣稱為跨瀏覽器部署格式；其它 color 模型仍須分別補齊合法 corpus，不能以系統 emoji fallback
或黑白 outline 冒充成功。

靜態 CFF 1.0 目前只接受含 ROS／FDArray／FDSelect 的 standalone CID-keyed `OTTO`。有界 parser
會驗證 CFF INDEX、Top DICT、Font DICT、Private DICT、local Subrs、charset 與 FDSelect；未選
glyph 以相同 CharString 長度的合法無 outline 程式取代，因此所有 CFF absolute／relative offset
保持不變，不剪 global／local subroutine。這是 correctness-first retain-GIDs，不是 compact CFF
重寫：鎖定的 Source Han Sans 2.005R 案例來源為 16,528,276 bytes，managed OTF 為
16,297,544 bytes，WOFF 為 1,788,872 bytes，WOFF2 為 1,443,492 bytes。數字只適用該 corpus。
Chromium、Firefox 與 WebKit 亦已對九組 CFF 中文、Arabic 與 Devanagari 字串完成來源／subset
逐像素差分。

CFF2 variable 路徑使用 32-bit INDEX count、Top／Font／Private DICT、FDSelect 0／3／4、
Item Variation Store、`vsindex`、`blend` 與最多十層 subroutine 的有界 parser。未選 glyph 以
等長的零位移 CFF2 CharString 取代，保留 GID、INDEX offsets、`fvar`、`avar`、`STAT`、HVAR 與
其它 variation metadata。Source Han Sans 2.005R `SourceHanSansTW-VF.otf` 的來源為
10,495,320 bytes，managed OTF 為 10,396,064 bytes、WOFF 為 200,728 bytes、WOFF2 為
144,408 bytes；SHA-256 為
`e66bca1da93f068521f3ab10dc7fa0c6691a37c64a0ccfdb6bb3a2ee879deb77`。Chromium、Firefox 與
WebKit 均以 300／500／700 三個 `wght` 座標完成來源／subset DOM 截圖逐 byte 差分；能力仍因
缺少第三方惡意字型稽核與更廣 CFF2 corpus 而維持 experimental。

`font-display` 支援 `auto`、`block`、`swap`、`fallback` 與 `optional`。fallback metrics 必須由
部署者依實際 fallback 字型量測後提供，不能由套件猜測；CLI 的 `--fallback-local`、
`--size-adjust`、`--ascent-override`、`--descent-override` 與 `--line-gap-override` 會產生獨立的
本機 fallback `@font-face`。

ASP.NET Core 的唯讀資產介面維持少量設定：

```csharp
builder.Services.AddOdfWebFonts("wwwroot/_odf-fonts");

WebApplication app = builder.Build();
app.MapOdfWebFonts();
```

動態 endpoint 必須另外註冊 managed engine、具名授權、具名 rate limiter，以及 face、Profile、
format allowlist。用戶端只能傳 sequence 與 allowlist ID，不能傳字型路徑、URL 或來源 hash。
成功結果是 manifest，後續以 `/{sha256}/{fileName}` GET 不可變資產。

### API key 產製與管理

每個環境應使用不同的 32-byte cryptographic random key；不要使用密碼、時間戳或可預測識別碼。
PowerShell 7 可直接以 .NET 產生 Base64 值：

```powershell
$bytes = [Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
$apiKey = [Convert]::ToBase64String($bytes)
```

ASP.NET Core 使用標準鍵 `OdfKit:WebFonts:ApiKey`。`appsettings.json` 可設定 `ApiKey`，而
`OdfKit__WebFonts__ApiKey`、User Secrets 或 Key Vault provider 可依 ASP.NET Core 原生 provider
順序覆寫；`ODFKIT_WEBFONT_API_KEY` 只在標準鍵不存在時作為相容 fallback。Microsoft 明確建議
不要把 production secret 放進可能提交的 `appsettings.json`；Secret Manager 只適合開發，正式
環境應使用受控 secret store。參考
[ASP.NET Core configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0)、
[Safe storage of app secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0)
與 [Azure Key Vault provider](https://learn.microsoft.com/en-us/aspnet/core/security/key-vault-configuration?view=aspnetcore-10.0)。

目前 sample 每次啟動載入單一 key。輪替時先在受信任後端與目標節點部署新 key，再重新啟動；
完成切換後從所有節點與 secret store 撤銷舊值。需要無中斷雙 key overlap、每租戶 key、到期時間
或集中撤銷時，應接入既有身分提供者／API gateway，不應把長效共用 key 發給瀏覽器。access log
必須遮蔽 `X-OdfKit-WebFont-Key`，監控只記錄結果與租戶 opaque ID，不記錄 key 或 request sequence。

JSON 本文可能含姓名、PUA 或機關資料，不得放入 URL、metric label 或一般 access log。正式環境
應使用 TLS、短效 token、租戶配額與資料最小化；大量下載交給 CDN／Object Storage。

`OdfWebFontResourceProvider.CreateFontPreloadLink` 只對呼叫端明確指定且已在 manifest 驗證的單一
資產產生 preload。它不會自動 preload 所有 slice，因為 preload 會略過 `unicode-range` 的延遲
選取而可能造成不必要下載。

## ASP.NET Web Forms

Web Forms 的 `net48` 提供 `OdfWebFontDynamicHandler`。它只接受 API key 授權、JSON 本文、精確
face／Profile／font-family／format allowlist，並以非阻塞 semaphore 限制 request-time 產字數；
容量已滿回傳 429，不建立無界 queue。`net48` 可由 managed engine 產生 TrueType TTF／WOFF，
以及 standalone／OTC face 的 CID-keyed 靜態 CFF 或有 VariationStore 的 CFF2 variable WOFF；
TrueType variable、靜態 CFF、CFF2 variable 與 color font 為 experimental。`net48` 要求 WOFF2、
名稱式 CFF、無 VariationStore 的 CFF2、未知 color table 版本或直接輸出 collection 會明確失敗。

API key 先由 JSON 指定的環境變數載入；若未設定，再讀取 `apiKeyAppSettingName` 指定的
`web.config/appSettings` 鍵，預設為 `OdfKit.WebFonts.ApiKey`。環境變數優先序是明確契約。
直接放在 `web.config` 時必須使用 ASP.NET Protected Configuration 加密 `appSettings`，並限制
解密金鑰與檔案 ACL；Microsoft 說明 Protected Configuration 可由 ASP.NET 在執行期透明解密。
參考 [Protected Configuration](https://learn.microsoft.com/en-us/previous-versions/aspnet/hh8x3tas(v=vs.100))。
JSON 設定可放在 `App_Data`，來源字型路徑只由部署端設定，HTTP 用戶端不能傳入路徑、URL 或
hash。範例設定見
[`samples/WebFonts.WebForms/webfonts.dynamic.example.json`](../samples/WebFonts.WebForms/webfonts.dynamic.example.json)。

```xml
<appSettings>
  <add key="OdfKit.WebFonts.AssetRootPath" value="~/App_Data/OdfWebFonts" />
  <add key="OdfKit.WebFonts.PublicBaseUrl" value="/_odf-fonts" />
  <add key="OdfKit.WebFonts.StylesheetFileName" value="webfonts.css" />
  <add key="OdfKit.WebFonts.DynamicConfigurationPath"
       value="~/App_Data/webfonts.dynamic.json" />
  <add key="OdfKit.WebFonts.ApiKey" value="&lt;protected-deployment-secret&gt;" />
</appSettings>
<system.webServer>
  <handlers>
    <add name="OdfWebFonts" path="_odf-fonts/*" verb="GET,HEAD,POST"
         type="OdfKit.WebFonts.Hosting.SystemWeb.OdfWebFontDynamicHandler, OdfKit.WebFonts.Hosting.SystemWeb"
         resourceType="Unspecified" />
  </handlers>
</system.webServer>
```

受信任後端以 `POST /_odf-fonts/generate` 傳入：

```json
{
  "fontSourceId": "cns-ext-b",
  "faceIndex": 0,
  "profileId": "cns11643-euc-tw-2026-05-05",
  "fontFamily": "OdfKit CNS Ext-B",
  "sequences": ["A𠆩", "邉󠄐"],
  "formats": ["Woff", "TrueType"]
}
```

要求標頭必須包含 `X-OdfKit-WebFont-Key`。成功回傳 manifest，公開頁面只 GET
`/{sha256}/{fileName}`；資產會重新驗證 SHA-256、大小與副檔名，再以 immutable cache 與 ETag
傳送。Handler 重啟後仍可安全讀取內容定址產物，不依賴 process-local registry。
動態 POST 產生的成功與錯誤回應使用 `Cache-Control: no-store, no-cache`；manifest、CSS 與字型
資產皆明確支援 GET／HEAD。System.Web 與 ASP.NET Core 會以實際傳輸 bytes 的 SHA-256 作為
ETag，收到相符的 `If-None-Match` 時回傳無本文的 304。讀取 CSS 時會拒絕無效 UTF-8，而不是
靜默轉碼後讓檔名、manifest hash 與回應內容不一致。

Master Page 加入：

```aspx
<%= OdfKit.WebFonts.Hosting.SystemWeb.OdfWebFontHtml.StylesheetLink() %>
```

`OdfWebFontHandler` 仍提供純唯讀部署；動態 Handler 找不到動態資產時會委派既有 manifest／CSS
資產，因此 CLI／MSBuild 預產生仍是暖機與故障 fallback。System.Web 目前沒有跨節點 durable
lease；多節點必須在外部受控服務產生後部署至共用 object store，不能把單機 semaphore 宣稱為
distributed lock。

## WAF 與 HiNet CDN 部署

可以部署在 WAF／CDN 後方，但必須依路徑分離動態控制平面與公開資料平面。中華電信官方目前
公開說明 CDN 提供快取，並可搭配 WAF、BOT Challenge 與 DDoS 防護；公開產品頁未提供租戶層級
cache key、header pass-through 或 purge API 契約，因此下列規則必須由實際服務設定與 staging
probe 驗收，不能僅依產品名稱推定。參考
[中華電信 CDN 官方產品頁](https://www.cht.com.tw/home/campaign/Hinet/cdn-service-6.html)。

| 路徑 | WAF／CDN 規則 | 原因 |
| --- | --- | --- |
| `POST /_odf-fonts/generate` | 不快取；origin Handler 回應 `no-store, no-cache`；只允許受信任後端；保留 `Content-Type` 與 `X-OdfKit-WebFont-Key`；本文上限 64 KiB；不記錄本文與 key | sequence 可能是個資／PUA，回應依授權與即時 cache 狀態而異 |
| `GET/HEAD /_odf-fonts/{sha256}/{fileName}` | 快取 200；保留 `Cache-Control`、`ETag`、`Content-Type`、CORS、CORP 與 `nosniff`；不得用 BOT HTML challenge 取代字型 | URL 已含內容 hash，可安全長期 immutable cache |
| `GET/HEAD /_odf-fonts/manifest.json`、`webfonts.css` | alias 使用 `no-cache`、強 ETag 與 304 重驗證；有指紋的 CSS 使用一年 `immutable`；CDN 必須保留原始 bytes 與 ETag | alias 內容可能在部署後改變，但不需每次重傳本文 |
| 401／400／413／429／503 | 不快取 | 避免把授權、限流或暫時失敗擴散到所有使用者 |

依 [RFC 9111](https://www.rfc-editor.org/rfc/rfc9111.html)，`no-cache` 允許保存回應，但重新使用前
必須向 origin 驗證；`no-store` 才禁止保存。ASP.NET Core 的
[Minimal API file result](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0#file-results)
在提供 ETag 時會處理 `If-None-Match` 與 304。ASP.NET Core 的 authentication／rate-limiter
middleware 可能在 generation Handler 執行前就回傳 401／429，因此 sample 會在這些 middleware
前對整個 POST 路徑加入 `no-store`；WAF 仍須對該路徑及所有錯誤狀態強制不快取，不能只依成功
Handler 的 header。

若 CDN 使用 `fonts.example.com`，而網站使用另一個 origin，ASP.NET Core 應設定精確
`AllowedOrigins` 與 cross-origin CORP。System.Web 的 JSON 與 `Web.config` 則把
`AllowPublicCrossOriginAssets`／`OdfKit.WebFonts.AllowPublicCrossOriginAssets` 設為 `true`；這只對
公開內容定址 GET 輸出 `Access-Control-Allow-Origin: *` 與 `Cross-Origin-Resource-Policy:
cross-origin`，不會替 generation API 開 CORS。資產不含 cookie 或使用者特定資料，因此不使用
credentialed CORS。

ASP.NET Core 在 proxy 後必須只信任已知 WAF／CDN proxy 的 forwarded headers，且 middleware 要在
HSTS、驗證與限流前執行；Microsoft 明確警告不得信任未知 proxy 提供的 `X-Forwarded-*`，否則可
遭 IP／scheme spoofing。參考
[ASP.NET Core proxy 與 load balancer 指南](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0)。
IIS 亦應把 `requestFiltering/requestLimits/maxAllowedContentLength` 設為不大於 Handler 的本文上限；
超限要求會由 IIS 以 413.1 擋下。參考
[IIS request limits](https://learn.microsoft.com/en-us/iis/configuration/system.webserver/security/requestfiltering/requestlimits/)。

Repository 的 `eng/Test-WebFontIisExpressSmoke.ps1` 會以隔離站台與隨機 localhost port 啟動 IIS
Express，實際編譯 Web Forms 頁面並以官方 CNS Ext-B 執行 401、動態 TTF／WOFF、GET／HEAD、
SHA-256、ETag 與 304。IIS Express 與 IIS 使用相同的 `applicationHost.config`／`Web.config`
設定模型，但 IIS Express 由使用者啟動且沒有 WAS；因此這項證據只涵蓋 Integrated pipeline，
不取代完整 IIS Classic mode 或正式站台驗收。

`eng/Test-WebFontAspNetCoreIisExpressSmoke.ps1` 則使用完整隔離 `applicationhost.config` 與 ANCM
V2，分別發布並實際啟動 In-Process 與 Out-of-Process。前者驗證
`appsettings.{Environment}.json` API key，後者驗證環境變數覆寫；兩者皆執行 401/no-store、
動態 WOFF2、GET／HEAD、SHA-256、ETag 與 304。In-Process 在 `iisexpress.exe` 內執行，
Out-of-Process 由 ANCM 啟動 Kestrel 並代理。參考
[Microsoft IIS Express 概觀](https://learn.microsoft.com/en-us/iis/extensions/introduction-to-iis-express/iis-express-overview)、
[IIS Express 命令列](https://learn.microsoft.com/en-us/iis/extensions/using-iis-express/running-iis-express-from-the-command-line)
、[ASP.NET Core Module](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/aspnet-core-module?view=aspnetcore-10.0)
與 [IIS Integrated／Classic 架構](https://learn.microsoft.com/en-us/iis/get-started/introduction-to-iis/introduction-to-iis-architecture)。

正式切換前至少以外部 probe 驗證：首次 MISS 到 origin、第二次 HIT、ETag 304、三種字型 MIME、
CORS／CORP、401／429 不快取、POST 不快取、WAF 阻擋超限本文，以及 purge 後重新回源。HiNet
租戶若無法針對上述路徑與標頭設定，就只能讓 CDN 承載 immutable GET，generation API 改走不經
CDN 的內部 origin。

## Windows EUDC.TTE

Microsoft 文件說明 TrueType EUDC／PUA 字型可安裝為 `.ttf` 或 `.tte`；`.tte` 會被作業系統隱藏，
GDI 無法用一般字型列舉 API 檢查，關聯資料位於目前使用者的 `HKEY_CURRENT_USER\EUDC\<codePage>`。
參考 [Character Sets and Fonts](https://learn.microsoft.com/en-us/windows/win32/intl/character-sets-and-fonts)
與 [EUDC](https://learn.microsoft.com/en-us/windows/win32/intl/eudc)。

`OdfKit.WebFonts.Windows` 在 `net10.0` 與 `netstandard2.0` 提供
`WindowsEudcFontSourceResolver`，只讀取目前使用者登錄設定，並只接受存在的 `.tte`／`.ttf`
檔案。CLI 可直接指定合法取得的檔案：

```powershell
odfkit-webfonts build --font C:\Windows\Fonts\EUDC.TTE --text pua.txt --output wwwroot\_odf-fonts
```

或在 Windows 上由 code page 的 system default／指定 typeface 關聯解析：

```powershell
odfkit-webfonts build --eudc-code-page 950 --text pua.txt --output wwwroot\_odf-fonts
odfkit-webfonts build --eudc-code-page 950 --eudc-typeface "MingLiU" --text pua.txt --output wwwroot\_odf-fonts
```

解析器不寫入登錄、不接受來自 HTTP request 的 code page、typeface 或路徑，也不繞過來源
SHA-256、`fsType`、sfnt 結構與輸出上限。`.tte` 只是 Windows 安裝方式；目前 EUDC resolver
只接受 `.tte`／`.ttf` 路徑及 TrueType outline，color、PostScript outline 或損毀檔案照常
明確拒絕。
TrueType Variable Fonts 仍須通過 experimental 閘門。EUDC／PUA 的語意不會
自動跨電腦保存，部署者必須提供版本化 mapping、字型來源 SHA-256、授權與資料治理；使用者個人
EUDC 字型不得在未授權時散布或上傳 CDN。

## 全字庫 CNS 11643 Profile

`OdfKit.WebFonts.Profiles` 提供可追溯的 EUC-TW provider 與資料身分，不把全字庫字型或完整
第三方資料塞入 nupkg。目前鎖定的 Profile 為 `cns11643-euc-tw-2026-05-05`，對應官方
`MapingTables.zip`，SHA-256：

```text
f59dacc4dbdef334d7a887c3da671af02778e2c80adb2a7fd1053f64dbf9e659
```

字型須由部署者依授權合法取得並設定 `FontSourceId`、路徑與 SHA-256。引擎在子集化前必須檢查
`OS/2.fsType`，拒絕禁止 embedding、禁止 subsetting 或 bitmap-only 的來源。全字庫資料要求的
來源標示、OFL 字型的著作權與授權全文仍由實際散布方式決定，不因使用 OdfKit 而消失。

CI 首次只從政府資料開放平臺列出的全字庫官方端點取得宋體封存檔，並以鎖定的封存檔
SHA-256 驗證。驗證成功後可存入 GitHub Actions cache，供相同 hash 的後續執行重用；cache
內容每次仍會重新驗證，且不會進入 nupkg。冷 cache 遇到官方端點不可達時會明確失敗，不會
切換至未追溯的第三方鏡像或略過真實 CNS 測試。

自訂 JSON Profile 必須含版本、來源、SHA-256、授權與 attribution：

```json
{
  "schemaVersion": 1,
  "profileId": "agency-eudc-2026.07",
  "dataVersion": "2026.07",
  "sourceUri": "file:///deployment/profiles/agency-eudc.json",
  "sourceSha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
  "licenseId": "LicenseRef-Agency-EUDC",
  "attribution": "機關自造字對照表 2026.07。",
  "mappings": {
    "8140": "𠀀",
    "8EA140": "󰀁"
  }
}
```

C# provider 實作 `ICharacterMappingProvider`；需要完整稽核資料時實作
`ITraceableCharacterMappingProvider`。缺字、衝突或未對應 bytes 一律失敗，不改猜 Big5、
不替換成 `?`，也不靜默 fallback。

## ORM 與資料庫

ORM 只決定資料取得方式，不改變 WebFont 管線：`nchar`／`nvarchar` 以 .NET `string` 建立
`WebFontTextSequence`；保留 Big5／Big5E 原始資料時，以 `varbinary`／`byte[]` 交給明確 mapping
provider。資料若已被 code page 轉成 `?` 或亂碼，字型套件無法事後還原。

## CSP、Cache 與安全

同源建議使用 `font-src 'self'`；CDN 部署只加入精確 HTTPS origin。套件不放寬 CSP、不反射任意
`Origin`，也不對 generation API 啟用 CORS。只有不含 cookie 或使用者特定資料的公開內容定址
字型資產可選擇輸出萬用字元 CORS。內容指紋資產使用一年 `immutable`、SHA-256 ETag、正確 MIME
與 `nosniff`；穩定 manifest／CSS alias 不得標成不可變。

來源字型只由部署端 allowlist 解析；parser 與 Worker 必須有 bytes、table、glyph、composite
depth、sequence、產出、queue、timeout 與 concurrency 上限。GitHub Actions 可以證明有限併發與
可重現錯誤處理，不能代替真實 CDN、WAF、跨區容量或第三方惡意字型安全審查。

## 狀態與證據

目前可相信的完成度、不能宣稱的能力與升級條件見
[WebFont 證據矩陣](webfont-evidence-matrix.md)。產品與 smoke 產字路徑均不使用 FontTools、
Python、Node 或外部字型程序；Playwright 只作瀏覽器 oracle。

NuGet pack 後執行 `pwsh eng/Test-WebFontSupplyChain.ps1`，會以所有 WebFont 專案的
`project.assets.json` 驗證完整相依版本與 nuspec 授權宣告，並為同批 `0.0.1` nupkg 產生
SPDX 2.3 JSON。CI consumer 使用 `-VerifyExisting` 重新計算套件 SHA-256 與 SBOM；任何新增、
移除、版本或授權中繼資料漂移都會失敗，必須人工更新並審查政策檔，不能自動接受。

`pwsh eng/Test-WebFontReleaseRehearsal.ps1` 會把同批 nupkg 實際 `push` 至隔離本機 feed，強制
`OdfKit*` 只從該 feed 還原，並由乾淨 net10 consumer 與 CLI 執行。外部 package source mapping
由同批 SBOM 精確產生；NuGet Audit 以 `all` 模式查詢 nuget.org 的獨立漏洞資料 endpoint，
moderate 以上 advisory、audit 來源失效或通訊失敗都會使 CI 失敗。演練輸出 commit、套件數、
雜湊與 audit policy JSON，但不把「目前沒有已知 advisory」誤寫成第三方安全保證。

## 第一方規格依據

- [WebFont 純 .NET 架構契約](webfont-managed-architecture.md)
- [Microsoft OpenType 1.9.1](https://learn.microsoft.com/en-us/typography/opentype/spec/)
- [W3C WOFF 1.0](https://www.w3.org/TR/WOFF/)
- [W3C WOFF 2.0](https://www.w3.org/TR/WOFF2/)
- [Microsoft OpenType 1.9.1 color tables](https://learn.microsoft.com/en-us/typography/opentype/spec/otff)
- [Microsoft COLR](https://learn.microsoft.com/en-us/typography/opentype/spec/colr)
- [Microsoft CPAL](https://learn.microsoft.com/en-us/typography/opentype/spec/cpal)
- [W3C Incremental Font Transfer](https://www.w3.org/TR/IFT/)
- [WebFont IFT 標準追蹤與相容性閘門](webfont-ift-tracking.md)
- [W3C CSS Fonts Level 4](https://www.w3.org/TR/css-fonts-4/)
- [Unicode 17.0.0](https://www.unicode.org/versions/Unicode17.0.0/)
- [Unicode Ideographic Variation Database](https://www.unicode.org/ivd/)
- [Microsoft ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
- [Microsoft .NET bounded channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels)
- [NuGet `.nuspec` license metadata](https://learn.microsoft.com/en-us/nuget/reference/nuspec#license)
- [NuGet 本機 feed](https://learn.microsoft.com/en-us/nuget/hosting-packages/local-feeds)
- [NuGet 套件漏洞稽核](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages)
- [SPDX 2.3 specification](https://spdx.github.io/spdx-spec/v2.3/)
- [GitHub artifact attestations](https://docs.github.com/en/actions/concepts/security/artifact-attestations)
