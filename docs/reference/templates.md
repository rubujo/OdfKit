# 模板繫結工作流

## ODT 任務入口

具名書籤、欄位、圖片與整份文件附加皆回傳 `OdtMutationReport`，呼叫端可直接檢查
`UpdatedCount`、`MissingTargets`、`AmbiguousTargets` 與建立的 package path：

```csharp
OdtMutationReport bookmark = document.SetBookmarkText("Customer", "Contoso");
OdtMutationReport field = document.SetFieldValue("Status", "Ready");
OdtMutationReport image = document.ReplaceImage("Logo", logoBytes);
```

附加文件由 facade 處理 style、媒體與 package 關係：

```csharp
OdtMutationReport appended = document.AppendDocument(appendix);
```

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
| NativeAOT 物件路徑 | 字典路徑不需反射；POCO 路徑可設定 `OdfTemplateBindOptions.ValueResolver` |

同一模板節點混用多個集合會記錄警告，不進行含糊展開。TemplateBinder 不負責條件式模板語言、
任意腳本執行、跨文件 include 或完整報表排版引擎。

NativeAOT 應以實作 `IOdfTemplateValueResolver` 的靜態型別 switch 解析 POCO 屬性。一般 JIT
執行仍保留既有反射 fallback；NativeAOT 不會為未知執行期型別無界保留所有公開屬性。

完整範例見 [Cookbook：低魔法模板填值](../cookbook.md#低魔法模板填值)。
