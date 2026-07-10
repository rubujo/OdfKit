# Spreadsheet 資料工作流

`SpreadsheetDocument` 與 `OdfTableSheet` 都提供物件資料繫結入口。文件層多載以工作表名稱定位，
工作表層多載直接操作目前 facade；兩者共用相同 options 與 report 契約。

| 目的 | API | 結果 |
| --- | --- | --- |
| 寫入物件集合 | `WriteObjects<T>` | `OdfObjectBindingReport` |
| 讀回物件集合 | `ReadObjects<T>` | `IReadOnlyList<T>` |
| 寫入前檢查欄位與資料 | `ValidateObjectBinding<T>` | `OdfObjectBindingValidationReport` |
| 依 key 更新既有列 | `UpdateObjects<T>` | `OdfObjectBindingReport` |
| 更新既有列並新增缺少列 | `UpsertObjects<T>` | `OdfObjectBindingReport` |

`OdfObjectColumnMap` 控制欄名、順序、忽略欄位、別名、必要欄位與預設值；讀取與更新行為分別由
`OdfObjectReadOptions`、`OdfObjectUpdateOptions` 控制。Upsert 可保留未對應儲存格，並可從範本列
複製樣式與公式；相對 A1 列參照會依列位移，但不承諾完整 OpenFormula AST rewrite。

大量循序輸出應改用 `OdsStreamWriter`；物件繫結適合需要讀回、驗證、更新、樣式或既有文件
round-trip 的工作流。巢狀集合展開、ORM tracking 與完整試算表重算不屬於此 API 契約。

完整範例見 [Cookbook：ODS 表格化資料、篩選與排序](../cookbook.md#ods-表格化資料篩選與排序)。
