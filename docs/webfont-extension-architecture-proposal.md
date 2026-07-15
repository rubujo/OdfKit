# OdfKit 多國罕用字 WebFont 架構與實作規劃草案

> 草案日期：2026-07-15
>
> 決策狀態：已完成 experimental 套件與端到端驗證；供人工審查是否產品化
> 前置評估：[WebFont 擴充套件可行性與必要性評估](webfont-extension-feasibility-assessment.md)

## 1. 結論

需求在技術上**可以完成**，也可以同時支援 ASP.NET Core 與 ASP.NET Web Forms，但必須拆成
中性核心、字型處理引擎、離線工具與兩個宿主適配套件。不能用單一 TFM、單一執行模式或
單一臺灣字集規則涵蓋全部需求。

建議產品預設採用「建置期／背景預產生＋靜態不可變資產」：先從 ODF 或文字內容收集實際需要的
Unicode 序列，產生 CSS、manifest 與多格式字型，再由網站當成靜態檔案傳送。這條路徑最容易
自動化，也有最佳的安全性、延遲與可快取性。要求到達時才進行 native 字型子集化，應是明確
啟用的進階模式，且在隔離 worker 中執行。

「多國中性」不等於內建所有國家的資料，而是核心不把 CNS、PUA、特定字型名稱或 Unicode
平面當成唯一模型；各國資料集與私有造字對照透過可版本化 profile／mapping provider 接入。

## 2. 已驗證事實與最小網路依據

### 2.1 本機 minimal smoke 證據

目前測試已證明靜態預產生與兩種 .NET 宿主的端到端鏈可以運作：

- 從合法測試字型抽取 Plane 0、1、2、3 的 13 個碼位，產生 10,468 bytes 的 WOFF2。
- Chromium 的 `document.fonts.check` 回傳成功，頁面顯示 `PASS`，並已保留瀏覽器截圖。
- 全字庫官方 `TW-Sung-Plus-98_1.ttf` 的 Plane 15 PUA 三個自造字，可產生 2,608 bytes、
  4 glyph 的有效 WOFF2。
- 同一輸入可重現相同 SHA-256，證明 content-addressed cache key 的概念可行。
- Arabic、Devanagari、香港 TTC face、香港 OpenType CFF、日本 IPAmj IVS 與全字庫
  Plane 15 PUA 共六組案例，可產生 WOFF2、WOFF、TTF、OTF 共十一個資產。
- ASP.NET Core 以 allowlist、SHA-256 URL、ETag、`immutable`、`nosniff` 與正確 MIME
  提供資產；net48 Web Forms handler 與 HTML helper 已通過 CLR smoke。
- `nvarchar`／`nchar` 類型由 ADO.NET provider 取得 Unicode；Big5、明確 Big5E mapping 與
  tenant-scoped PUA 則從受限位元組或版本化 profile 轉成 Unicode 序列。

這仍不代表 CFF2、color font、所有瀏覽器或不受信任字型已達 production 品質；目前外部
FontTools 必須置於受控／隔離環境，且尚未完成四瀏覽器與惡意字型 fuzz matrix。

### 2.2 官方資料對架構的直接約束

1. Microsoft 明確指出 Web Forms 只存在於 .NET Framework，ASP.NET Core 不能用於 Web
   Forms，因此兩個宿主必須是不同套件與 TFM：
   [Microsoft：.NET 與 .NET Framework 的選擇](https://learn.microsoft.com/en-us/dotnet/standard/choosing-core-framework-server)。
2. WOFF2 是 W3C Recommendation，可承載 TrueType／OpenType 的輪廓與進階表格；但字型
   collection 雖在格式層可表示，瀏覽器互通性不應由此推定：
   [WOFF 2.0](https://www.w3.org/TR/WOFF2/)。
3. CSS Fonts Level 4 的字型匹配包含 variation sequence 等完整序列語意，不能只按
   UTF-16 code unit 或 Unicode Plane 分段：
   [CSS Fonts Module Level 4](https://www.w3.org/TR/css-fonts-4/)。
4. HarfBuzz subset 支援 TrueType、CFF／CFF2、部分 color／variable font 與 OpenType
   layout closure，也明列 SVG、部分 bitmap 與 Graphite／AAT 的限制；所以正式能力必須以
   支援矩陣表達，而不能宣稱任意字型皆可處理：
   [HarfBuzz subset API](https://harfbuzz.github.io/harfbuzz-hb-subset.html)。
5. Unicode PUA 的意義只存在於私下協議；相同碼位可能在不同組織代表不同字形，因此 PUA
   cache、manifest 與 mapping 必須具備 tenant／profile 與版本識別：
   [Unicode Private-Use Characters FAQ](https://unicode.org/faq/private_use.html)。
6. ASP.NET Core 的 rate limiting 用於防止濫用與資源過載，但 Microsoft 也要求部署前進行
   load test；它不是 DDoS 或字型解析安全的完整替代方案：
   [ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)。
7. `HybridCache` 可提供同鍵 stampede protection；仍需另外設計 durable asset store、容量
   上限與跨節點一致性：
   [ASP.NET Core HybridCache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid?view=aspnetcore-10.0)。

## 3. 多國中性的領域模型

### 3.1 核心不得假設的事項

- 不把「代理對」視為罕字；它也可能是 emoji、音樂、數學或歷史文字。
- 不把「Unicode Plane」視為字型實際涵蓋範圍。
- 不把 PUA 當成跨組織有共同語意的字元。
- 不預設中文、全字庫、臺灣 locale 或特定字型家族。
- 不把單一 code point 清單當成所有塑形的充分輸入。
- 不由套件自動推定字型授權允許轉檔、子集化或 Web 散布。

### 3.2 中性資料單位

| 模型 | 用途 |
| --- | --- |
| `FontSourceId` | 伺服器設定的 opaque ID，不暴露或接受任意檔案路徑 |
| `FontFaceIdentity` | 來源 SHA-256、face index、named instance／variation coordinates |
| `TextSequenceRequest` | Unicode scalar sequence、字素／塑形 cluster 與可選語言／script hint |
| `FontProfileId` | 國家、機關、專案或租戶的資料集與 mapping 版本 |
| `FontFeaturePolicy` | OpenType feature、layout closure、variation 與 color policy |
| `WebFontFormat` | `Woff2`、`Woff`、`OpenType`、`TrueType`；collection 為輸入能力 |
| `WebFontManifest` | CSS family、unicode-range、資產 hash、授權與來源追蹤資訊 |
| `SubsetEngineIdentity` | 引擎與 encoder 版本，避免升級後誤用舊 cache |

cache key 至少包含上述會影響輸出的欄位，以及 canonical 化後的序列集合。canonical 化只能
移除完全相同的重複要求，不能拆散 IVS、emoji ZWJ 或複雜文字 cluster 後任意排序。

### 3.3 可插拔 profile

中性核心只定義 `ICharacterMappingProvider`、`IFontCoverageProvider` 與 `IFontLicensePolicy` 等
契約。資料與規則放在獨立 profile 或由部署者注入，例如：

- 臺灣：CNS 11643、全字庫 Plane 15 PUA、機關 EUDC 對照。
- 日本：MJ 文字資訊基盤、IVS／IVD、縮退對照。
- 香港：HKSCS 與其 Unicode／舊 PUA 遷移資料。
- 中國：GB 18030 與歷史 PUA 重指派資料。
- 韓國：KS X 1001、Hangul／Hanja 與機構自訂字。
- 其他：越南喃字、歷史文字、學術轉寫、MUFI、企業自訂 icon／EUDC。

profile 只描述 mapping、建議字型與授權 metadata，不應把第三方字型直接包進 NuGet，除非已
完成個別版本的再散布審查。部署者可同時載入多個 profile；發生 PUA 衝突時必須由 tenant／
document context 明確選擇，不能全域猜測。

## 4. 建議套件邊界

| 套件／工具 | 建議 TFM | 責任與相依方向 |
| --- | --- | --- |
| `OdfKit.WebFonts.Abstractions` | `net10.0;netstandard2.0` | 中性模型、canonical request、manifest、provider 契約；不相依 Web 或 native engine |
| `OdfKit.WebFonts.Encoding.Legacy` | `net10.0;netstandard2.0` | 嚴格 Big5、明確 Big5E 與 tenant PUA mapping |
| `OdfKit.WebFonts.Data.SqlServer` | `net10.0;netstandard2.0` | ADO.NET Unicode 與有界 legacy `varbinary` 讀取；不猜 code page |
| `OdfKit.WebFonts.OpenType` | `net10.0` | 受信任來源 registry、FontTools 子集、WOFF／WOFF2／TTF／OTF 輸出驗證 |
| `OdfKit.WebFonts.Build` | `net10.0` | CLI／MSBuild 可重現預產生、manifest 與資產目錄 |
| `OdfKit.WebFonts.Worker` | `net10.0` | 有界背景工作、single-flight、逾時與滿載拒絕；不冒充 OS sandbox |
| `OdfKit.WebFonts.Hosting.AspNetCore` | `net10.0` | DI、唯讀 endpoint、CSP／CORS 輔助與 HTTP cache headers |
| `OdfKit.WebFonts.Hosting.SystemWeb` | `net48` | Web Forms `IHttpHandler`、Web.config 與控制項；預設只讀預產生資產或呼叫 worker |
| `OdfKit.Extensions.Html.WebFonts` | 對齊 HTML extension | ODF→HTML 字型需求收集、CSS／manifest 引用，不負責 native 子集 |
| `OdfKit.WebFonts.Profiles` | `net10.0;netstandard2.0` | 有界、版本化、多位元組 JSON mapping profile |

依賴方向固定為「宿主／Build／HTML integration → Abstractions」，而 engine 由 DI 或 worker
協定接入。ASP.NET Core 與 System.Web 不互相參考；如此才可避免為了 Web Forms 把現代宿主
鎖在 .NET Framework，也避免把 native 字型解析器載入舊 IIS worker process。

## 5. 三種執行模式

### 5.1 Static／build-time（預設且第一優先）

1. 掃描 ODF、HTML model 或明確文字 corpus。
2. 保留 Unicode sequence／cluster 與來源 font context。
3. 一次產生 WOFF2、選配 WOFF／TTF／OTF、CSS 與 manifest。
4. 部署到 `wwwroot`、App_Data 對應的只讀資產區、CDN 或 object storage。
5. 網站只以 hash URL 傳送靜態檔案，不在要求路徑執行子集化。

此模式同時適用 Web Forms 與 ASP.NET Core，最符合「自動化簡易使用、安全、極致效能」。

### 5.2 Background runtime（第二階段）

受授權的 POST 或應用程式內工作提交 canonical request；worker 產生資產後回傳 hash manifest。
公開 GET 只允許讀取已存在的 content-addressed asset。相同 key 使用 single-flight，佇列滿載時
快速拒絕，不讓大量要求堆積在網站執行緒。

### 5.3 Dynamic edge（進階 opt-in）

只有實際市場案例證明預產生不可用時才提供。必須使用隔離 worker、強制驗證／授權、嚴格配額
與 durable cache；不接受把姓名或原始文字放在 GET URL。這不應是套件的零設定預設值。

## 6. 預期的易用 API 形狀

下列只定義使用體驗，不是已核准的公開 API。

ASP.NET Core：

```csharp
builder.Services.AddOdfWebFonts(options =>
    options.AssetRootPath = "wwwroot/_odf-fonts");

app.MapOdfWebFonts("/_odf-fonts");
```

ASP.NET Web Forms：

```xml
<add name="OdfWebFonts"
     path="_odf-fonts/*"
     type="OdfKit.WebFonts.Hosting.SystemWeb.OdfWebFontHandler, OdfKit.WebFonts.Hosting.SystemWeb" />
```

CLI／CI：

```text
odfkit-webfonts build --font licensed.ttf --text corpus.txt \
  --profile organization-default --formats woff2,woff \
  --output wwwroot/_odf-fonts
```

Big5／Big5E 是輸入解碼層，不是瀏覽器字型編碼。瀏覽器與 CSS 最終只接收 Unicode；SQL
`nchar`／`nvarchar` 可保留 Unicode，但 `text`／`varchar` 若已在資料庫 code page 轉換時變成
`?`，套件無法事後復原。要保留原始 Big5／Big5E bytes，應使用 `varbinary` 並明確指定 mapping。

理想的最小設定只有資產目錄、受信任 font ID 與 profile；授權、輸出上限或隔離限制不能為了
「零設定」而關閉。Web Forms 預設不在 IIS 行程內載入 native subset engine。

## 7. 格式政策

| 格式 | 政策 |
| --- | --- |
| WOFF2 | Web 預設、必要支援；不再套 HTTP gzip／Brotli |
| WOFF1 | 選配相容格式；由同一 canonical job 一次產生並永久快取 |
| TTF／OTF | 選配輸出，用於允許的下載、ODF 內嵌或受控 legacy 情境；Web 不優先 |
| TTC／OTC | 必要輸入能力，要求明確 face index；預設輸出獨立 WOFF2／WOFF／TTF／OTF |
| WOFF2 collection | 只列實驗功能；格式規格存在不等於瀏覽器已證明互通 |
| EOT | 不進核心；只有明確 IE legacy 商業需求才做獨立相容套件 |
| SVG font | 不支援；屬過時 Web 格式且 subset engine 支援亦有限 |

「支援格式」必須區分輸入、可子集化、可編碼與瀏覽器可用四種能力，不能只檢查副檔名。

## 8. 安全設計

- 只接受設定好的 opaque font ID、face index 與 profile；拒絕任意路徑、任意 URL 與預設上傳。
- 驗證 canonical root、來源 SHA-256、實際字型 signature、table offsets 與輸出 cmap。
- 設定輸入 bytes、scalar／cluster 數、glyph 數、輸出 bytes、CPU、wall time、記憶體、併發與
  佇列長度上限；失敗結果使用短期 negative cache。
- native engine 在低權限隔離 worker／container 中執行；網站行程不因損壞字型 crash。
- generation 僅允許受授權的 POST／背景工作；公開 GET 只讀 hash 資產。
- URL、log、metric label 與例外不得包含原始姓名、PUA 內容、實體字型路徑或內部授權資訊。
- manifest 記錄來源 hash、face、profile／mapping 版本、engine 版本與授權聲明；先通過 license
  policy 才可產生或散布。
- 資產回應使用正確 MIME、`X-Content-Type-Options: nosniff`、content hash、長效
  `immutable`、ETag；跨 origin 時明確設定 CORS／CORP，並提供 CSP 指引。
- 使用損壞字型與 fuzz corpus 驗證 parser／subsetter；rate limit 只是其中一層，不宣稱能單獨
  抵禦 DDoS。

## 9. 效能設計與可量測閘門

### 9.1 設計原則

- 靜態預產生優先；cache hit 絕不執行子集化。
- 以來源 SHA＋face index 快取 HarfBuzz 預處理 face；HarfBuzz 提供
  `hb_subset_preprocess()` 供重複子集加速。
- canonical key 去重與 single-flight；不同輸出格式在同一工作中產生一次。
- memory cache 以 bytes 計價並有硬上限；大資產放 disk／object store，不使用無界 dictionary。
- 靜態傳送使用框架的 file／sendfile 路徑；不把整個字型重複複製到多層 `byte[]` cache。
- 已知大型 corpus 可依 script／profile／unicode-range 預分片；私密文件則產生精確文件子集，
  避免把請求字元集合外洩給第三方 CDN。
- 不承諾固定檔案大小、固定毫秒數或「零 CPU」；所有數字來自可重現 benchmark artifact。

### 9.2 第一版閘門

| 閘門 | 驗收方式 |
| --- | --- |
| 靜態熱路徑 | 本機 p95 目標不高於 10 ms，且 profile 顯示沒有 subset 工作；硬體與檔案大小一併記錄 |
| 同鍵併發 | 1,000 個同鍵 miss 只啟動一個 generation job；256 個靜態要求皆可完成 |
| 有界資源 | 超過 bytes／cluster／queue／timeout 限制可預期地拒絕，峰值工作集不持續上升 |
| 可重現性 | 相同來源、profile、engine 與 request 產生相同資產 hash |
| 多節點 | 兩個宿主可讀同一 durable asset，且不依賴 process-local lock 保證唯一工作 |
| 冷工作 | 依字型類型與 1／10／100／1,000 cluster 分別記錄，不設定未經量測的通用承諾 |

## 10. 實作與驗證階段

### Phase 0：中性契約與真實 corpus

- 定義 sequence／cluster、font face、profile、license policy 與 canonical key。
- 收集可合法測試的 CNS PUA、日本 IVS、HKSCS／GB18030、阿拉伯文、天城文、Hangul／Hanja、
  越南喃字及至少一組私有 EUDC corpus。
- 取得 `educ.ttc` 實檔後再判斷其含義、face 與授權；不可由檔名推定。

### Phase 1：engine spike

- 驗證 TTF、OTF、TTC face、CFF、CFF2、variable 與 color font。
- 產生 WOFF2、WOFF、TTF、OTF，檢查 signature、cmap、layout closure 與可重現性。
- 用 HarfBuzz shaping 比對子集前後 glyph sequence／position；再做 Chromium、Firefox、WebKit
  的截圖與 `document.fonts.check` smoke。
- 對 unsupported table／format 採明確拒絕或 pass-through policy，不靜默產生錯字。

### Phase 2：預產生工具與 HTML 整合

- 先完成 CLI／MSBuild、manifest schema 與 `OdfKit.Extensions.Html` 的字型需求輸出。
- 讓 ASP.NET Core 與 Web Forms 範例使用同一組預產生資產與相同 hash。
- 此階段通過即可提供高價值功能，不必等待動態服務。

### Phase 3：宿主套件

- ASP.NET Core：DI、static asset endpoint、HybridCache、授權、限流與 observability。
- System.Web：net48 handler／控制項，只讀資產或呼叫外部 worker。
- 建立兩個 framework 的 consumer smoke；驗證 package TFM 與部署文件。

### Phase 4：隔離 runtime worker

- 只有設計夥伴證明需要即時動態內容時才進行。
- 完成 bounded queue、single-flight、durable store、低權限隔離、逾時、crash recovery、fuzz 與
  load test 後，才開放受授權 generation API。

### Phase 5：是否發布的人工決策

正式 NuGet 發布前至少要有：

1. 一個真實設計夥伴與可匿名化 workload。
2. 多國 corpus 中每一項宣稱能力的自動測試證據。
3. 字型授權清單與 profile 更新責任人。
4. 兩個宿主的部署、安全與效能基準。
5. native binary 的平台、更新與漏洞回應策略。

若市場需求主要是固定 ODF→HTML，停在 Phase 2 會比建立公開動態服務更安全、便宜且快速。

## 11. 人工審查要決定的事項

1. 第一版是否接受「預產生優先，runtime generation 非預設」。
2. 第一版必要輸出是否為 WOFF2＋WOFF，TTF／OTF 僅列選配。
3. Web Forms 是否接受以預產生資產／外部 worker 為正式支援，而不在 IIS 內做 native subset。
4. 首批 profile 是只做 provider 範例，或由 OdfKit 維護 CNS、MJ 等版本化資料。
5. 能否取得至少一組日本 IVS、香港／中國 mapping 與實際 `educ.ttc`／EUDC 合法測試資料。
6. 是否已有願意提供 workload、部署限制與效能目標的設計夥伴。

在上述決策完成前，建議繼續維持 PoC／experimental 狀態，不凍結公開 API，也不宣稱支援所有
語系與字型格式。
