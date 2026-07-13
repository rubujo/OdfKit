# 統一診斷模型（OdfDiagnostic）

> 內容語系：正體中文（臺灣）（`zh-TW`）。

高階 API 的 report 類別以 `OdfKit.Core.OdfDiagnostic` 統一表達非致命診斷，取代各報告自行維護的
警告或缺漏名稱等純字串集合。原始字串集合仍然保留，`Diagnostics` 是其強型別檢視；既有程式碼
不需遷移，新程式碼建議一律改讀 `Diagnostics`。

## 形狀

| 屬性 | 型別 | 說明 |
| --- | --- | --- |
| `Code` | `string` | 識別診斷種類的簡短機器可讀代碼 |
| `Severity` | `OdfIssueSeverity` | 診斷嚴重程度，與 compliance 驗證共用同一列舉 |
| `Message` | `string` | 人類可讀描述（依 `OdfLocalizer` 語系在地化） |
| `PackagePath` | `string?` | 相關的套件內相對路徑（若適用） |
| `ObjectId` | `string?` | 受影響物件的識別碼，例如圖形或書籤名稱（若適用） |
| `Location` | `string?` | 自由格式位置提示，例如儲存格位址或 XPath（若適用） |

## 承載 `Diagnostics` 的 report 類別

| Report | 所屬工作流 |
| --- | --- |
| `OdfSingleSignatureValidationResult` | 數位簽章驗證 |
| `OdfShapeLayoutResult` | 繪圖版面配置 |
| `OdfExportReport` | 匯出 facade（HTML／PDF／Markdown／SVG 等） |
| `OdfImageBatchUpdateResult` | 影像批次更新 |
| `OdfBatchUpdateResult` | 文件批次更新 |
| `OdfTemplateBindReport` | 模板繫結（見 [模板繫結工作流](templates.md)） |
| `OdfRangeWriteReport` | 試算表儲存格與範圍寫入（見 [Spreadsheet 資料工作流](spreadsheet-data.md)） |
| `OdtMutationReport` | ODT 文字搜尋取代與結構變更 |

## 契約

- `Diagnostics` 只承載**非致命**資訊；會使作業失敗的錯誤仍以例外擲出。
- 空集合代表沒有診斷，不代表未執行檢查。
- `Code` 值屬穩定契約的一部分：新增 diagnostic code 時，依 [文件契約](index.md#文件契約)
  同步更新對應 reference 或明列其非目標。
