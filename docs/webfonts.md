# WebFont 多國罕用字套件

> 目前狀態：純 C#／.NET 子集引擎、TTF／OTF／WOFF／WOFF2、Build、ASP.NET Core 與
> System.Web 動態端點，以及單機 durable Worker 已有可執行實作。官方 CNS Ext-B、TTC／OTC、
> IVS／PUA、CFF／CFF2、Arabic／Devanagari／Bengali／Khmer／Thai 與有界 color corpus 已進入
> managed verifier 及瀏覽器矩陣。這是有界格式契約，不代表任意歷史字型、所有 layout engine、
> 跨 color 技術轉碼或 production 部署均已獲證。權威實作邊界見
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
Variable Fonts 的 retain-GIDs／`gvar` 重建，以及 standalone CID-keyed／名稱式靜態 CFF 1.0 的
retain-GIDs 路徑已有鎖定 corpus。靜態 CFF OTC face 可依 `faceIndex` 抽出 standalone
OTF／WOFF／WOFF2；含 `fvar`／VariationStore 的 standalone 或 OTC CFF2 variable `OTTO`，以及
依規格省略 VariationStore 且不使用 `vsindex`／`blend` 的非變動 CFF2，亦有真實 corpus 的
retain-GIDs 路徑。輸入容器另接受 TTC／OTC 指定 face、Windows `.tte`、WOFF，
以及 `net10.0` standalone WOFF2；WOFF2 decoder 會有界重建標準 `glyf`／`loca` version 0 與
`hmtx` version 1 transform，也接受規格合法的 null transform。`net10.0` 另以 experimental
路徑接受 WOFF2 collection 的指定 face，驗證 collection directory、table index 與共享
transformed `glyf`／`loca` 配對後，才正規化成獨立 sfnt。輸出只產生瀏覽器部署用的獨立
TTF／OTF／WOFF／WOFF2，不輸出 collection。名稱式 CFF 的 `seac` 會依 StandardEncoding 與
charset 保留 base／accent 元件；找不到元件、巢狀組字、缺少 VariationStore 卻使用
`vsindex`／`blend` 的 CFF2 與未知 color table 版本必須明確拒絕，不能刪表或 fallback。
Arabic／Devanagari／Bengali／Khmer／Thai 可使用下述 correctness-first 模式；其它尚未具合法
corpus 與三瀏覽器差分證據的 complex script
不得據此推定為已支援。

設定來源 SHA-256 時，engine 的有界 source cache 同時保留已驗證 bytes 與依 face 解析的 immutable
sfnt 模型；相同來源／face 的後續動態請求不再重新複製所有 table。CFF／CFF2 的 INDEX、DICT、
VariationStore 與 subroutine 結構使用以 table byte array 為生命週期的弱參照快取；CFF 快取
另核對 glyph count，CFF2 快取另核對 glyph count 與 variation axis context，避免同一 bytes 在
不同 face metadata 下誤用解析結果。來源 cache 淘汰後解析模型可一併回收，不建立跨來源的
無界靜態字典。輸出 bytes、選字 closure 與 verifier
仍依每個 canonical request 重新產生及驗證。

WOFF2 的 .NET `BrotliEncoder` API 由 Runtime 提供，但官方 Runtime 原始碼顯示底層使用 native
encoder。因此正確宣稱是「OdfKit 不帶入額外 native 相依」，不是「Brotli 演算法由純 managed
C# 實作」。`net48` 第一階段使用 TTF／WOFF，不為 WOFF2 引入 native 套件。

transformed WOFF2 輸入的 table 反轉換本身是 OdfKit 依 W3C WOFF2 規格 clean-room 撰寫的
純 C#：包含 triplet 座標、simple／composite glyph、bbox、instructions、short／long `loca`
與 `hmtx` bearing 重建。Brotli bitstream 仍由 .NET Runtime API 解壓。CI 會下載 SHA-256 鎖定的
W3C decoder corpus，以及 Google Fonts production Noto Sans v42 Latin／Devanagari WOFF2；檔案
只進 cache／artifact，不進 repository 或 nupkg。WOFF2 collection 以官方 CNS 宋體 Ext-B／PUA
真實 face 建立 null-transform collection，分別選取兩個 face 後產生獨立 TTF／WOFF／WOFF2。
另以 W3C 鎖版 DSIG 移除與 face-order corpus 驗證兩個 transformed collection、每個 3 face、
`glyf`／`loca` v0、`hmtx` v1、非重建表與官方 TTC reference 逐 byte 一致、重建表結構有效及
越界 face 拒絕。瀏覽器端直接
collection 部署不在產品輸出契約內，不列為未完成能力。

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
壓縮承諾。相同矩陣另以 [Adobe Source Code Pro](https://github.com/adobe-fonts/source-code-pro/releases/tag/2.042R-u/1.062R-i/1.026R-vf)
`2.042R-u/1.062R-i/1.026R-vf` 的 [OFL-1.1](https://github.com/adobe-fonts/source-code-pro/blob/2.042R-u/1.062R-i/1.026R-vf/LICENSE.md) 官方 release 驗證名稱式 CFF；ZIP 與 OTF 分別鎖定
SHA-256 `754a2e3ebb945ae905d720ac5896b3b34acc9546dd6551ef9536869788629dae` 與
`9f9664e2edf6f045c11e774f9bd0be6993971f2544a39061a5ce478b96b051f8`，字型不進入 nupkg。
Apache-2.0 的 Adobe AFDKO commit `a843a0a87d9db0ea62d5ce719900acf5749c143e` 另提供
真實名稱式 `seac.otf` 與非變動 `regular_CFF2.otf`；兩者分別鎖定 SHA-256
`b7aba7ad260e62794e57563726c377d5140253679f62bd97152d52b47c744daa` 與
`e607fdc99e3386e3818ce3ee6d6e7218fd911370c25501dd9ad6c17cf40e72da`，只下載至 CI cache。

Arabic／Devanagari／Bengali／Khmer／Thai 等需要 GSUB／GPOS 的文字會進入 correctness-first
模式：輸出保留來源的完整
glyph ID space、`cmap`、GDEF、GPOS 與 GSUB，不嘗試重寫 layout lookup。這能由瀏覽器維持塑形
正確性，但檔案通常只獲得 WOFF／WOFF2 壓縮效益，不應宣稱是 aggressive subset。實際驗證以
`pwsh eng/Test-WebFontLayoutBrowserSmoke.ps1` 比較來源 TTF 與 managed WOFF2 的逐像素結果；
鎖定矩陣已在 Chromium、Firefox 與 WebKit 比較 Arabic／Devanagari／Bengali／Khmer／Thai
來源與 managed WOFF2 的 RGBA bytes、文字 metrics 及 variable axis DOM 結果。

Color font 採相同 correctness-first 原則：COLR／CPAL、CBDT／CBLC、EBDT／EBLC、`sbix` 與
`SVG ` 先做有界結構驗證，保留 glyph ID 編號與 color tables，再縮減外部 `cmap`。COLRv0 layer、
COLRv1 全部 32 種 paint、layer list、`PaintGlyph`／`PaintColrGlyph` DAG 與 `sbix dupe` 會建立
實際 outline closure；循環、超深 graph、未知 paint、非法 palette／clip／offset 與 `sbix` 類型
會明確拒絕。SVG document 本身不跨 glyph 引用 outline；bitmap location table 也不建立跨 glyph
outline 關係，因此只保留使用者要求的 fallback outline。鎖定的 Noto Color Emoji v2.047
bitmap-only 與 COLRv1 字型用於 managed 正向矩陣；COLRv1 來源與 managed
WOFF2 已在 Chromium／Firefox／WebKit 通過逐 RGBA byte 差分，且測試要求非灰階像素。CBDT
bitmap-only 可作輸入，但 Firefox WebFont sanitizer 不接受沒有 outline 的來源／輸出，因此不能
宣稱為跨瀏覽器部署格式。Google Color Fonts commit
`0046ea4c3b69e9fbbe464c2594816894e3aa5e4b` 的 Apache-2.0 `samples-sbix.ttf` 與
`samples-picosvg.ttf` 另以 SHA-256 鎖定；兩者都通過 deterministic TTF／WOFF／WOFF2
managed verifier。瀏覽器差分只執行該引擎能實際產生彩色像素的模型：Chromium 驗證 `sbix`，
Firefox 驗證 OpenType SVG，COLRv1 仍由三者驗證；不渲染的組合記錄為
`browser-unavailable`，不能以兩張相同空白畫布冒充成功。這是保留 color table 的
correctness-first 能力，不是把 `sbix`／SVG 轉換成 COLR 或 outline 的跨格式轉碼器。
EBDT／EBLC 與 `SVG ` 目前也不是細粒度子集編譯器：前者不逐 strike／glyph 重編 bitmap，後者
不裁切 document index；兩者保留完整 color table，只縮減對外 `cmap` 與可證明安全的 fallback
outline。這項限制是公開契約，不得以「支援 color font」掩蓋。
EBDT／EBLC 目前只有產生式結構與越界測試，尚無可再散布的真實瀏覽器 corpus，因此不列入
任何 `RequiredBrowserTargets` 相容集合。

需要嚴格部署相容性時，呼叫端可設定 `RequiredBrowserTargets`。目前鎖定的 Playwright
實證矩陣允許 Chromium 的 COLR v0／v1 與 `sbix`、Firefox 的 COLR v0／v1 與 OpenType
SVG，以及 WebKit 的 COLR v0／v1；只要保留的 color 技術不在任一必要目標的已驗證集合內，
引擎就會在寫出資產前拋出 `NotSupportedException`，不會靜默 fallback。空集合維持既有
correctness-first 行為，代表呼叫端自行承擔瀏覽器選擇；Playwright WebKit 證據不等同 Safari
實機證據。

```csharp
var request = new WebFontSubsetRequest
{
    // 省略 face、profile、family、sequence 與 format 設定。
    RequiredBrowserTargets =
    [
        WebFontBrowserTarget.Chromium,
        WebFontBrowserTarget.Firefox,
        WebFontBrowserTarget.WebKit
    ]
};
```

CLI 使用 `--browser-targets chromium,firefox,webkit`；MSBuild 使用
`<OdfKitWebFontsBrowserTargets>chromium,firefox,webkit</OdfKitWebFontsBrowserTargets>`。
ASP.NET Core 與 System.Web 的 generation JSON 使用 `requiredBrowserTargets` 字串 enum 陣列。
這項 API 是產生前的嚴格相容性閘門，不是 color table 轉碼功能。

靜態 CFF 1.0 接受含 ROS／FDArray／FDSelect 的 CID-keyed `OTTO`，以及不含 ROS／FDArray／
FDSelect 的名稱式 CFF。名稱式路徑接受三種預定義 charset 或有界自訂 charset，Private DICT
可省略；存在時仍驗證 local Subrs。名稱式 `seac` 會從 Type 2 `endchar` 取得 bchar／achar，依
StandardEncoding SID 反查 charset GID 並保留兩個元件；代碼非 0～255 整數、元件不存在或元件
本身再使用 `seac` 時明確拒絕，不能遺失元件後繼續。
有界 parser 會驗證 CFF INDEX、Top DICT、Font DICT、Private DICT、local Subrs、charset 與
FDSelect；未選
glyph 會縮成單一 `endchar`，再以兩趟 relocation 重建 CharStrings／Top DICT／Font DICT／Private
DICT 與 local Subrs 相對 offset；GID、charset、FDSelect 與 subroutine bytes 保持不變。鎖定的
Source Han Sans 2.005R 案例來源為 16,528,276 bytes，managed OTF 為 2,312,096 bytes、WOFF 為
1,565,276 bytes、WOFF2 為 1,170,684 bytes；Source Code Pro 來源 OTF 為 131,128 bytes，managed
OTF 為 63,368 bytes、WOFF 為 40,404 bytes、WOFF2 為 31,496 bytes。數字只適用該 corpus。
Chromium、Firefox 與 WebKit 亦會對九組 CID-keyed CFF 中文、Arabic 與 Devanagari 字串，以及
三組名稱式 CFF Latin 字串完成來源／subset 逐像素差分。名稱式路徑另有最小結構 fixture、
retain-GIDs、cache-context 負向測試、ISOAdobe／Expert／ExpertSubset／自訂 charset 的 `seac`
closure、巢狀／缺漏元件拒絕及 64 組固定種子來源 mutation；AFDKO 的真實 `seac` corpus 另在
Chromium、Firefox 與 WebKit 通過來源／subset 差分。第三方安全審查只屬採用者額外證據，
不阻擋工程完成。

CFF2 variable 路徑使用 32-bit INDEX count、Top／Font／Private DICT、FDSelect 0／3／4、
Item Variation Store、`vsindex`、`blend` 與最多十層 subroutine 的有界 parser。未選 glyph 縮為
規格允許的零長度 CharString，再以兩趟 relocation 重建 Top／Font／Private DICT、32-bit INDEX、
Header Top DICT length 與 local Subrs 相對 offset；GID、VariationStore、subroutine bytes、
`fvar`、`avar`、`STAT`、HVAR 與其它 variation metadata 保持不變。Source Han Sans 2.005R
`SourceHanSansTW-VF.otf` 的來源為 10,495,320 bytes，managed OTF 為 343,400 bytes、WOFF 為
72,324 bytes、WOFF2 為 54,736 bytes；來源 SHA-256 為
`e66bca1da93f068521f3ab10dc7fa0c6691a37c64a0ccfdb6bb3a2ee879deb77`。Chromium、Firefox 與
WebKit 均以 300／500／700 三個 `wght` 座標完成來源／subset DOM 截圖逐 byte 差分；AFDKO 的
非變動 CFF2 corpus 另以三瀏覽器驗證靜態路徑。第三方惡意字型稽核只屬採用者額外證據。

OpenType 1.9.1 明定不支援 Font Variations 的 CFF2 必須省略 VariationStore，因此 parser 也接受
省略 VariationStore 且不使用 `vsindex`／`blend` 的非變動 CFF2；`fvar` 可依字型其它變動資料
存在或省略，但存在時仍須通過結構驗證。這條路徑已用依官方
CFF2 結構建立的有界二進位 fixture 驗證 INDEX、DICT、CharString、retain-GIDs 與錯誤拒絕，並
修正解析快取，使不同 glyph count／variation axis context 不會共用結果；AFDKO
`regular_CFF2.otf` 補上可再散布的真實靜態 CFF2 與三瀏覽器證據。這仍不代表任意 CFF2 operator
或未列入矩陣的字型皆受支援。

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
以及 standalone／OTC face 的 CID-keyed／名稱式靜態 CFF、有 VariationStore 的 CFF2 variable 或省略
VariationStore 的非變動 CFF2 WOFF；這些能力只承諾鎖定 corpus 的有界格式矩陣。`net48` 要求
WOFF2、缺少或巢狀元件的名稱式 CFF `seac`、缺少 VariationStore
卻使用 `vsindex`／`blend` 的 CFF2、未知 color table 版本或
直接輸出 collection 會明確失敗。

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

低階產字引擎維持單一 `FontSourceId` 契約；官方全字庫的 Plane 0 與 Plane 2
分屬不同字型檔，因此頁面混排文字不得原封不動送到單一來源。瀏覽器端可使用 samples 內的
`webfont-autosubset.js` 掃描文字節點，以 grapheme cluster 為不可拆分單位將設定範圍內的 Plane 2
難字去重、分批，再由應用程式提供的 `odfKitRequestWebFonts` callback 交給受信任後端。這可避免
拆散 IVS、ZWJ emoji、combining mark 或區域指示符號。helper 會監看後續 DOM 與 open shadow root
變更、重試失敗批次，並略過 `script`、`style`、`textarea` 及 `data-odf-ignore` 範圍。callback
必須使用既有登入身分或同等授權機制；不得把 WebFont API key 寫進 HTML 或 JavaScript，也不得
記錄原始頁面文字。

託管端另有第二道防線：managed engine 會先依實際來源字型的 `cmap` 篩選文字。錯送混排文字時，
只把該來源確實支援的連續序列交給嚴格的低階引擎；若完全沒有可產生的 glyph，回傳 HTTP 204，
不建立空資產，也不以 400／503 表示一般字型 fallback。非法 JSON、未允許的來源／格式仍回 400；
佇列或速率限制回 429；503 僅保留給暫時性基礎設施失敗。所有動態回應仍禁止快取。

產出的每個 `@font-face` 必須帶精確 `unicode-range`。頁面 CSS 將預設字型排在前面，WebFont
只補上預設字型缺少的難字；瀏覽器依 CSS Fonts 字型比對規則選擇 face，無需把一般字重做成
WebFont。

要求標頭必須包含 `X-OdfKit-WebFont-Key`。成功回傳 manifest，公開頁面只 GET
`/{sha256}/{fileName}`；資產會重新驗證 SHA-256、大小與副檔名，再以 immutable cache 與 ETag
傳送。Handler 重啟後仍可安全讀取內容定址產物，不依賴 process-local registry。
ASP.NET Core 節點若共用同一個受信任資產目錄，可由 hash URL 重新驗證並發現另一節點已產生的
資產；這只解決公開讀取，不取代跨節點 generation lease 或 fencing。durable manifest cache 另以
條目數、總 bytes 與閒置時間進行 LRU 清理；內容定址字型本體仍應由共用儲存體或 CDN 的生命週期
政策管理，不能在要求路徑任意刪除。
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

依 [RFC 9111](https://www.rfc-editor.org/rfc/rfc9111.html)，`no-cache` 允許儲存回應，但重新使用前
必須向 origin 驗證；`no-store` 才禁止儲存。ASP.NET Core 的
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
設定模型；CI 分別以 `Clr4IntegratedAppPool` 與 `Clr4ClassicAppPool` 執行完整 HTTP smoke，Classic
路徑由 ASP.NET 4 ISAPI mapping 交給 `system.web/httpHandlers`。IIS Express 由使用者啟動且沒有
WAS。Smoke 從 IIS Express 安裝目錄複製官方 `AppServer/applicationhost.config` 至 artifact，不依賴
使用者曾啟動 IIS Express 所產生的個人設定；因此這項證據不取代正式 IIS 站台或客戶安全設定的驗收。
兩種 pipeline 另對內容定址 WOFF 執行 16 路有界負載，至少 256、最多 1,024 次 GET；每個回應
重算 SHA-256，evidence 記錄總傳輸 bytes、elapsed、CPU 與 IIS Express initial／peak working set。

`eng/Test-WebFontAspNetCoreIisExpressSmoke.ps1` 則使用完整隔離 `applicationhost.config` 與 ANCM
V2，分別發布並實際啟動 In-Process 與 Out-of-Process。前者驗證
`appsettings.{Environment}.json` API key，後者驗證環境變數覆寫；兩者皆執行 401/no-store、
動態 WOFF2、GET／HEAD、SHA-256、ETag 與 304。In-Process 在 `iisexpress.exe` 內執行，
Out-of-Process 由 ANCM 啟動 Kestrel 並代理。隔離設定會以已驗證存在的 ANCM V2 DLL 顯式加入
`aspNetCore` section、global module 與 locked module，避免依賴個人 `applicationhost.config`。
兩種 hosting model 亦執行相同有界 WOFF2 負載；In-Process 量測 IIS Express，Out-of-Process
同時計入 IIS Express proxy 與其唯一 Kestrel `dotnet` 子程序，避免只量到代理層就宣稱應用程式資源量。參考
[Microsoft IIS Express 概觀](https://learn.microsoft.com/en-us/iis/extensions/introduction-to-iis-express/iis-express-overview)、
[IIS Express 命令列](https://learn.microsoft.com/en-us/iis/extensions/using-iis-express/running-iis-express-from-the-command-line)
、[ASP.NET Core Module](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/aspnet-core-module?view=aspnetcore-10.0)
與 [IIS Integrated／Classic 架構](https://learn.microsoft.com/en-us/iis/get-started/introduction-to-iis/introduction-to-iis-architecture)。

較長的本機有界持續負載由 `eng/Test-WebFontIisSustainedLoad.ps1` 執行，GitHub workflow
預設關閉，只有手動指定 `run_webfont_iis_sustained_load` 才會啟動。2026-07-19 在 Windows x64、
12 logical processors、16 路併發與官方 CNS Ext-B 字型下，實際結果如下：

| Hosting path | Requests | Elapsed | CPU | Peak working set |
| --- | ---: | ---: | ---: | ---: |
| System.Web Integrated | 10,928 | 30.033 秒 | 25.547 秒 | 313.8 MiB |
| System.Web Classic | 10,960 | 30.036 秒 | 21.734 秒 | 308.1 MiB |
| ASP.NET Core In-Process | 8,288 | 30.047 秒 | 44.656 秒 | 100.7 MiB |
| ASP.NET Core Out-of-Process | 4,096 | 67.107 秒 | 74.688 秒 | 115.7 MiB |

每個回應都重新計算 SHA-256；Out-of-Process 的 CPU 與 working set 同時計入 IIS proxy 與
Kestrel 子程序。這是單機、固定 corpus 的回歸證據，不是 production 容量、長時間 soak、
WAF／CDN 或不同硬體的效能承諾。

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
自動跨電腦保留，部署者必須提供版本化 mapping、字型來源 SHA-256、授權與資料治理；使用者個人
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
depth、sequence、產出、queue、timeout 與 concurrency 上限。timeout 只有在取消權杖抵達實際
耗用 CPU 的解析與子集化迴圈時才成立；`WebFontGenerationWorker.JobTimeout` 會取消交給引擎的
權杖，引擎則將其貫穿至字圖級迴圈，逾時因而能真正回收 consumer 執行緒。GitHub Actions 可以
證明有限併發與可重現錯誤處理，不能代替真實 CDN、WAF、跨區容量或第三方惡意字型安全審查。

動態產生 endpoint 將語法、allowlist 與要求形狀錯誤回應 400；來源沒有要求 glyph 時回 204；
合法但引擎不支援的格式或技術回應 422；佇列已滿回 429；逾時、I/O、權限與密碼學服務等暫時性
基礎設施失敗回應 503。產物結構或內部狀態不一致回應 500，不偽裝成用戶端錯誤，也不讓例外
終止服務。資產回應在來源允許清單非空時一律輸出 `Vary: Origin`，
避免一年期 `immutable` 快取讓共享快取把缺少 `Access-Control-Allow-Origin` 的回應提供給
合法跨來源請求（`@font-face` 一律以 CORS 模式抓取）。

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
演練另會撤除隔離 feed 中的 OpenType nupkg、清空 consumer cache 並確認 restore fail closed，
再由同批 SHA-256 快照復原及重跑 restore／build／run。

`pwsh eng/Test-WebFontStandardsAndDependencies.ps1 -Online` 會從 NuGet 官方資料確認 WebFont direct
相依仍是最新穩定版，並以 90 天期限追蹤 OpenType errata、Unicode、WOFF／WOFF2、CSS Fonts 與
IFT。Preview 不得直接加入 WebFont；目前唯一例外是 OdfKit core 傳遞的
`CSharpMath 1.0.0-pre.1`，具有精確理由、移除條件與複查期限。

真實大型傳輸基準以官方 CNS Ext-B 67,492,856-byte 字型的 2,048 個 supplementary-plane
scalar 執行：256 code-point bucket 產生 8 個 WOFF2，兩輪 hash 一致；冷啟字型、CSS 與 manifest
合計 2,154,873 bytes。這是固定 runner corpus 的回歸基準，不等同特定客戶頁面或 CDN 網路量測。

## 第一方規格依據

- [WebFont 純 .NET 架構契約](webfont-managed-architecture.md)
- [Microsoft OpenType 1.9.1](https://learn.microsoft.com/en-us/typography/opentype/spec/)
- [Microsoft OpenType 1.9.1 errata](https://learn.microsoft.com/en-us/typography/opentype/spec/errata)
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
