# 次要格式與變體高階物件模型補完計畫（P2 待辦）

本文件記錄 [docs/odf-format-support.md](odf-format-support.md) 中列為 `usable`／`usable-variant`
的次要格式（ODC／ODB／ODI／ODF 公式）與範本／主控文件／Flat XML 變體，在 Batch 1（P0/P1）與
Batch 2（ODC/ODB 進階項目）完成後，仍待推進的 P2 原子任務清單。

沿用專案既有的原子任務 + FIND/REPLACE 精確度慣例（見 `AGENTS.md`），任務之間彼此獨立，可依優先級
分批執行，不要求單批全部完成。

**狀態（Batch 6 後）**：原規劃的 P2 原子任務已全數處理完畢 —— 部分完成實作（見各 Batch 完成紀錄），
部分經查證後確認不可行而從待辦移除（見「已查證為不可行」一節）。僅剩「測試補強」一節列出的
開放性測試覆蓋率改善項目，性質上非原子任務，留待未來視需要再行處理。

## Batch 2 完成紀錄（ODC + ODB 進階項目）

以下項目已完成，測試見 `ChartHighLevelApiTests`（C-3~C-7）與 `DatabaseHighLevelApiTests`（B-3、B-4、B-6、B-8、B-9）：

- **C-3**：軸標籤格式 —— 擴充 `OdfChartStyle.LabelPosition`／`LabelPositionNegative`／`AxisLabelPosition`（`chart:label-position`、`chart:label-position-negative`、`chart:axis-label-position` style 屬性）。
- **C-4**：圖例進階設定 —— 新增 `OdfChartDocument.LegendAlignment`（`chart:legend-align`）與 `LegendStyle`（重用 `OdfChartStyle` 背景／邊框屬性）。
- **C-5**：資料點細粒度樣式 —— 新增 `OdfChartSeries.GetDataPoints`／`AddDataPoint`／`ClearDataPoints`（`chart:data-point` 搭配 `chart:repeated`）。
- **C-6**：3D 透視與照明 —— 新增 `OdfChartDocument.GetLights`／`AddLight`／`ClearLights`（`dr3d:light`）與 `OdfChartStyle.Projection`／`LightingMode`（`dr3d:projection`、`dr3d:lighting-mode`）。
- **C-7**：股票圖標記 —— 新增 `OdfChartDocument` 的 `Get/SetStockGainMarkerStyleName`、`Get/SetStockLossMarkerStyleName`、`Get/SetStockRangeLineStyleName`（`chart:stock-gain-marker`／`chart:stock-loss-marker`／`chart:stock-range-line`）。
- **B-3**：表單進階控制項 —— `OdfDatabaseFormDesigner` 新增 `AddRadioButton`／`AddComboBox`／`AddNumericField`／`AddDateField`／`AddTimeField`（`form:radio`／`form:combobox`／`form:number`／`form:date`／`form:time`，均為真實 schema 元素）。
- **B-4**：連線登入與驅動程式設定 —— `OdfDatabaseDocument` 新增 `Get/SetLogin`（`db:login`）與 `Get/SetDriverSettings`（`db:driver-settings`）。
- **B-6**：查詢結構化設定（原規劃的「查詢參數化」經查證 ODB schema 並無 `db:parameter` 概念，改以實際存在的元素實作）—— 新增 `Get/SetQueryOrderStatement`（`db:order-statement`）、`Get/SetQueryFilterStatement`（`db:filter-statement`）、`Get/SetQueryColumns`（`db:columns`）、`Get/SetQueryUpdateTable`（`db:update-table`）。
- **B-8**：表單控制項事件與驗證（原規劃的 `SetControlValidation` 經查證後改為直接曝露真實 schema 屬性）—— `OdfDatabaseFormDesigner` 新增 `SetControlEvent`（`office:event-listeners`／`script:event-listener`）、`SetControlRequired`（`form:input-required`）、`SetControlMaxLength`（`form:max-length`）。
- **B-9**：表單分組容器 —— `OdfDatabaseFormDesigner` 新增 `AddGroupBox`（`form:frame`，真實 schema 群組框元素；不含格線版面配置，因 ODF 表單版面一律採絕對座標，無格線版面概念）。

## 已查證為不可行／需另行處理的項目

- **B-5（原規劃：檢視表定義 `db:view-definition`）**：經比對官方 ODF 1.4 RELAX NG schema，
  `database:1.0` 命名空間下**不存在** `view-definition` 或任何「視圖」相關元素。ODF 資料庫文件
  格式不在套件層級儲存 SQL 視圖定義（視圖由實際資料庫引擎管理）。此任務**不可行**，從待辦中移除。
- **B-7（原規劃：報表詳細設計 `report:master-detail`／`report:group`）**：經查證，
  `urn:oasis:names:tc:opendocument:xmlns:report:1.0` **並非官方 OASIS ODF 命名空間**
  （未出現在 `Odf14OfficialSchemaProvider.g.cs` 任何一處）。專案既有的
  `OdfDatabaseFormDesigner.DefineReportStructure` 方法本身就建立在這個非標準命名空間上，
  屬於先前批次遺留的問題，**不應在此基礎上繼續擴充**。
  **已修正**（2026-06-22）：查證 LibreOffice 原始碼（`reportbuilder/.../OfficeNamespaces.java`）確認
  「報表設計工具」實際使用的是 LibreOffice 自有的 `http://openoffice.org/2005/report`（`rpt:`）命名空間，
  屬於 Pentaho／JFreeReport 內部實作細節，非公開穩定的 OASIS 規格，不適合逆向工程後納入本專案 API。
  而 LibreOffice Base「使用嚮導建立報表」產生的報表，其本質是一般 ODT 文字文件，搭配官方 ODF schema
  既有的 `text:database-display`／`text:database-next` 欄位元素綁定資料表欄位。
  因此移除了 `ReportNamespace` 常數與 `DefineReportStructure` 方法（**破壞性變更**；
  經 Grep 確認專案中除其自身單元測試外無其他呼叫處），改為：
  1. 在 `OdfKit.Text`（`TextDocumentFieldsEngine`／`TextDocument.Elements.Fields.cs`／`OdfParagraph`）
     新增 `OdfParagraph.AddDatabaseDisplayField`／`AddDatabaseNextField`，產生真實 schema 元素。
  2. 報表內容應建立為獨立 `TextDocument`，再透過既有的
     `OdfDatabaseDocument.AddReport(name, href, ...)` 的 `xlink:href` 參照機制連結至 .odb 套件
     （`AddReport` 先前已支援此機制，無需新增）。
  3. 測試見 `OdfKit.Tests/DatabaseSchemaAndFormTests.cs` 的
     `Paragraph_DatabaseFields_ProduceValidSchemaElements`／
     `Database_ReportLinkedViaHref_ToTextDocumentBasedReport`。

## Batch 3 完成紀錄（F-3、T-2、T-3、M-2）

以下項目已完成，測試見 `FormulaHighLevelApiTests`（F-3）、`TemplateRoundTripTests`（T-2、T-3）、
`MasterDocumentTests`（M-2）：

- **F-3**：MathML 屬性層級代理 API —— `OdfMathToken` 新增 `Attributes` 與 `WithAttribute`，
  支援 `mathvariant`、`mathsize`、`mathcolor`、`mathbackground`、`displaystyle`、`stretchy`、
  `lspace`、`rspace` 等 W3C MathML 標準屬性（此為 W3C MathML 規格範疇，非 OASIS ODF schema 管轄，
  故依 W3C 規格知識實作，不在 `Odf14OfficialSchemaProvider.g.cs` 查證範圍內）。
  附帶修正：`CreateStyleNode`／`CreateStyleToken` 原先以 MathML 命名空間寫入／讀取 `displaystyle`
  屬性（序列化為 `math:displaystyle`），不符合 MathML 屬性應無命名空間前綴的規格，已修正為空命名空間。
- **T-2**：範本清除使用者資料 —— `OdfDocument` 新增 `protected virtual ClearTemplateUserContent()`
  鉤子，`CreateFromTemplate` 系列方法新增 `clearUserContent` 選用參數（預設 `false`，不影響既有行為）。
  `TextDocument` 清空 `BodyTextRoot` 子節點；`SpreadsheetDocument` 移除各工作表 `table:table-row`
  但保留 `table:table-column`（欄寬）；`PresentationDocument`／`DrawingDocument` 遞迴清空
  `text:p` 文字內容但保留形狀／框架結構（透過共用的 `ClearParagraphTextContentRecursive`）。
- **T-3**：範本區段唯讀標記 —— `OdfSection.IsProtected`（`text:protected`）原僅有 getter，
  已補上 setter，可在範本中將特定區段標記為唯讀。
- **M-2**：主控文件子文件條件式載入 —— `OdfSubDocumentReference` 新增 `Actuate` 欄位（**破壞性變更**：
  3 元位置記錄解構需多接一個變數，已修正 `TextApiUsabilityTests` 既有呼叫處）；
  `AddSubDocumentReference` 新增 `loadOnRequest` 參數；新增 `TextMasterDocument.SetSubDocumentLoadOnRequest`
  可變更既有參照的 `xlink:actuate` 為 `onLoad`／`onRequest`。

## Batch 4 完成紀錄（M-4）與 I-4 查證結論

- **M-4**：主控文件合併 —— 新增 `TextMasterDocument.MergeSubDocuments(baseDirectory, options?)`，
  依文件順序走訪主控文件本身內容，將子文件參照區段就地替換為實際載入的外部 `.odt` 子文件內容
  （透過既有的 `OdfDocument.AppendDocument`／`OdfMergeOptions` 樣式合併與衝突重新命名機制），
  其餘一般內容節點直接複製。測試見 `MasterDocumentTests.MergeSubDocuments_CombinesOwnContentAndSubDocumentsInOrder`。
- **I-4（查證結論：原規劃不可行，已移除）**：經比對官方 ODF 1.4 schema，`dc:title`／`dc:description`
  僅是 `office:meta`（文件層級 `meta.xml`）的子元素，**並非** `draw:frame`／`draw:image` 之類逐框架
  元素的合法子節點；`svg:metadata` 在任何版本 schema 中**完全不存在**。原規劃的「per-frame dc:\*／
  svg:metadata 型別化存取」因此不可行。實際上文件層級中繼資料（標題、作者等）`OdfImageDocument`
  已透過繼承 `OdfDocument.Title`／`Creator` 等屬性免費取得；逐框架無障礙替代文字則由 Batch 1 既有的
  `svg:title`／`svg:desc`（`OdfImageFrameInfo.Title`／`Description`）涵蓋。此項目視為已達成現有 schema
  下的完整覆蓋，從待辦中移除。

## Batch 5 完成紀錄（F-4）與 I-3 查證結論

- **F-4**：MathML → LaTeX 反向轉換 —— 新增 `OdfFormulaLatexConverter.ToLatex(IReadOnlyList<OdfMathToken>)`
  與 `OdfFormulaDocument.ToLatex()`，依 `OdfMathToken` 樹狀結構（分數、根號、矩陣、上下標、
  上下方標記、括號群組、樣式群組）反向組裝為 LaTeX 字串，並支援 `mathvariant`（bold／italic／
  bold-italic）對應至 `\mathbf`／`\mathit`／`\boldsymbol`。屬 best-effort 轉換：LaTeX 與 MathML
  並非一對一對應，部分語意（例如 `Fenced` 還原為 `\left`／`\right` 而非原始字面括號字元的巨集）
  可能無法完整保留原始 LaTeX 輸入的字面形式，但語意等價。
- **I-3（查證結論：原規劃不可行，已移除）**：經比對官方 ODF 1.4 schema，`office:image` 的
  `office-image-content-main` 規則**僅允許恰好一個 `draw:frame`** 作為內容（`exactlyOne`），
  `draw:g`（群組）與 `office:layer`（圖層）**並非** `office:image` 內容模型的合法子節點 —— 這兩者
  僅在 `office:drawing`（ODG 繪圖文件）等不同內容模型中才有效。原規劃的「ODI 影像分組／圖層結構」
  因此不可行，從待辦中移除。
  （附帶觀察，非本批修正範圍：嚴格而言 schema 規定 `office:image` 僅能有一個 `draw:frame`，但
  Batch 1 既有的 `GetImageFrames`／`AddImageFrame` 支援同一份 `.odi` 內有多個 `draw:frame`
  並通過既有真機 LibreOffice 互通測試，屬已驗證過的既有設計，不在本次查證範圍內變動。）
- **M-3**：主控文件子文件大綱層級管理 —— 新增 `TextDocument.ShiftHeadingOutlineLevels(offset)`，
  遞迴調整文件中所有 `text:h`（含區段內巢狀標題）的 `text:outline-level`，結果限制最小為 1；
  並於 `TextMasterDocument.MergeSubDocuments` 新增 `subDocumentOutlineOffset` 參數，
  在併入前先位移每份子文件的標題階層，使其能正確巢狀於主控文件本身的標題結構之下。

## Batch 6 完成紀錄（F-5）

- **F-5**：數學樣式與語意標記化 —— 經查證，`accent`／`accentunder` 是 W3C MathML 標準屬性
  （`munder`／`mover`／`munderover` 用於標示重音裝飾記號，與一般極限記號語意有別），但讀取端
  `KnownMathAttributeNames` 既有允許清單未涵蓋，造成寫入後讀回會遺失此屬性。已修正補上
  `accent`／`accentunder`，並新增 `OdfMathBuilder.Accent` 便利方法（序列化為 `mover` 並設定
  `accent="true"`）。另新增 `OdfMathTokenKind.Apply`／`OdfMathToken.Apply`／
  `OdfMathBuilder.Apply` 提供 Content MathML `<apply>` 基礎支援（運算子 + 運算元清單），
  支援常見二元運算子（`plus`／`minus`／`times`／`divide`／`eq`／`neq`／`lt`／`gt`／`leq`／`geq`）
  與 `power`／`root`，並擴充 `OdfFormulaLatexConverter.ToLatex` 對應轉換為 LaTeX 中綴／前綴運算式；
  未知運算子 fallback 為 `\operatorname{name}(...)`。
- **X-2**：Flat XML 大型文件記憶體優化 —— 經查證現有 `OdfPackageFlatXmlLoader`／
  `OdfPackageArchiveWriter` 的載入／儲存管線本身已對最大記憶體成本來源（內嵌二進位影像的
  Base64 編碼文字）採用串流式分塊處理（`ReadElementContentAsBase64` 搭配 `ArrayPool`），
  屬既有合理設計，非天真疏漏；但其餘 XML 結構內容（清理後的暫存緩衝區、組裝/拆解
  `content.xml`／`styles.xml`／`meta.xml`／`settings.xml` 四份子文件用的中間緩衝區）一律使用
  `MemoryStream`，對結構元素極多但二進位內容較少的大型文件（例如百萬列的試算表）仍會一次性佔用
  大量記憶體；非同步儲存路徑 `WriteFlatXmlToStreamAsync` 更是無條件先完整緩衝於記憶體後才非同步
  複製至目標串流，完全未達到串流節省記憶體的效果。新增共用 `OdfTempStreamFactory`
  （依預估大小於 50 MB 門檻切換記憶體／隨關閉自動刪除暫存檔資料流，沿用既有
  `OdfPackageSaver.CreateTempStream` 已驗證過的機制），並套用至 `OdfPackageFlatXmlLoader.Initialize`
  的清理用暫存緩衝區與 `OdfPackageArchiveWriter.WriteFlatXmlToStreamAsync` 的輸出緩衝區，
  超過門檻時改用暫存檔，避免大型文件一次性佔滿記憶體（完全重寫為逐節點串流組裝
  屬架構層級變更、風險過高，不在此原子任務範圍內，故採用此風險可控的記憶體門檻優化）。

## 測試補強

- 補齊試算表／簡報／繪圖範本的 Round-trip 測試（目前 `TemplateRoundTripTests` 僅覆蓋文字範本）。
- Flat XML 大檔案轉換／載入的效能基準測試。
- 主控文件子文件合併、條件式載入場景的整合測試。

## 重要提醒：實作前必須先查證 schema

Batch 2 過程中發現原規劃的 B-5、B-6、B-7、B-8 部分項目，是基於對 ODF 格式的推測而非實際 schema
查證所寫。**後續批次開始實作任何項目前，務必先比對
`OdfKit/Compliance/Generated/Odf14OfficialSchemaProvider.g.cs`（或其他版本對應檔案）確認元素／
屬性確實存在於對應命名空間，避免發明不存在的 XML 結構。** 上方「已查證為不可行」一節即為此教訓的記錄。

## 驗證步驟

1. `dotnet build`
2. `dotnet test`
3. `pwsh eng/Format-Safe.ps1 -IncludeTests`
4. `pwsh eng/Test-MergeConflictMarkers.ps1`
