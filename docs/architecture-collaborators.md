# 領域根與協作者地圖（v0.0.1）

本文件是 **god-class／大型 partial 的完滿基線地圖**：說明哪些型別以
façade + 協作者／partial 邊界維護，**禁止**再以機械 `Split-*` 切檔。

## 原則

1. 公開型別可為 façade；重邏輯進 **engine／collaborator／partial 領域檔**。  
2. 單檔 > ~1000 行或跨檔總量 > ~2000 行時，優先抽協作者，而非再切無意義 partial。  
3. 診斷：`pwsh eng/Analyze-PartialSplits.ps1`、`pwsh eng/List-LargeCsFiles.ps1`。  
4. 歷史腳本僅在 `eng/historical-refactor/`，預設不重跑。

## 已收斂的重大邊界（v0.0.1）

| 領域 | 公開／根型別 | 協作者或 partial 邊界 |
|------|----------------|----------------------|
| 封裝 I/O | `OdfPackage` | Loading／Saving／Encryption／*Collaborators*／Transaction |
| 封存寫入 | `OdfPackageArchiveWriter` | 本體 ZIP 路徑；`.Streams` 池化串流；`.FlatXml` Flat 序列化 |
| 文件生命週期 | `OdfDocument` | Lifecycle／Merge／Metadata／Signatures／*Collaborators* |
| 試算表 | `OdfTableSheet`、`SpreadsheetDocument` | ObjectBinding／Formulas／Charts／RangeDepth 等 |
| 表格 DOM | `TableTableElement` | `.Table` 結構；`.Import` 匯入；`.Sparse` 稀疏分頁；`.CellViews` 檢視 |
| 串流郵件合併 | `OdfStreamingMailMerge` | 本體；`.Segments`；`.ExpressionCache` |
| 文字 | `TextDocument` | TrackChanges／FormFields／Html*／*Collaborators* |
| 在地化 | `OdfLocalizer` | JSON `Compliance/i18n` → 產生 `Exceptions.<culture>.cs` |
| Schema | `Odf*OfficialSchemaProvider` | **產生碼**；體積屬產品策略（可選套件，非 0.0.1 拆 nupkg） |

## 刻意保留的大型面

| 項目 | 原因 |
|------|------|
| `OdfElement` 屬性／值 partial | Schema 驅動屬性面，KEEP |
| `OdfElementSchemaRegistry.*` | 枚舉 token 註冊，KEEP |
| 生成 DOM 包裝 | 規格覆蓋代價；不可手改 `.g.cs` |

## 後續僅在「改到該區」時小步抽取

- `OdsStreamWriter` 熱路徑已共用 `OdfRawXmlWriter`；再拆以功能邊界為準。  
- `TextDocumentBuilder`／`SpreadsheetDocumentBuilder` 等 builder 維持高階 API 聚合。  

## 相關

- [maintainability.md](maintainability.md)  
- [AGENTS.md](../AGENTS.md) §C2  
