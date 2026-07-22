# WebFont 純 .NET 架構契約

> 基準日期：2026-07-16
>
> 狀態：TrueType／OpenType collection、WOFF／WOFF2 輸入正規化與瀏覽器格式輸出已實作；
> 各 outline、layout、variation 與 color table 能力依本文件閘門維持 experimental 或拒絕。

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
notice；因此「可以安裝」不等於「適合成為 OdfKit 的預設相依」。新增套件前必須保留下列
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

### 3.1 輸入與輸出契約

- 輸入是來源容器：standalone TTF／OTF、TTC／OTC 的指定 face、Windows EUDC `.tte`，以及
  可安全展開的 standalone WOFF；執行期具 Brotli 時另接受 null 或標準 transformed-table 的 standalone
  WOFF2，並以 experimental 路徑接受 WOFF2 collection 的指定 face。來源副檔名不作
  信任依據，均以 signature、table directory、checksum、展開上限與 `faceIndex` 驗證。
- 輸出是瀏覽器部署資產：獨立 TTF／OTF、WOFF 1.0；執行期具 Brotli 時另提供 WOFF2。TTC／OTC
  只作輸入，不作產品輸出；每次先抽出指定 face，再產生內容定址的獨立 WebFont。
- 一般工具產生的 WOFF2 可使用 `glyf`／`loca` version 0 或 `hmtx` version 1 transform；目前
  clean-room decoder 已有界重建上述標準 transform。WOFF2 collection 另解析 collection directory、
  每個 face 的全域 table index，以及共享 transformed `glyf`／`loca` 配對；未知 transform version
  仍須明確拒絕，不能由已支援路徑推定為任意 WOFF2 支援。
- 字元：Unicode scalar、Supplementary Plane、PUA、IVS；`cmap` format 4／12／14。輸出的
  format 4 會將 `idDelta` 相同的相鄰碼位合併為單一 segment，使大型 CJK 字集遠低於該格式
  16-bit 的 `length` 上限；仍無法表示的極端稀疏字集依 OpenType 1.9.1「format 12 存在時
  format 4 為相容性選配」省略 format 4，不得因此中止產生。encoding record 依規格先以
  platformID、再以 encodingID 排序。
- glyph closure：`.notdef`、要求字元及 TrueType composite component 的遞迴閉包。
- 法律資料：預設保留 `name` license description／URL、`OS/2` 與必要 metadata。
- 確定性：相同來源 bytes、face、Profile、sequence 與 options 產生完全相同 bytes 與 SHA-256。

「所有可用輸入」以能由公開規格安全正規化為 sfnt face 為邊界，不等於接受任意歷史或私有字型
封裝：

| 輸入族群 | 狀態 | 產品輸出 |
| --- | --- | --- |
| TTF／OTF standalone | 已實作 | 依 outline 產生 TTF 或 OTF，另可產生 WOFF／WOFF2 |
| TTC／OTC | 已實作，指定 `faceIndex` | 抽出單一 face 後產生獨立瀏覽器資產 |
| Windows EUDC `.tte` | 已實作，內容仍須為合法 sfnt | 產生獨立 TTF／WOFF；執行期具 Brotli 時可產生 WOFF2 |
| WOFF 1.0 standalone | 已實作有界 zlib 展開 | 重新子集化後產生獨立瀏覽器資產 |
| WOFF2 standalone | 執行期具 Brotli 時已實作 null transform、`glyf`／`loca` v0 與 `hmtx` v1 有界反轉換 | 重新子集化後產生獨立瀏覽器資產 |
| WOFF2 collection | 執行期具 Brotli 時已實作；指定 `faceIndex`，有界解析 collection directory、共享 transformed `glyf`／`loca` 配對與 `hmtx`；以 W3C 官方多 face corpus 逐表比對 reference | 抽出指定 face，重新子集化為獨立瀏覽器資產；不直接輸出 collection |
| TrueType／CFF／CFF2 variable，以及省略 VariationStore 的非變動 CFF2 | 已實作有界 correctness-first 路徑；只承諾鎖定 corpus 與已驗證 operator | 保留必要 metadata 的獨立資產 |
| COLR／CPAL、CBDT／CBLC、EBDT／EBLC、SVG、sbix | 已實作有界 correctness-first 路徑；color 來源必須鎖定 SHA-256 | 保留 color table 的獨立資產；實際可部署性依瀏覽器模型矩陣 |
| Type 1 PFA／PFB、bare CFF／CFF2、Mac suitcase／dfont、EOT、SVG Fonts | 非現代 sfnt WebFont 輸入，明確拒絕 | 無；不得以副檔名猜測或靜默 fallback |

初版保留原 glyph ID 與 `maxp.numGlyphs`，以相同 `loca` offset 清空未使用 outline。這可在不
重寫所有 layout table glyph reference 的前提下，移除 CJK outline 的主要體積。修改後必須重建
`glyf`、`loca`、`cmap`、table checksum、`head.checkSumAdjustment` 與 directory；`DSIG` 因
內容變更而移除。

### 3.2 必須拒絕的輸入

下列能力在有完整 parser、closure、writer 與瀏覽器證據前不得宣稱支援：

- 名稱式 CFF 的 `seac` 只依第 3.5 節的 StandardEncoding／charset closure 邊界解封；找不到
  base／accent 元件、元件代碼不是 0～255 整數或巢狀 `seac` 必須拒絕。名稱式與 standalone／OTC
  face 的 CID-keyed 靜態 CFF 1.0，以及含 VariationStore 的 CFF2 variable 僅依第 3.5 節的
  experimental 邊界解封。TTC／OTC／WOFF2 collection 只作輸入；直接 collection 輸出不在
  瀏覽器資產產品契約內，也不是完成條件。
- 尚未通過第 3.5 節證據閘門的 variable font；缺少 VariationStore 卻使用 `vsindex`／`blend`，
  或 VariationStore／`fvar` 不一致的 CFF2 維持拒絕。
- 尚未通過第 3.6 節結構驗證的 color table 版本或組合。
- AAT layout 的 `morx`／`mort`／`kerx` 與 Graphite 的 `Silf`／`Glat`／`Gloc`／`Feat`／`Sill`
  一律明確拒絕；不得把保留 ancillary table 說成 AAT／Graphite shaping 支援。需要尚未驗證
  shaping closure 的 script／feature 亦維持拒絕。
- `OS/2.fsType` 禁止 embedding、禁止 subsetting 或只允許 bitmap embedding 的字型。
- table 越界／重疊、checked arithmetic 溢位、checksum 不符、重複必要 table、glyph cycle、
  超過設定上限或任何無法唯一解讀的輸入。

拒絕必須回報格式、table tag 與原因；不得刪除未知 table 後繼續產出。

### 3.3 shaping 策略

只依 `cmap` 收 glyph 不足以支援 Arabic、Devanagari 或任意 OpenType shaping。現有引擎會對
GSUB lookup 1／2／3／4／7／8 建立保守 glyph closure，並對 contextual 結構採有界驗證，供 CNS
direct-glyph 與可證明 closure 完整的情境使用。對
Arabic／Devanagari／Bengali／Khmer／Thai，現階段改採
correctness-first 路徑：保留完整 glyph ID space、`cmap`、GDEF、GPOS 與 GSUB，且來源與輸出的
layout tables 必須 byte-identical，不做 aggressive glyph pruning 或 lookup 重寫。鎖定的 Noto
字型已在 Chromium、Firefox 與 WebKit 通過逐 RGBA byte、文字 metrics 與 variable axis DOM
差分。

這項證據只涵蓋目前鎖定的 Arabic／Devanagari／Bengali／Khmer／Thai corpus，不代表任意
complex-script shaping。

**廣度擴充沒有終點，但每一步有固定程序。** 本文件未訂定目標 script 清單，因此
「complex-script 廣度」是可持續擴充的能力而非有完成狀態的任務。新增單一 script 需要
下列四處協調修改：

1. `eng/external-tools.json`：加入鎖定字型的 URI、版本、授權與 SHA-256。
2. `eng/Test-WebFontFormatMatrix.ps1`：產生該 script 的子集 evidence 目錄。
3. `eng/Test-WebFontLayoutBrowserSmoke.ps1`：宣告 `$<name>Source` 與 `$<name>Subsets`，
   並於 `Invoke-LayoutBrowserSmoke` 的引數陣列追加 (source, subset) 配對。
4. `tests/OdfKit.WebFontBrowserSmoke/LayoutBrowserSmoke.cs`：消費新增的配對。

**已處理的阻力**：第 3 與第 4 步之間原為**跨語言的位置式契約**——37 個扁平引數，
PowerShell 的追加順序必須與 C# 的讀取順序精確對齊，任一方漏改不會產生編譯或執行錯誤，
而是靜默載入到別的字型，證據看起來仍是綠的。此契約已改為 `name=path` 具名形式：缺漏或
拼錯立即以 `Missing layout argument: <name>` 失敗，重複鍵亦被攔下。驗證方式為先以位置式
版本建立三瀏覽器通過基線，改動後重跑同一閘門比對，全數逐像素一致。

**仍存在的成本**：新增 script 仍需四處修改，因為 C# 端保有逐 script 的專屬內容（證據
欄位、case id，以及 `@font-face` 的逐字型 variable 軸範圍如 `font-weight: 100 900`）。
將這些抽成 case descriptor 以達成「一處設定修改」是獨立的後續工作，涉及該檔案 851 行的
資料流重構；其驗證前提是先完整執行 format matrix 以產生 evidence 目錄。
其它 script／language／feature 必須先擴充 GSUB closure 或採相同 correctness-first 路徑，驗證
contextual、ligature、alternate、extension lookup、GDEF 關聯及 GPOS glyph reference；只有
managed 結構驗證、合法 corpus 與 Chromium／Firefox／WebKit golden 一致時，才能新增支援聲明。

### 3.4 WOFF 與 WOFF2

WOFF 1.0 允許 table 保持未壓縮，因此第一版 writer 不需要額外 zlib 套件；後續壓縮只能使用
通過本文件授權與 managed 稽核的實作。

WOFF2 使用 Brotli。`net10.0` 資產直接透過 `System.IO.Compression.BrotliEncoder`／
`BrotliDecoder`；`netstandard2.0` 資產不以編譯 TFM 推斷功能，而是在執行時偵測
`System.IO.Compression.BrotliStream`。因此同一份 `netstandard2.0` 資產在 .NET 8 Runtime
可完整產生及讀取 WOFF2，在未提供該型別的 net48 Runtime 則明確拒絕。公開能力可由
`WebFontRuntimeCapabilities.IsWoff2Available` 查詢。

.NET Runtime 官方來源顯示上述 API 呼叫 runtime native encoder，文件與證據矩陣必須標示為
「沒有額外 native 產品相依」，不得標示為「Brotli 純 managed 實作」。OdfKit 不為舊 Runtime
引入額外 native package。

**2026-07-20 候選調查結論：閘門維持關閉。** 目前唯一符合「純 managed C#＋encoder」的
現成套件是 [BrotliSharpLib](https://www.nuget.org/packages/BrotliSharpLib/)（MIT、
完整 C# 移植、支援 .NET Standard 1.1 起）。功能、授權與 managed 邊界三項皆符合，但
**維護一項不成立**：最新版 `0.3.3` 發布於 2019-02-06，逾七年無更新，因而不含
`google/brotli` 其後的修正。第 2 節要求候選須「同時滿足功能、維護、授權及 managed
邊界」，故不採用。壓縮器位於字型產生管線上，若移植碼日後出現漏洞將無上游修補可循，
這使維護落差成為安全風險而非單純的新鮮度問題。

此缺口現在是 Runtime 能力缺口，不再是 `netstandard2.0` 編譯資產缺口。net48 若未提供
`BrotliStream`，仍可由現代 .NET 於建置期預產生 WOFF2 資產，再以靜態內容定址路徑供應
（見第 5 節 Phase 3 的「Web Forms config／handler 與離線預產生」）。

WOFF 依 W3C WOFF 1.0 規則逐 table 使用 zlib；壓縮結果未小於原 table 時保留未壓縮 bytes。
WOFF2 writer 目前維持規格允許的 `glyf`／`loca` null transform；decoder 則依 W3C WOFF2
Recommendation clean-room 實作 `glyf`／`loca` version 0 與 `hmtx` version 1 反轉換。重建採
checked arithmetic、分 stream 精確消耗、輸出上限、simple／composite glyph、bbox、instructions、
triplet、short／long `loca` 與 bearing 驗證。鎖定 W3C corpus 及 Google Fonts Noto Sans v42
Latin／Devanagari production WOFF2 會在 CI 產生 JSON 證據；synthetic 負向測試另涵蓋 reserved
flags、尾端資料、缺少依存 table 與 composite bbox。第三方文章所稱固定壓縮百分比不得作為
產品承諾。

IFT 的標準狀態、retain-gids 實證邊界與升級閘門見
[WebFont IFT 標準追蹤與相容性閘門](webfont-ift-tracking.md)。

### 3.5 CFF 與 Variable Fonts 分階段解封

這兩類能力採獨立閘門，不因其中一項完成而連帶解封另一項：

1. **TrueType Variable Fonts**：retain-GIDs 路徑重建 `gvar` 的 glyph data offsets，未保留
   GID 使用相鄰相同 offset 表示零長度 variation data；short offsets 依規格以實際位移除以 2
   編碼，long offsets 使用 32-bit 位移。`fvar` 與 `gvar` 必須成對存在且 axis count 一致；
   `avar`、`STAT`、`HVAR`、`VVAR`、`MVAR` 與 `cvar` 在 GID 不變的前提下原樣保留。正式能力
   Source Han 與 Noto Arabic／Devanagari／Bengali／Khmer／Thai 的 short／long offset、
   `wdth`／`wght` 三瀏覽器 DOM
   截圖與 Canvas 差分及 mutation 已通過；只對鎖定格式矩陣作有界承諾。
2. **靜態 CFF 1.0**：已解封 standalone／OTC face、含 ROS／FDArray／FDSelect 的 CID-keyed
   `OTTO`，以及不含這三個 CID operator 的名稱式 CFF。有界 parser 驗證 collection 絕對 table
   offset、CFF INDEX、Top DICT、Font DICT、Private DICT、local Subrs、預定義／自訂 charset 與
   FDSelect；解析快取同時核對 glyph count。CharStrings 採 retain-GIDs，未選 glyph 縮成單一
   `endchar`；兩趟 relocation 以固定 32-bit DICT offset 重建 Top DICT、FDArray、Private 與
   local Subrs 相對位置，global／local subroutine bytes 不剪枝。名稱式 `seac` 會解析 Type 2
   `endchar` 的 StandardEncoding base／accent code，經 ISOAdobe／Expert／ExpertSubset 或自訂 charset
   找回元件 GID 並納入保留集合；找不到元件、非整數／超界代碼與規格禁止的巢狀組字明確拒絕。
   Compact INDEX／DICT 與 subroutine 重寫須另有結構與效能證據。
3. **Subroutine 剪枝**：**已實作**。基準先行完成並滿足閘門條件，實作隨後落地。

   量測對象為 Source Han Sans TC `2.005R` 產生的 4 字元子集 OTF（CFF 表 1,559,452 bytes）：

   | 項目 | bytes | 佔 CFF |
   | --- | --- | --- |
   | local subroutines（跨所有 Font DICT） | 1,283,669 | 82% |
   | global subroutines | 9,795 | 0.6% |
   | CharStrings INDEX（65,535 個保留槽） | 263,845 | 17% |

   local subroutine 體積**與子集大小完全無關**：4 字元、4,608 字元與 9,000 字元三種子集
   量到的都是同一個 1,283,669 bytes，因為目前保留完整 subroutine bytes 不剪枝。對「每次
   請求產生小字集」的動態情境而言，這是純固定成本。

   關鍵問題是 WOFF2 的 Brotli 是否已經吸收掉這部分。答案是否定的：

   | | Brotli 後 |
   | --- | --- |
   | 完整 CFF | 1,021,537 |
   | 移除 local subroutines | 272,801 |
   | **Brotli 之外的額外收益** | **748,736（73.3%）** |

   量測方式為解析產出 OTF 的 CFF 結構，取出各 Font DICT 的 local Subrs INDEX 區段，
   比較完整與移除後的 Brotli（`CompressionLevel.SmallestSize`）體積。

   **實作方式**：不重編號。callsubr 的運算元已烘入 charstring，改變 INDEX 項目數會連帶
   改變 bias 而使既有運算元全部失效。因此保留 INDEX 項目數不變，僅將未使用的 subroutine
   本體替換為單一 `return`（op 11），與現行 CharStrings 對未選字圖改用單一 `endchar` 的
   作法一致。global 與各 Font DICT 的 local Subrs 皆適用；seac 元件的 subroutine 一併納入
   使用集合。

   此替換**可證明安全**：`Type2CharStringVerifier` 對動態計算的 subroutine 索引一律拒絕
   （運算元非靜態常數時 `Pop` 回傳 unknown，隨即拋出 `CFF-Subrs-index`），因此靜態可達性
   等同實際可達性，而該 verifier 已在子集化過程中走訪每個保留字圖的全部 callsubr。

   實測效果（Source Han Sans TC `2.005R`）：

   | 案例 | 格式 | 剪枝前 | 剪枝後 | 縮減 |
   | --- | --- | --- | --- | --- |
   | 4 字元 | WOFF2 | 1,166,964 | 281,708 | −75.9% |
   | 4 字元 | WOFF | 1,557,624 | 490,472 | −68.5% |
   | 4,608 字元 | WOFF2 | 2,537,292 | 1,778,384 | −29.9% |

   驗證涵蓋三層：OTS 差分預言機（9 個資產全數通過）、Chromium／Firefox／WebKit 逐字圖
   canvas 墨跡檢查（9 個組合全數通過），以及 OTS oracle 內的體積上界斷言。第三項為必要
   成分——前兩者只驗證正確性，剪枝若失效會產出「合法但臃腫」的字型，兩者都不會攔下。
4. **CFF2／PostScript Variable Fonts**：已解封 standalone／OTC face、含 `fvar`／VariationStore
   的 variable `OTTO`。有界 parser 驗證 collection 絕對 table offset、32-bit INDEX、
   Top／Font／Private DICT、FDSelect
   0／3／4、VariationRegion、ItemVariationData、`vsindex`、`blend` 與 subroutine；未選 glyph
   以規格允許的零長度 CharString 取代，再以兩趟 relocation 重建 32-bit INDEX、Top／Font／Private
   DICT、Header length 與 local Subrs 相對 offset；variation metadata 原樣保留。Source Han Sans 2.005R
   已在三瀏覽器以 300／500／700 `wght` 座標完成來源／subset DOM 截圖逐 byte 差分。Microsoft
   OpenType 1.9.1 明定非變動 CFF2 必須省略 VariationStore；此結構已由有界規格 fixture 與
   Apache-2.0 AFDKO `regular_CFF2.otf` 的三瀏覽器證據解封。缺少 VariationStore
   卻使用 `vsindex`／`blend`、VariationStore／`fvar` 不一致、超出資源上限的 INDEX／region 與
   無法唯一驗證的 operator 仍明確拒絕。

OTC 瀏覽器差分不假設所有引擎都接受 raw collection：Chromium 直接比較 raw OTC 與 managed
WOFF2；Firefox／WebKit 以同一 face 的 managed standalone OTF 對 WOFF2。如此三個引擎都實際
驗證獨立部署資產，raw `format(collection)` 則只列為 Chromium 能力證據。

### 3.6 Color font correctness-first 路徑

OpenType 1.9.1 定義的 color 輸入族群為 COLR／CPAL v0／v1、CBDT／CBLC、EBDT／EBLC、
`sbix` 與 `SVG `。目前 managed parser 會驗證成對表格、版本、計數、offset、strike／document
範圍與 glyph ID。COLRv0 layer 與 COLRv1 全部 32 種 paint 會建立有界 DAG，巡訪 layer list、
`PaintGlyph` 與 `PaintColrGlyph`，拒絕循環、未知格式、超深 graph、非法 palette／clip／offset；
`sbix dupe` 亦建立有界 glyph closure 並拒絕循環及非 OpenType 圖片類型。SVG document 不跨 glyph
引用 outline，bitmap location table 也不建立跨 glyph outline 關係，因此這兩類只保留要求 glyph
的 fallback outline。所有路徑維持原始 glyph ID 編號及 color tables，只縮減對外 `cmap` 與未被
closure 觸及的 outline；不宣稱已完成 aggressive color-table pruning。

EBDT／EBLC 與 `SVG ` 的支援層級是安全保留，不是細粒度 subsetting。前者驗證 bitmap
location／data 配對、strike、index、image format 與資料範圍，但不逐 strike／glyph 重編；後者
驗證 document index、glyph range、壓縮上限與 XML 主動內容，但不裁切 SVG document。兩者均保留
完整 color table，避免產生引用不一致的資產。

每一種 color 模型只有在可再散布且鎖定 SHA-256 的真實 corpus 通過 TTF／OTF、WOFF、WOFF2
managed verifier，並於至少一個實際支援該模型的瀏覽器完成來源／subset 彩色像素差分後，才可
列入 `RequiredBrowserTargets` 相容矩陣。EBDT／EBLC 目前只有產生式結構測試，不在任何瀏覽器
目標的已驗證集合。瀏覽器不支援的模型須記錄為 `browser-unavailable`，不得以相同空白畫布
視為通過；跨瀏覽器部署若需要 color table 轉碼，必須另建 writer、授權與像素證據閘門。未知版本、缺少
CPAL 或 bitmap location／data 配對、越界頂層 paint root／document／strike，以及任何無法唯一
驗證的組合均明確拒絕。SVG document 另維持禁止 DTD、script、外部參照、互動及主動內容的安全
邊界。完成各格式真實 corpus 與採用者額外安全稽核前，不得宣稱可接受任意不受信任 color font。

目前真實 color corpus 另鎖定 [Google Color Fonts](https://github.com/googlefonts/color-fonts)
commit `0046ea4c3b69e9fbbe464c2594816894e3aa5e4b` 的 Apache-2.0
`samples-sbix.ttf`（SHA-256
`0fd0a23379b0e982db8bef5f9a50cf7960a6ee3504778a9a9c039bde4d2f573d`）與
`samples-picosvg.ttf`（SHA-256
`c55758a47ce0c0493eed2ba4a7ec131eed44649ab38a22ce318371427f841470`）。Chromium 的 `sbix`、
Firefox 的 OpenType SVG，以及三瀏覽器 COLRv1 必須含非灰階像素且來源／managed WOFF2
逐 RGBA byte 相同；其它組合只記錄瀏覽器不可用，不形成虛假綠燈。

公開要求可用 `RequiredBrowserTargets` 將這份鎖定的實證矩陣提升為產生前閘門。Chromium
目前接受 COLR v0／v1 與 `sbix`，Firefox 接受 COLR v0／v1 與 OpenType SVG，Playwright
WebKit 接受 COLR v0／v1。保留的 color 技術若超出任一必要目標的集合，即明確拒絕且不寫檔；
目標集合也必須進入 single-flight 與 durable cache key，避免嚴格要求誤用寬鬆要求的結果。
空目標集合只表示未要求引擎替部署者判定瀏覽器，不表示所有瀏覽器皆相容。這個閘門不進行
`sbix`／SVG／bitmap／COLR 互轉，也不把 Playwright WebKit 證據推論為 Safari 實機證據。

真實 corpus 鎖定 Adobe Source Han Sans 官方 `2.005R` 單檔，不把字型納入 repository 或
nupkg：`SourceHanSansTC-Regular.otf`、`SourceHanSansTW-VF.ttf` 與
`SourceHanSansTW-VF.otf` 皆記錄來源 URI、SHA-256 與 OFL-1.1。相較下載大型 release zip，官方
tag 的單檔可減少 CI 傳輸量；下載後仍須驗證完整檔案 SHA-256。

第一方規格入口：[Adobe CFF TN #5176](https://adobe-type-tools.github.io/font-tech-notes/pdfs/5176.CFF.pdf)、
[Adobe Type 2 TN #5177](https://adobe-type-tools.github.io/font-tech-notes/pdfs/5177.Type2.pdf)、
[Microsoft `gvar`](https://learn.microsoft.com/en-us/typography/opentype/spec/gvar)、
[Microsoft CFF2](https://learn.microsoft.com/en-us/typography/opentype/spec/cff2)、
[W3C WOFF2](https://www.w3.org/TR/WOFF2/)。實作不得參考 FontTools、HarfBuzz、FreeType 或其它
subset compiler 原始碼；第三方工具只能放在隔離 oracle job。

## 4. 安全與資源模型

parser 使用 `ReadOnlyMemory<byte>`、`Span<T>` 與 big-endian `BinaryPrimitives`，所有 offset、
length、乘加及 alignment 採 checked arithmetic。預設上限至少涵蓋來源 bytes、table count、
glyph count、composite depth、sequence count、唯一 scalar 數、產出 bytes、工作逾時與並行數。

工作逾時要能成立，取消權杖必須抵達實際耗用 CPU 的迴圈，而非只在工作邊界檢查。
`CancellationToken` 因此貫穿 `SfntFont.Parse`／`CreateSubset`、`cmap` format 4／12／14 解析、
GSUB 與 composite closure、CFF／CFF2 subsetter 與 compactor、`gvar` 子集化及 WOFF2 `glyf`
重建，並在每個字圖級迴圈檢查。若取消權杖僅止於格式迴圈，`WebFontGenerationWorker` 的
`JobTimeout` 會如期觸發卻無人觀察，單一惡意或損毀字型即可永久占住 consumer 執行緒。
`ColorFontValidator` 僅在各色彩技術階段之間檢查取消，依據是其所有圖狀巡訪都有記憶化
（COLR paint 的 visit 狀態與 dependency 快取、`sbix dupe` 閉包、`ColorGlyphClosure`），
每個節點至多處理一次。

記憶化是這裡的關鍵，位元組範圍檢查本身並不足夠。CharString 直譯器即為反例：它限制遞迴
深度（10）卻不限制每層呼叫廣度，也沒有記憶化，巢狀 subroutine 因而可產生 breadth^depth
的展開——約 600 位元組的 subroutine 資料即足以造成實質無限的執行時間，且會繞過逐字圖
層級的取消檢查。因此兩個 CharString 直譯器另設一百萬次的總操作預算。

通則：**遞迴深度上限不等於工作量上限**。任何遞迴或圖狀巡訪都必須指出其有界性來自
記憶化、總量預算或可證明的線性消耗，不得只以「有深度上限」或「有範圍檢查」帶過。

依規格上界推導迴圈次數是必要步驟，不能只確認索引不越界。`cmap` format 4 的 `segCount`
必須以 subtable 自身宣告的 `length` 約束，且 segment 須依 `endCode` 遞增、不得重疊；只比對
整張 `cmap` table 的範圍檢查會讓誇大的 `segCount` 通過，使展開迴圈達數十億次迭代而不觸發
任何越界。`ManagedOpenTypeWebFontVerifier` 的公開 `Verify` 系列是這條路徑上唯一直接接受
外部字型的入口，其位元組上限與取消權杖由呼叫端提供。

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
| 1 managed engine | 有界 sfnt／TTC parser、TrueType composite closure、`cmap` 4／12／14、TTF writer、`fsType` 與格式拒絕 | C# 產生的最小 fixtures、真實 CNS／多 Plane／IVS／PUA；checksum round-trip、固定種子變異韌性測試、雙 TFM build；不支援矩陣逐項有負向測試 |
| 2 build 與格式 | WOFF writer、具 Brotli Runtime 的 WOFF2 null-transform writer 與標準 transformed-table decoder、CLI／MSBuild、manifest、CSS／HTML integration 與一致 hash | 無 Python／Node 的 pack consumer 完成 TTF／WOFF／WOFF2；net8 consumer 實際載入 `netstandard2.0` 資產完成 Brotli round-trip；W3C 與 production transformed WOFF2 corpus；重複建置 byte-identical；Chromium／Firefox／WebKit 載入與截圖 artifact |
| 3 Web 託管 | ASP.NET Core 少量設定的 CNS Profile、受控 dynamic endpoint、durable cache；Web Forms config／handler 與離線預產生 | 真實 HTTP auth／429／hash GET／CSP／CORS；manifest、CSS 與字型的 GET／HEAD、原始 bytes SHA-256 ETag 與 304；動態 Handler 回應 `no-store`；net48 consumer；256 並行 GET、同鍵 single-flight 與 process restart 復原 |
| 4 closure 與規模 | 逐 lookup 增加 GSUB output closure；複雜 script 先以完整 glyph ID／`cmap`／layout tables 的 correctness-first 模式支援；有界多節點介面、load 與固定種子變異韌性測試 | 每個新增 script 具合法鎖定 corpus、來源／輸出 layout table 一致性與三瀏覽器 golden；只有具結構驗證與差分證據後才能做 aggressive pruning；跨節點只在本機／CI 可重現時啟用，否則保留閘門 |
| 5 工程發布 | NuGet／DocFX／Public API／SBOM／授權漂移、安全與證據矩陣 | 同一批 nupkg 通過 net10、netstandard2.0、net48 consumer；無額外 native／tool／process path；pack、文件、SBOM、provenance 與復原演練均由 repository 閘門重現 |

Phase 是能力閘門，不是日期。不得因已存在 API、mock engine 或測試工具成功就跳過前一階段。
客戶 WAF／CDN 現場、Safari 實機、第三方安全／法律審查、市場採用與正式對外發布營運不納入
套件工程完成條件；它們只作採用者驗收建議，不得取代或阻擋上述可重現工程閘門。

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
- 真實 CNS PUA、IPAmj IVS、雙 CNS face TTC；Noto CJK 靜態 CFF OTC 與 Source Han Sans 2.005R
  靜態 CFF 1.0、TrueType variable、CFF2 variable、CFF2 OTC、WOFF／standalone transformed WOFF2 輸入
  正向矩陣，以及 Noto Color Emoji bitmap color 正向矩陣。
- 官方 CNS 楷體 Ext-B／PUA、AFDKO `seac`／靜態 CFF2，以及 Noto
  Arabic／Devanagari／Bengali／Khmer／Thai 的 layout 保留與真實瀏覽器逐像素差分。
- WOFF2 壓縮資料尾端四位元組對齊，並拒絕非零或超過三 bytes 的 padding。
- 真實來源字型、TTF／WOFF／WOFF2 與直接 CFF／CFF2 table 共 736 組固定種子 mutation
  有界結構測試；所有有效 CFF／CFF2 產物另由公開 verifier 逐 glyph 驗證 CharString。
- 同批 `0.0.1` nupkg 安裝的 library 與 dotnet tool clean consumer，以真實 CNS 字型完成三格式產字與 byte-identical 重建。
- 同批 WebFont nupkg 的 SPDX 2.3 SBOM、SHA-256、完整 NuGet 相依版本與 nuspec 授權漂移閘門。
- OpenType 1.9.1 加官方 errata、Unicode 17.0、W3C WOFF／WOFF2／CSS Fonts Level 4 與 IFT
  2025-11-18 CRD 的 90 天複查閘門；WebFont direct NuGet 相依須在線上 CI 比對官方最新穩定版。
  Preview 相依必須有精確版本、理由、移除條件及到期日。目前唯一例外是經由 OdfKit core
  傳遞的 `CSharpMath 1.0.0-pre.1`，WebFont 本身未直接參照，且到期前必須重新評估拆除 core
  localization 相依或升級至相容穩定版。
- 官方 CNS Ext-B 67,492,856-byte 字型的 2,048 個真實 supplementary-plane scalar 已依 256
  code-point bucket 產生 8 個 deterministic WOFF2；冷啟 CSS、manifest 與字型 payload 合計
  2,154,873 bytes，兩輪 hash 一致，CI 同時記錄耗時、工作集與配置量。
- 同批 nupkg 的隔離本機 feed 發布、SBOM 精確 source mapping、乾淨 consumer／CLI 與 NuGet Audit
  `all` 演練；正式 tag workflow 另建立 GitHub Sigstore provenance 與 WebFont SBOM attestation。

剩餘工作以第 5、6 節的來源字型固定種子變異韌性測試、complex-script shaping 廣度與外部人工閘門為準；
不得因上述核心可用而把整套產品標示 production-ready。

### 7.1 corpus 切片會遮蔽字集規模缺陷

上述 CNS Ext-B 證據以 256 code-point bucket 切成 8 個 WOFF2，每片遠小於 `cmap` format 4 的
16-bit `length` 上限；既有測試與範例設定亦一律使用 1,024／4,096 等小值。這使「每字元一個
segment」的 format 4 建構在超過 8,188 個 BMP 字元時必定失敗的缺陷長期零覆蓋，儘管完整
Big5 與 CNS 字集都遠超該界線。新增的 `CmapMappingTests` 以 20,000 字直接釘住此路徑。

由此得出的通則：**分片產生的證據不能用來聲稱單片字集規模的能力**。任何以 bucket、slice 或
取樣方式建立的 corpus，都必須另有一組刻意逼近格式結構上限的測試，否則格式層的規模缺陷
不會出現在任何綠燈中。

### 7.2 `cmap` 規模路徑的實機證據

由 `eng/Test-WebFontCmapScaleBrowserProof.ps1` 與
`tests/OdfKit.WebFontCmapScaleProof` 提供，來源為鎖定的 Adobe Source Han Sans TC `2.005R`
（`SourceHanSansTC-Regular.otf`、SHA-256 `10e6d832…75c24a`、OFL-1.1，不納入 repository），
該 face 於 CJK 表意文字區段提供 27,950 個可用 BMP 純量：

| 案例 | 內容 | 輸出 encoding record | Chromium | Firefox | WebKit |
| --- | --- | --- | --- | --- | --- |
| dense | 12,000 個 BMP 字元的單片子集 | `(0,3)`、`(3,1)`、`(3,10)` | 通過 | 通過 | 通過 |
| sparse | 9,000 個非相鄰 BMP 字元，format 4 依規格省略 | `(3,10)` | 通過 | 通過 | 通過 |
| control（負向對照） | dense 資產截斷至 60% | 不適用 | 正確拒絕 | 正確拒絕 | 正確拒絕 |

通過條件為 `document.fonts.load` 完成、`document.fonts.check` 為真、`document.fonts` 內屬於
該子集的 `FontFace` 狀態為 `loaded`，且取樣字元逐一以該 family 在 canvas 描繪出實際墨跡、
無 console 錯誤。負向對照是必要成分：若截斷資產仍回報 `FontFace` 已載入，代表量測只是在觀察
fallback，正向結果不能採信。三個引擎對 control 均回報未載入且 canvas 無墨跡，量測敏感度成立。

dense 案例同時是修正前後的分界證據：12,000 個 BMP 字元在 format 4 範圍合併之前必定以
`cmap4-size` 失敗，因此這條路徑在此之前不可能有任何瀏覽器證據。

### 7.3 OTS 差分預言機

由 `eng/Test-WebFontOtsOracle.ps1` 提供，使用 PyPI 釘選版本的 OpenType Sanitiser
（`9.2.0`、BSD-3-Clause、wheel SHA-256 釘選、只下載不安裝）。OTS 是 Chromium 與
Firefox 內建的字型消毒器：瀏覽器不會把 `@font-face` 下載的位元組直接交給作業系統字型
引擎，而是先經 OTS 解析、驗證並重新序列化，任一步失敗即整個拒絕。因此「通過 OTS」是
「能在瀏覽器載入」的必要條件。

這個閘門補的是 clean-room 實作的結構性盲點：本專案刻意不參考 FontTools、HarfBuzz 或
FreeType 原始碼，因此規格讀錯的地方沒有任何東西會指出來。`cmap` encoding record 未依
規格排序與 format 4 長度上限兩項缺陷，都通過了 mutation 測試、三瀏覽器截圖與 managed
verifier 才被逐行審查發現。差分預言機提供的是唯一能自動反對我們自身判斷的證據來源。

涵蓋三種字集規模（小樣本、跨越 format 4 合併路徑、超過長度上限而省略 format 4 的稀疏
字集）× 三種輸出格式（OTF／WOFF／WOFF2），共 9 個資產，全部通過。稀疏案例的通過同時
獨立確認了「format 12 存在時省略 format 4」在瀏覽器 sanitiser 層是可接受的。

負向對照為必要成分：截斷的資產必須遭 OTS 拒絕。若對照通過，代表預言機並未判別，正向
結果不可採信。

OTS 僅作隔離 oracle，不進入任何產品相依圖，符合第 6 節「第三方 validator 只是獨立
oracle job」的規定。

## 8. 第一方依據

- [Microsoft OpenType 1.9.1](https://learn.microsoft.com/en-us/typography/opentype/spec/)
- [OpenType font file 與 checksum](https://learn.microsoft.com/en-us/typography/opentype/spec/otff)
- [OpenType `cmap`](https://learn.microsoft.com/en-us/typography/opentype/spec/cmap)
- [OpenType `glyf`](https://learn.microsoft.com/en-us/typography/opentype/spec/glyf)
- [OpenType `OS/2.fsType`](https://learn.microsoft.com/en-us/typography/opentype/spec/os2)
- [OpenType GSUB](https://learn.microsoft.com/en-us/typography/opentype/spec/gsub)
- [OpenType GPOS](https://learn.microsoft.com/en-us/typography/opentype/spec/gpos)
- [W3C WOFF 1.0](https://www.w3.org/TR/WOFF/)
- [W3C WOFF 2.0](https://www.w3.org/TR/WOFF2/)
- [W3C CSS Fonts Level 4](https://www.w3.org/TR/css-fonts-4/)
- [OpenType 1.9.1 font file 與 color tables](https://learn.microsoft.com/en-us/typography/opentype/spec/otff)
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
- [NuGet 套件漏洞稽核](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages)
- [GitHub artifact attestations](https://docs.github.com/en/actions/concepts/security/artifact-attestations)
