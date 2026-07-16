# WebFont 多國罕用字套件

> 目前狀態：純 C#／.NET TrueType 子集引擎、TTF／WOFF／WOFF2、Build、ASP.NET Core 動態端點
> 與單機 durable Worker 已有可執行實作。官方 CNS Ext-B 真字型已通過 managed verifier、
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

## ASP.NET Web Forms

Web Forms 的 `net48` Handler 維持唯讀資產模式；managed Phase 2 的 CLI／MSBuild 在部署前產生
TTF／WOFF，IIS worker process 不同步子集化：

```xml
<appSettings>
  <add key="OdfKit.WebFonts.AssetRootPath" value="~/App_Data/OdfWebFonts" />
  <add key="OdfKit.WebFonts.PublicBaseUrl" value="https://fonts.example.com/odf" />
  <add key="OdfKit.WebFonts.StylesheetFileName" value="webfonts.css" />
</appSettings>
<system.webServer>
  <handlers>
    <add name="OdfWebFonts" path="_odf-fonts/*" verb="GET,HEAD"
         type="OdfKit.WebFonts.Hosting.SystemWeb.OdfWebFontHandler, OdfKit.WebFonts.Hosting.SystemWeb"
         resourceType="Unspecified" />
  </handlers>
</system.webServer>
```

Master Page 加入：

```aspx
<%= OdfKit.WebFonts.Hosting.SystemWeb.OdfWebFontHtml.StylesheetLink() %>
```

若 Web Forms 也需要即時新文字，應由受控後端服務或排程呼叫同一 managed engine，再將完成資產
部署到共用 store；不得在每個頁面要求內無界產字。

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
`Origin`，也不使用萬用字元 CORS。內容指紋資產使用一年 `immutable`、SHA-256 ETag、正確 MIME
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
- [W3C CSS Fonts Level 4](https://www.w3.org/TR/css-fonts-4/)
- [Unicode 17.0.0](https://www.unicode.org/versions/Unicode17.0.0/)
- [Unicode Ideographic Variation Database](https://www.unicode.org/ivd/)
- [Microsoft ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
- [Microsoft .NET bounded channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels)
