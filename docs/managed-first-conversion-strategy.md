# Managed-first 轉檔策略

本文件修正「格式轉換」的產品定位：OdfKit 的主要目標是完整支援 ODF 規範、
維持 LibreOffice 相容，並提供 C# / .NET 友善的 Fluent API 與高階 API。
跨格式輸出應先使用授權相容的 .NET 程式庫或淨室實作；只有在 fidelity、
格式語意或工程成本不合理時才 fallback 到 LibreOffice。

## 決策順序

1. 核心 ODF DOM / 高階 API 直接輸出。
2. 授權相容且截至 2026 年仍積極維護／更新的 managed library（MIT、BSD-2-Clause、Apache-2.0 等寬鬆授權）。
3. 依公開規範與實測輸出進行淨室實作。
4. LibreOffice headless fallback，用於高 fidelity 或尚未 managed 化的格式。

## 5-3 格式轉換矩陣

| 轉換 | 主要路徑 | 狀態 | LibreOffice 角色 |
|------|----------|------|------------------|
| ODT -> DOCX | `OdfKit.Extensions.Ooxml` + Open XML SDK | managed 基礎 ✅ | 視覺 golden 對照 |
| DOCX -> ODT | `OdfKit.Extensions.Ooxml` + Open XML SDK | managed 基礎 ✅ | 互通驗收 |
| ODS -> XLSX | `OdfKit.Extensions.Ooxml` + ClosedXML / Open XML SDK | managed 基礎 ✅ | 視覺 golden 對照 |
| XLSX -> ODS | `OdfKit.Extensions.Ooxml` + ClosedXML | managed 基礎 ✅ | 互通驗收 |
| ODT -> PDF | `OdfKit.Extensions.Pdf` + PDFsharp-MigraDoc | managed 基礎 ✅ | 高 fidelity fallback |
| ODT -> HTML | `OdfKit.Extensions.Html` 淨室 StringBuilder | managed 基礎 ✅ | 互通對照 |
| ODT <-> Markdown | `OdfKit.Extensions.Html` exporter + Markdig-backed importer | managed 基礎 ✅（匯出：段落、標題、清單、表格、註腳、超連結、圖片參照、註解、inline 粗斜體／底線／GitHub-GitLab 刪除線／字號／色彩；匯入：段落、標題、清單、表格、超連結、圖片參照、粗體／斜體／粗斜體／GitHub-GitLab 刪除線、HTML span 樣式（含 `background-color`、`text-transform` 與 `font-variant`）、註解／footnote 往返） | fallback |
| ODT <-> RTF | `OdfKit.Extensions.Html` 淨室 exporter/importer | managed 基礎 ✅（匯出：段落、標題、段落對齊、段落縮排 `\\li`/`\\ri`/`\\fi`、段落間距／行距 `\\sb`/`\\sa`/`\\sl`、分頁 `\\page`、typographic symbols `\\emdash`/`\\endash`/quotes/typographic spaces/bullet、nonbreaking/optional hyphen controls `\\~`/`\\_`/`\\-`、zero-width break controls `\\zwbo`/`\\zwnbo`、清單、表格、註腳、超連結、圖片參照、註解、inline 粗斜體／底線／刪除線／上標／下標／字號／色彩／`\highlightN` 背景色；匯入：段落、段落對齊 `\\ql`/`\\qc`/`\\qr`/`\\qj`、段落縮排 `\\li`/`\\ri`/`\\fi`、段落間距／行距 `\\sb`/`\\sa`/`\\sl`、分頁 `\\page`、`\\sect`/`\\column` soft-page-break fallback、`\\softline`/`\\softpage` fallback、`\\line`/`\\tab` 節點化匯入、typographic symbols `\\emdash`/`\\endash`/quotes/typographic spaces/bullet、Unicode escapes `\\uN` with `\\ucN` fallback skip、ANSI hex escapes `\\'hh`、nonbreaking/optional hyphen controls `\\~`/`\\_`/`\\-`、zero-width break controls `\\zwbo`/`\\zwnbo`、表格、inline 粗體／斜體／底線／刪除線／上標／下標／字號／色彩、`\\caps`／`\\scaps` 大小寫效果與 `\\plain` 重設、註腳與 `\\chftn` current footnote marker、超連結、巢狀 `fldrslt` field result 與非 hyperlink field result 保留、`PAGE`／`NUMPAGES`／`DATE`／`TIME`／`AUTHOR`／`TITLE`／`SUBJECT`／`DOCPROPERTY` metadata 白名單語意映射、`MERGEFIELD` ODF placeholder 語意映射、`SEQ` ODF sequence 語意映射、`PAGEREF`（含 `\\p` 方向 switch）／`REF`（含 `\\p` 方向 switch）欄位 instruction 語意映射、exporter 圖片參照與註解標記、RTF `\\pict` PNG/JPEG 圖片與 `picwgoal`/`pichgoal` 尺寸、標準 `\\annotation`/`\\atnauthor`/`\\atnid` 註解群組與缺少 `atnid` 時沿用 annotation range id） | fallback |
| ODS -> CSV | `OdfKit.Csv` | managed 基礎 ✅ | 不需要 |
| ODP -> PPTX | `OdfKit.Extensions.Ooxml` + Open XML SDK / PresentationML 淨室 mapping | managed 基礎 ✅（投影片、slide size、基本 slide layout type、投影片背景色、ODF 色彩萃取為 PPTX theme palette、ODF 字型萃取為 PPTX theme font scheme 與 run font、文字框與多段落 API/轉換／段落對齊、基本圖形與形狀文字、line API 與 solid fill/stroke color/width/dash 與 line direction/flip、shape shadow effect export、theme effect style matrix export、圖片與裁切、講者備忘多段落 API/轉換、slide transition type/duration/speed、基礎 object animation duration/delay timing、root timing restart metadata、main sequence previous/next action metadata、low-level animation tree `transitionFilter` export、build list 與 paragraph range round-trip、emphasis animation trigger/delay round-trip、WithPrevious/WithEffect node type round-trip、混合文字 run 粗斜體／底線／刪除線／上標／下標／字號／色彩、嵌入表格文字與文字樣式／合併儲存格／儲存格背景色／全邊框／table theme style id） | fallback |
| PPTX -> ODP | `OdfKit.Extensions.Ooxml` + Open XML SDK / PresentationML 淨室 mapping | managed 基礎 ✅（投影片、slide size、基本 slide layout type、投影片背景色、layout 背景繼承與 theme background style reference 解析、theme scheme color 解析、theme font scheme token 解析與 run font 匯入、layout/master placeholder text style inheritance、文字框與多段落 API/轉換／段落對齊、基本圖形與形狀文字、line API 與 solid fill/stroke color/width/dash 與 line direction/flip、theme effect style shadow color/offset/opacity import／direct shape shadow import、圖片與裁切、講者備忘多段落 API/轉換、slide transition type/duration/speed、基礎 object animation duration/delay timing、build list 與 paragraph range round-trip、BuildParagraph-only paragraph build import fallback、emphasis animation trigger/delay round-trip、WithPrevious/WithEffect node type round-trip、混合文字 run 粗斜體／底線／刪除線／上標／下標／字號／色彩、嵌入表格文字與文字樣式／合併儲存格／儲存格背景色／全邊框／table theme style id） | fallback |
| ODG -> SVG | `OdfKit.Extensions.Html` 淨室 exporter | managed 基礎 ✅（幾何、文字框、文字 run 粗斜體／字號／色彩、樣式色彩、線端／線接合、fill-rule、opacity、transform、線性命名漸層、漸層角度、放射漸層、封裝圖片 data URI、frame/image rect 裁切、draw:contour-path / draw:contour-polygon 非矩形裁切、相容 enhanced-path custom-shape、modifier/equation 與常見公式函式展開、substitution 後 SVG `H`/`V` 水平／垂直線段與相對 `m`/`l`/`h`/`v`/`q`/`c` 命令、enhanced-path `A`/`B`/`G`/`T`/`U`/`V`/`W`/`X`/`Y` arc commands、full ellipse arcs split 與 `N`/`F`/`S` state command 安全處理、LibreOffice 風格 custom-shape corpus regression） | fallback |

## 程式庫採用策略

原則上，若第三方 managed library 同時符合授權、2026 年仍積極維護／更新、效能與語意控制需求，應優先採用或包成 backend，避免把 parser / package plumbing / binary decoding 都手刻在 OdfKit 內。採用時仍保留三個邊界：

1. ODF DOM、高階 API、Fluent API 與 LibreOffice 相容 mapping 由 OdfKit 掌控。
2. Library 只負責它擅長的語法解析、封裝格式、繪圖或低階文件模型，不讓外部 API 洩漏到 OdfKit public surface。
3. 淨室實作保留作為小體積、可預測 fallback；LibreOffice 只作高 fidelity 或尚未 managed 化的最後 fallback。

目前判斷：

| 區域 | 建議 | 理由 |
|------|------|------|
| Markdown import | 採用 Markdig backend | Markdig 為 BSD-2-Clause，NuGet 1.3.2，支援 CommonMark/GFM tables/footnotes/task lists/`~~` 等 extension，且有 AST，可減少手刻 parser 邊界錯誤。 |
| Markdown export | 暫保留 OdfKit exporter | ODF -> Markdown 是語意 mapping，不是 Markdown parser 問題；手寫 StringBuilder 可控且輕量。 |
| RTF import | 暫不採用 RtfPipe | RtfPipe 授權可接受，但截至 2026 年未呈現近期活躍更新；目前維持 OdfKit 淨室 importer，後續若找不到活躍且授權相容的 RTF library，才在安全與高性能前提下繼續淨室擴充。 |
| RTF export | 暫保留 OdfKit exporter | ODF -> RTF 子集輸出可控；外部 RTF writer 選擇少，為少量 control word 引入重依賴不划算。 |
| HTML import | 評估 AngleSharp backend | AngleSharp 為 MIT、近期更新，適合 HTML DOM parsing；匯出熱路徑不建議重回 DOM，避免重複先前效能問題。 |
| ODG/SVG rendering | 暫不以 Svg.Skia 取代 ODF->SVG mapping | Svg.Skia 主要是 SVG rendering/Skia bridge，不是 ODF draw enhanced-path translator；可用於 SVG 驗證或 raster preview，不適合替代 mapper。 |
| OOXML | 維持 Open XML SDK / ClosedXML | 專案已使用，兩者授權相容且是 .NET 生態主流選擇。 |

新增第三方套件前，必須更新 `THIRD-PARTY-NOTICES.md` 與 `docs/provenance/README.md`，並新增至少一個不依賴 LibreOffice 的 managed regression test。

## 授權相容候選

Markdown / RTF / HTML managed 匯出可直接使用高階 extension methods：

```csharp
using OdfKit.Export;

string markdown = document.ToMarkdown();
string gitLabMarkdown = document.ToMarkdown(OdfMarkdownExportOptions.GitLab);
string rtf = document.ToRtf();
document.SaveAsMarkdown("document.md");
document.SaveAsRtf("document.rtf");
TextDocument imported = markdown.ToOdtTextDocument();
TextDocument importedGitLab = markdown.ToOdtTextDocument(OdfMarkdownImportOptions.GitLab);
TextDocument importedFile = OdfManagedTextExportExtensions.LoadMarkdownAsOdt("document.md");
TextDocument importedRtf = rtf.ToOdtTextDocumentFromRtf();
TextDocument importedRtfFile = OdfManagedTextExportExtensions.LoadRtfAsOdt("document.rtf");
```

Markdown managed path exposes explicit flavor options for GitHub Flavored Markdown, GitLab Flavored Markdown, CommonMark, and Basic Markdown. GitHub/GitLab flavors keep pipe table import/export and `~~strikethrough~~` enabled; CommonMark/Basic avoid the table extension and preserve table text as tab-separated rows. CommonMark falls back to inline HTML for ODF strikethrough styling, while Basic preserves the text content without inline HTML.

ODG -> SVG 也走 managed exporter：

```csharp
using OdfKit.Export;

string svg = drawing.ToSvg();
drawing.SaveAsSvg("drawing.svg");
```

ODP <-> PPTX 走 managed PresentationML path，提供 C# 友善的高階 API：

```csharp
using OdfKit.Conversion;

byte[] pptx = presentation.ToPptx();
presentation.SaveAsPptx("slides.pptx");
presentation.Slides[0].SetTransition(OdfTransitionType.Fade, OdfLength.FromPoints(180), OdfTransitionSpeed.Fast);
presentation.Slides[0].SetSpeakerNotes(["Opening reminder", "Detailed follow-up"]);
OdfShape animatedShape = presentation.Slides[0].AddShape(
    OdfShapeType.Rectangle,
    OdfLength.FromCentimeters(1),
    OdfLength.FromCentimeters(4),
    OdfLength.FromCentimeters(4),
    OdfLength.FromCentimeters(2));
presentation.Slides[0].AddEntranceEffect(
    animatedShape.Id,
    OdfAnimationEffect.Fade,
    OdfAnimationTrigger.OnClick,
    delay: TimeSpan.FromMilliseconds(150),
    duration: TimeSpan.FromMilliseconds(750));

presentation.AddSlide("Summary")
    .AddShape(OdfShapeType.Rectangle, OdfLength.FromCentimeters(1), OdfLength.FromCentimeters(1), OdfLength.FromCentimeters(8), OdfLength.FromCentimeters(3))
    .AddEmbeddedTable(1, 2)
    .SetCellText(0, 0, "Metric")
    .SetCellTextStyle(0, 0, bold: true, fontSize: "18pt", color: "#AA5500");

using PresentationDocument odp = pptxStream.ToOdpPresentation();
using PresentationDocument fromPath = OdfPresentationOoxmlExtensions.LoadPptxAsOdp("slides.pptx");
```

| 套件 | 授權 | 用途 |
|------|------|------|
| DocumentFormat.OpenXml | MIT | DOCX / PPTX / low-level XLSX |
| ClosedXML | MIT | XLSX 高階處理 |
| PDFsharp-MigraDoc | MIT | PDF 建立與簡易排版 |
| Markdig | BSD-2-Clause | Markdown parser backend；NuGet 1.3.2 |
| RtfPipe | MIT | 暫不採用：截至 2026 年未呈現近期活躍更新 |
| AngleSharp | MIT | HTML parser / DOM bridge 候選 |
| Svg.Skia | MIT | SVG render/verification 候選，不取代 ODF -> SVG mapping |

## 淨室實作規則

- 只依 ODF / OOXML / RTF / Markdown / SVG 公開規範、專案自有測試與 LibreOffice
  行為觀察結果設計 mapping。
- 不複製 LibreOffice、Apache POI、ODF Toolkit 或商用元件原始碼；若外部程式庫不符合活躍維護或授權門檻，淨室重寫必須保留規格／測試來源記錄，並優先處理輸入安全、資源上限與高效能資料結構。
- 以小範圍語意逐步擴充：先文字、段落、標題、清單，再表格、樣式、圖片與修訂。
- 每個格式補 managed path 時，保留 LibreOffice 實機互通測試作為相容性哨兵。
- 格式 token、ODF/RTF/OOXML 屬性值、頁面尺寸與數值序列化使用 invariant culture 或 ordinal 比較；chart class、MIME、路徑與 control word 判斷不依賴目前執行緒 culture 的大小寫或小數分隔符行為。

## 下一批 managed-first 缺口

| 優先序 | 缺口 | 原因 |
|--------|------|------|
| 1 | ODG -> SVG ODF enhanced-path 進階命令 fidelity | 已有色彩、文字 run 樣式、線條呈現、transform、線性／放射漸層、封裝圖片 data URI、frame/image rect 裁切、draw:contour-path / draw:contour-polygon 非矩形裁切、SVG-compatible custom-shape enhanced-path、modifier/equation 與常見公式函式展開、substitution 後 SVG `H`/`V` 水平／垂直線段與相對 `m`/`l`/`h`/`v`/`q`/`c` 命令、ODF `A`/`B`/`G`/`T`/`U`/`V`/`W`/`X`/`Y` arc commands、full ellipse arcs split 與 `N`/`F`/`S` state command 安全處理，並以流程圖、平行四邊形、圓柱、笑臉、雲形等 LibreOffice 風格 custom-shape corpus regression 鎖定 managed path |
| 2 | ODT <-> Markdown/RTF 更深控制字與極端 fidelity | Markdown 已有 GitHub/GitLab/CommonMark/Basic flavor options，以及段落、標題、清單、表格、超連結、圖片參照、粗體／斜體／粗斜體／GitHub-GitLab 刪除線／HTML span 樣式（含 `background-color`、`text-transform` 與 `font-variant`）、註解／footnote 與 `annotation-start`／`annotation-end` 範圍 managed 往返；RTF 匯入已有段落、段落對齊 `\\ql`/`\\qc`/`\\qr`/`\\qj`、段落縮排 `\\li`/`\\ri`/`\\fi`、段落間距／行距 `\\sb`/`\\sa`/`\\sl`、分頁 `\\page`、`\\sect`/`\\column` soft-page-break fallback、`\\softline`/`\\softpage` fallback、`\\line`/`\\tab` 節點化匯入、typographic symbols `\\emdash`/`\\endash`/quotes/typographic spaces/bullet、Unicode escapes `\\uN` with `\\ucN` fallback skip、ANSI hex escapes `\\'hh`、nonbreaking/optional hyphen controls `\\~`/`\\_`/`\\-`、zero-width break controls `\\zwbo`/`\\zwnbo`、表格、inline 粗體／斜體／底線／刪除線／上標／下標／字號／色彩、`\\caps`／`\\scaps` 大小寫效果與 `\\plain` 重設、註腳與 `\\chftn` current footnote marker、超連結、巢狀 `fldrslt` field result 與非 hyperlink field result 保留、`PAGE`／`NUMPAGES`／`DATE`／`TIME`／`AUTHOR`／`TITLE`／`SUBJECT`／`DOCPROPERTY` metadata 白名單語意映射、`MERGEFIELD` ODF placeholder 語意映射、`SEQ` ODF sequence 語意映射、`PAGEREF`（含 `\\p` 方向 switch）／`REF`（含 `\\p` 方向 switch）欄位 instruction 語意映射、exporter 圖片參照、RTF `\\pict` PNG/JPEG 圖片與 goal 尺寸、exporter 註解標記、標準 annotation group、annotation range round-trip 與缺少 `atnid` 時沿用 range id；下一步聚焦複合欄位 switch 與更高保真註解邊界案例。 |
| 3 | ODP -> PPTX 版面、主題與進階動畫 fidelity | 已有 slide/name/size/layout type/placeholder type/text/background、ODF 色彩萃取為 PPTX theme palette、ODF 字型萃取為 PPTX theme font scheme 與 run font、text（含文字框多段落與段落對齊）/shape（含 shape text 與 line 與 solid fill/stroke color/width/dash 與 line direction/flip）/image alt text/crop/notes 多段落 API/轉換、slide transition type/duration/speed、基礎 object animation duration/delay timing、root timing restart metadata、main sequence previous/next action metadata、low-level animation tree `transitionFilter` export、build list 與 paragraph range round-trip、emphasis animation trigger/delay round-trip、WithPrevious/WithEffect node type round-trip、混合 run 粗斜體／底線／刪除線／上標／下標／字號／色彩、嵌入表格文字與文字樣式、合併儲存格、儲存格背景色、全邊框與 table theme style id 基礎，下一步補更完整 PresentationML master style 與更深 timeline/build sequence（parallel/sequence 群組語意與更完整 build 條件） |
| 4 | PPTX -> ODP theme、layout 與進階動畫 fidelity | 已有 slide/name/size/layout type/placeholder type/text/background/layout background inheritance/theme background style reference/theme scheme color、theme font scheme token、layout/master placeholder text style inheritance 與 shape fill/line style matrix reference 解析與 run font 匯入、text（含文字框多段落與段落對齊）/shape（含 shape text 與 line 與 solid fill/stroke color/width/dash 與 line direction/flip）/image alt text/crop/notes 多段落 API/轉換、slide transition type/duration/speed、基礎 object animation duration/delay timing、build list 與 paragraph range round-trip、BuildParagraph-only paragraph build import fallback、emphasis animation trigger/delay round-trip、WithPrevious/WithEffect node type round-trip、混合 run 粗斜體／底線／刪除線／上標／下標／字號／色彩、嵌入表格文字與文字樣式、合併儲存格、儲存格背景色、全邊框與 table theme style id 基礎，下一步補更完整 theme master/format style（例如 effect style matrix 與更多母片繼承）與更高保真 timeline/build sequence 群組語意匯入 |
