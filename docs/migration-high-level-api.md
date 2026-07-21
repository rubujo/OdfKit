# 高階 API 破壞性重整遷移指南

本指南適用於從早期 `0.0.1` API 草稿遷移至目前 ODT、ODS、ODP、ODG 高階
facade。此重整刻意不提供舊 API shim；載入既有文件與建立新文件均使用相同的
`TextDocument`、`SpreadsheetDocument`、`PresentationDocument`、`DrawingDocument`
入口。

## 統一生命週期契約

| 目的 | 新契約 | 回傳語意 |
| --- | --- | --- |
| 列舉集合 | `Get*` | 唯讀集合或 info 快照 |
| 查找單一項目 | `Find*` | 找不到時回傳 `null` |
| 建立項目 | `Add*`／`Create*` | 回傳領域物件或建立結果 |
| 修改項目 | `Set*`／`Update*`／`Rename*` | 單項修改回傳成功狀態或領域結果 |
| 移除指定項目 | `Remove*` | 找到並移除時回傳 `true` |
| 清空集合 | `Clear*` | 回傳移除數量，單一狀態則回傳 `bool` |

不要以元素順序、XML 前綴或封裝 entry 名稱自行維護關聯。高階 facade 會同步
處理 ID、style、manifest、media、公式參照、master／layout 與嵌入物件關係；只有
規格罕見語意或未知擴充需要進入 typed DOM。

## 主要遷移模式

### ODT

將直接操作 `text:*` 節點的欄位、表單、註解、修訂及內嵌公式程式碼，改為
`Get*`／`Find*`／`Add*`／`Update*`／`Remove*`／`Clear*` 集合生命週期。例如表單
控制項使用 `FindFormControl` 與 `ClearFormControls`；欄位使用 typed field info，
不再要求呼叫端解析裸 XML attribute。

### ODS

工作表、範圍、validation、conditional format、filter、sort、pivot、chart、external
link 與 view 狀態均由試算表領域 API 管理。嵌入圖表使用 `FindEmbeddedChart`、
`RemoveEmbeddedChart`、`ClearEmbeddedCharts`；凍結與分割窗格使用 sheet 上的
`Set*`／`Clear*` 或文件層的 `Find*`。公式 API 保證語法、參照位移與 cached value
保真，不應依賴它進行完整重算。

### ODP

投影片、master、layout、notes、handout、文字、表格、媒體、shape、connector、group、
transition 與 animation 使用簡報領域型別。複製或移除投影片時，讓 facade 修正
master／layout、媒體與 package 關聯，請勿複製底層 XML 節點。

### ODG

page、layer、shape、path、connector、group 與 appearance resource 使用繪圖領域型別。
gradient、marker、clip 與 z-order 可讀回後修改；例如以 `FindGradient`、
`RenameGradient`、`RemoveGradient`、`ClearGradients` 管理資源，而不是修改
`styles.xml`。

## 舊版本與未知內容

ODF 1.1～1.3 載入後映射至同一個 1.4 高階模型。預設儲存保留來源版本及無法映射的
未知內容；需要指定目標版本時，先呼叫
`document.AnalyzeVersionCompatibility(targetVersion)`。回傳的
`OdfVersionCompatibilityReport` 會列出目標版本無法表示的標準元素與屬性、命名空間、
DOM 路徑及來源／目標版本。指定 `TargetVersion` 或 `OdfSaveOptions.ForceVersion` 儲存時，
相同報告會保留於 `LastVersionCompatibilityReport`，也可透過
`VersionCompatibilityReportHandler` 即時接收。OdfKit 不會為問題項目捏造等價語意或
刪除未知內容。對 foreign namespace 的保留契約另見
[Foreign 擴充政策](foreign-extension-policy.md)。

## 遷移驗證

遷移完成後至少執行：建立、載入既有文件、修改、移除、儲存重載與未知內容保留測試。
完整能力與測試證據以 [semantic coverage manifest](semantic-coverage.json) 為準；API
工作流見 [四主格式語意 facade reference](reference/semantic-facades.md)。
