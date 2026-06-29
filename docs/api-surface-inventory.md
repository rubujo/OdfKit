# API Surface Inventory

本文件記錄本輪 API 一致性工作的靜態盤點結果。它是
`api-surface-consistency.md` 的工作清單補充，不取代命名契約本身。

## 盤點方式

盤點以手寫 C# 公開 API 的靜態搜尋為準；本輪完成後已搭配格式化、build 與
targeted tests 驗證，不依賴 codegen 或外部 office runtime。

```powershell
rg -n "public sealed class .*Builder|Builder\(" OdfKit\<domain> -g "*.cs"
rg -n "public .* Add[A-Z]|public void Add[A-Z]" OdfKit\<domain> -g "*.cs"
rg -n "public .* Get[A-Z]" OdfKit\<domain> -g "*.cs"
rg -n "public .* Set[A-Z]" OdfKit\<domain> -g "*.cs"
rg -n "public .* Remove[A-Z]" OdfKit\<domain> -g "*.cs"
rg -n "public .* Find[A-Z]|public .* Find\(" OdfKit\<domain> -g "*.cs"
```

## 高階 facade 命名分布

| Domain | Builder hits | Add hits | Get hits | Set hits | Remove hits | Find hits | 判讀 |
|--------|--------------|----------|----------|----------|-------------|-----------|------|
| Chart | 10 | 3 | 15 | 24 | 1 | 7 | 已將依序列或軸向查找的 nullable API 改為 `Find*`。 |
| Database | 0 | 18 | 14 | 10 | 6 | 8 | `Find*` 皆為單一 nullable lookup，符合契約；缺 builder 屬可接受，ODB workflow 偏 CRUD。 |
| Drawing | 22 | 34 | 20 | 0 | 0 | 4 | `Find*` 命中多為內部 helper 或屬性 initializer 呼叫；無集合型公開 `Find*` 違規。 |
| Formula | 14 | 1 | 22 | 5 | 1 | 4 | 已將 token 集合查詢改為 `GetAll`，並將 annotation lookup 改為 `FindAnnotation`；`FindFirst` 保留為單一 lookup。 |
| Image | 0 | 2 | 3 | 5 | 2 | 2 | 已將依框架名稱查找的 nullable image filter API 改為 `Find*`。 |
| Presentation | 11 | 42 | 16 | 26 | 1 | 6 | 已將依名稱查找的 nullable page layout API 改為 `Find*`。 |
| Spreadsheet | 20 | 51 | 65 | 34 | 4 | 4 | 已將公式儲存格集合查詢改為 `GetFormulaCells` overload，並將 sheet / cell annotation lookup 改為 `Find*`。 |
| Text | 42 | 97 | 29 | 11 | 2 | 4 | `Find*` 命中為單一節點 helper；追蹤修訂 affected nodes 已改為 `GetAffectedNodesForFormatChange`。 |

## 已完成的 breaking rename

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
| Package entry encryption lookup | `FindEntryEncryptionInfo` |
| Custom metadata property lookup | `FindCustomProperty` |

## 文件掃描基線

`eng/Test-BilingualXmlDocs.ps1` 目前為 report mode。最近一次靜態掃描結果：

```text
TOTAL=1971; FILES=342
```

這是後續文件批次的基線，不作為目前 CI fail gate。掃描器已排除 generated DOM wrapper、
`bin/`、`obj/`，並避免把 private / internal helper 型別中的 public 成員誤判為公開 API。

## 下一批建議

- 剩餘 nullable `Get*` 目前歸類為低階 DOM / typed attribute accessor、無 key 的目前狀態 getter、集合 snapshot 或必要讀取，不列入本輪 `Find*` rename。
- `Clear*` 維持 no-op 命令語意；指定項目移除已由 `Remove*` 統一回傳 `bool`。
- Database / Image 暫列 domain-specific builder exception；未來若出現高重複 fluent 建立 workflow，再另案補 builder。
