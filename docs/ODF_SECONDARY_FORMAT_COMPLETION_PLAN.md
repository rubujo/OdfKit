# 次要格式與變體高階物件模型補完計畫（Batch 2 待辦，P2）

本文件記錄 [docs/odf-format-support.md](odf-format-support.md) 中列為 `usable`／`usable-variant`
的次要格式（ODC／ODB／ODI／ODF 公式）與範本／主控文件／Flat XML 變體，在 Batch 1（P0/P1，已完成，
詳見對應測試：`ChartHighLevelApiTests`、`DatabaseHighLevelApiTests`、`ImageHighLevelApiTests`、
`FormulaHighLevelApiTests`、`TemplateRoundTripTests`、`MasterDocumentTests`）之後，仍待推進的
P2 原子任務清單。

沿用專案既有的原子任務 + FIND/REPLACE 精確度慣例（見 `AGENTS.md`），任務之間彼此獨立，可依優先級
分批執行，不要求單批全部完成。

## ODC 圖表（Chart）

- **C-3**：軸標籤格式（`chart:axis` 的 `chart:label-placement`、刻度標籤旋轉角度）的型別化 API。
- **C-4**：圖例進階設定（`chart:legend` 背景色、邊框樣式、自動位置調整）。
- **C-5**：資料點細粒度樣式（`chart:series` 下個別 `chart:data-point` 的顏色／樣式覆蓋）。
- **C-6**：3D 圖表透視與照明設定（`chart:scene` 下的 perspective／lighting 屬性）。
- **C-7**：股票圖漲跌標記（`chart:stock-gain-marker`／`chart:stock-loss-marker`）。

## ODB 資料庫（Database）

- **B-3**：表單進階控制項（`form:radio`、`form:combobox`、`form:numeric`、`form:date`、`form:time`）。
- **B-4**：連線資源進階設定（`db:connection-data` 下多組 `db:parameter`、連線字串建構器）。
- **B-5**：檢視表定義（`db:view-definition`，`OdfDatabaseSchema.AddView`／`Views`）。
- **B-6**：查詢參數化（`OdfDatabaseQueryParameter`，結構化 JOIN／排序／篩選條件 builder）。
- **B-7**：報表詳細設計（`report:master-detail`、`report:group` 群組與欄位繫結的可編輯集合）。
- **B-8**：表單控制項事件與驗證屬性（`SetControlEvent`／`SetControlValidation`）。
- **B-9**：表單分組與版面配置容器（`form:groupbox`／`form:frame`、格線版面定義）。

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

## 驗證步驟（沿用 Batch 1）

1. `dotnet build`
2. `dotnet test`
3. `pwsh eng/Format-Safe.ps1 -IncludeTests`
4. `pwsh eng/Test-MergeConflictMarkers.ps1`
