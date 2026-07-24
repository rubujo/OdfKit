# OdfKit WebFonts

OdfKit WebFonts 是供 C#／.NET 使用的多國罕用字 WebFont 動態產生、預產生與安全託管套件組。

- ASP.NET Core 與 ASP.NET Web Forms 均提供須經授權、有界且以內容定址的動態產生路徑；
  CLI／MSBuild 預產生作為暖機與 fallback。
- 接受 TTF／OTF／TTC／OTC 指定 face、`.tte`、WOFF，以及執行期具 Brotli 時的 standalone WOFF2 null／
  `glyf`／`loca`／`hmtx` transform；WOFF2 collection 可指定 face，輸出瀏覽器部署用
  WOFF2／WOFF／TTF／OTF；支援 Unicode、Big5、明確 Big5E 與版本化 PUA Profile；TrueType
  Variable Fonts、standalone／OTC face 的 CID-keyed／名稱式靜態 CFF 1.0、含 VariationStore 的
  CFF2 variable `OTTO` 與不含 VariationStore 的非變動 CFF2 採鎖定 corpus 的有界契約；名稱式 CFF 的
  `seac` 會依 StandardEncoding 與 charset 保留 base／accent 元件，找不到元件或巢狀組字明確拒絕；缺少
  VariationStore 卻使用 `vsindex`／`blend` 的 CFF2 與直接 collection 輸出明確拒絕；color font
  會驗證 COLR v0／v1、CPAL、SVG、sbix、CBDT／CBLC 與 EBDT／EBLC 結構，並將
  COLR／sbix 引用的字形加入閉包；未知 paint、循環引用、外部 SVG 資源及越界 bitmap
  資料會明確拒絕。色彩表目前採 correctness-first 保留；EBDT／EBLC 不逐 strike／glyph 重編，
  OpenType SVG 不裁切 document index，且不宣稱 aggressive table pruning；也不會把 SVG／sbix
  轉成 COLR 或 outline。`RequiredBrowserTargets`、CLI `--browser-targets` 與
  MSBuild `OdfKitWebFontsBrowserTargets` 可依鎖定的 Chromium／Firefox／Playwright WebKit
  實證矩陣，在寫檔前拒絕不相容模型；不能把相同空白畫面視為成功，也不能把 Playwright
  WebKit 證據推論為 Safari 實機證據。
  EBDT／EBLC 目前只有產生式結構證據，不列入任何瀏覽器相容集合。
- Arabic／Devanagari／Bengali／Khmer／Thai 採保留完整 glyph ID、`cmap`、GDEF／GPOS／GSUB 的 correctness-first
  路徑；其它 complex script 必須先有合法 corpus 與三瀏覽器差分證據。
- AAT layout 與 Graphite layout 不支援；辨識到其核心 layout table 時明確拒絕，不靜默 fallback。
- 提供 CLI／MSBuild 自動內容掃描、CSP/CDN URL、精確 CORS allowlist 與有界背景 Worker。
- managed verifier 可分別限制輸入 bytes、WOFF／WOFF2 展開 bytes 與 sfnt table 數量，
  避免只用單一檔案大小限制掩蓋壓縮展開風險。
- 核心不綁定 ADO.NET、Dapper、EF Core 或其它 ORM。

快速開始、套件選型、安全界線與完整範例請參閱
[WebFont 多國罕用字套件文件](https://github.com/rubujo/OdfKit/blob/main/docs/webfonts.md)。

## 依情境選擇套件

| 情境 | 主要套件 | 下一步 |
|------|----------|--------|
| ASP.NET Core 動態優先與靜態 fallback | `OdfKit.WebFonts.Hosting.AspNetCore` | [執行 ASP.NET Core 範例](https://github.com/rubujo/OdfKit/tree/main/samples/WebFonts.AspNetCore) |
| ASP.NET Web Forms／System.Web | `OdfKit.WebFonts.Hosting.SystemWeb` | [執行 Web Forms 範例](https://github.com/rubujo/OdfKit/tree/main/samples/WebFonts.WebForms) |
| net48 request-time WOFF2 | `OdfKit.WebFonts.Hosting.SystemWeb` + `OdfKit.WebFonts.Sidecar` | [部署 NativeAOT sidecar](https://github.com/rubujo/OdfKit/blob/main/docs/webfonts.md#aspnet-web-forms) |
| CLI／MSBuild 預產生 | `OdfKit.WebFonts.Build` | [查看最短使用方式](https://github.com/rubujo/OdfKit/blob/main/docs/webfonts.md#預定最短使用方式) |
| 純 managed 字型解析與子集化 | `OdfKit.WebFonts.OpenType` | [查看格式與拒絕矩陣](https://github.com/rubujo/OdfKit/blob/main/docs/webfont-managed-architecture.md) |
| CNS 11643／JSON／C# Profile | `OdfKit.WebFonts.Profiles` | [查看全字庫 Profile 與來源鎖定](https://github.com/rubujo/OdfKit/blob/main/docs/webfonts.md#全字庫-cns-11643-profile) |
| Big5／Big5E／legacy mapping | `OdfKit.WebFonts.Encoding.Legacy` | [查看完整 WebFont 用法](https://github.com/rubujo/OdfKit/blob/main/docs/webfonts.md) |
| SQL Server mapping provider | `OdfKit.WebFonts.Data.SqlServer` | [查看 ORM 與資料庫整合](https://github.com/rubujo/OdfKit/blob/main/docs/webfonts.md#orm-與資料庫) |
| Windows EUDC／`.tte` 輸入 | `OdfKit.WebFonts.Windows` | [查看 EUDC 安全與授權限制](https://github.com/rubujo/OdfKit/blob/main/docs/webfonts.md#windows-eudctte) |
| 有界背景工作與快取 | `OdfKit.WebFonts.Worker` | [查看完整 WebFont 用法](https://github.com/rubujo/OdfKit/blob/main/docs/webfonts.md) |
| HTML exporter 整合 | `OdfKit.Extensions.Html.WebFonts` | [查看完整 WebFont 用法](https://github.com/rubujo/OdfKit/blob/main/docs/webfonts.md) |

底層契約型別位於 `OdfKit.WebFonts.Abstractions`。一般網站應從對應 Hosting 套件開始，
不要只安裝底層套件後自行重建授權、限流、內容定址與快取邊界。

使用任何字型前，採用者必須確認該字型允許修改、子集化及 Web 散布。本套件不會替使用者推定
或授予第三方字型授權。
