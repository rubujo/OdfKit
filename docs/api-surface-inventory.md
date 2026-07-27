# API 表面盤點

本文件記錄本輪 API 一致性工作的靜態盤點結果。它是
`api-surface-consistency.md` 的工作清單補充，不取代命名契約本身。

## 盤點方式

盤點以手寫 C# 公開 API 的靜態搜尋為準；本輪完成後已搭配格式化、建置與
目標測試驗證，不依賴 codegen 或外部 Office 執行階段。

```powershell
rg -n "public sealed class .*Builder|Builder\(" OdfKit\<domain> -g "*.cs"
rg -n "public .* Add[A-Z]|public void Add[A-Z]" OdfKit\<domain> -g "*.cs"
rg -n "public .* Get[A-Z]" OdfKit\<domain> -g "*.cs"
rg -n "public .* Set[A-Z]" OdfKit\<domain> -g "*.cs"
rg -n "public .* Remove[A-Z]" OdfKit\<domain> -g "*.cs"
rg -n "public .* Find[A-Z]|public .* Find\(" OdfKit\<domain> -g "*.cs"
```

## 高階外觀層命名判讀

各領域的命中數會隨每次 API 補強而變動，因此本表只保留不隨計數漂移的**契約判讀**；
需要當下分布時請直接重跑上節指令，不要引用歷史快照數字。

| 領域 | 判讀 |
|--------|------|
| Chart | 已將依序列或軸向查找的 nullable API 改為 `Find*`。 |
| Database | `Find*` 皆為單一 nullable lookup，符合契約；缺 builder 屬可接受，ODB 工作流程偏 CRUD。 |
| Drawing | `Find*` 命中多為內部 helper 或屬性 initializer 呼叫；無集合型公開 `Find*` 違規。 |
| Formula | 已將 token 集合查詢改為 `GetAll`，並將 annotation lookup 改為 `FindAnnotation`；`FindFirst` 保留為單一 lookup。 |
| Image | 已將依框架名稱查找的 nullable image filter API 改為 `Find*`。 |
| Presentation | 已將依名稱查找的 nullable page layout API 改為 `Find*`。 |
| Spreadsheet | 已將公式儲存格集合查詢改為 `GetFormulaCells` overload，並將 sheet / cell annotation lookup 改為 `Find*`；`Get*` 佔比偏高主要來自 `ObjectDataReader<T>` 實作 `DbDataReader` 所需的欄位存取 API。 |
| Text | `Find*` 命中為單一節點 helper；追蹤修訂 affected nodes 已改為 `GetAffectedNodesForFormatChange`。 |

## 已完成的破壞性重新命名

本批次移除集合型 `Find*` 公開 API，不保留相容 shim，並同步更新測試與文件。

| 範圍 | 新 API |
|------|--------|
| Schema name-class collection query | `GetMatchingNameClasses` |
| RDF triple collection query | `GetTriples` |
| Math token collection query | `GetAll` |
| Workbook formula-cell predicate query | `GetFormulaCells(Func<OdfFormulaCellInfo, bool>)` |
| Worksheet formula-cell predicate query | `GetFormulaCells(Func<OdfFormulaCellInfo, bool>)` |
| Tracked-change affected-node query | `GetAffectedNodesForFormatChange` |

本批次也將語意明確的單一 nullable lookup 改為 `Find*`：

| 範圍 | 新 API |
|------|--------|
| Workbook sheet lookup by name | `FindSheet` |
| Formula annotation lookup by encoding | `FindAnnotation` |
| Spreadsheet cell annotation lookup | `FindAnnotation` |

第三批統一指定項目移除 API 的 `bool` 語意：

| 範圍 | API |
|------|-----|
| Package entry removal | `RemoveEntry` |
| DOM attribute / child removal | `RemoveAttribute`、`RemoveChild` |
| Spreadsheet cell hyperlink / annotation removal | `RemoveHyperlink`、`RemoveAnnotation` |
| Spreadsheet print page break removal | `RemoveRowPageBreak`、`RemoveColumnPageBreak` |
| Presentation placeholder removal | `RemovePlaceholder` |

第四批將依 key / name / dimension 查找單一 nullable 項目的 API 改為 `Find*`：

| 範圍 | API |
|------|-----|
| Database query optional children | `FindQueryOrderStatement`、`FindQueryFilterStatement`、`FindQueryUpdateTable` |
| Chart series optional children | `FindDataLabels`、`FindErrorIndicator`、`FindRegressionCurve`、`FindMeanValue` |
| Chart document optional lookups | `FindSeriesDataLabels`、`FindAxisInfo`、`FindAxisTitle` |
| Presentation page layout lookup | `FindPresentationPageLayout` |
| Image frame filter lookup | `FindImageFilter` |
| Image frame summary lookup | `FindImageFrame` |
| Package entry encryption lookup | `FindEntryEncryptionInfo` |
| Custom metadata property lookup | `FindCustomProperty` |

## 文件掃描基線

`eng/Test-BilingualXmlDocs.ps1` 預設為報告模式。v0.0.1 完滿後 **missing 雙語文件已清零**；
`-FailOnNewIssues` 的預設基線為 `TOTAL=0`／`FILES=0`（零容忍新增債務）。
`-FailOnIssues` 可於 CI 或專門文件批次要求完全乾淨。掃描器已排除產生的 DOM wrapper、
`bin/`、`obj/`，並避免把 private / internal helper 型別中的 public 成員誤判為公開 API。

最近一次靜態掃描：

```text
No bilingual XML documentation issues found.
```

## 收尾後維護原則

- 剩餘 nullable `Get*` 目前歸類為低階 DOM / 型別化屬性 accessor、無 key 的目前狀態 getter、集合快照或必要讀取，不列入本輪 `Find*` rename。
- `Clear*` 維持 no-op 命令語意；指定項目移除已由 `Remove*` 統一回傳 `bool`。
- Database / Image 維持領域特定 builder 例外；只有出現高重複且已有真實使用案例的 fluent
  建立工作流程時才另案補 builder。
- ODB schema collection 已改為唯讀快照並由具名 editor 修改；Chart series snapshot、
  Formula predicate rewrite 與 ODI image-content batch 已完成。後續不再進行無案例驅動的
  API「加厚」。
