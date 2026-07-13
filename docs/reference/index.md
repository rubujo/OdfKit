# OdfKit API Reference

本目錄依 C# / .NET 實務工作流整理高階 API、options、report、diagnostics 與能力邊界。它不重複
列出 ODF schema 的全部元素；需要精確 XML 控制時，請使用 typed 或 schema-aware DOM。

| 工作流 | Reference | 主要入口 |
| --- | --- | --- |
| Spreadsheet data | [Spreadsheet 資料工作流](spreadsheet-data.md) | `WriteObjects`、`ReadObjects`、`ValidateObjectBinding`、`UpdateObjects`、`UpsertObjects` |
| Chart | [圖表工作流](charts.md) | `InsertChartFromRange`、`GetEmbeddedChartDocument`、`OdfChartDocument` |
| Template | [模板繫結工作流](templates.md) | `TemplateBinder.Bind`、`OdfTemplateBindOptions`、`OdfTemplateBindReport` |
| Interop | [互通與風險工作流](interop.md) | `OdfPracticalCompatibilityValidator`、LibreOffice backend、validation profiles |
| ODT／ODS／ODP／ODG | [四主格式語意 Facade](semantic-facades.md) | `TextDocument`、`SpreadsheetDocument`、`PresentationDocument`、`DrawingDocument` |
| Diagnostics | [統一診斷模型](diagnostics.md) | `OdfDiagnostic`、各 report 類別的 `Diagnostics` 檢視 |

## 文件契約

- Cookbook 提供可直接採用的情境片段；Reference 說明 options、report 與限制。
- 新增 public options、report 或 diagnostic code 時，同步更新對應 reference 或明列其非目標。
- Reference 只承諾程式碼與測試已涵蓋的行為，不以未來清單暗示必要功能留待後續版本。
- NuGet 上架與發行管道屬交付資訊，不是 API 完滿條件。
