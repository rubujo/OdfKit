# Public API 基線（PublicApiAnalyzers）

本目錄為 [Microsoft.CodeAnalysis.PublicApiAnalyzers](https://www.nuget.org/packages/Microsoft.CodeAnalysis.PublicApiAnalyzers)
所使用的公開 API 表面記錄，對齊 .NET 執行階段、Azure SDK、Roslyn 等函式庫的業界慣例。

## 檔案配置

| 路徑 | 用途 |
|------|------|
| `net10.0/PublicAPI.Shipped.txt` | 已發佈（穩定）API；1.0 起啟用 |
| `net10.0/PublicAPI.Unshipped.txt` | 尚未「正式凍結」的 API（**0.x 全量在此**） |
| `netstandard2.0/…` | 同上，供 netstandard2.0 TFM |

雙 TFM 各自一份基線：`net10.0` 可能含 `netstandard2.0` 沒有的 API（例如較新 BCL 形狀）。

## 規則嚴重度

| 診斷 | 嚴重度 | 說明 |
|------|--------|------|
| **RS0016** | error | 公開 API 未登錄於 Shipped／Unshipped（新增表面必須更新基線） |
| **RS0017** | error | 基線中有、原始碼已移除（破壞性變更） |
| **RS0026** | suggestion | 勿新增多個皆含可選參數的公開多載（既有 0.x 表面 grandfather） |
| **RS0027** | suggestion | 含可選參數的公開 API 應為同名多載中參數最多者（同上） |

詳見根目錄 [`.editorconfig`](../../.editorconfig) 與 [docs/maintainability.md](../../docs/maintainability.md)。

## 工作流程

1. **日常 PR**：變更公開 API 後建置會出現 RS0016／RS0017。  
   - 新增：將簽章加入對應 TFM 的 `PublicAPI.Unshipped.txt`（可用 IDE code fix 或下方腳本）。  
   - 刪除／改簽章：同步更新 Unshipped；若已在 Shipped，視為破壞性變更，需 major 版本策略。  
2. **整批重產基線**（schema 重產、大量 API 調整後）：

   ```powershell
   pwsh eng/Generate-PublicApiBaseline.ps1 -Verify
   ```

3. **0.x → 1.0**：將 Unshipped 內容移入 Shipped，並清空 Unshipped（僅留 `#nullable enable`）。

## 產生腳本注意事項

- 產生時會設 `ODFKIT_PUBLICAPI_BASELINE=1`，使 RS0016／RS0017 暫不視為錯誤，方便 code fix 寫檔。  
- 腳本會**暫時**將 `TargetFrameworks` 鎖成單一 TFM 再呼叫 `dotnet format analyzers`，避免多 TFM 只寫入第一個目標。  
- 產生後請以 `-Verify` 或不帶 BASELINE 的 CI 建置確認通過。

## 與 Package Validation 的關係

| 工具 | 時機 | 重點 |
|------|------|------|
| PublicApiAnalyzers | 每次建置 | 是否**有意**新增／移除公開 API（基線 diff） |
| `EnablePackageValidation` | `dotnet pack` | 多 TFM 套件內 netstandard2.0 ↔ net10.0 **是否前向相容** |

兩者互補，皆為 .NET 函式庫業界黃金標準。詳見 [docs/maintainability.md](../../docs/maintainability.md)。

## 參考

- [PublicApiAnalyzers.Help.md](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md)  
- [Adding Optional Parameters in Public API](https://github.com/dotnet/roslyn/blob/main/docs/Adding%20Optional%20Parameters%20in%20Public%20API.md)  
- [Package validation overview](https://learn.microsoft.com/dotnet/fundamentals/package-validation/overview)  
