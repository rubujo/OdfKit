# 次要格式與變體高階物件模型補完計畫（P2 待辦）

本文件記錄 [docs/odf-format-support.md](odf-format-support.md) 中列為 `usable`／`usable-variant`
的次要格式（ODC／ODB／ODI／ODF 公式）與範本／主控文件／Flat XML 變體，在 Batch 1（P0/P1）與
Batch 2（ODC/ODB 進階項目）完成後，仍待推進的 P2 原子任務清單。

沿用專案既有的原子任務 + FIND/REPLACE 精確度慣例（見 `AGENTS.md`），任務之間彼此獨立，可依優先級
分批執行，不要求單批全部完成。

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

## ODI 影像（Image）

- **I-3**：影像分組／圖層結構（`draw:g`、`office:layer`，新增 `OdfImageGroup`／`OdfImageLayer`）。
- **I-4**：完整無障礙與中繼資料擴充（`svg:metadata`、`dc:*`、ARIA 屬性的型別化存取）。

## ODF 公式文件（Formula）

- **F-3**：MathML 屬性層級代理 API（`mathvariant`、`displaystyle`、`rspace`／`lspace`、`stretchy` 等）。
- **F-4**：MathML → LaTeX 反向轉換引擎（`OdfFormulaDocument.ToLatex`），與既有 `FromLatex` 對稱。
- **F-5**：數學樣式與語意標記化（accent 語意、Content MathML `<apply>` 基礎支援）。

## 範本／主控文件／Flat XML 變體

- **T-2**：試算表／簡報／繪圖範本專屬「清除使用者資料但保留格式／版面配置」方法
  （目前僅 `CreateFromTemplateInternal` 完整複製，無選擇性清空選項）。
- **T-3**：範本鎖定／唯讀區段標記（標記範本中特定區段為唯讀，防止使用者誤改）。
- **M-2**：主控文件子文件條件式納入／排除（對應 `xlink:actuate="onRequest"` 等延遲載入語意）。
- **M-3**：主控文件子文件大綱層級管理（`text:outline-level` 跨子文件一致性）。
- **M-4**：將主控文件與所有子文件內容合併為單一 ODT 的便利 API。
- **X-2**：Flat XML 串流處理 API（避免一次性將整個 XML 載入記憶體，供大型文件使用）。

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
