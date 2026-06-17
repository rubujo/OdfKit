# ODF 格式支援矩陣

本文件記錄 OdfKit 對主要 ODF 格式的實作狀態。狀態以目前程式碼與
測試證據為準，不把路線圖中的目標預先標為完成。

ODF Toolkit / ODF Validator 對標線另見 [odf-toolkit-parity.md](odf-toolkit-parity.md)。
完滿化路線圖見 [eng/OdfKit-Completeness-Plan.md](../eng/OdfKit-Completeness-Plan.md)。

## 狀態標記

### 封裝與驗證層

- `complete`：已有直接 API 與測試覆蓋，可作為目前支援能力使用。
- `validated`：已有驗證或偵測測試證據，但高階 API 仍可能有限。
- `package-level`：可建立、載入、保存與驗證最小封裝，但高階語意 API 尚未完整。
- `partial`：已有部分高階模型或 round-trip 能力，但仍有明確缺口。
- `planned`：尚未有足夠程式與測試證據支撐。

### 高階 API 層（Wave 1 分級）

- `complete`：日常辦公自動化不需下沉 DOM（Wave 2 目標，目前尚無）。
- `usable`：常用建立／編輯 API 可用，仍有明確語意缺口。
- `package-only`：僅封裝層 round-trip；高階語意模型尚未專屬化或仍共用基底 wrapper。
- `usable-variant`：具專屬 typed 文件類別與 `Create`/`Load` 入口；語意 API 繼承基底格式（Wave 2 VAR-1）。

## 矩陣

| Extension | MIME type | `OdfDocumentKind` | Detect | Create | Load | Save | Validate | Round-trip | High-level API | Test evidence |
|---|---|---|---|---|---|---|---|---|---|---|
| `.odt` | `application/vnd.oasis.opendocument.text` | `Text` | complete | complete | complete | complete | validated | complete | usable | `TextApiUsabilityTests`, `FourFormatApiScenarioTests`, `TextAdvancedFidelityTests`, `ComplianceTests`, `InteropCorpusTests` |
| `.ott` | `application/vnd.oasis.opendocument.text-template` | `TextTemplate` | complete | complete | complete | complete | validated | complete | usable-variant | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `E2ETests`, `InteropCorpusTests` |
| `.odm` | `application/vnd.oasis.opendocument.text-master` | `TextMaster` | complete | complete | complete | complete | validated | complete | usable-variant | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `InteropCorpusTests` |
| `.fodt` | `application/vnd.oasis.opendocument.text` | `FlatText` | complete | complete | complete | complete | validated | complete | usable-variant | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `E2ETests`, `PackageRoundTripTests` |
| `.ods` | `application/vnd.oasis.opendocument.spreadsheet` | `Spreadsheet` | complete | complete | complete | complete | validated | complete | usable | `SpreadsheetApiUsabilityTests`, `FourFormatApiScenarioTests`, `SpreadsheetCommonApiTests`, `OpenFormulaSupportTests`, `InteropCorpusTests` |
| `.ots` | `application/vnd.oasis.opendocument.spreadsheet-template` | `SpreadsheetTemplate` | complete | complete | complete | complete | validated | complete | usable-variant | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `E2ETests`, `InteropCorpusTests` |
| `.fods` | `application/vnd.oasis.opendocument.spreadsheet` | `FlatSpreadsheet` | complete | complete | complete | complete | validated | complete | usable-variant | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `E2ETests`, `PackageRoundTripTests` |
| `.odp` | `application/vnd.oasis.opendocument.presentation` | `Presentation` | complete | complete | complete | complete | validated | complete | usable | `PresentationApiUsabilityTests`, `FourFormatApiScenarioTests`, `PresentationAndRenderingTests`, `PresentationBoundaryTests`, `InteropCorpusTests` |
| `.otp` | `application/vnd.oasis.opendocument.presentation-template` | `PresentationTemplate` | complete | complete | complete | complete | validated | complete | usable-variant | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `E2ETests`, `InteropCorpusTests` |
| `.fodp` | `application/vnd.oasis.opendocument.presentation` | `FlatPresentation` | complete | complete | complete | complete | validated | complete | usable-variant | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `PackageRoundTripTests` |
| `.odg` | `application/vnd.oasis.opendocument.graphics` | `Graphics` | complete | complete | complete | complete | validated | complete | usable | `DrawingApiUsabilityTests`, `FourFormatApiScenarioTests`, `ComplianceTests`, `InteropCorpusTests` |
| `.otg` | `application/vnd.oasis.opendocument.graphics-template` | `GraphicsTemplate` | complete | complete | complete | complete | validated | complete | usable-variant | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `E2ETests`, `InteropCorpusTests` |
| `.fodg` | `application/vnd.oasis.opendocument.graphics` | `FlatGraphics` | complete | complete | complete | complete | validated | complete | usable-variant | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `PackageRoundTripTests` |
| `.odc` | `application/vnd.oasis.opendocument.chart` | `Chart` | complete | complete | complete | complete | validated | complete | usable | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `InteropCorpusTests` |
| `.odf` | `application/vnd.oasis.opendocument.formula` | `Formula` | complete | complete | complete | complete | validated | complete | usable | `DocumentKindApiUsabilityTests`, `PackageRoundTripTests`, `InteropCorpusTests` |
| `.odi` | `application/vnd.oasis.opendocument.image` | `Image` | complete | complete | complete | complete | validated | complete | usable | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `InteropCorpusTests` |
| `.odb` | `application/vnd.oasis.opendocument.database` | `Database` | complete | complete | complete | complete | validated | complete | usable | `DocumentKindApiUsabilityTests`, `ComplianceTests`, `InteropCorpusTests` |
| `.fodc` | `application/vnd.oasis.opendocument.chart` | `FlatChart` | complete | complete | complete | complete | validated | complete | usable-variant | `OdfFormatRoundTripTests`, `CorpusComplianceTests` |
| `.fdf` | `application/vnd.oasis.opendocument.formula` | `FlatFormula` | complete | complete | complete | complete | validated | complete | usable-variant | `OdfFormatRoundTripTests`, corpus manifest |
| `.fodi` | `application/vnd.oasis.opendocument.image` | `FlatImage` | complete | complete | complete | complete | validated | complete | usable-variant | `OdfFormatRoundTripTests`, `CorpusComplianceTests` |

## 目前缺口

- 統一的 `OdfDocument.Load` / `OdfDocument.Create` 與
  `OdfDocumentFactory.LoadDocument` / `CreateDocument` 高階入口已建立。
- ODT、ODS、ODP、ODG 標為 `usable`：已有常用高階 API 與 `FourFormatApiScenarioTests` 場景背書，但尚非完整辦公套件物件模型（Wave 2 `complete` 目標）。升級 `complete` 前仍須補齊：
  - **ODT**：索引進階編輯、頁首頁尾進階編輯（書籤／超連結／參考標記／腳注尾注／索引／註解／表格結構修訂讀取 `GetBookmarks`／`GetHyperlinks`／`GetReferenceMarks`／`GetFootnotes`／`GetEndnotes`／`GetPageSetups`／`GetIndexInfos`／`GetIndexMarks`／`GetCommentInfos`／`GetTableStructuralChanges` 已補；表格結構修訂寫入 `OdfTable.InsertRows`／`DeleteRows`／`InsertColumns`／`DeleteColumns` 已補）
  - **ODS**：樞紐表重算（見 non-goals）、完整圖表編輯模型（命名範圍／具名運算式／資料庫範圍／列印範圍／樞紐表／凍結／分割窗格讀取 `GetNamedRanges`／`GetNamedExpressions`／`GetDatabaseRanges`／`GetPrintAreas`／`GetPivotTables`／`GetFrozenPanes`／`GetSplitPanes` 已補）
  - **ODP**：版面配置進階編輯（母片列舉 `GetMasterPages`、切換效果與持續時間讀取 `GetSlideTransition`／`GetSlideTransitions`、動畫時間軸摘要（dur／begin／序號）與文件層聚合 `GetAnimations`／`GetPlaceholderInfos`／`GetSpeakerNotes`／`GetLayouts` 已補；動畫時間軸寫入 `Add*Effect` 持續時間參數與 `OdfAnimation.SetDuration`／`SetDelay` 已補）
  - **ODG**：群組圖形寫入進階（群組／圖層／文字方塊／圖片／圖層指派／連接線路由點讀取 `GetGroups`／`GetLayers`／`GetTextBoxes`／`GetPictures`／`GetShapeLayerAssignments`／`GetConnectors` 之 `Points` 已補；連接線路由寫入 `OdfShape.SetConnectorRoutePoints` 已補）
- `.ott`、`.ots`、`.otp`、`.otg`、`.odm` 與 Flat 變體標為 `usable-variant`（VAR-1 ✅）：
  具專屬 typed 文件類別與 `Create`/`Load` 入口；語意 API 繼承四主格式基底類別。
- ODT `text:tracked-changes` 已支援段落與表格儲存格插入／格式變更記錄；LO 互通測試已備（`TrackedChangesInteropTests`）。
- ODS `table:tracked-changes` 已支援儲存格內容／公式變更、列／欄插入刪除與儲存格移動；LO 互通測試已新增（需本機 LibreOffice 26.x）。
- ODG 已補強路徑、多邊形、連接線（含 `draw:points` 路由）、自定義幾何、群組、圖層、文字方塊、圖片與圖層指派讀取 API（`GetPaths`／`GetConnectors`／`GetPolygons`／`GetCustomShapes`／`GetGroups`／`GetLayers`／`GetTextBoxes`／`GetPictures`／`GetShapeLayerAssignments`）；測試見 `DrawingHighLevelApiTests`。
- ODC／嵌入圖表已補強 `OdfChartDocument.GetChartDefinition`；ODB 已補強 `AddForm`／`GetForms` 表單元件 API（`DatabaseHighLevelApiTests`）。
- ODF 已補強 `GetMathTokens` 讀取 API；ODI 已補強 `GetImageFrames`／`AddImageFrame`（`FormulaHighLevelApiTests`、`ImageHighLevelApiTests`）。
- LibreOffice `loext` Argon2id 與 `calcext` 條件格式／sparkline 寫入已實作；CALCEXT-1 基礎 ✅：工作表層與 `SpreadsheetDocument.GetConditionalFormats`／`GetSparklineGroups` 文件層聚合讀取。
- `.odc`、`.odf`、`.odi`、`.odb` 標為 `usable`：已有摘要與常用編輯 API；
  完整語意模型仍屬 Wave 2 DEPTH-2。
- RDF-1 基礎 ✅：`manifest.rdf` 文件層往返、`pkg:` ontology 同步；corpus 含 `repo-generated-manifest-rdf-text`（`RdfMetadataTests`）。
- LOEXT-1 基礎 ✅：`loext:decorative` 載入映射至 `draw:decorative`（`OdfLoExtInteropEngine`、`LoExtInteropTests`）。
- repo 內 corpus 已擴充至 200+ fixtures（`tools/OdfCorpusGenerator` + 手工負向／版本特例）；
  外部 ODF Validator baseline corpus 仍可依 `ODFKIT_PARITY_CORPUS_ROOT` 選用擴充。
- Typed DOM 已新增 `office:text`、`table:table`、`draw:page` content model facade（Wave 1 M-3）；
  完整 schema generator 產出仍屬後續擴充。