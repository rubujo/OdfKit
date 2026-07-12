# 四主格式語意 Facade

四主格式共用一致的集合與生命週期命名，但保留各自的領域型別。公開高階模型以 ODF
1.4 語意為準；ODF 1.1～1.3 文件載入時映射至同一模型。

| 格式 | 文件入口 | 主要領域 |
| --- | --- | --- |
| ODT | `TextDocument` | 內容、reference、automation |
| ODS | `SpreadsheetDocument` | data、analysis、presentation |
| ODP | `PresentationDocument` | structure、content、timeline |
| ODG | `DrawingDocument` | geometry、appearance |

## 操作契約

- `Get*` 列舉集合；`Find*` 查找單一項目並以 `null` 表示不存在。
- `Set*`、`Update*`、`Rename*` 修改既有語意，指定移除的 `Remove*` 回傳 `bool`。
- `Clear*` 清空集合並回傳變更數；只有單一狀態的 clear 操作回傳 `bool`。
- 保存後重新載入仍可透過同一 facade 讀取及修改，不區分「新建」與「既有」物件模型。
- 跨文件複製與移除由 facade 維護 style、ID、manifest、media 與格式專屬 reference。

## 版本映射與診斷

`AnalyzeVersionCompatibility(OdfVersion)` 會在不修改文件的情況下，比較 ODF 1.4 語意
模型與目標 1.1～1.3 schema，回傳 `OdfVersionCompatibilityReport`。`IsSafe` 為
`false` 時，`Issues` 會指出無法表示的元素或屬性及其路徑。指定版本儲存後可從
`LastVersionCompatibilityReport` 取得同一類型的結果；需要在儲存管線立即處理時，設定
`OdfSaveOptions.VersionCompatibilityReportHandler`。foreign namespace 不會被誤判為
標準版本損失，也不會因降版而遭刪除。

## 高頻工作範例

文字查詢、取代與範本填入不需接觸 DOM，並回傳可檢查的領域結果：

```csharp
IReadOnlyList<OdfTextMatch> matches = document.FindText("alpha", new OdfTextQueryOptions { MatchCase = false });
OdfTextReplaceResult replaced = document.ReplaceText("alpha", "beta");
OdfTemplateBindReport bound = document.FillTemplate(values);
```

繪圖對齊、等距分布及群組維持 page 領域邊界；缺少的識別碼與不完整幾何會寫入結果，
不需要呼叫端手動處理 `svg:x`、`svg:y` 或 group XML：

```csharp
OdfShapeLayoutResult aligned = page.AlignShapes(["title", "body"], OdfShapeAlignment.Left);
OdfShapeLayoutResult distributed = page.DistributeShapes(ids, OdfShapeDistribution.Vertical);
OdfDrawGroup group = page.GroupShapes(ids, "內容群組");
```

簡報預留位置使用具型別的 `OdfPlaceholderType`，並回報 missing／ambiguous 類型：

```csharp
OdpPlaceholderUpdateResult title = slide.SetPlaceholderText(OdfPlaceholderType.Title, "Quarterly report");
```

## 能力與非目標

12 個語意族群及每個 topic 的 `Create`、`Get`、`Find`、`Set`、`Update`、`Remove`、
`Clear`、`RoundTrip`、`Interop` 證據，均由
[`semantic-coverage.json`](../semantic-coverage.json) 管理。該 manifest 是能力範圍的
機器可讀來源，reference 不另建一份可能失步的功能清單。

下列工作刻意不屬於 facade 完滿承諾：物理分頁與像素級渲染、完整公式或 pivot 重算、
SmartArt 佈局、Office 專屬效果模擬，以及完整多人協同演算法。這些邊界不會降低 typed
模型、CRUD、round-trip 或未知內容保留的要求。

遷移現有程式碼請參閱[高階 API 破壞性重整遷移指南](../migration-high-level-api.md)；可執行
情境請參閱 [Cookbook](../cookbook.md)。
