# OdfKit WebFont 擴充套件可行性與必要性評估

> 評估日期：2026-07-15
>
> 評估對象：`odfkit_webfont_extension_proposal.md`
> 文件性質：已驗證的技術評估；experimental 套件不代表已承諾正式發布

## 1. 決策摘要

### 1.1 結論

動態產生罕用字 WebFont 在技術上**可以實作**，OdfKit 現有的字型註冊、Unicode 平面分段與
`IFontSubsetter` 擴充點也能重用一部分。然而，原提案不能直接照案實作，主要原因如下：

1. 專案目前只有 `IFontSubsetter` 介面，沒有可正式產生 WOFF2 的子集化實作。
2. `OdfFontContext.SegmentText` 是依 Unicode 平面與字型名稱規則分段，不是依字型實際
   glyph 覆蓋、字素叢集、IVS 或 OpenType shaping closure 分段，不能直接當成通用
   WebFont 子集規劃器。
3. 提案把 ASP.NET Core、ASP.NET Web Forms 與 `.NET Standard 2.0` 放在同一個套件，
   目標框架設計不可行。Web Forms 僅存在於 .NET Framework；ASP.NET Core 適配器也不應
   成為 `.NET Standard 2.0` 核心套件的必要相依。
4. 提案中的公開 GET 查詢會把罕用姓名或其他敏感文字放入 URL、記錄、代理與快取鍵，
   不適合作為預設設計。
5. 快取、鎖定、CDN、CSP、授權與效能主張有多處過度保證或實作錯誤。
6. 目前的 HTML exporter 尚未保留 ODF 字型家族，也沒有 WebFont 資產輸出契約；提案所附
   JavaScript 則是掃描任意網頁 DOM，與 ODF 的直接關係偏弱。

因此，本文件在完成 minimal 驗證後的建議是：

- 已依責任拆成中性契約、legacy encoding、SQL、profile、OpenType engine、Build tool、
  Worker、ASP.NET Core、System.Web 與 HTML integration 套件。
- 維持 experimental／0.x API；先供人工審查與真實 workload 試用，不宣稱 production ready。
- 正式產品預設採建置期預產生；公開 GET 只讀內容雜湊資產，runtime generation 不直接暴露。
- 是否正式發布仍應由市場採用、字型授權、四瀏覽器與 fuzz／效能資料決定。

### 1.2 必要性判斷

| 問題 | 判斷 |
| --- | --- |
| 對「罕字仍須是可選取文字」是否有價值 | 有，尤其是戶政、公文、檔案與姓名顯示情境 |
| 是否是 ODF 規格本身要求 | 否；這是 ODF 內容進入 Web 後的交付與字型服務問題 |
| 是否補足目前 OdfKit 的直接缺口 | 部分是；HTML 匯出未保留字型資訊是直接缺口，通用動態字型服務則不是 |
| 現在是否值得成為官方套件 | 尚無足夠證據；需求、授權、效能與維運成本都未以實際工作負載驗證 |
| 建議優先項目 | 先改善 HTML 字型資訊輸出與建立 PoC，再決定是否產品化 |

### 1.3 市場判斷

目前可確認的是**問題市場存在，但尚未證明有足以支撐獨立通用 NuGet 套件的產品市場**：

- 全字庫資料指出 CNS 11643 已包含 48,027 個中文字，戶役政資料庫另曾出現三萬餘姓名用字；
  這證明政府、戶政、醫療、教育、金融或檔案系統確實面對長尾字形交換問題：
  [全字庫中文碼介紹](https://www.cns11643.gov.tw/pageView.jsp?ID=9&SN=&la=0&lang=tw)。
- 全字庫已提供讓開發者在 HTML 以 PNG 顯示罕字的「字形即時顯示」服務，代表 Web 顯示需求
  不是假設；同時它也是免費、簡單且已存在的替代方案。WebFont 的差異價值必須落在可選取、
  複製、縮放、列印、樣式一致與離線部署，而不能只訴求「看得到」：
  [全字庫字形即時顯示](https://www.cns11643.gov.tw/pageView.jsp?ID=75)。
- W3C 正在制定 Incremental Font Transfer，並明確指出大型 CJK 字型即使使用 WOFF2 仍可能
  大到不實用。這證明「大型字集的 Web 傳輸」是業界真問題，但也表示未來標準瀏覽器能力
  可能取代部分自訂動態子集服務：
  [Incremental Font Transfer](https://www.w3.org/TR/IFT/)。
- ODF 仍持續演進，OASIS 已於 2025-10-06 核准 ODF 1.4；但「使用 ODF」與「把 ODF
  轉成 Web 且必須保留罕字」是兩層篩選，不能用 ODF 的整體採用量直接推估本功能市場：
  [OpenDocument 1.4](https://docs.oasis-open.org/office/OpenDocument/part1-introduction/OpenDocument-v1.4-os-part1-introduction.html)。

因此市場定位應是**利基型 B2B／B2G 基礎元件**，不是大眾型 WebFont 產品。最可能的買方或
採用者是已有 ODF→HTML 流程、處理法定姓名或歷史字形、且不能接受 PNG／缺字方框的系統整合商
與公部門承包商。現階段沒有公開資料能回答願付價格、.NET 使用比例、部署數或動態子集頻率；
正式產品化前應先訪談 5～10 個目標團隊，並取得至少一個設計夥伴與可匿名化 corpus。

## 2. 評估依據

### 2.1 目前專案實作

本次檢視的主要程式碼與文件如下：

- [`OdfFontContext`](../OdfKit/Styles/OdfFontContext.cs)：字型註冊、路徑解析、替代規則、
  Unicode 平面分段、ODF 字型內嵌與 PUA 子集化協調。
- [`IFontSubsetter`](../OdfKit/Styles/IFontSubsetter.cs)：呼叫端提供的同步子集化擴充點；
  結果可攜帶任意副檔名與媒體類型，但目前沒有正式實作者。
- [`OdfCjkFontFallbackEngine`](../OdfKit/Styles/OdfCjkFontFallbackEngine.cs)：全字庫、Jigmo、
  HanaMin 與常見系統字型的 ODF font-face 宣告。
- [`OdfHtmlExporter`](../OdfKit.Extensions.Html/OdfHtmlExporter.cs) 與
  [`OdfHtmlExportOptions`](../OdfKit.Extensions.Html/OdfHtmlExportOptions.cs)：目前輸出基本 HTML
  與行內樣式，但未輸出 ODF 字型家族、`@font-face` 或字型資產 manifest。
- [`Cns11643InteropTests`](../OdfKit.Tests/Cns11643InteropTests.cs)、
  [`OdfFontSegmenterTests`](../OdfKit.Tests/OdfFontSegmenterTests.cs) 與
  [`OdfFontEmbeddingComplianceTests`](../OdfKit.Tests/OdfFontEmbeddingComplianceTests.cs)：證明
  現有 ODF 分段、font-face 宣告與子集化擴充點的範圍。
- [`package-catalog.md`](package-catalog.md)：目前擴充套件均以 ODF 匯出、渲染、互通或中繼資料
  為明確邊界。

### 2.2 最新外部資料

截至評估日，相關規格與平台狀態為：

- W3C CSS Fonts Level 4 定義 `@font-face`、`unicode-range`、`font-display` 及 font metric
  overrides，也要求 variation selector 與前一字元以完整序列做字型匹配。這表示單純按
  UTF-16 surrogate 或 Unicode 平面拆分不足以保證正確顯示：
  [CSS Fonts Module Level 4](https://www.w3.org/TR/css-fonts-4/)。
- WOFF2 是 W3C Recommendation，使用 Brotli 壓縮；標準媒體類型為 `font/woff2`：
  [WOFF2](https://www.w3.org/TR/WOFF2/)、
  [RFC 8081](https://www.rfc-editor.org/rfc/rfc8081)。
- W3C Incremental Font Transfer（IFT）已於 2025-11-18 成為 Candidate Recommendation
  Draft，目標正是大型 CJK 與複雜文字的增量字型傳輸；但規格仍明示為工作草案，且尚無
  implementation report。因此可列入追蹤，不宜在目前把它當成可依賴的跨瀏覽器正式方案：
  [Incremental Font Transfer](https://www.w3.org/TR/IFT/)。
- .NET `HybridCache` 已提供同鍵請求的 stampede protection，也可搭配分散式二級快取；
  多節點部署仍須注意各節點的一級記憶體快取不會彼此同步失效：
  [HybridCache in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid?view=aspnetcore-10.0)。
- `IMemoryCache` 的大小限制必須在專用 cache instance 上明確設定；只有 entry 的
  `SetSize` 並不會讓未設定 `SizeLimit` 的 cache 變成有界，而且共用 DI cache 設大小限制
  可能使其他未標示大小的項目失敗：
  [Cache in-memory in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0)。
- ASP.NET Web Forms 僅屬於 .NET Framework，不能以 ASP.NET Core 或單一
  `.NET Standard 2.0` 專案涵蓋：
  [Choose between .NET and .NET Framework for server apps](https://learn.microsoft.com/en-us/dotnet/standard/choosing-core-framework-server)。
- Unicode IVS 是「基底漢字＋variation selector」的序列；已註冊 IVS 才適合文字交換：
  [Unicode Variation Sequences FAQ](https://www.unicode.org/faq/vs.html)、
  [UTS #37](https://www.unicode.org/reports/tr37/)。

## 3. 原提案的正確部分

下列方向合理，可以保留：

1. 使用 WebFont 可讓瀏覽器顯示的內容維持文字，而不是把每個字轉成圖片。
2. WOFF2 適合 Web 傳輸，且通常不需要再套用 HTTP gzip／Brotli content encoding。
3. `unicode-range` 可組成 composite font，讓瀏覽器只下載命中碼位範圍的字型資產。
4. 同源 stylesheet 與 font endpoint 可在適當 CSP 下配合 `style-src 'self'` 與
   `font-src 'self'`。
5. 動態子集化是 CPU 密集且可被濫用的工作，需要輸入上限、併發上限、逾時、快取與
   single-flight。
6. 字型授權是發布前置條件，不能因 OdfKit 原創程式碼採 CC0 就推定輸出的字型也可自由散布。
7. URL 版本化或 content-addressed asset 是使用長效 `immutable` 快取的必要條件。
8. 複雜文字不可任意刪除 GSUB／GPOS 等 shaping 資料；子集器必須做 layout closure。

## 4. 必須修正的內容

### 4.1 功能與 Unicode 模型

| 原提案主張 | 問題 | 修正後說法 |
| --- | --- | --- |
| 所有 surrogate pair 都是罕字 | 會同時命中 emoji、音樂符號、數學字元、歷史文字及 variation selector | 應依目標字型實際 cmap、字素叢集與明確 policy 判斷，不以 surrogate pair 當罕字定義 |
| `SegmentText` 可直接完成通用多國字型路由 | 現有方法僅依平面與字型名稱規則；同一平面內的 glyph 覆蓋可能不同 | `SegmentText` 只能作為既有 CJK profile 的候選路由，正式規劃器必須檢查 glyph coverage 與 cluster |
| 掃描 PUA 後會產生動態字型 | BMP PUA 位於 Plane 0，現有 `SegmentText` 不會把它改派到另一字型；原始 CSS 生成只處理「字型名稱不同」的 segment | PUA 必須有租戶／資料集專屬的碼位到字型 mapping，不可沿用平面分段假設 |
| 依碼位清單即可保證 IVS | IVS 是基底漢字與 variation selector 的序列；拆開後可能選錯 glyph | cache key、subset request 與測試都要保留 variation sequence／cluster 語意 |
| 保留 GSUB、GPOS 就能完美支援複雜文字 | 還需要 glyph、lookup、variation、color table 與其他相依 closure，且須經 shaping 與瀏覽器驗證 | 只承諾經支援矩陣驗證的 script、feature 與字型格式 |
| 可複製、SEO、讀屏皆 100% 正常 | PUA 沒有跨系統共同語意；罕字的發音與輔助科技支援也不由字形檔保證 | WebFont 可保留原始 Unicode 文字與選取能力，但語意、發音與跨系統貼上結果須另行驗證 |

### 4.2 現有 OdfKit 能力的誤讀

1. `IFontSubsetter` 只是擴充契約，不是內建編譯器。專案測試使用 fake subsetter，不能據此宣稱
   已可產生 WOFF2。
2. `OdfFontSubset` 雖可攜帶 `.woff2` 與 `font/woff2`，現有 ODF 內嵌流程主要處理
   TrueType／OpenType；WebFont 產出仍需新實作與瀏覽器驗證。
3. `ResolveFontPath` 解析已註冊或掃描到的字型名稱；它不是一個接收任意路徑後執行
   canonical-path containment 驗證的通用安全邊界。Web API 不應把未受信任的 `fontName`
   直接交給系統字型掃描與解析。
4. OdfKit 的 SimSun／MingLiU ExtB／ExtG 對照是依字型名稱的路由規則，不代表伺服器具有合法
   字型檔，也不代表符合 GB 18030-2022 的完整 glyph、編碼或合規要求。
5. 目前 HTML exporter 沒有輸出文字 run 的 `font-family`。在這一層尚未建立正確輸出前，
   另加一個掃描整頁 DOM 的通用服務不會自然形成 ODF→Web 的完整功能鏈。

### 4.3 套件與目標框架

原提案的單一套件結構應改為下列邊界；名稱仍可在 PoC 後調整：

| 元件 | 建議 TFM | 責任 |
| --- | --- | --- |
| WebFont 規劃與子集抽象 | `net10.0;netstandard2.0`，前提是所選引擎可支援 | canonical request、glyph/cluster 規劃、CSS descriptor、結果模型 |
| WOFF2 子集引擎 | 依 native／managed 相依的實際支援決定 | 字型解析、layout closure、WOFF2 編碼、輸出驗證 |
| ASP.NET Core 適配器 | `net10.0` | DI、endpoint、HybridCache、rate/concurrency limit、HTTP cache headers |
| Web Forms 適配器 | 若確有需求則獨立 `net48` 套件或範例 | `IHttpHandler` 整合；不得混入 ASP.NET Core 套件 |
| 瀏覽器端程式 | 非預設或獨立資產 | 只服務動態內容；靜態 ODF 匯出優先由伺服器直接產生 manifest/CSS |

原始評估時 Web Forms 不應進入第一個里程碑，因為它不會驗證最核心的子集正確性。子集鏈通過後，
目前已把 Web Forms 做成獨立 `net48` 唯讀 Hosting 套件，沒有把 System.Web 或字型引擎混入
ASP.NET Core 套件。

### 4.4 API 與專案規範

原提案的 C# 範例若直接加入專案，會違反目前規範：

- 使用 block-scoped namespace，而非手寫檔案要求的 file-scoped namespace。
- 公開 API 缺少完整英文＋正體中文 XML 文件。
- 例外訊息是 hard-coded 英文，沒有使用 `OdfLocalizer.GetMessage` 與對等的 i18n key。
- 同步 `SemaphoreSlim.Wait()` 會占住要求執行緒，未傳遞 `CancellationToken`。
- 原提案沒有規劃公開 API、雙 TFM baseline、package validation 與測試矩陣；目前實作已補上
  對應腳本與 GitHub Actions 閘門。
- 類別命名空間在 `OdfKit.WebShared`、`OdfKit.Web` 與
  `Microsoft.AspNetCore.Builder` 間不一致，也未對應提議的 package identity。

### 4.5 快取與高併發

原提案的範例不是可靠的 production cache：

1. `.SetSize(1)` 只標記 entry 大小，沒有建立 `MemoryCacheOptions.SizeLimit`；因此不能宣稱
   cache 已有 10,000 筆上限。
2. 以筆數而不是 `byte[]` 長度計價，無法限制實際記憶體。應將字型 bytes、物件 overhead 與
   metadata 納入 size policy。
3. 不應把外部輸入原字串直接作為 cache key。字元順序與重複字會產生大量等價 key，且 key
   可能含敏感內容。
4. `_locks` 是 process-wide static dictionary，只保護單一行程，無法保護多節點服務。
5. 釋放 semaphore 後再依 `CurrentCount` 移除 dictionary entry 存在競態：新要求可能取得即將
   被移除的舊 semaphore，另一要求同時建立新 semaphore，導致同鍵工作再次併發。
6. 讓數萬個要求排在同一 semaphore 不是完整的 overload control。還需要全域有界工作佇列、
   concurrency limiter、要求逾時與快速拒絕。
7. `HybridCache` 可處理單行程 single-flight 與二級快取，但仍須搭配 durable asset store、
   跨節點策略和輸出版本。
8. CDN 不會保證 200,000 個要求只有一個到 origin；不同 PoP、同時 miss、query-string cache
   policy、失效與 eviction 都會影響結果。只能說「可顯著降低 origin 流量」，不能說壓力歸零。
9. `immutable` 不代表瀏覽器永久保存；資源仍可能被 eviction、清除或因政策不快取。

### 4.6 安全與隱私

原提案只處理了部分 CSS 字串 escaping，仍缺少下列邊界：

- 不接受任意字型名稱；公開端點只接受伺服器設定的 opaque font ID allowlist。
- 不把原始文字放在 GET query。罕用姓名的字元集合本身可能識別內容；W3C IFT 也把依字元請求
  所造成的 content inference 列為隱私風險。
- 子集產生應是受驗證或受授權的 POST／背景工作；公開 GET 只讀取已產生、content-addressed
  的 immutable asset。
- canonical key 應至少包含字型檔 SHA-256、face index、子集引擎版本、輸出格式、
  feature／variation policy，以及排序去重後的 code point／sequence 集合。
- font family 與 CSS identifier 必須由系統產生並正確序列化。原提案允許空白的 regex 仍會產生
  無效 class selector，且未完整處理 CSS identifier、quoted family 與 URL context。
- 限制要求 bytes、Unicode scalar 數、唯一 cluster 數、產出 bytes、處理時間與同時工作數；
  對失敗結果做短期 negative caching。
- 子集器處理的是複雜二進位格式，必須隔離 native crash／資源耗盡風險，並用惡意與損壞字型
  corpus 測試。
- CSP 除 `style-src` 與 `font-src` 外，載入 `odf-webfont.js` 仍須符合 `script-src`；若從不同
  origin 載入字型，也須正確處理 CORS。
- 錯誤回應不可把伺服器字型路徑、內部例外或授權資訊直接回傳給使用者。

### 4.7 法律與授權

原提案的二分矩陣過度簡化，應改為逐字型、逐版本、逐使用方式審查：

- 全字庫官方頁面目前同時說明政府資料開放授權條款第 1 版與 OFL-1.1，並要求適當來源標示，
  散布或打包 OFL 字型時也須提供著作權聲明與 OFL 全文。不能只寫成「政府開放授權／CC-BY」：
  [全字庫授權](https://www.cns11643.gov.tw/pageView.jsp?ID=59&SN=&la=0&lang=tw)。
- SIL OFL FAQ 明確把刪 glyph 或 feature 的 WebFont subsetting 視為修改；若有 Reserved Font
  Name，通常須改名並保留必要 metadata／授權資訊：
  [SIL Open Font License FAQ](https://software.sil.org/oflt/)。
- Microsoft 明確表示 Windows 內附字型不得複製到 Web server，也不得轉為 WOFF／WOFF2，
  除非另取得對應 Web 授權。文件內嵌權限不能延伸解讀為 WebFont self-hosting 權限：
  [Microsoft font redistribution FAQ](https://learn.microsoft.com/en-us/typography/fonts/font-faq)。
- 商業字型不能一律歸類為禁止；正確說法是「只有授權明確允許 Web self-hosting、格式轉換、
  subsetting 與預期流量／網域範圍時才能使用」。
- OdfKit 應提供授權 metadata、allowlist 與部署檢查點，但不能宣稱自動判定「100% 合法」，也
  不應隨 NuGet 套件直接打包字型，除非完成第三方授權與 NOTICE 流程。

### 4.8 效能與品質主張

下列數字在沒有可重現 benchmark 前都應移除：

- 固定 8 KB 或 5–10 KB 的 WOFF2 大小。
- 子集化小於 5 ms、MMF 可把工作壓到 1 ms。
- cache hit CPU 為 0。
- 任何平台無縫顯示。
- CLS 必定為 0。
- CDN 可使 origin CPU 與頻寬歸零。
- 只保留 GSUB／GPOS 即可完美支援所有複雜語系。

正確做法是建立 benchmark matrix，至少涵蓋：

- 全字庫、Noto／思源、Jigmo、IPAmjMincho 與可合法測試的 variable／color font。
- 1、10、100 與 1,000 個 unique glyph／cluster。
- cold／warm filesystem cache、單行程與多節點 cache miss。
- TTF／OTF／TTC、CFF／CFF2、variable font、IVS、emoji sequence、阿拉伯文與印度系文字。
- 產生時間、峰值工作集、配置量、輸出 bytes、cache bytes、shaping 正確性與瀏覽器載入結果。

Font metric overrides 可降低 layout shift，但需要從實際 primary 與 fallback font 量測比例，且
瀏覽器可能使用不同 metrics 來源。應把它列為選配最佳化，不宣稱固定為零位移。

## 5. 修正版技術方案

### 5.1 使用情境邊界

第一版只考慮下列情境：

> OdfKit 將 ODT／ODS／ODP／ODG 的文字內容輸出為同源 HTML；部署者已提供允許 Web
> self-hosting 與 subsetting 的字型；系統要讓指定 CNS／Unicode／PUA／IVS 字元在瀏覽器中
> 保持為文字並正確顯示。

不把下列項目列入第一版：

- 任意網站的全 DOM 自動掃描。
- 商業字型授權判定或字型市集。
- Web Forms 官方適配器。
- 任意語系、所有 OpenType table 與所有瀏覽器的完整保證。
- 20 萬同時上線的固定容量承諾。
- W3C IFT；待瀏覽器實作與工具鏈成熟後再評估。

### 5.2 建議資料流

1. HTML exporter 從 ODF run、font-face declaration 與文件內容建立字型需求 manifest。
2. 規劃器以字素叢集／variation sequence 為單位，對 allowlisted font face 查詢實際 glyph
   coverage；平面 mapping 只能提供候選字型順序。
3. canonicalizer 排序、去重並保存必要 sequence 與 shaping 設定，再計算 SHA-256 asset ID。
4. 受控背景工作以經驗證的子集引擎建立 WOFF2，保留必要 cmap、name、OS/2、layout、variation
   與 color closure。
5. 以獨立 verifier 重新開啟輸出字型，確認 table bounds、glyph coverage、metadata 與大小限制。
6. 將結果寫入 content-addressed durable store；公開 endpoint 只依 asset ID 回傳
   `font/woff2`、ETag 與長效 immutable cache header。
7. exporter 產生靜態 `@font-face` 與 `unicode-range` CSS。只有頁面內容在執行期變更時，才啟用
   選配的 client loader。

### 5.3 建議 API 原則

- `FontId` 是伺服器設定的 opaque ID，不是檔案路徑或用戶端任意 family name。
- 核心 request 以 Unicode scalar／sequence 模型表達，不以原始 query string 表達。
- 所有 I/O 與子集工作採 async API 並接受 `CancellationToken`。
- 明確區分 `PlanAsync`、`BuildAsync`、`OpenAssetAsync`，避免在公開 GET 要求中隱含執行昂貴編譯。
- cache 與 storage 由介面注入；不得把 `IMemoryCache` 寫死在演算法服務。
- 結果包含 font digest、engine version、輸出 media type、license attribution、supported ranges、
  warnings 與可重現 benchmark metadata。
- 錯誤採 typed result 或已在地化的公開例外；HTTP adapter 再轉成一致的 problem details。

### 5.4 子集引擎選型

可行路線包括 HarfBuzz subset API 加 WOFF2 encoder，或經隔離的外部工具。HarfBuzz 官方專案提供
`libharfbuzz-subset`，但目前 OdfKit 的 HarfBuzzSharp 只在 Imaging extension 用於 shaping／量測，
專案尚未證明其 .NET binding 與 native assets 足以涵蓋所需 subset 與 WOFF2 API：
[HarfBuzz](https://github.com/harfbuzz/harfbuzz)。

選型前必須驗證：

- 支援的 TTF／OTF／TTC／CFF／variable／color font 範圍。
- cmap 與 layout closure、IVS、name／license metadata 保留。
- WOFF2 encoder 的 native 平台矩陣、授權、AOT／trimming 與 NuGet 體積。
- 無效字型的錯誤隔離、取消能力與峰值記憶體。
- 是否能在 `netstandard2.0` 消費；若不能，不應為了形式一致而偽造雙 TFM 支援。

## 6. 分階段建議

### 階段 0：需求與樣本確認

先取得至少一個真實、可合法使用的 ODF→Web 情境，並建立匿名化 corpus。確認：

- 是 Unicode 已編碼字、IVS 還是 PUA。
- 預期瀏覽器、裝置、同時使用量與離線需求。
- 字型來源、版本、授權與 attribution 方式。
- 是否真的需要執行期動態產生；若字集可預知，建置期預產生通常更安全、便宜且容易 CDN 快取。

若沒有這些資料，停止，不建立新套件。

### 階段 1：非公開 PoC

- 實作一個字型、CJK 罕字與 IVS 的最小子集流程。
- 比較「每份文件預產生」、「固定 unicode-range 分片」與「要求時動態產生」三種策略。
- 驗證 Chrome、Edge、Firefox、Safari 的實際載入、複製、列印與 accessibility tree。
- 完成惡意輸入、cache key、隱私與字型授權測試。
- 產出可重現 benchmark，不更新正式效能敘事。

### 階段 2：先整合 HTML exporter

- 讓 HTML exporter 可選擇性保留 ODF run 的 font-family／font profile。
- 輸出字型需求 manifest 與 caller-provided asset resolver hook。
- 即使不發布動態服務，這個邊界仍可讓採用者接自己的靜態 WebFont/CDN。

### 階段 3：決定是否產品化

只有 PoC 通過下一節閘門，才建立正式 ASP.NET Core adapter。Web Forms 必須有獨立需求與維護者
承諾後再評估。

## 7. 正式發布閘門

以下條件全部滿足後，才能把功能列入正式套件：

1. 至少一個真實採用情境證明靜態字型資產不足以滿足需求。
2. 子集引擎有明確授權、跨平台 native asset 策略與維護來源。
3. CJK、PUA、IVS 與至少一個複雜 script 的 glyph／shaping golden tests 通過。
4. WOFF2 可被四個主要瀏覽器載入，且輸出經獨立 verifier 檢查。
5. 沒有原始文字出現在公開 URL、cache key、一般 access log 或 exception response。
6. 專用 cache 有 byte-based bound；多節點 single-flight、durable store 與失效策略經壓測。
7. rate limit、concurrency limit、timeout、request/output limit 與 negative caching 已驗證。
8. 每個測試字型都有 license manifest、attribution／OFL metadata 與禁止字型測試。
9. 效能數字由專案 benchmark 產生，並記載硬體、字型版本、字集與 cold/warm 條件。
10. 公開 API 文件、i18n、Public API baseline、雙 TFM／package validation 與安全文件符合專案閘門。

## 8. 已完成的最小實證

2026-07-15 已加入可重跑的非公開 smoke project：
[`OdfKit.WebFontSmoke`](../tests/OdfKit.WebFontSmoke/README.md)。它使用鎖定版本與 SHA-256 的
Noto Sans TC Sans2.004 變動字型作為 OFL-1.1 測試資料，並以鎖定版本的 FontTools 4.63.0
與 Brotli 1.2.0 產生 WOFF2。這些工具目前只屬於測試鏈，不代表正式套件已選定子集引擎。

實測鏈與結果如下：

1. 既有 CNS 11643 CI 下載腳本取得的官方對照表，確認測試字元分別來自 CNS 第
   3、4、5、6、7、10、11、12、15 字面。
2. 來源字型為 11,942,800 bytes，SHA-256 為
   `ac091cc8cd19e848202afc8fe6d3809b4526c8fdbdb4be82da20c4f785949591`。
3. 測試保留 Unicode Plane 0、1、2、3 的 13 個字元，產生 10,468-byte WOFF2；Python
   驗證輸出 `wOF2` 簽章、全部碼位的 cmap，且相同輸入連續兩次產生相同 SHA-256。
4. 最小 ASP.NET Core 應用程式以 `font/woff2` 提供字型，健康檢查與 HTTP 回應驗證通過。
5. 實際 Chromium 瀏覽器的 `FontFaceSet.check()` 回傳 true，頁面狀態為
   `PASS：瀏覽器已載入 WOFF2`；同頁也確認 OdfKit 保留 Plane 0 基礎字型，並將
   Plane 1、2、3 分別路由到設定的 WebFont family。

可在方案根目錄執行：

```powershell
pwsh eng/Test-WebFontSmoke.ps1
```

另以 2026-05-05 全字庫官方宋體包做唯讀實測。官方 ZIP
`Fonts_Sung.zip` 為 62,595,855 bytes，SHA-256 為
`25cb90ddf7c98bfeebd9e88a79c63dcde7eaaf81409a15b323ace744bade7867`，內容不是 TTC，
而是三個 TTF：

- `TW-Sung-98_1.ttf`：39,202 個 BMP cmap 碼位。
- `TW-Sung-Ext-B-98_1.ttf`：48,812 個 Plane 2 與 652 個 Plane 3 cmap 碼位。
- `TW-Sung-Plus-98_1.ttf`：24,980 個 Plane 15 PUA cmap 碼位，範圍為
  U+F0000～U+FFFFD。

將 Plus 字型中的 U+F0000、U+F0587、U+FFE39 裁成 WOFF2 後，輸出為 2,608 bytes、
`wOF2` 簽章、4 個 glyph，重新開啟後 cmap 三碼位均存在。這證明全字庫官方 Plane 15
自造字可以進入相同子集鏈，但 PUA 的文字語意仍須綁定全字庫版本、原始字型與對照表。
官方資料集明列字型包、政府資料開放授權第 1 版／OFL-1.1 擇一授權及顯名要求：
[全字庫政府開放資料](https://data.gov.tw/dataset/5961/)。

此機制本身不綁定臺灣。日本 MJ／IVS、香港 HKSCS、中國大陸 GB 18030 重指派、
韓國罕用漢字或其他文字，只要輸入字型具有合法可用的 cmap／OpenType 資料，就能沿用
「字元或序列規劃→glyph closure→WOFF2→瀏覽器驗證」流程；但 mapping、IVS collection、
shaping feature、fallback 與授權都必須各國／各資料集獨立測試。日本 IPA 的文字資訊基盤
即以約六萬個行政人名漢字處理互通需求：[IPA IMI 文字資訊基盤](https://imi.ipa.go.jp/)。

目前未在全字庫官方包、測試電腦字型目錄或專案中找到 `educ.ttc`。若實際指的是 Windows
`EUDC.TTE`，它是本機 End-User Defined Characters 字型，不等同全字庫公開 TTF；官方
全字庫軟體包說明也記載造字資料位於 `C:\CNSFonts`，並透過 EUDC／造字轉入流程安裝：
[全字庫軟體包使用說明](https://www.cns11643.gov.tw/files/files/readme-win.pdf)。
若 `educ.ttc` 是特定機關自行彙整的 TTC，必須取得實檔、face index、碼位對照與散布授權
才能測試，不能依檔名推定內容或合法性。

這項實證把結論從「依規格推定可行」提升為可由 GitHub Actions 重現的端到端鏈：Plane 0～3、
阿拉伯文 shaping、印度文 conjunct、香港 TTC face、香港 CFF、日文 IVS 與全字庫 Plane 15
PUA 共六組案例，輸出 WOFF2／WOFF／TTF／OTF 十一個資產，並由 Playwright Chromium 驗證
實際字型載入、IVS 像素差異與完整頁面截圖。它仍未證明 PUA 的跨系統語意、實際
`EUDC.TTE`／`educ.ttc`、CFF2／color font、四瀏覽器矩陣或任意不受信任字型安全，因此不能
把 experimental 套件描述成涵蓋所有字型的 production 服務。

## 9. 最終建議

原提案指出了一個真實問題，也選中了合理的 Web 技術方向，但目前把「概念可行」寫成了「已具備
production 完整度」，並將尚未實作或驗證的能力、效能與法遵結果當成既定事實。

目前決策應為：**技術可行，experimental 多套件實作與 minimal 端到端驗證已完成，但是否正式
產品化仍待人工與市場決策。**建置期預產生或固定 `unicode-range` 分片應維持預設；只有真實
動態內容與大字庫證明靜態方案不足時，才承擔公開動態子集服務的安全與維運成本。
