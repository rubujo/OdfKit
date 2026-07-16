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

第一個可交付 engine 以 TrueType outline 為界：支援 TTF、TTC 選定 face、Unicode scalar、
Supplementary Plane、PUA、IVS、TTF／WOFF 輸出，並在 `net10.0` 增加 WOFF2。CFF／CFF2、
variable、color／bitmap font 與未完成的 complex shaping 必須明確拒絕，不能刪表或 fallback。

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
140,504 bytes。該案例的 WOFF 比 TTF 小約 71.5%；這是鎖定 corpus 的證據，不是所有字型的固定
壓縮承諾。

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

JSON 本文可能含姓名、PUA 或機關資料，不得放入 URL、metric label 或一般 access log。正式環境
應使用 TLS、短效 token、租戶配額與資料最小化；大量下載交給 CDN／Object Storage。

`OdfWebFontResourceProvider.CreateFontPreloadLink` 只對呼叫端明確指定且已在 manifest 驗證的單一
資產產生 preload。它不會自動 preload 所有 slice，因為 preload 會略過 `unicode-range` 的延遲
選取而可能造成不必要下載。

## ASP.NET Web Forms

Web Forms 的 `net48` 提供 `OdfWebFontDynamicHandler`。它只接受 API key 授權、JSON 本文、精確
face／Profile／font-family／format allowlist，並以非阻塞 semaphore 限制 request-time 產字數；
容量已滿回傳 429，不建立無界 queue。`net48` 使用 managed TrueType engine 產生 TTF／WOFF，
要求 WOFF2、CFF／CFF2、variable 或 color font 會明確失敗。

API key 只能由指定環境變數載入。JSON 設定可放在 `App_Data`，來源字型路徑只由部署端設定，
HTTP 用戶端不能傳入路徑、URL 或 hash。範例設定見
[`samples/WebFonts.WebForms/webfonts.dynamic.example.json`](../samples/WebFonts.WebForms/webfonts.dynamic.example.json)。

```xml
<appSettings>
  <add key="OdfKit.WebFonts.AssetRootPath" value="~/App_Data/OdfWebFonts" />
  <add key="OdfKit.WebFonts.PublicBaseUrl" value="/_odf-fonts" />
  <add key="OdfKit.WebFonts.StylesheetFileName" value="webfonts.css" />
  <add key="OdfKit.WebFonts.DynamicConfigurationPath"
       value="~/App_Data/webfonts.dynamic.json" />
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
| `POST /_odf-fonts/generate` | 不快取；只允許受信任後端；保留 `Content-Type` 與 `X-OdfKit-WebFont-Key`；本文上限 64 KiB；不記錄本文與 key | sequence 可能是個資／PUA，回應依授權與即時 cache 狀態而異 |
| `GET/HEAD /_odf-fonts/{sha256}/{fileName}` | 快取 200；保留 `Cache-Control`、`ETag`、`Content-Type`、CORS、CORP 與 `nosniff`；不得用 BOT HTML challenge 取代字型 | URL 已含內容 hash，可安全長期 immutable cache |
| `GET /_odf-fonts/webfonts.json`、`webfonts.css` | alias 不長期快取；有指紋的 CSS 才可 immutable | alias 內容可能在部署後改變 |
| 401／400／413／429／503 | 不快取 | 避免把授權、限流或暫時失敗擴散到所有使用者 |

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
SHA-256、`fsType`、sfnt 結構與輸出上限。`.tte` 只是 Windows 安裝方式；內容仍必須是目前引擎
支援的 TrueType outline，CFF、variable、color 或損毀檔案照常明確拒絕。EUDC／PUA 的語意不會
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

## 第一方規格依據

- [WebFont 純 .NET 架構契約](webfont-managed-architecture.md)
- [Microsoft OpenType 1.9.1](https://learn.microsoft.com/en-us/typography/opentype/spec/)
- [W3C WOFF 1.0](https://www.w3.org/TR/WOFF/)
- [W3C WOFF 2.0](https://www.w3.org/TR/WOFF2/)
- [W3C Incremental Font Transfer](https://www.w3.org/TR/IFT/)
- [WebFont IFT 標準追蹤與相容性閘門](webfont-ift-tracking.md)
- [W3C CSS Fonts Level 4](https://www.w3.org/TR/css-fonts-4/)
- [Unicode 17.0.0](https://www.unicode.org/versions/Unicode17.0.0/)
- [Unicode Ideographic Variation Database](https://www.unicode.org/ivd/)
- [Microsoft ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
- [Microsoft .NET bounded channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels)
