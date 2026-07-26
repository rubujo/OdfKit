# ODF 格式支援矩陣

本文件記錄 OdfKit 對主要 ODF 格式的實作狀態。狀態以目前程式碼與
測試證據為準，不把路線圖中的目標預先標為完成。

ODF Toolkit / ODF Validator 對標線另見 [odf-toolkit-parity.md](odf-toolkit-parity.md)。

## 三種獨立能力承諾

本矩陣的歷史狀態不得單獨解讀為「完整支援」。新的可稽核宣稱拆成三個互不推導的維度：

- `PackageFidelity`：封裝偵測、載入、儲存、驗證與未知內容儲存。
- `SemanticApiDepth`：高階 API 能理解及修改的文件語意深度。
- `InteropEvidence`：schema、corpus 與外部辦公軟體的實測證據。

機器可讀 claim、限制與證據路徑見 [`claims.json`](claims.json) 及
[能力宣稱與證據索引](evidence-index.md)。Package round-trip 成功不能推導為完整語意支援；
特定版本互通成功也不能推導為所有辦公套件或像素級一致。

## 狀態標記

### 封裝與驗證層

- `verified`：已有直接 API 與測試覆蓋，可作為目前支援能力使用。
- `validated`：已有驗證或偵測測試證據，但高階 API 仍可能有限。
- `package-level`：可建立、載入、儲存與驗證最小封裝，但高階語意 API 尚未完整。
- `partial`：已有部分高階模型或來回讀寫能力，但仍有明確缺口。
- `planned`：尚未有足夠程式與測試證據支撐。

### 高階 API 層

- `workflow-verified`：滿足本文件採用的 5 項完成標準——(1) 公開高階 API 可完成該格式常見
  工作流程、(2) 有專屬測試、(3) 有來回讀寫／邊界／互通證據（含誠實記錄的上游限制）、
  (4) `Validate()`／`ValidateAsync()` 可用且測試覆蓋正向與負向案例、(5) 文件已同步。
  截至 2026-06-24，全部 24 個 extension 均已達成此分級（詳見下方「全格式 workflow-verified 最低能力
  矩陣」）。日常辦公自動化不需下沉 DOM。
- `usable`／`usable-variant`：歷史分級，目前矩陣中已無格式使用；保留定義供未來新格式或
  迴歸情況參考——`usable` 指常用建立／編輯 API 可用但有明確語意缺口；`usable-variant` 指具
  專屬 typed 文件類別但語意 API 仍完全繼承基底格式。
- `package-only`：僅封裝層來回讀寫；高階語意模型尚未專屬化或仍共用基底 wrapper。

**重要說明（避免過度宣稱）**：對 ODT、ODS、ODP、ODG、ODC、ODB、ODF、ODI，
`workflow-verified` 另受 [`semantic-coverage.json`](semantic-coverage.json) schema v4
約束；16 個 family 的每個 topic
都必須連結 `Create`、`Get`、`Find`、`Set`、`Update`、`Remove`、`Clear`、`RoundTrip`、
`Interop`、規格、實作、測試、限制與 clean-room provenance。範本、Flat XML、主控文件及
Web 文件等變體的 `workflow-verified` 仍表示沿用其基底格式語意模型並滿足本文件 5 項通用
完成標準，不推導為另一套重複的深度 facade。物理分頁、像素級渲染、完整公式
與 pivot 重算、SmartArt 佈局、Office 專屬效果模擬及完整協同演算法，均為明列非目標。
Formula 已具備 `FindFirst`／`GetAll`／`WithChild`／`ReplaceFirst`／`RemoveFirst`／
`ClearMathTokens` 等「尋找→取得→更新→移除／清除」語意編輯 helper；這項完成聲明
不等同於完整公式重算引擎。

凡標示為「真機」的互通證據，均指已有 `LibreOfficeInteropTests` 與
`eng/Test-LibreOfficeInterop.ps1` 驗收路徑；一般 `dotnet test` 不會自動啟動真實
LibreOffice，且專用腳本在找不到 LibreOffice 時預設略過。若要作為發佈門檻，請使用
`pwsh eng/Test-LibreOfficeInterop.ps1 -RequireLibreOffice`。

### CNS 11643／全字庫字型支援邊界

OdfKit 的文字內容仍以 Unicode 儲存；一般 ODT 文字層不寫入 CNS 11643 交換碼。針對臺灣全字庫
與罕見中文字情境，核心能力聚焦在 **Unicode 平面分段、ODF font-face 宣告與外部字型註冊／子集化
擴充點**：

- `OdfFontContext.SegmentText` 可依 BMP、Plane 2、Plane 3、Plane 15/16 將文字拆成多個片段，並對應
  `TW-Kai-*`、`TW-Song-*`、HanaMin、Jigmo 或 Windows ExtB／ExtG 字型名稱。
- `TextDocument.ApplyCjkFontFallback()` 會宣告常見 CJK fallback 與 `TW-Kai-98_1`、
  `TW-Kai-Ext-B-98_1`、`TW-Kai-Plus-98_1`、`TW-Song-98_1`、`TW-Song-Ext-B-98_1`、
  `TW-Song-Plus-98_1` 等全字庫字型名稱。
- `OdfParagraph.AddText(..., OdfTextFontFallbackOptions.Cns11643())` 可自動呼叫分段邏輯，將同一段
  文字拆成多個 run 並套用對應 font-face。
- `OdfTextFontFallbackOptions.HanaMin()` 會宣告 `HanaMinA`／`HanaMinB`，並將 Plane 2 與
  Plane 15/16 文字切換至 `HanaMinB`；`OdfTextFontFallbackOptions.Jigmo()` 會宣告 `Jigmo`、
  `Jigmo2`、`Jigmo3`，並將 Plane 2 與 Plane 3 文字分別切換至 `Jigmo2` 與 `Jigmo3`。
- ODS／ODP／ODG 文字入口已共用同一套分段與 font-face 宣告基礎：`OdfCell.SetText(...,
  OdfTextFontFallbackOptions.Cns11643())`、`HanaMin()` 或 `Jigmo()` 會將儲存格文字寫成富文字 run；`OdfSlide.AddTextBox(...)`
  與 `OdfDrawPage.AddTextBox(...)` 的 fallback options overload 會在文字方塊內寫入帶文字樣式的
  `text:span`。
- 圖表（ODC 及嵌入圖表）與簡報嵌入表格亦有對應入口：`OdfChartDocument.SetChartTitle(...,
  options)`／`SetAxisTitle(dimension, ..., options)`（builder 對應 `WithTitle(..., options)`）與
  `OdfEmbeddedTable.SetCellText(row, column, ..., options)` 使用同一套分段與 font-face 宣告基礎；
  頁首頁尾經由 `OdfPageHeaderFooter.GetOrCreateParagraph()` 取得 `OdfParagraph` 後即可使用段落
  的 fallback options 多載。涵蓋範圍至此為所有承載 ODF 文字 run 的高階入口；MathML 公式內容
  本質上不適用字型分段。
- `OdfFontContext` 的 `RegisterFontDirectory`、`RegisterFont` 與 `RegisterFontSubsetter` 提供外部字型
  註冊與子集化擴充點；OdfKit 不內建政府字型，也不替第三方字型授權背書。

上述四個層次不得混為同一項保證：

1. **font-face 宣告**只是在 `content.xml`／`styles.xml` 中建立字型名稱與家族的 ODF 中繼資料，
   不代表讀取端已安裝該字型，也不代表字型檔已嵌入封裝。
2. **run 分段**只在呼叫帶有 `OdfTextFontFallbackOptions` 的高階文字寫入入口時執行；載入或解析
   既有 ODF 不會自動掃描整份文件並重寫 run。分段依 Unicode 平面選擇字型名稱，不能讓 OpenType
   GSUB／GPOS 跨越不同字型檔運作。
3. **完整字型內嵌**由 `OdfFontContext.EmbedFonts` 將已解析的實體字型檔寫入封裝，並以 ODF 1.1～1.4
   規定的 `style:font-face > svg:font-face-src > svg:font-face-uri` 結構連結；封裝中的字型檔會有
   對應的 manifest media type。
4. **字型子集內嵌**只在文件含 PUA 碼位且已註冊外部 `IFontSubsetter` 時於存檔管線執行。
   OdfKit 核心沒有 TTF／OTF 子集編譯器，倉庫亦不存在 `OdfKit.Extensions.WebFont` 套件。
   目前請求會把文件中蒐集到的 PUA 碼位集合交給每個已宣告的 font-face，尚未建立文字 run 至
   實際字型的逐一碼位歸屬；外部實作應容忍來源字型沒有部分請求字形。

`IFontSubsetter` 契約只傳遞字型名稱、可解析的檔案路徑與 PUA 碼位集合，並不保證保留 GSUB、
GPOS、GDEF、垂直 metrics 或 Unicode variation sequence。OpenType 的 `vert`／`vrt2` 是 GSUB
feature tags，不是獨立資料表；IVS 非預設變體通常由 `cmap` format 14 表示。因此，外部子集化器
必須自行完成 glyph closure、所需 layout tables／feature records 與 variation selector 對應的保留，
並以實際排版引擎驗證；僅「保留 GSUB 表」不足以宣稱直排或 IVS 完整相容。規格依據見
[OpenType GSUB](https://learn.microsoft.com/en-us/typography/opentype/spec/gsub)、
[`vert`／`vrt2` feature 定義](https://learn.microsoft.com/en-us/typography/opentype/otspec190/features_uz)
與 [`cmap` format 14](https://learn.microsoft.com/en-us/typography/opentype/otspec190/cmap)。

因此，現階段可說 OdfKit 具備全字庫與補充平面文字的 ODF 文件骨架支援；在缺少真實全字庫字型、
CNS 11643 私用區碼位版本對照與 LibreOffice 開啟／匯出 PDF 的端到端 corpus 前，不應宣稱完整
CNS 11643 官方語意相容或認證。

### Unicode 版本相容性與自訂罕字字型擴充點

OdfKit 核心處理**與 Unicode 版本無關**：文字內容以 Unicode 原樣通過 XML 層，字型分段以
Unicode 平面（plane）為單位路由而非以區塊（block）為單位，因此新版 Unicode 於既有平面新增的
區塊（例如 Unicode 17.0 於 Plane 3 新增的 CJK Ext J，U+323B0–U+33479）會自動歸入既有平面
路由，不需要修改程式碼。

內建對應表的 Plane 3（Ext G／H／J）覆蓋現況：

- 字雲（`Jigmo3`）與 Windows `SimSun-ExtG` 有對應字型可接。
- 全字庫（`TW-Kai-*`／`TW-Song-*`）與花園明朝（HanaMin）無 Plane 3 字面，維持基礎字型
  （對應字元將依讀取端字型後援機制呈現）。

若需接上內建規則未涵蓋的罕字字型（例如自備的黑體系 Ext B–J 補字字型），可組合下列公開
擴充點，全程不需修改 OdfKit：

- `OdfFontContext.RegisterSupplementaryPlaneFontMapping(pattern, planeFontNames)`：註冊自訂
  「基礎字型 → 平面 → 字型名稱」對應，優先於內建規則，可涵蓋 Plane 1 至 16；傳回
  `IDisposable` 供還原。
- `OdfTextFontFallbackOptions.Custom(baseFont, fontFaces)`：以自訂 `OdfFontFaceInfo` 清單宣告
  font-face，配合上述分段規則讓 `AddText`／`SetText` 高階入口自動套用。
- `OdfFontContext.RegisterFont`／`RegisterFontDirectory`：註冊實體字型檔以供解析與內嵌。
- `OdfFontContext` 是字型子系統的唯一入口：單租戶場景直接使用 `OdfFontContext.Default`；
  多租戶或測試隔離場景可建立獨立執行個體，經由 `OdfDocument.FontContext`（文件層級，含存檔
  時的字型內嵌）或 `OdfTextFontFallbackOptions.FontContext`（單次呼叫層級）注入，優先序為
  「選項 → 文件 → Default」。已知限制：PDF 匯出因 PDFsharp 全域字型解析器
  （`GlobalFontSettings.FontResolver`）為處理程序層級，一律使用 `Default` 情境；嵌入子文件與
  最外層文件共用封裝時，存檔內嵌以最外層文件的情境為準。

OdfKit 內建部分已知字型家族的平面路由名稱，但不內建任何第三方罕字字型檔；字型選擇、實體檔案
註冊、內嵌與授權政策仍由使用者自行決定。

### 中文碼對照、Big5／Big5E 與碼位遷移

延續「機制內建、資料外部」原則，OdfKit 對全字庫（CNS 11643 open data）提供下列支援；
對照表資料由使用者自[政府資料開放平臺](https://data.gov.tw/dataset/5961)下載
（政府資料開放授權條款－第 1 版），倉庫不內建任何對照資料：

> 資料來源標示：數位發展部，2026，CNS11643 中文標準交換碼全字庫中文碼對照表
> （釘選快照 2026-05-05）。該資料依[政府資料開放授權條款－第 1 版](https://data.gov.tw/license)
> 釋出。CI 只在驗證期間下載並快取原始封存檔，不會把對照表納入原始碼、NuGet 套件或發行成品；
> 因此 OdfKit 原創程式碼維持 CC0，而政府資料本身不宣告為 CC0。若使用者另行散布對照表或其
> 衍生資料，仍須履行原授權的顯名義務。全字庫資料集另提供 OFL 1.1 選項供字型用途選擇，
> 本專案的中文碼對照表 baseline 明確採用 OGDL-Taiwan-1.0。

- `OdfCns11643MappingTable.Parse`／`JoinOnCns`：解析官方「字面-編碼<TAB>十六進位」對照表
  格式並以 CNS 字碼聯結兩表（例如 CNS↔Unicode 聯 CNS↔Big5E 得 Unicode↔Big5E）。
- `OdfBig5EEncoding.Create(unicodeToBig5E)`：由對照表驅動的 Big5E 編碼，可直接餵入
  `OdfCsvOptions.Encoding`。官方資料若有多個 Unicode 純量值共用同一 Big5E 碼，解碼會以數值
  最小的純量值作為確定性的 canonical 結果；因此 alias 不保證逐字往返，但解碼後重新編碼會
  回到同一 Big5E 碼。CLI 的 `--encoding` 不內建 `big5e`（需外部資料）；Big5 則由
  .NET CP950 提供（`--encoding big5`），與官方 CNS↔Big5 表的差異經 baseline 測試量化為
  2 字（U+5F5E、U+7B9A 重複對應歧義字，CP950 不提供編碼）。
- `OdfDocument.MigrateTextCodePoints(mapping)`：資料驅動的文件碼位遷移（含 ContentDom 與
  StylesDom），供舊版全字庫 PUA 自造字遷移至新版 Unicode 正式碼位的封存情境，回傳
  `OdfCodePointMigrationReport` 統計。此方法只替換兩個 DOM 的文字節點，不會重新執行
  `SegmentText` 或重套文字 run 的字型；若遷移前後跨越 Unicode 平面，呼叫端應在套用 fallback
  樣式前先完成遷移，或於遷移後自行重建受影響的文字 runs。
- CI baseline：`.github/workflows/cns11643-baseline.yml` 以釘選版本（2026-05-05、SHA-256
  驗證）的官方對照表執行全集驗收——10.4 萬 CNS↔Unicode 碼位的平面路由與分段無損、
  CP950 差異白名單、Big5E 全碼位編解碼往返。本機無資料時對應測試自動略過。

碼位遷移引擎本身是語系中立的（吃 `IReadOnlyDictionary<int, int>`），對照表來源不限全字庫。
`OdfCodePointMappingTable` 提供與格式無關的階梯：

- `ParseDelimitedHex(reader, separator)`：行式十六進位對照表——可**直接餵入 unicode.org
  官方對照檔**（`Public/MAPPINGS` 下的 BIG5.TXT、CP950.TXT 等，TAB 分隔、`0x` 前綴、`#` 註解）
  與 UCD 式分號清單（`U+` 前綴亦可）；不支援 `XXXX..YYYY` 範圍語法。
- `Parse(reader, lineParser)`：委派擴充點，自訂行格式只需一行解析邏輯。
- `Join(keyToSource, keyToTarget)`：通用字串鍵聯結（`JoinOnCns` 為其 CNS 語境別名）。
- 資源預算（比照 security-limits 的入口原則）：兩個解析方法（含 CNS 特化版）施行每行
  4,096 字元與每表 2,000,000 筆上限，超出擲出 `FormatException`；8 位十六進位溢為負值
  一律視為無效，例外訊息中的原始行會截斷並清洗控制字元。對照表屬營運端提供的設定資料，
  行長檢查在該行讀入後執行——餵入不可信串流前應另行預先限制輸入大小。
- JSON／試算表來源（例如日本文字情報基盤的 MJ 縮退對照，JSON 格式）由呼叫端以對應的
  反序列化器轉成字典即可餵入 `MigrateTextCodePoints`——同一引擎同樣適用日本外字（MJ）、
  GB 18030-2022 的 PUA 重指派與歐洲 MUFI 等 PUA 遷移場景。

## 矩陣

| 副檔名 | MIME 類型 | `OdfDocumentKind` | 偵測 | 建立 | 載入 | 儲存 | 驗證 | 來回讀寫 | 高階 API | 測試證據 |
|---|---|---|---|---|---|---|---|---|---|---|
| `.odt` | `application/vnd.oasis.opendocument.text` | `Text` | verified | verified | verified | verified | validated | verified | workflow-verified | `TextApiUsabilityTests`, `TextHighLevelApiTests`, `FourFormatApiScenarioTests`, `TextAdvancedFidelityTests`, `ComplianceTests`, `InteropCorpusTests` |
| `.ott` | `application/vnd.oasis.opendocument.text-template` | `TextTemplate` | verified | verified | verified | verified | validated | verified | workflow-verified | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `E2ETests`, `InteropCorpusTests`, `TemplateRoundTripTests`, `LibreOfficeInteropTests` |
| `.odm` | `application/vnd.oasis.opendocument.text-master` | `TextMaster` | verified | verified | verified | verified | validated | verified | workflow-verified | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `InteropCorpusTests`, `MasterDocumentTests`, `LibreOfficeInteropTests` |
| `.oth` | `application/vnd.oasis.opendocument.text-web` | `TextWeb` | verified | verified | verified | verified | validated | verified | workflow-verified | `ComplianceTests`, `PackageRoundTripTests`, `TextWebDocumentTests`, `HtmlExportTests`, `LibreOfficeInteropTests` |
| `.fodt` | `application/vnd.oasis.opendocument.text` | `FlatText` | verified | verified | verified | verified | validated | verified | workflow-verified | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `E2ETests`, `PackageRoundTripTests`, `FlatVariantRoundTripTests`, `LibreOfficeInteropTests` |
| `.ods` | `application/vnd.oasis.opendocument.spreadsheet` | `Spreadsheet` | verified | verified | verified | verified | validated | verified | workflow-verified | `SpreadsheetApiUsabilityTests`, `SpreadsheetHighLevelApiTests`, `ChartHighLevelApiTests`, `FourFormatApiScenarioTests`, `SpreadsheetCommonApiTests`, `OpenFormulaSupportTests`, `InteropCorpusTests` |
| `.ots` | `application/vnd.oasis.opendocument.spreadsheet-template` | `SpreadsheetTemplate` | verified | verified | verified | verified | validated | verified | workflow-verified | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `E2ETests`, `InteropCorpusTests`, `TemplateRoundTripTests`, `LibreOfficeInteropTests` |
| `.fods` | `application/vnd.oasis.opendocument.spreadsheet` | `FlatSpreadsheet` | verified | verified | verified | verified | validated | verified | workflow-verified | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `E2ETests`, `PackageRoundTripTests`, `FlatVariantRoundTripTests`, `LibreOfficeInteropTests` |
| `.odp` | `application/vnd.oasis.opendocument.presentation` | `Presentation` | verified | verified | verified | verified | validated | verified | workflow-verified | `PresentationApiUsabilityTests`, `PresentationHighLevelApiTests`, `FourFormatApiScenarioTests`, `PresentationAndRenderingTests`, `PresentationBoundaryTests`, `InteropCorpusTests` |
| `.otp` | `application/vnd.oasis.opendocument.presentation-template` | `PresentationTemplate` | verified | verified | verified | verified | validated | verified | workflow-verified | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `E2ETests`, `InteropCorpusTests`, `TemplateRoundTripTests`, `LibreOfficeInteropTests` |
| `.fodp` | `application/vnd.oasis.opendocument.presentation` | `FlatPresentation` | verified | verified | verified | verified | validated | verified | workflow-verified | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `PackageRoundTripTests`, `FlatVariantRoundTripTests`, `LibreOfficeInteropTests` |
| `.odg` | `application/vnd.oasis.opendocument.graphics` | `Graphics` | verified | verified | verified | verified | validated | verified | workflow-verified | `DrawingApiUsabilityTests`, `DrawingHighLevelApiTests`, `FourFormatApiScenarioTests`, `ComplianceTests`, `InteropCorpusTests` |
| `.otg` | `application/vnd.oasis.opendocument.graphics-template` | `GraphicsTemplate` | verified | verified | verified | verified | validated | verified | workflow-verified | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `E2ETests`, `InteropCorpusTests`, `TemplateRoundTripTests`, `LibreOfficeInteropTests` |
| `.fodg` | `application/vnd.oasis.opendocument.graphics` | `FlatGraphics` | verified | verified | verified | verified | validated | verified | workflow-verified | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `PackageRoundTripTests`, `FlatVariantRoundTripTests`, `LibreOfficeInteropTests` |
| `.odc` | `application/vnd.oasis.opendocument.chart` | `Chart` | verified | verified | verified | verified | validated | verified | workflow-verified | `ChartHighLevelApiTests`, `SecondaryFormatApiScenarioTests`, `DocumentKindApiUsabilityTests`, `ComplianceTests`, `InteropCorpusTests`, `ChartVariantRoundTripTests`, `LibreOfficeInteropTests` |
| `.otc` | `application/vnd.oasis.opendocument.chart-template` | `ChartTemplate` | verified | verified | verified | verified | validated | verified | workflow-verified | `ComplianceTests`, `PackageRoundTripTests`, `ChartVariantRoundTripTests`, `LibreOfficeInteropTests` |
| `.odf` | `application/vnd.oasis.opendocument.formula` | `Formula` | verified | verified | verified | verified | validated | verified | workflow-verified | `FormulaHighLevelApiTests`, `SecondaryFormatApiScenarioTests`, `DocumentKindApiUsabilityTests`, `PackageRoundTripTests`, `InteropCorpusTests`, `FormulaVariantRoundTripTests`, `LibreOfficeInteropTests` |
| `.otf` | `application/vnd.oasis.opendocument.formula-template` | `FormulaTemplate` | verified | verified | verified | verified | validated | verified | workflow-verified | `ComplianceTests`, `PackageRoundTripTests`, `FormulaVariantRoundTripTests`, `LibreOfficeInteropTests` |
| `.odi` | `application/vnd.oasis.opendocument.image` | `Image` | verified | verified | verified | verified | validated | verified | workflow-verified | `ImageHighLevelApiTests`, `SecondaryFormatApiScenarioTests`, `DocumentKindApiUsabilityTests`, `ComplianceTests`, `InteropCorpusTests`, `ImageVariantRoundTripTests`, `LibreOfficeInteropTests` |
| `.oti` | `application/vnd.oasis.opendocument.image-template` | `ImageTemplate` | verified | verified | verified | verified | validated | verified | workflow-verified | `ComplianceTests`, `PackageRoundTripTests`, `ImageVariantRoundTripTests`, `LibreOfficeInteropTests` |
| `.odb` | `application/vnd.oasis.opendocument.base` | `Database` | verified | verified | verified | verified | validated | verified | workflow-verified | `DatabaseHighLevelApiTests`, `DatabaseSchemaAndFormTests`, `DatabaseBoundaryTests`, `SecondaryFormatApiScenarioTests`, `DocumentKindApiUsabilityTests`, `ComplianceTests`, `InteropCorpusTests`, `LibreOfficeInteropTests` |
| `.fodc` | `application/vnd.oasis.opendocument.chart` | `FlatChart` | verified | verified | verified | verified | validated | verified | workflow-verified | `PackageRoundTripTests`, `CorpusComplianceTests`, `ChartVariantRoundTripTests`, `LibreOfficeInteropTests` |
| `.fdf` | `application/vnd.oasis.opendocument.formula` | `FlatFormula` | verified | verified | verified | verified | validated | verified | workflow-verified | `PackageRoundTripTests`, corpus manifest, `FormulaVariantRoundTripTests`, `LibreOfficeInteropTests` |
| `.fodi` | `application/vnd.oasis.opendocument.image` | `FlatImage` | verified | verified | verified | verified | validated | verified | workflow-verified | `PackageRoundTripTests`, `CorpusComplianceTests`, `ImageVariantRoundTripTests`, `LibreOfficeInteropTests` |

## 全格式 workflow-verified 最低能力矩陣（Workstream A）

依本文件定義的 5 項完成標準逐格式列出條件是否滿足，作為上方矩陣
`workflow-verified` 分級的可稽核依據。圖例：✅ 滿足；✅\* 滿足，但互通證據為「已實機驗證並誠實
記錄上游應用程式限制」而非綠燈通過（依使用者決策，至少一個驗收案例即符合本矩陣互通欄位要求，
不要求每案必須通過）。

| 副檔名 | (1) 高階 API 完成常見工作流程 | (2) 專屬測試 | (3) 來回讀寫／邊界／互通證據 | (4) Validate() 正負向 | (5) 文件同步 |
|---|---|---|---|---|---|
| `.odt` | ✅ | ✅ `TextHighLevelApiTests` | ✅ 真機（`LibreOfficeHeadless_LoadsGeneratedDocuments`） | ✅ 通用骨架 | ✅ |
| `.ott` | ✅ `CreateFromTemplate`／`CreateFromDocument` | ✅ `TemplateRoundTripTests` | ✅ 真機（`LibreOfficeHeadless_LoadsTemplateVariantDocuments`） | ✅ 通用骨架 | ✅ |
| `.odm` | ✅ 子文件 CRUD／合併／大綱位移 | ✅ `MasterDocumentTests` | ✅ 真機（`LibreOfficeHeadless_LoadsMasterDocument`，`writerglobal8`） | ✅ 通用骨架 | ✅ |
| `.oth` | ✅ `CreateFromDocument`／`CreateFromWebDocument`＋HTML 匯出整合 | ✅ `TextWebDocumentTests` | ✅ 真機（`LibreOfficeHeadless_LoadsWebTemplateDocument`，`writerweb8_writer`） | ✅ 通用骨架 | ✅ |
| `.fodt` | ✅ `CreateFromFlatDocument`／`CreateFromDocument` | ✅ `FlatVariantRoundTripTests` | ✅ 真機（`LibreOfficeHeadless_LoadsNativeFlatXmlDocuments`） | ✅ 通用骨架 | ✅ |
| `.ods` | ✅ | ✅ `SpreadsheetHighLevelApiTests` | ✅ 真機（`LibreOfficeHeadless_LoadsGeneratedDocuments`） | ✅ 通用骨架 | ✅ |
| `.ots` | ✅ | ✅ `TemplateRoundTripTests` | ✅ 真機 | ✅ 通用骨架 | ✅ |
| `.fods` | ✅ | ✅ `FlatVariantRoundTripTests` | ✅ 真機 | ✅ 通用骨架 | ✅ |
| `.odp` | ✅ | ✅ `PresentationHighLevelApiTests` | ✅ 真機 | ✅ 通用骨架 | ✅ |
| `.otp` | ✅ | ✅ `TemplateRoundTripTests` | ✅ 真機 | ✅ 通用骨架 | ✅ |
| `.fodp` | ✅ | ✅ `FlatVariantRoundTripTests` | ✅ 真機 | ✅ 通用骨架 | ✅ |
| `.odg` | ✅ | ✅ `DrawingHighLevelApiTests` | ✅ 真機 | ✅ 通用骨架 | ✅ |
| `.otg` | ✅ | ✅ `TemplateRoundTripTests` | ✅ 真機 | ✅ 通用骨架 | ✅ |
| `.fodg` | ✅ | ✅ `FlatVariantRoundTripTests` | ✅ 真機 | ✅ 通用骨架 | ✅ |
| `.odc` | ✅ 軸線／序列／樣式／error-indicator／regression-curve／mean-value | ✅ `ChartHighLevelApiTests` | ✅\* 嵌入 ODS 真機成功；獨立檔案經實機確認上游不支援（已記錄） | ✅ 通用骨架 | ✅ |
| `.otc` | ✅ `CreateFromDocument`／`CreateFromTemplate` | ✅ `ChartVariantRoundTripTests` | ✅\* 封裝結構驗證＋上游限制已記錄 | ✅ 通用骨架 | ✅ |
| `.fodc` | ✅ `CreateFromDocument`／`CreateFromFlatDocument` | ✅ `ChartVariantRoundTripTests` | ✅\* 封裝結構驗證＋上游限制已記錄（誤判為 Writer document） | ✅ 通用骨架 | ✅ |
| `.odf` | ✅ MathML token／builder／LaTeX／annotation | ✅ `FormulaHighLevelApiTests` | ✅ 真機（`LibreOfficeHeadless_LoadsFormulaDocument`，`math8`） | ✅ 通用骨架 | ✅ |
| `.otf` | ✅ `CreateFromDocument`／`CreateFromTemplate` | ✅ `FormulaVariantRoundTripTests` | ✅\* 封裝結構驗證＋上游限制已記錄 | ✅ 通用骨架 | ✅ |
| `.fdf` | ✅ `CreateFromDocument`／`CreateFromFlatDocument` | ✅ `FormulaVariantRoundTripTests` | ✅\* 封裝結構驗證＋上游限制已記錄（誤判為 Calc document） | ✅ 通用骨架 | ✅ |
| `.odi` | ✅ 多框架／版面／旋轉／裁切／濾鏡／批次操作 | ✅ `ImageHighLevelApiTests` | ✅\* 封裝結構驗證＋上游限制已記錄 | ✅ 通用骨架 | ✅ |
| `.oti` | ✅ `CreateFromDocument`／`CreateFromTemplate` | ✅ `ImageVariantRoundTripTests` | ✅\* 封裝結構驗證＋上游限制已記錄 | ✅ 通用骨架 | ✅ |
| `.fodi` | ✅ `CreateFromDocument`／`CreateFromFlatDocument` | ✅ `ImageVariantRoundTripTests` | ✅\* 封裝結構驗證＋上游限制已記錄（誤判為 Writer document） | ✅ 通用骨架 | ✅ |
| `.odb` | ✅ 連線／查詢／表單設計器／報表 href／Schema CRUD | ✅ `DatabaseHighLevelApiTests`、`DatabaseBoundaryTests` | ✅\* mimetype／manifest 驗證＋ LibreOffice UNO API 人工驗證＋ `--convert-to` 行為已誠實記錄 | ✅ 通用骨架 | ✅ |

第 (4) 項「Validate() 正負向」對所有列皆標示「✅ 通用骨架」：因 `OdfDocument.Validate()`／
`ValidateAsync()` 定義於基底類別，對全部文件種類自動生效，無需逐格式重複實作或測試；正向
與負向覆蓋見 `OdfValidatorApiTests.DocumentInstance_Validate_AcrossSecondaryFormatKinds_AllSucceed`
與 `DocumentInstance_Validate_DetectsUnregisteredElementUnderStrictProfile`。

## 深度狀態與限制

- 統一的 `OdfDocument.Load` / `OdfDocument.Create` 與
  `OdfDocumentFactory.LoadDocument` / `CreateDocument` 高階入口已建立。
- ODT、ODS、ODP、ODG 高階 API 已達 `semantic-contract-verified`：schema v4 manifest 對每個
  topic 驗證完整生命週期，並逐 family 驗證既有文件、重複儲存載入、未知內容保留、
  ODF 1.1～1.3 映射、結構化降版診斷、非法輸入、隨機 mutation、
  corpus differential 與外部辦公軟體互通證據；使用者工作流不需下沉裸 DOM。明列非目標不屬於
  此分級的未完成項目。
- `.ott`、`.ots`、`.otp`、`.otg`、`.odm` 與 Flat 變體（VAR-1 ✅）：具專屬 typed 文件類別與
  `Create`/`Load` 入口；內容編輯語意 API 繼承四主格式基底類別。2026-06-24 依 5 項完成標準
  標準重新檢視後已升級為 `workflow-verified`，詳見下方各 Batch 說明。
- `.ott`／`.ots`／`.otp`／`.otg`（Batch 1 第一波，2026-06-23）：新增雙向範本生命週期工作流——
  `TextDocument.CreateFromTemplate`／`SpreadsheetDocument.CreateFromTemplate`／
  `PresentationDocument.CreateFromTemplate`／`DrawingDocument.CreateFromTemplate`（範本→文件，
  既有）與新增的 `TextTemplateDocument.CreateFromDocument`／`SpreadsheetTemplateDocument.CreateFromDocument`／
  `PresentationTemplateDocument.CreateFromDocument`／`GraphicsTemplateDocument.CreateFromDocument`
  （文件→範本，本次新增），並各補上 `TemplateRoundTripTests` 雙向往返測試與
  `LibreOfficeInteropTests.LibreOfficeHeadless_LoadsTemplateVariantDocuments` 實機互通驗收。
  2026-06-23 完成時依「範本內容編輯仍沿用基底格式語意 API，尚未有範本專屬深度內容模型」為
  理由維持 `usable-variant`；2026-06-24 依 5 項完成標準（高階 API、
  專屬測試、round-trip／boundary／interop 證據、`Validate()` 正負向、文件同步）重新檢視，
  確認上述條件已全部滿足，**升級為 `workflow-verified`**。
- 文件級 `OdfDocument.Validate(OdfComplianceProfile?)` / `ValidateAsync(...)` 已新增（Workstream E
  ✅，2026-06-23／24）：所有文件種類現皆可直接呼叫實例方法驗證目前（含尚未儲存的編輯）記憶體
  狀態，內部委派既有 `OdfValidator` 靜態進入點與 `OdfValidationReport` 結構化結果。因定義於
  `OdfDocument` 基底類別，對全部文件種類（包含 Chart／Formula／Image／Database 等次要格式與其
  Template／Flat 變體）皆通用，無需逐格式重複實作。測試見
  `OdfValidatorApiTests.DocumentInstance_Validate_ReflectsUnsavedEdits`、
  `DocumentInstance_ValidateAsync_ReturnsStructuredReport`（正向，Text）、
  `DocumentInstance_Validate_AcrossSecondaryFormatKinds_AllSucceed`（正向，跨 Chart／Formula／
  Image／Database 驗證 API 通用性）、
  `DocumentInstance_Validate_DetectsUnregisteredElementUnderStrictProfile`（負向，插入未註冊
  schema 元素於嚴格設定檔下應回報失敗）。
- `.fodt`／`.fods`／`.fodp`／`.fodg`（Batch 1 第二波，2026-06-23）：新增型別化 Flat XML↔ZIP
  雙向轉換工作流——`FlatTextDocument.CreateFromDocument(TextDocument)`／
  `FlatSpreadsheetDocument.CreateFromDocument(SpreadsheetDocument)`／
  `FlatPresentationDocument.CreateFromDocument(PresentationDocument)`／
  `FlatGraphicsDocument.CreateFromDocument(DrawingDocument)`（ZIP→Flat）與對應的
  `TextDocument.CreateFromFlatDocument`／`SpreadsheetDocument.CreateFromFlatDocument`／
  `PresentationDocument.CreateFromFlatDocument`／`DrawingDocument.CreateFromFlatDocument`
  （Flat→ZIP），共用 `OdfDocument.ConvertFlatVariantInternal` 基礎實作，呼應範本批次的
  `CreateFromTemplate`／`CreateFromDocument` 模式。新增 `FlatVariantRoundTripTests` 雙向往返與
  邊界測試，並新增 `LibreOfficeInteropTests.LibreOfficeHeadless_LoadsNativeFlatXmlDocuments`——
  以 OdfKit **直接產生**（非由 ZIP 轉換而來）的原生 FODT／FODS／FODP／FODG 檔案實機驗證
  LibreOffice 26.x 可直接開啟，證明 Flat XML 與 ZIP 封裝的高階工作流對 LibreOffice 而言互通等價。
  2026-06-24 依 5 項完成標準重新檢視，**升級為 `workflow-verified`**（理由與範本變體相同：
  上述 5 項條件已全部滿足；內容編輯仍沿用基底格式語意 API、未有 Flat 專屬深度
  內容模型，但這不在最低完成標準之內）。
- `.odm`（Batch 2 第一項，2026-06-23）：子文件 CRUD、條件式載入、合併為單一文件、大綱階層
  位移等變體專屬 workflow 在此之前即已完備（`TextMasterDocument.AddSubDocumentReference`／
  `GetSubDocumentReferences`／`RemoveSubDocumentReference`／`ReorderSubDocumentReferences`／
  `SetSubDocumentLoadOnRequest`／`MergeSubDocuments`），本次補上：(1) 實機 LibreOffice 26.x
  互通驗收——已確認 LibreOffice 將 .odm 識別為「Writer master document」並使用
  `writerglobal8` 篩選器，非僅理論相容（`LibreOfficeInteropTests.LibreOfficeHeadless_LoadsMasterDocument`）；
  (2) 邊界測試——子文件參照目標檔案不存在時擲出 `FileNotFoundException`，
  `baseDirectory` 為空白時擲出 `ArgumentException`（`MasterDocumentTests`）。
- `.oth`（Batch 2 第二項，2026-06-23）：新增型別化雙向轉換工作流
  `TextWebDocument.CreateFromDocument(TextDocument)` ↔ `TextDocument.CreateFromWebDocument(TextWebDocument)`，
  重用範本批次的種類／MIME 置換基礎實作。新增 `TextWebDocumentTests`（雙向往返、高階 API
  編輯、null 引數邊界）；新增 `OdfHtmlExporter_AcceptsTextWebDocumentDirectly` 證明
  `OdfKit.Extensions.Html` 的 `OdfHtmlExporter.Export(TextDocument)` 因繼承關係可直接接受
  OTH 文件，不需任何轉接層，滿足「與 HTML／export 行為一致性」要求。新增實機 LibreOffice
  互通驗收——已確認 LibreOffice 將 .oth 識別為「Writer/Web document」並使用
  `writerweb8_writer` 篩選器轉換為 ODT（`LibreOfficeHeadless_LoadsWebTemplateDocument`）。
  2026-06-24 依 5 項完成標準重新檢視，**升級為 `workflow-verified`**（內容模型本身與 ODT 相同、
  未有專屬深度內容模型，但這不在最低完成標準之內）。
- `.odc`／`.otc`／`.fodc`（Batch 3，2026-06-23）：現況調查發現既有圖表實作已遠比文件先前
  描述的「摘要層」深入——`OdfChartDocument` 早已具備軸線（對數刻度、範圍、反向、網格）、
  序列、樣式（含 3D 投影、光源）、牆面／地板、股票圖標記等大量可變更高階 API。本次補上先前
  明確列出但確認缺失的部分：
  - 新增 `OdfChartSeries.FindErrorIndicator`／`SetErrorIndicator`、`FindRegressionCurve`／
    `SetRegressionCurve`、`FindMeanValue`／`SetMeanValue`（對應 `chart:error-indicator`／
    `chart:regression-curve`／`chart:mean-value`），新增時依 OASIS ODF 1.4 schema 規定的
    子元素順序（domain、mean-value、regression-curve、error-indicator、data-point、
    data-label）正確插入，避免產生不合規文件。
  - 新增雙向轉換工作流 `ChartTemplateDocument.CreateFromDocument(ChartDocument)` ↔
    `ChartDocument.CreateFromTemplate(ChartTemplateDocument)`；
    `FlatChartDocument.CreateFromDocument(ChartDocument)` ↔
    `ChartDocument.CreateFromFlatDocument(FlatChartDocument)`，重用既有種類／MIME 置換
    基礎實作。新增 `ChartVariantRoundTripTests`（雙向往返、null 引數邊界）。
  - 新增 `ChartHighLevelApiTests.SeriesErrorIndicatorRegressionCurveAndMeanValue_RoundTripAfterSaveAndLoad`
    驗證上述三項 API 與其 schema 順序。
  - **互通驗收的誠實負向結果**：實測確認 LibreOffice 26.2.1 不支援將獨立（非嵌入
    ODS/ODT/ODP）的 ODC／OTC 開啟為主文件（回報 `source file could not be loaded`），
    FODC 則被誤判為「Writer document」僅原樣回顯來源 XML，並非真正剖析。
    這與既有 `ImageDocument_PackageStructureMatchesOdf14Schema` 註解中
    「LibreOffice 已在 draw.xcd 註冊 ODC」的舊有假設不符——已在
    `LibreOfficeInteropTests.OdfChartDocument_PackageStructureMatchesOdf14Schema` 的文件
    註解中修正此假設。改以封裝結構驗證取代真機驗證，並以既有
    `LibreOfficeHeadless_LoadsGeneratedDocuments` 中「圖表嵌入 ODS 後由 LibreOffice 開啟」
    的驗收佐證嵌入式圖表（ODF Chart 設計上唯一的真實使用情境）的互通性。
  - 2026-06-23 完成時依「仍缺 Legend 統一可編輯模型與 fluent builder API」為理由維持
    `usable`／`usable-variant`；2026-06-24 依 5 項完成標準重新檢視，
    確認已全部滿足，**升級為 `workflow-verified`**。後續深度工作已補齊 Legend 物件模型、
    `ChartDocument.Builder()`、序列樣式 builder 與資料標籤 preset（`OdfChartDataLabelPreset`／
    `WithDataLabels`），目前剩餘 Chart 追蹤重點轉為更完整的跨格式 fidelity 與高階樣式
    preset，而不是基礎 builder 缺口。
- `.odf`／`.otf`／`.fdf`（Batch 4，2026-06-23）：現況調查同樣發現既有 MathML token 模型
  （`OdfMathToken`／`OdfMathBuilder`，17 種 token 類型）與 LaTeX↔MathML 雙向轉換早已完整支援
  row／fraction／script／table 等必要結構；本次補上確認缺失的部分：
  - 新增 `OdfFormulaDocument.FindAnnotation`／`SetAnnotation`，支援 `math:semantics`／
    `math:annotation`（先前確認缺失的 annotation 結構）；`LoadFromLatex` 現會自動將
    原始 LaTeX 來源附加為 `application/x-tex` 標註，`ToLatex` 優先回傳該標註以達成**精確**
    往返，而非僅 best-effort 由 MathML 重建。
  - **修正一個既有的潛在缺陷**：實作過程中發現 `MathText`（公式純文字摘要）直接對整個
    `MathNode` 取 `TextContent`，會將 `math:annotation` 標註文字與呈現內容文字混雜串接；
    真實 LibreOffice 的 `math8` 匯出篩選器恰好就會附加 StarMath 來源標註，導致 `MathText`
    回傳髒資料。已修正為僅遍歷呈現內容（略過 annotation／annotation-xml）。
  - 新增雙向轉換工作流 `FormulaTemplateDocument.CreateFromDocument(FormulaDocument)` ↔
    `FormulaDocument.CreateFromTemplate(FormulaTemplateDocument)`；
    `FlatFormulaDocument.CreateFromDocument(FormulaDocument)` ↔
    `FormulaDocument.CreateFromFlatDocument(FlatFormulaDocument)`。
  - **修正一個會導致 Flat 公式文件遺失內容的既有缺陷**：`OdfFormulaDocument` 為相容真實
    LibreOffice ZIP 封裝慣例，`GetContentXmlForPersistence` 一律回傳裸 `math:math` 根節點
    （略過 `office:document-content/office:body` 包裹）；但 Flat XML 寫入器
    （`OdfPackageArchiveWriter.WriteFlatXmlToStream`）需要從 `content.xml` 根節點的
    `office:body` 子元素取出內容才能組成單一 Flat XML 文件，因此先前任何 `FlatFormulaDocument`
    存檔都會遺失公式內容（在開發本批次新增的 round-trip 測試時發現並修正：現在僅在
    `Package.IsFlatXml` 為 `false`（ZIP 封裝）時才轉換為裸根節點，Flat XML 情境維持包裹結構）。
  - 新增 `FormulaVariantRoundTripTests`（雙向往返、邊界測試）。
  - **互通驗收**：實測確認獨立 `.odf` 文件**確實有真機支援**——LibreOffice 26.2.1 將其識別為
    「Math document」並使用 `math8` 篩選器（`LibreOfficeHeadless_LoadsFormulaDocument`），是
    目前唯一一個獨立 ZIP 主格式有真機支援的次要格式（不同於 Chart／Image）。但 `.otf`／`.fdf`
    變體仍與 Chart／Image 的變體一樣不受 LibreOffice 支援為獨立主文件（`.otf` 回報
    「source file could not be loaded」；`.fdf` 被誤判為「Calc document」），改以封裝結構驗證
    取代（`OdfFormulaVariantDocument_PackageStructureMatchesOdf14Schema`）。
  - 2026-06-23 完成時依「仍缺公式語意編輯 helper（例如『尋找分數→取得分子→
    更新分子』這類查詢－修改－更新 API）」為理由維持 `usable`／`usable-variant`；
    2026-06-24 依 5 項完成標準重新檢視，確認已全部滿足，**升級為
    `workflow-verified`**。2026-06-28 已補齊 `OdfMathToken.FindFirst`／`GetAll`／`WithChild`／
    `ReplaceFirst` 與 `OdfFormulaDocument.ReplaceFirst`，最小「尋找→取得→更新」語意編輯 helper
    有程式與 `FormulaHighLevelApiTests` 證據；2026-06-29 已補齊分數／根號／上下標／矩陣的具名
    符號級存取與替換 API（`Numerator`／`Denominator`／`Radicand`／`RootIndex`／`Exponent`／
    `SubscriptIndex`／`RowCount`／`GetRow`／`GetCell`／`WithRow`／`WithCell`／`AddRow`／
    `RemoveRow`），符號級編輯模型延伸工作已完成。
- `.odi`／`.oti`／`.fodi`（Batch 5，2026-06-23）：現況調查發現多影像框架、版面配置、旋轉、
  裁切、濾鏡與描述性 metadata（`svg:title`／`svg:desc`）等必要能力早已完整實作
  （`GetImageFrames`／`AddImageFrame`／`UpdateImageFrame`／`RemoveImageFrame`／
  `SetImageRotation`／`SetImageCrop`／`SetImageFilter`）；圖層與群組支援經查證為 ODF 規格層級
  不支援（`office:image` 不同於 `office:drawing`，規格未定義 layer／group 容器），維持先前
  「已查證不可行」的結論不變。主文件建議使用短名 facade `ImageDocument`；
  既有 `OdfImageDocument` 入口仍保留相容性。本次補上確認缺失的部分：
  - 新增批次操作 API `ImageDocument.AddImageFrames(IEnumerable<OdfImageFrameRequest>)`／
    `RemoveImageFrames(IEnumerable<string>)`，對應「Frame／picture／layout
    的批次操作」要求。
  - 新增雙向轉換工作流 `ImageTemplateDocument.CreateFromDocument(...)` ↔
    `ImageDocument.CreateFromTemplate(...)`；
    `FlatImageDocument.CreateFromDocument(...)` ↔
    `ImageDocument.CreateFromFlatDocument(...)`。新增
    `ImageVariantRoundTripTests`（雙向往返、邊界測試）。
  - **修正一個既有文件註解的不準確描述**：`ImageDocument_PackageStructureMatchesOdf14Schema`
    原先聲稱 LibreOffice 對 ODI／OTI／FODI 一律回報「source file could not be loaded」；
    實測確認此描述對 ODI／OTI 成立，但 **FODI 實際上被誤判為「Writer document」**，以
    `writer_png_Export` 篩選器產生與影像內容完全無關的輸出，與 `.fodc`（誤判為 Writer
    document）、`.fdf`（誤判為 Calc document）的誤判模式一致。已修正文件註解用語，並擴充該
    測試涵蓋 ODI／OTI／FODI 三者的封裝結構驗證（先前僅涵蓋 ODI）。
  - 2026-06-23 完成時依「Template／Flat 變體內容編輯仍沿用基底格式語意 API，未有專屬深度
    內容模型」為理由維持 `usable`／`usable-variant`；2026-06-24 依 5 項完成標準
    重新檢視，確認已全部滿足，**升級為 `workflow-verified`**（理由與 Chart／Formula
    相同：深度內容模型差異不在最低完成標準之內）。
- `.odb`（Batch 6，2026-06-23）：現況調查確認 Database 已具備資料來源、查詢、表單與報表等
  常見工作流能力，
  並無比照 Chart（Legend 物件模型與資料標籤 preset）那樣明確追蹤的延伸項目；
  Formula 的最小語意編輯 helper 已另有程式與測試證據，
  經評估後依使用者先前確認的「ODB workflow-verified 標準採真實可用工作流為準」決策，**升級為
  `workflow-verified`**。主文件建議使用短名 facade `DatabaseDocument`；既有
  `OdfDatabaseDocument` 入口仍保留相容性：
  - **資料來源**：連線 href、登入（`DatabaseDocument.GetLogin`／`SetLogin`）、驅動程式設定
    （`GetDriverSettings`／`SetDriverSettings`）。
  - **查詢**：SQL 命令、`ORDER BY`／`WHERE` 陳述式、可見欄位、更新目標表、escape processing
    （`DatabaseDocument` 繼承的查詢 API，實作位於 `OdfDatabaseDocument.Queries.cs`）。
  - **表單**：完整表單設計器 `OdfDatabaseFormDesigner`，涵蓋文字框、核取方塊、選項按鈕、下拉
    選單、列表框、按鈕、標籤、群組框、數值／日期／時間欄位，並支援事件繫結與必填／長度驗證。
  - **報表**：因官方 OASIS ODF schema 並未定義報表內容結構（先前以虛構命名空間
    `urn:oasis:names:tc:opendocument:xmlns:report:1.0` 推測的設計已查證不可行並移除），改以
    `AddReport` 的 `href` 參照機制連結至獨立 `TextDocument`（搭配
    `text:database-display`／`text:database-next` 欄位），這是真實可用且符合規格的作法。
  - **Schema 導覽與 mutation**：`OdfDatabaseSchema` 提供資料表、欄位、主鍵、外鍵、索引的完整
    CRUD（先前基於推測的「ODB 檢視表定義」已查證不可行並移除）。
  - **互通驗收**：實機重新驗證（2026-06-23）發現 LibreOffice 26.2.1 headless 的
    `--convert-to` 對 ODB 的失效模式比先前記錄更隱晦——轉換目標為 `odb` 時明確回報
    「no export filter」，但轉換目標為 `txt`／`ods`／`xlsx`／`csv` 時卻以結束碼 0 成功，
    實際上只是逐位元組原樣複製來源檔案、並未真正轉換（已修正
    `LibreOfficeInteropTests.DatabaseSchemaPackageUsesLibreOfficeCompatibleMimeType` 的文件
    註解用語）。改以封裝層級 mimetype／manifest 驗證搭配先前已完成的 LibreOffice UNO API
    （`desktop.loadComponentFromURL`）人工驗證佐證真實載入能力。
  - **邊界測試**：新增 `DatabaseBoundaryTests`，涵蓋 `AddTable`／`AddQuery` 空白名稱或命令時
    擲出 `ArgumentException`、`RemoveTable`／`RemoveQuery`／`RemoveDataSourceSetting` 對不存在
    名稱回傳 `false`、`FindTable`／`FindQuery`／`FindDataSourceSetting` 對不存在名稱回傳
    `null`。
  - **Template／Flat 變體**：ODF 規格設計上即未定義 ODB 的 template 或 flat XML 變體（不同於
    其他七個格式族），故「變體專屬 workflow」此項不適用（N/A），非缺口。
- ODT `text:tracked-changes` 已支援段落與表格儲存格插入／格式變更記錄；LO 互通測試已備（`TrackedChangesInteropTests`）。
- ODS `table:tracked-changes` 已支援儲存格內容／公式變更、列／欄插入刪除與儲存格移動；LO 互通測試已新增（需本機 LibreOffice 26.x）。
- ODG 已補強路徑、多邊形、連接線（含 `draw:points` 路由）、自定義幾何、群組、圖層、文字方塊、圖片與圖層指派讀取 API（`GetPaths`／`GetConnectors`／`GetPolygons`／`GetCustomShapes`／`GetGroups`／`GetLayers`／`GetTextBoxes`／`GetPictures`／`GetShapeLayerAssignments`）；測試見 `DrawingHighLevelApiTests`。
- ODC／嵌入圖表已補強 `OdfChartDocument.GetChartDefinition`；ODB 已補強 `AddForm`／`GetForms` 表單元件 API（`DatabaseHighLevelApiTests`）。
- ODF 已補強 `GetMathTokens` 讀取 API；ODI 已補強 `GetImageFrames`／`AddImageFrame`（`FormulaHighLevelApiTests`、`ImageHighLevelApiTests`）。
- LibreOffice `loext` Argon2id 與 `calcext` 條件格式／sparkline 寫入已實作；CALCEXT-1 基礎 ✅：工作表層與 `SpreadsheetDocument.GetConditionalFormats`／`GetSparklineGroups` 文件層聚合讀取。
- `.odc`、`.odb`、`.odf`、`.odi` 已納入 schema v4 深度語意證據契約：
  ODC 補齊單一序列移除，ODB 補齊 table／query 更新與集合清除，ODF 補齊 token 移除與清除，
  ODI 補齊保留框架版面／替代文字時的影像內容替換；既有圖表樣式、資料庫 schema、
  MathML 符號樹與影像效果 API 均納入逐操作證據。ODI 的多框架操作屬 extended-document
  工作流；ODF 1.4 嚴格 ODI 仍是單一 `draw:frame` 包含單一 `draw:image`，不宣稱圖層或群組。
- 次要格式與變體高階物件模型補完工作（原 Batch 1-6 + 測試補強，已於 2026-06-23 全數完成並移除
  追蹤文件）：ODC／ODB／ODI／ODF 公式四項次要格式高階物件模型，以及範本變數系統
  （`text:user-field-decls`）、範本清除使用者資料、範本區段唯讀標記、ODM 主控文件子文件
  CRUD 完整化／條件式載入／合併為單一文件／大綱階層位移、Flat XML ↔ ZIP 就地轉換 API 與
  大型文件記憶體優化。原規劃中基於推測而非實際 schema 查證的項目（ODB 檢視表定義、
  報表詳細設計、ODI 中繼資料擴充與分組圖層）已查證為不可行並從規劃中移除。
- RDF-1 基礎 ✅：`manifest.rdf` 文件層往返、`pkg:` ontology 同步；corpus 含 `repo-generated-manifest-rdf-text`（`RdfMetadataTests`）。
- LOEXT-1 基礎 ✅：`loext:decorative` 載入映射至 `draw:decorative`（`OdfLoExtInteropEngine`、`LoExtInteropTests`）。
- repo 內 corpus 已擴充至 266 筆 fixtures（`tools/OdfCorpusGenerator` + 手工負向／版本特例，詳見 [Corpus Manifest 規則](corpus-manifest.md)）；
  外部 ODF Validator baseline corpus 仍可依 `ODFKIT_PARITY_CORPUS_ROOT` 選用擴充。
- Typed DOM 已新增 `office:text`、`table:table`、`draw:page`、`office:presentation`／`office:drawing` 與次格式 `office:chart`／`office:image`／`office:database`／`office:spreadsheet` content model facade（Wave 1 M-3）；
  `tools/OdfSchemaGenerator/oasis-odf14-dom-wrappers.json` 供手動重產 DOM wrappers。
