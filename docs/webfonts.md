# WebFont 多國罕用字套件

OdfKit WebFonts 將受信任字型與文字 corpus 預先轉成內容定址的 WOFF2／WOFF／TTF／OTF
資產。正式預設是建置期產生與唯讀傳送；HTTP 要求不執行 FontTools，也不自動上傳頁面文字。
這項邊界同時改善 CSP、隱私、效能與 CDN cache hit ratio。

## 最短使用方式

先安裝 Build Tool 與 ASP.NET Core Hosting 套件，並確認來源字型授權允許子集化及 Web 散布：

```powershell
dotnet tool install OdfKit.WebFonts.Build
odfkit-webfonts build `
  --font Fonts/licensed.ttf `
  --content-root . `
  --content-extensions .cshtml,.razor,.resx,.html,.txt `
  --output wwwroot/_odf-fonts `
  --profile organization-v1 `
  --formats woff2
```

ASP.NET Core：

```csharp
builder.Services.AddOdfWebFonts("wwwroot/_odf-fonts");

WebApplication app = builder.Build();
app.MapOdfWebFonts();
```

需要 CDN 時只增加公開基底 URL 與精確 CORS allowlist：

```csharp
builder.Services.AddOdfWebFonts(options =>
{
    options.AssetRootPath = "wwwroot/_odf-fonts";
    options.PublicBaseUrl = "https://fonts.example.com/odf";
    options.AllowedOrigins.Add("https://app.example.com");
    options.CrossOriginResourcePolicy = OdfWebFontCrossOriginPolicy.CrossOrigin;
});
```

`OdfWebFontResourceProvider` 會提供內容指紋 CSS URL、HTML link 與 CSP 來源，無須行內
JavaScript 或行內 CSS。

## 自動內容收集

`--content-root` 會依確定性路徑順序掃描指定副檔名，略過 `bin`、`obj`、`.git` 與
`node_modules`。掃描內容只在受信任的 build／publish 環境處理；不會在瀏覽器將 DOM、姓名或
PUA 文字回傳伺服器。CLI 會去除重複 Unicode 純量值，並套用 corpus bytes 與唯一純量值硬上限。

NuGet 的 `buildTransitive` target 也能在 publish 前自動執行：

```xml
<PropertyGroup>
  <OdfKitWebFontsEnabled>true</OdfKitWebFontsEnabled>
  <OdfKitWebFontsFontPath>$(MSBuildProjectDirectory)\Fonts\licensed.ttf</OdfKitWebFontsFontPath>
  <OdfKitWebFontsContentRoot>$(MSBuildProjectDirectory)</OdfKitWebFontsContentRoot>
  <OdfKitWebFontsProfile>organization-v1</OdfKitWebFontsProfile>
  <OdfKitWebFontsFormats>woff2</OdfKitWebFontsFormats>
</PropertyGroup>
```

執行期新資料應由應用程式明確送到有界背景工作，不應攔截完整 HTML response 或掃描瀏覽器
DOM。大量使用者應共用國家、組織或租戶 Profile 字型分片，避免 per-user 字型破壞 CDN cache。

## ASP.NET Web Forms

Web Forms 使用 `net48` Handler，並維持唯讀資產模式：

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

Web Forms 不在 IIS worker process 內執行字型子集化。

## SQL、Dapper、EF 與其他 ORM

ORM 只決定資料如何取得，不改變 WebFont 管線：

- `nchar`／`nvarchar` 對應 .NET `string`，直接建立 `WebFontTextSequence`。
- 保留 Big5／Big5E 原始資料時使用 `varbinary` 對應 `byte[]`，再以明確 mapping provider 解碼。
- `varchar`／`text` 若已因 code page 轉成 `?` 或亂碼，字型套件無法事後還原。

Dapper：

```csharp
string text = connection.QuerySingle<string>(
    "SELECT RareText FROM Documents WHERE Id = @id",
    new { id });
WebFontTextSequence sequence = WebFontTextSequence.Create(text);
```

EF Core：

```csharp
string[] values = await db.People
    .Select(person => person.Name)
    .ToArrayAsync(cancellationToken);
WebFontTextSequence[] sequences = values
    .Select(WebFontTextSequence.Create)
    .ToArray();
```

ADO.NET legacy bytes：

```csharp
byte[] bytes = SqlServerWebFontTextReader.ReadLegacyBytes(reader, ordinal, 1_048_576);
string text = new Big5CharacterMappingProvider().Decode(bytes);
```

相同原則適用於 RepoDb、NPoco、PetaPoco 與 LINQ to SQL。

## CSP、CORS 與 HTTP Cache

同源部署建議：

```http
Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self'; font-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'
```

CDN 部署在 `style-src` 與 `font-src` 加入精確 HTTPS origin。套件不會自動寫入或放寬網站的
CSP。跨來源資產只對 `AllowedOrigins` 內的精確 origin 輸出
`Access-Control-Allow-Origin`；不反射任意 `Origin`，也不使用萬用字元。

字型與內容指紋 CSS 使用一年 `immutable`、SHA-256 ETag、正確 MIME 與 `nosniff`。穩定
manifest／`webfonts.css` alias 則不得當成不可變資產。WOFF2 已壓縮，不應再套 HTTP Brotli。

## 安全與效能邊界

- 來源字型只由 `FontSourceId` 對應到部署端 allowlist，不接受任意 URL 或要求參數路徑。
- FontTools 使用 `ProcessStartInfo.ArgumentList`，不經 shell；具備來源、輸出、純量值與逾時上限。
- 目前產品 engine 在啟動 FontTools 前明確拒絕 CFF2、COLR／CPAL、CBDT／CBLC、`sbix` 與
  OpenType SVG table；這些能力尚未取得跨瀏覽器與 closure 證據，不會靜默刪表後宣稱成功。
- 取消或逾時會終止完整子行程樹。
- Worker 使用有界 Channel、立即拒絕滿載佇列，並對相同 canonical request 執行 single-flight。
- manifest 只接受純檔名、SHA-256、已知格式與有界集合；啟動時驗證實際 bytes 與 hash。
- 正式資料平面應是 CDN／Object Storage。ASP.NET endpoint 適合開發、小型部署及 origin fallback，
  不作為十萬級流量的單機承諾。

GitHub Actions 可持續驗證單元、封裝、真實字型、瀏覽器與有限併發 smoke；跨區十萬人容量、
WAF 與供應商 SLA 必須在採用者的 staging／CDN 帳號驗收，不應由 GitHub runner 假裝證明。

## 多國與 PUA

核心以 Unicode sequence、字型 face 與版本化 Profile 表達，不內建「所有 PUA 都相同」的假設。
同一 PUA 碼位在不同機關可代表不同字形，因此 cache key 與 manifest 必須包含 Profile 版本。
目前 corpus 涵蓋 Arabic、Devanagari、香港 TTC／CFF、日本 IVS、CNS Plane 15 PUA，以及
Unicode Plane 0～3；新增國家或機構資料應透過 `ICharacterMappingProvider` 或 JSON Profile。

### 全字庫 CNS 11643 Profile

`OdfKit.WebFonts.Profiles` 內建的是可追溯的 EUC-TW provider 與資料身分，不把全字庫字型或
完整對照表塞進 nupkg。目前已驗證的 Profile 為 `cns11643-euc-tw-2026-05-05`，對應官方
`MapingTables.zip`，SHA-256 為
`f59dacc4dbdef334d7a887c3da671af02778e2c80adb2a7fd1053f64dbf9e659`。取得流程：

```powershell
$root = pwsh eng/Install-Cns11643MappingTables.ps1 `
  -DestinationRoot artifacts/cns11643
odfkit-webfonts build `
  --font C:\CNSFonts\TW-Sung-98_1.ttf `
  --text App_Data\legacy-euc-tw.bin `
  --encoding euc-tw `
  --cns-mapping-archive artifacts\cns11643\MapingTables.zip `
  --profile cns11643-euc-tw-2026-05-05 `
  --output wwwroot\_odf-fonts
```

CLI 會再次驗證官方封存檔 SHA-256，拒絕未對應 EUC-TW bytes、衝突 mapping、Profile 版本不符
與損毀資料。字型須由部署者依授權自行取得；全字庫官方授權要求來源標示，若選擇 OFL-1.1
散布修改後字型，還須隨附著作權聲明與 OFL 全文。

自訂 JSON Profile 必須包含來源與授權追溯欄位：

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

程式碼 provider 只需實作 `ICharacterMappingProvider`；需要把版本、來源、SHA-256、授權與顯名
寫入稽核資料時，實作 `ITraceableCharacterMappingProvider`。缺字、衝突或未對應 bytes 一律
失敗，不會改猜 Big5、替換成 `?` 或靜默 fallback。

各 Phase 的自動證據與保留閘門見
[WebFont 證據矩陣](webfont-evidence-matrix.md)。

## 第一方規格依據

- [W3C WOFF 2.0](https://www.w3.org/TR/WOFF2/)
- [W3C CSS Fonts Level 4](https://www.w3.org/TR/css-fonts-4/)
- [W3C CSS Font Loading Level 3](https://www.w3.org/TR/css-font-loading/)
- [W3C Content Security Policy Level 3](https://www.w3.org/TR/CSP3/)
- [Unicode 17 Private-Use Characters](https://www.unicode.org/versions/Unicode17.0.0/core-spec/chapter-23/)
- [Microsoft SQL Server nchar／nvarchar](https://learn.microsoft.com/en-us/sql/t-sql/data-types/nchar-and-nvarchar-transact-sql?view=sql-server-ver17)
- [Microsoft ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
- [Microsoft Azure Front Door 與 Blob Storage](https://learn.microsoft.com/en-us/azure/frontdoor/scenario-storage-blobs)
- [FontTools subset](https://fonttools.readthedocs.io/en/latest/subset/)
- [Playwright .NET Continuous Integration](https://playwright.dev/dotnet/docs/ci)
- [GitHub Actions workflow artifacts](https://docs.github.com/en/actions/concepts/workflows-and-actions/workflow-artifacts)

## 可執行驗證

```powershell
dotnet test tests/OdfKit.WebFonts.Tests/OdfKit.WebFonts.Tests.csproj -c Release -f net10.0
dotnet run --project tests/OdfKit.WebFonts.SystemWebSmoke/OdfKit.WebFonts.SystemWebSmoke.csproj -c Release
pwsh eng/Test-WebFontSmoke.ps1 -RunBrowser
pwsh eng/Test-NuGetPack.ps1
```

真實字型 smoke 使用鎖定版本與 SHA-256，不把第三方字型提交到 repository。GitHub Actions
會安裝 Playwright Chromium、Firefox 與 WebKit，驗證六組多國案例並上傳完整頁面截圖；單元測試另驗證 1,000 個
同鍵工作只執行一次、滿載佇列立即拒絕、256 個並行靜態資產要求，以及損毀資產啟動失敗。
