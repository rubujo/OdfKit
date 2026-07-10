# 模板繫結工作流

`TemplateBinder.Bind` 支援 Text、Spreadsheet、Presentation 與 Drawing 文件。短多載回傳變更數；
接受 `OdfTemplateBindOptions` 的多載回傳 `OdfTemplateBindReport`，供呼叫端檢查命中、未解析
placeholder、集合展開與非致命警告。

| 能力 | 契約 |
| --- | --- |
| Scalar | `{{Name}}` 等純量 placeholder |
| Collection | `{{Items[].Field}}` 集合展開 |
| Image | `{{Image:Name}}` 搭配 `OdfTemplateImageValue` |
| Dry run | `OdfTemplateBindOptions.DryRun` 只產生 report，不修改 DOM |
| Unknown placeholder | 由 `OdfTemplateUnknownPlaceholderPolicy` 決定保留或輸出空字串 |

同一模板節點混用多個集合會記錄警告，不進行含糊展開。TemplateBinder 不負責條件式模板語言、
任意腳本執行、跨文件 include 或完整報表排版引擎。

完整範例見 [Cookbook：低魔法模板填值](../cookbook.md#低魔法模板填值)。
