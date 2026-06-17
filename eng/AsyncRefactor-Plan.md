# OdfKit 非同步／CancellationToken 統一計畫

## 目標

將核心 I/O 與長時間作業改為真非同步管線，公開 `*Async` API 一律支援 `CancellationToken cancellationToken = default`，並以測試與 XML 註解固化取消語意。

## CancellationToken 慣例

| 層級 | 慣例 |
|------|------|
| 公開 `*Async` | 必須 `CancellationToken cancellationToken = default` |
| internal 協作者 | 建議 `= default` |
| private 輔助 | 可選 |

伺服器環境（ASP.NET Core 等）應優先使用 `*Async`；同步 API 保留為薄門面，XML `<remarks>` 註明建議改用非同步版本。

## Phase 1–8（已完成）

| Phase | 狀態 | 提交 | 內容 |
|-------|------|------|------|
| 1 ZIP 寫入 | ✅ | `6d3a878` | `WriteToArchiveAsync`、`CopyToAsync` |
| 2 載入管線 | ✅ | `8345310` | `InitializeAsync`→`OpenAsync`→`LoadDocumentAsync` |
| 3 Save path/stream | ✅ | `a50badd` | `OdfDocument.SaveAsync`／`SaveToStreamAsync` |
| 4 簽章驗證 | ✅ | `ddf4a0e` | `VerifySignaturesAsync`、`SignAsync`、HTTP `ct` |
| 5 子類型 LoadAsync | ✅ | `a6b90b3` | Presentation／Drawing／Chart／Formula／Image／Database |
| 6 ExternalValidator | ✅ | `e08ba5f` | `ValidateWith*Async`、`RunProcessAsync` |
| 7 取消測試貫穿 | ✅ | `1a60809` | 核心 `*Async` 預取消／`CancelAfter` 測試 |
| 8 XML 註解整理 | ✅ | `1a60809` | 核心 `*Async` `<remarks>` + 同步 API 伺服器建議 |

## 後續強化（已完成）

| 項目 | 狀態 | 提交 | 內容 |
|------|------|------|------|
| try-catch 意圖補強 | ✅ | `e8c8ebd` | 9 處生產程式碼靜默 catch 補 `OdfKitDiagnostics.Warn` |
| LibreOffice 真非同步 | ✅ | `2541b95` | `ConvertFileAsync`、`LocalProcessBackend` 移除 `Task.Run` |
| Mock HTTP 非同步 | ✅ | `2541b95` | `MockHttpMessageHandler` 支援 async 委派 |
| LocalProcessBackend 取消測試 | ✅ | `74eb4a7` | 預取消與轉檔中取消 |
| AdvancedSecurity xUnit1051 | ✅ | `74eb4a7` | `SignAsync`／`VerifySignaturesAsync` 傳遞測試 `ct` |
| 剩餘 xUnit1051 | ✅ | `d6e42e1` | `LibreOfficeHttpRendererTests`、`OdfSecurityBoundaryTests` |

## try-catch 審查準則（已建立）

| 類型 | 處理 |
|------|------|
| A. 輸入解析失敗 | 回傳 default，不需 log |
| B. 可選功能／平台差異 | 降級 + Warn |
| C. best-effort | Warn + 繼續 |
| D. 驗證 API | 填入結果物件，不需額外 log |
| E. 公開 API 契約 | 回傳 default，不需 log |
| F. 必須傳播 | throw（可包裝） |
| G. 測試程式碼 | 允許 `catch { }` |

## 刻意保留（合理設計）

| 項目 | 理由 |
|------|------|
| `OdfSigner.Sign`／`VerifySignatures` 同步薄門面 | 公開 API 相容；內部委派 `*Async` |
| `OdfDocument.Sign`、`OdfExternalValidator` 同步 API | 同上 |
| `LibreOfficeRenderer.ConvertFile` 同步薄門面 | 內部委派 `ConvertFileAsync` |
| 測試壓力／對抗 `Task.Run` | 類型 G；刻意並行壓力，非生產假非同步 |

## 測試覆蓋（1154+ 通過）

- `OdfPackageAsyncCancellationTests`
- `OdfDocumentAsyncCancellationTests`
- `OdfSignerAsyncCancellationTests`
- `OdfExternalValidatorAsyncTests`
- `OdfSubtypeAsyncCancellationTests`
- `LocalProcessBackendAsyncCancellationTests`

測試慣例：非同步測試中呼叫可取消 API 時使用 `TestContext.Current.CancellationToken`（xUnit1051）。

## 驗證

每輪變更：`dotnet build` → `dotnet test`（1154+）→ `pwsh eng/Format-Safe.ps1`（測試含 `-IncludeTests`）→ GPG 簽署提交。