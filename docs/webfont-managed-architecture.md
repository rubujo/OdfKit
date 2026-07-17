# WebFont 純 .NET 架構契約

> 基準日期：2026-07-16
>
> 狀態：TrueType／WOFF／WOFF2 核心已實作；其餘能力依本文件閘門維持 experimental 或拒絕。

本文件取代以 FontTools 外部程序作為產品引擎的舊設計。OdfKit WebFonts 的產品路徑必須讓
只安裝受支援 .NET SDK／Runtime 與 NuGet 套件的乾淨 consumer 完成字型子集化、預產生與
有界動態產生；不得依賴 Python、Node.js、命令列字型工具、開發者 `PATH` 或 GitHub runner
預裝軟體。

這項限制不是宣稱 CLR 或所有 .NET Runtime 實作均由 managed IL 組成，而是界定 OdfKit
可控制的產品邊界：OdfKit 套件及其產品相依只包含 managed assemblies 與資料資產，不攜帶、
下載或呼叫額外 native 程式碼。BCL 內部實作細節須如實揭露，不得把「可由 C# 呼叫」誤寫為
「演算法由純 managed C# 實作」。

## 1. 不變量

1. `OdfKit.WebFonts.*` 的產品組件不得使用 `Process`、P/Invoke、`LibraryImport`、
   `NativeLibrary`、COM 或 shell 啟動字型引擎。
2. nupkg 不得包含 `runtimes/*/native`、`tools` 可執行檔、Python／Node module、第三方字型，
   也不得在 build、publish 或 request time 下載工具。
3. 所有產字能力由 C#／.NET API 完成。測試可用瀏覽器或獨立 validator 作 oracle，但該工具
   不得進入產品 dependency graph，也不得成為 clean consumer 成功的必要條件。
4. 子集化前先檢查字型的 embedding／subsetting 權限、來源 SHA-256 與 Profile 版本；限制、
   缺字、mapping 衝突、格式不支援或資料損毀一律回報明確錯誤，不得靜默 fallback。
5. 動態產生是主要使用情境，但資料平面仍以內容定址的不可變資產、durable cache 與 CDN
   為主；公開 HTTP request 同步執行無界工作不是預設。
6. 所有 WebFont 套件沿用根目錄共同版本、pack、Public API、文件、翻譯及 CI 機制，版本持續
   為 `0.0.1` 滾動更新，不建立第二套版本來源。

目前版本稽核結果：`OdfKit.WebFonts.Abstractions`、`Build`、`Data.SqlServer`、
`Encoding.Legacy`、`Hosting.AspNetCore`、`Hosting.SystemWeb`、`OpenType`、`Profiles`、`Windows`、`Worker`
及 `OdfKit.Extensions.Html.WebFonts` 均直接匯入 `eng/OdfKit.Package.props`；共同檔案設定
`Version=0.0.1`、Package Validation、snupkg 與 repository metadata。各專案沒有另設 WebFont
版本值，目前不需新增版本機制。後續 CI 應以 MSBuild evaluated property 及實際 nupkg metadata
持續驗證，避免只靠文字搜尋。

## 2. 授權準入

OdfKit 原創程式碼採 CC0-1.0。第三方程式碼不會因此改成 CC0，而是保留原授權、著作權與
notice；因此「可以安裝」不等於「適合成為 OdfKit 的預設相依」。新增套件前必須保存下列
可重現證據：

- 精確套件版本、NuGet SHA-512、來源 repository commit／tag 與授權全文 permalink。
- 直接與傳遞相依的 SPDX identifier、商業使用、修改、再散布及專利條款。
- 不得有營收、席次、用途、非商業、source-available 或另購商業授權等條件。
- nupkg 內容掃描證明沒有 native asset、工具下載 target 或未聲明的 bundled code。
- `THIRD-PARTY-NOTICES.md`、SBOM 與套件鎖定資料同步更新；版本變更須重新審查授權漂移。

目前候選裁定如下。這是工程準入判斷，不是對任何授權提供法律意見。

| 候選 | 第一方證據 | 裁定 |
| --- | --- | --- |
| SixLabors.Fonts 3.0.0 | 現行 Six Labors Split License 依使用者身分、營收及相依型態決定 Apache-2.0 或商業授權 | 不作產品相依；授權結果會因 consumer 條件而改變，也沒有可驗證的子集寫回契約 |
| OpenFontSharp 1.0.0 | NuGet 標示 MIT、純 C#，功能描述為讀取 TTF／OTF／TTC／WOFF／WOFF2 | 不採用；授權可接受，但沒有 production subset writer 證據，且只支援 `net8.0` |
| LayoutFarm Typography | repository 標示 MIT 並列多項來源授權，主要契約為 reader 與 glyph layout | 不直接複製或相依；缺少所需 subset writer，來源混合使 clean-room 邊界較難稽核 |
| FontTools／Brotli CLI | MIT，但需要 Python／外部工具 | 僅可作測試 oracle，不是產品或 build consumer 相依 |
| HarfBuzzSharp／SkiaSharp | managed binding 搭配 native runtime asset | 不得進入 WebFont 引擎；與其它明確標示 native 的 OdfKit 擴充套件分離 |
| Microsoft.Playwright | .NET client 為 MIT，瀏覽器／driver 是外部測試資產 | 僅限 browser smoke；不得出現在任何 WebFont 產品 nupkg |

在沒有同時滿足功能、維護、授權及 managed 邊界的現成套件前，子集 writer 採 clean-room
實作：只依 W3C、Unicode 與 Microsoft OpenType 規格撰寫原創 C#，不得從 FontTools、
FreeType、HarfBuzz、SixLabors 或其它實作移植程式碼。

可稽核的允許來源、禁止來源、黑箱 oracle 隔離及 fixture 政策見
[WebFont managed 引擎 Clean-room 來源紀錄](provenance/webfont-managed-clean-room.md)。

## 3. 格式與行為邊界

### 3.1 第一個可交付核心

- 輸入：TrueType outline 的 standalone sfnt，以及從 TTC 選定的一個 TrueType face。
- 輸出：standalone TTF、WOFF 1.0；`net10.0` 另提供 WOFF2。
- 字元：Unicode scalar、Supplementary Plane、PUA、IVS；`cmap` format 4／12／14。
- glyph closure：`.notdef`、要求字元及 TrueType composite component 的遞迴閉包。
- 法律資料：預設保留 `name` license description／URL、`OS/2` 與必要 metadata。
- 確定性：相同來源 bytes、face、Profile、sequence 與 options 產生完全相同 bytes 與 SHA-256。

初版保留原 glyph ID 與 `maxp.numGlyphs`，以相同 `loca` offset 清空未使用 outline。這可在不
重寫所有 layout table glyph reference 的前提下，移除 CJK outline 的主要體積。修改後必須重建
`glyf`、`loca`、`cmap`、table checksum、`head.checkSumAdjustment` 與 directory；`DSIG` 因
內容變更而移除。

### 3.2 必須拒絕的輸入

下列能力在有完整 parser、closure、writer 與瀏覽器證據前不得宣稱支援：

- CFF／CFF2 outline、OTC collection。
- variable font（包含 `fvar`／`gvar` 等 variation tables）。
- COLR／CPAL、CBDT／CBLC、`sbix`、SVG 或其它 color／bitmap font。
- AAT、Graphite，或需要尚未支援 shaping closure 的 script／feature。
- `OS/2.fsType` 禁止 embedding、禁止 subsetting 或只允許 bitmap embedding 的字型。
- table 越界／重疊、checked arithmetic 溢位、checksum 不符、重複必要 table、glyph cycle、
  超過設定上限或任何無法唯一解讀的輸入。

拒絕必須回報格式、table tag 與原因；不得刪除未知 table 後繼續產出。

### 3.3 shaping 策略

只依 `cmap` 收 glyph 不足以支援 Arabic、Devanagari 或任意 OpenType shaping。現有引擎會對
GSUB lookup 1／2／3／4／7／8 建立保守 glyph closure，並對 contextual 結構採有界驗證；這只
足以支援目前已驗證的 CNS direct-glyph 情境，不代表完整 complex-script shaping。

後續依 script／language／feature 實作 GSUB lookup closure，並涵蓋 contextual、ligature、
alternate、extension lookup 及 GDEF 關聯。GPOS 不產生新 glyph，但仍須驗證 coverage 與 glyph
reference。只有 managed closure、獨立 shaping oracle 與 Chromium／Firefox／WebKit golden
一致時，才將對應 script 從 experimental 升為已支援。

### 3.4 WOFF 與 WOFF2

WOFF 1.0 允許 table 保持未壓縮，因此第一版 writer 不需要額外 zlib 套件；後續壓縮只能使用
通過本文件授權與 managed 稽核的實作。

WOFF2 使用 Brotli。規格允許 `glyf`／`loca` 採 null transform，故第一版不必實作複雜的
glyf transform。`net10.0` 可透過 `System.IO.Compression.BrotliEncoder` 產生標準 Brotli
bitstream；但 .NET Runtime 官方來源顯示該 API 呼叫 runtime native encoder，文件與證據矩陣
必須標示為「沒有額外 native 產品相依」，不得標示為「Brotli 純 managed 實作」。

`netstandard2.0`／`net48` 第一階段只承諾 TTF／WOFF。若未來找到授權相容且可稽核的純
managed Brotli encoder，才可增加舊 TFM 的 WOFF2；不得為追求格式一致性而引入 native package。

WOFF 依 W3C WOFF 1.0 規則逐 table 使用 zlib；壓縮結果未小於原 table 時保留未壓縮 bytes。
WOFF2 目前維持規格允許的 `glyf`／`loca` null transform。W3C 規格建議在多個 transform 可用時
通常選擇較小者，但在 clean-room writer、獨立解碼驗證與真實 corpus 基準完成前，不實作或宣稱
支援 transformed `glyf`。第三方文章所稱固定壓縮百分比不得作為產品承諾。

IFT 的標準狀態、retain-gids 實證邊界與升級閘門見
[WebFont IFT 標準追蹤與相容性閘門](webfont-ift-tracking.md)。

## 4. 安全與資源模型

parser 使用 `ReadOnlyMemory<byte>`、`Span<T>` 與 big-endian `BinaryPrimitives`，所有 offset、
length、乘加及 alignment 採 checked arithmetic。預設上限至少涵蓋來源 bytes、table count、
glyph count、composite depth、sequence count、唯一 scalar 數、產出 bytes、工作逾時與並行數。

來源字型只能由部署端 `FontSourceId` allowlist 解析，不接受 request URL 或任意路徑。每次工作
均驗證來源 SHA-256、face index、Profile 版本、授權 policy 與 canonical request。動態 API
維持具名授權、具名 rate limiter、有界 Channel、single-flight、租戶配額及不可變 hash GET；
Worker 不需要也不得啟動隔離外部程序。

多節點只在 object store、具 fencing／ownership token 的 durable coordination、失敗接手與
重複執行測試均可重現後才宣稱支援。單機檔案 lease 只能標示 experimental。

## 5. Phase 0～5 執行與驗收

| Phase | 實作內容 | 升級為已完成的必要證據 |
| --- | --- | --- |
| 0 契約與治理 | 移除 FontTools 產品 API；建立 neutral engine、版本化 Profile、CNS provider、license policy 與 managed guard | 所有 WebFont csproj 繼承共同 `0.0.1`／pack；clean package scan；合法 corpus 均有 URI、版本、SHA-256、授權與不散布裁定 |
| 1 managed engine | 有界 sfnt／TTC parser、TrueType composite closure、`cmap` 4／12／14、TTF writer、`fsType` 與格式拒絕 | C# 生成的最小 fixtures、真實 CNS／多 Plane／IVS／PUA；checksum round-trip、mutation／fuzz、雙 TFM build；不支援矩陣逐項有負向測試 |
| 2 build 與格式 | WOFF writer、net10 WOFF2 null transform、CLI／MSBuild、manifest、CSS／HTML integration 與一致 hash | 無 Python／Node 的 pack consumer 完成 TTF／WOFF／WOFF2；重複建置 byte-identical；Chromium／Firefox／WebKit 載入與截圖 artifact |
| 3 Web 託管 | ASP.NET Core 少量設定的 CNS Profile、受控 dynamic endpoint、durable cache；Web Forms config／handler 與離線預產生 | 真實 HTTP auth／429／hash GET／CSP／CORS；net48 consumer；256 並行 GET、同鍵 single-flight 與 process restart 復原 |
| 4 closure 與規模 | 逐 lookup 增加 GSUB closure、明確 script 能力；有界多節點介面、load 與 deterministic mutation fuzz | 每個新增 script 具 managed closure、獨立 oracle 與三瀏覽器 golden；跨節點只在本機／CI 可重現時啟用，否則保留閘門 |
| 5 產品化 | NuGet／DocFX／Public API／SBOM／授權漂移／安全與證據矩陣；人工發布決策 | 同一批 nupkg 通過 net10、netstandard2.0、net48 consumer；無 native／tool／process path；外部安全與法律審查、真實客戶 corpus 與容量驗收仍分開標示 |

Phase 是能力閘門，不是日期。不得因已存在 API、mock engine 或測試工具成功就跳過前一階段。

## 6. CI 的真實性要求

產品能力至少由下列互相獨立的檢查證明：

1. 乾淨 consumer 只 restore OdfKit nupkg 與支援的 .NET SDK；restore 後斷網、清空工具 `PATH`，
   實際產生並讀回字型。
2. 靜態掃描全部產品 nupkg 與 assembly：禁止 native／tools asset，以及 P/Invoke、
   `System.Diagnostics.Process`、Python／Node 字串或下載 target。
3. managed verifier 重新解析輸出並檢查 table bounds、checksum、cmap、glyph closure、Profile、
   manifest 與 SHA-256；測試不能只檢查 magic number。
4. Playwright 在 Chromium、Firefox、WebKit 實際載入，檢查 `document.fonts`、要求 sequence 的
   glyph 呈現、console／network error，並上傳 HTML、字型、manifest 與完整截圖。
5. 第三方 validator／FontTools 若使用，只是獨立 oracle job；該 job 失敗不得被誤解為 consumer
   需要 Python，也不能取代 managed verifier。
6. corpus 包含 CNS 真實資料、多國文字、Plane 0～3、IVS、PUA、TTF／TTC，以及所有拒絕格式；
   外部檔案下載前後都驗證 SHA-256，不把無散布權字型放入 repository 或 nupkg。
7. parser 執行 deterministic mutation、截斷、offset overflow、table overlap、composite cycle、
   cancellation、併發與資源上限測試；錯誤不得造成 hang、unbounded allocation 或部分成功資產。

## 7. 已完成的遷移與剩餘閘門

`FontToolsWebFontSubsetEngine`、其 options 與 Python 安裝 smoke 已自產品及編譯測試路徑移除。
目前實作包含：

- Managed OpenType 公開 API 與 net10／netstandard2.0 Public API 基線。
- 中性的 Abstractions、Profile、CNS mapping、manifest、Build、Hosting 與 Worker。
- 只執行 `dotnet` 的雙 process smoke、真實 WOFF2 verifier、失敗接手與 HTTP 安全驗證。
- 官方 CNS Ext-B 真字型與 Chromium／Firefox／WebKit 截圖證據。
- 真實 CNS PUA、IPAmj IVS、雙 CNS face TTC，以及 CFF／CFF2／variable／color 負向格式矩陣。
- 真實來源字型與 TTF／WOFF／WOFF2 共 448 組固定種子 mutation verifier 測試。
- 同批 `0.0.1` nupkg 安裝的 library 與 dotnet tool clean consumer，以真實 CNS 字型完成三格式產字與 byte-identical 重建。
- 同批 WebFont nupkg 的 SPDX 2.3 SBOM、SHA-256、完整 NuGet 相依版本與 nuspec 授權漂移閘門。

剩餘工作以第 5、6 節的來源字型 fuzz、complex-script shaping 廣度與外部人工閘門為準；
不得因上述核心可用而把整套產品標示 production-ready。

## 8. 第一方依據

- [Microsoft OpenType 1.9.1](https://learn.microsoft.com/en-us/typography/opentype/spec/)
- [OpenType font file 與 checksum](https://learn.microsoft.com/en-us/typography/opentype/spec/otff)
- [OpenType `cmap`](https://learn.microsoft.com/en-us/typography/opentype/spec/cmap)
- [OpenType `glyf`](https://learn.microsoft.com/en-us/typography/opentype/spec/glyf)
- [OpenType `OS/2.fsType`](https://learn.microsoft.com/en-us/typography/opentype/spec/os2)
- [W3C WOFF 1.0](https://www.w3.org/TR/WOFF/)
- [W3C WOFF 2.0](https://www.w3.org/TR/WOFF2/)
- [Unicode 17.0.0](https://www.unicode.org/versions/Unicode17.0.0/)
- [Unicode Ideographic Variation Database](https://www.unicode.org/ivd/)
- [Unicode UTS #37](https://www.unicode.org/reports/tr37/)
- [.NET `BrotliEncoder`](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.brotliencoder?view=net-10.0)
- [.NET Runtime `BrotliEncoder` 原始碼](https://github.com/dotnet/runtime/blob/main/src/libraries/System.IO.Compression.Brotli/src/System/IO/Compression/enc/BrotliEncoder.cs)
- [SixLabors.Fonts 現行授權](https://github.com/SixLabors/Fonts/blob/main/LICENSE)
- [OpenFontSharp 1.0.0 NuGet](https://www.nuget.org/packages/OpenFontSharp/1.0.0)
- [LayoutFarm Typography repository](https://github.com/LayoutFarm/Typography)
- [Microsoft.Playwright .NET repository 與 MIT 授權](https://github.com/microsoft/playwright-dotnet)
- [NuGet `.nuspec` license metadata](https://learn.microsoft.com/en-us/nuget/reference/nuspec#license)
- [SPDX 2.3 specification](https://spdx.github.io/spdx-spec/v2.3/)
- [GitHub SBOM 匯出與 Actions 指引](https://docs.github.com/en/code-security/how-tos/secure-your-supply-chain/establish-provenance-and-integrity/export-dependencies-as-sbom)
