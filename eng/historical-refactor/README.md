# 歷史性重構腳本（勿常規執行）

本目錄存放 god-class／partial 遷移時期的一次性腳本：

- `Split-*`：依行數或 AST 機械拆 partial
- `Merge-*`：弱 partial 合併批次
- `Migrate-*`／`Rename-*`：命名與屬性存取遷移

**預設不要重跑。** 現行 partial 準則見 [docs/maintainability.md](../../docs/maintainability.md)。
診斷請用 `eng/Analyze-PartialSplits.ps1` 與 `eng/List-LargeCsFiles.ps1`。
