# 可維護性與複雜度債（Maintainability）

本文件定義 OdfKit 在**程式碼結構、在地化字典、公開 API 表面、產生碼與歷史腳本**上的長期維護準則。
目標是降低認知負擔，避免「為了過門檻而機械拆檔／堆功能」再度膨脹，並對齊業界黃金標準。

## 1. Partial 型別拆分準則

### 允許拆分的理由（KEEP）

| 理由 | 範例 |
|------|------|
| Schema 驅動、產生或巨大屬性面 | `OdfElement`、`OdfElementContentModel` |
| 生命週期／I/O 管線邊界 | `OdfPackage`（Loading／Saving／Encryption） |
| 功能區切割且可獨立理解 | `TextDocument` 追蹤修訂、`OdfChartDocument` 序列 |
| 加密／簽章管線 | `OdfSigner`、`OdfBouncyCastleOpenPgpProvider` |
| 語系資源表 | `OdfLocalizer.Exceptions.<culture>.cs` |
| 串流／繫結引擎邊界 | `OdsStreamReader`、`TemplateBinder`、`DrawingDocument`、`OdfTable` |

### 禁止或應避免

1. **僅因行數超過門檻**而用 `eng/Split-*.ps1` 機械切檔。  
2. 產生「弱 partial」：`< 90` 行且檔名為 `Helpers`／`Candidates` 等、無法對應領域概念。  
3. 為通過 IDE／review 門檻而把同一方法拆到多檔卻無邊界註解。

### 建議流程

1. 新增功能前，先確認是否可放進既有領域 partial（見檔名與 `///` 區塊註解）。  
2. 單型別跨檔總行數長期 **> ~1500–2000** 時，優先**抽協作者型別**（collaborator），而非再切 partial。  
3. 診斷：`pwsh eng/Analyze-PartialSplits.ps1`、`pwsh eng/List-LargeCsFiles.ps1`。  
4. 歷史 `Split-*`／`Merge-*`／`Migrate-*`／`Rename-*` 已移至 `eng/historical-refactor/`，**預設不要重跑**。  
5. `Analyze-PartialSplits.ps1` 應輸出 **MERGE: 0 | REVIEW: 0**；新 REVIEW 須人工評估後升格 KEEP 或合併。

## 2. 在地化字典（OdfLocalizer）

| 檔案 | 角色 |
|------|------|
| `OdfLocalizer.cs` | 查找、快取、文化回退 |
| `OdfLocalizer.Languages.cs` | 合規建議等較短語系工廠 |
| `OdfLocalizer.Exceptions.cs` | 例外字典**入口**（註冊 12 語系） |
| `OdfLocalizer.Exceptions.<culture>.cs` | **單一語系**例外／診斷字串表 |
| `OdfLocalizer.ComplianceSuggestions.cs` | 合規建議補充 |
| `OdfLocalizer.ExtensionDiagnostics.cs` | Extensions 診斷 |

### 更新規則

1. 新增 `Err_*`／`Warn_*`／`Cli_*` 等鍵時，**12 語系同步**（`en`、`zh-TW`、`de`、`fr`、`nl`、`nb`、`pt`、`it`、`sk`、`da`、`ms`、`ko`）。  
2. 禁止只改 `en` 或只改 `zh-TW`。  
3. 禁止在呼叫端 hard-code 例外訊息。  
4. 完整語意以 `en` 為準；其他語系可走 fallback，但**新鍵必須登錄**所有語系表（可先貼近英文再潤飾）。  

### 鍵值對等閘門（業界最佳實踐）

- **靜態鍵集合對等**：所有語系的訊息鍵清單必須與 `en` 相同（gettext／ICU／resx 閘門同理）。  
- 腳本：`pwsh eng/Test-LocalizerKeyParity.ps1 -FailOnIssues`  
- 契約測試：`DocsAndCorpusContractTests.ExceptionDictionaryKeysAreParityAcrossCultures`  
- 呼叫端解析測試：`LiteralLocalizerMessageKeysResolveForAllSupportedCultures`（執行期 GetMessage）

## 3. 公開 API 表面（PublicApiAnalyzers）

對齊 [Microsoft.CodeAnalysis.PublicApiAnalyzers](https://www.nuget.org/packages/Microsoft.CodeAnalysis.PublicApiAnalyzers)
（.NET 執行階段、Azure SDK、Roslyn 等同款）：

| 項目 | 說明 |
|------|------|
| 套件 | `Microsoft.CodeAnalysis.PublicApiAnalyzers`（`PrivateAssets=all`） |
| 基線路徑 | `OdfKit/PublicAPI/$(TargetFramework)/PublicAPI.{Shipped,Unshipped}.txt` |
| 0.x 策略 | **全量在 Unshipped**；1.0 發佈時移入 Shipped |
| RS0016／RS0017 | **error**（阻擋未登錄新增與意外移除） |
| RS0026／RS0027 | **suggestion**（既有可選參數多載與生成 DOM grandfather；新增 API 仍應遵循 Roslyn 可選參數指引） |
| 重產腳本 | `pwsh eng/Generate-PublicApiBaseline.ps1 -Verify` |
| 說明 | [OdfKit/PublicAPI/README.md](../OdfKit/PublicAPI/README.md) |

產生基線時可設 `ODFKIT_PUBLICAPI_BASELINE=1`，讓 RS0016／RS0017 暫不視為錯誤以便 code fix 寫檔。

### 套件雙 TFM 相容性（Package Validation）

| 項目 | 說明 |
|------|------|
| 屬性 | `EnablePackageValidation=true`（`OdfKit.csproj` 與 `eng/OdfKit.Package.props`） |
| 時機 | `dotnet pack`／`eng/Test-NuGetPack.ps1`／`nuget-pack.yml` |
| 檢查 | Compatible framework validator 等（netstandard2.0 ↔ net10.0 前向相容） |
| 文件 | [Package validation overview](https://learn.microsoft.com/dotnet/fundamentals/package-validation/overview) |

與 PublicApiAnalyzers 互補：後者追蹤**逐 TFM 表面變更**；前者確保**多 TFM 套件內**相容。

## 4. 產生碼（Generated）

| 路徑 | 來源 | 規則 |
|------|------|------|
| `OdfKit/DOM/Generated/*.g.cs` | `OdfSchemaGenerator` | **不可手改**；重產見 `eng/Generate-OdfSchemaProvider.ps1` |
| `OdfKit/Compliance/Generated/Odf*OfficialSchemaProvider.g.cs` | 同上 | 同上 |

- 產生碼體積大是規格覆蓋的代價；不應為了「看起來乾淨」而刪減 schema 覆蓋。  
- 建置效能：本機可關 analyzer（`Directory.Build.props`）；CI 維持檢查。  
- Trim analyzer 對巨型產生碼關閉，改以 `eng/Test-TrimSmoke.ps1` 實機把關。  
- 若未來要縮小預設套件，評估**可選 schema 套件**（例如僅 1.4），而非手改 `.g.cs`。  
- schema 重產後若公開表面變動，須重跑 `Generate-PublicApiBaseline.ps1`。

## 5. XML 文件與註解

- 公開／受保護 API：雙語 `<summary>` 多行區塊（英在前、中在後）。  
- **禁止一行式** `/// <summary>…</summary>`（見 `AGENTS.md`）。  
- 契約測試：`DocsAndCorpusContractTests` 與 `eng/Test-BilingualXmlDocs.ps1`。  
- 一行式 summary 靜態閘門：`eng/Test-OneLineXmlSummary.ps1`。

## 6. 進度與後續債

### 已完成（摘要）

| 項目 | 說明 |
|------|------|
| 在地化拆檔 | `OdfLocalizer.Exceptions.<culture>.cs` × 12 + 入口 |
| 語系鍵值對等 | `Test-LocalizerKeyParity.ps1` + 契約測試；補齊缺漏鍵 |
| 歷史腳本歸檔 | `eng/historical-refactor/` |
| 產生碼 README | DOM／Compliance `Generated/` |
| 一行式 summary 閘門 | `eng/Test-OneLineXmlSummary.ps1` |
| 弱 partial 合併 | 合併 `OdfAnimation`；移除空殼 matcher／registry 根檔；合併 `OdfSignatureVerifier.Common` |
| REVIEW → KEEP | `TemplateBinder`、`OdsStreamReader`、`DrawingDocument`、`OdfTable` 升格 |
| Public API 基線 | PublicApiAnalyzers 5.6.0 + 雙 TFM Unshipped 基線 |
| Package Validation | 核心與 Extensions 啟用 `EnablePackageValidation` |
| CI maintainability job | 合併衝突、一行 summary、鍵值對等、PublicApi 建置 |

### 建議的後續債

| 項目 | 說明 |
|------|------|
| 協作者抽取 | 對仍 >2000 行總量的領域根再抽 engine |
| 在地化產線 | 自 JSON／resx 產生 `Exceptions.*.cs`（可選） |
| Schema 套件拆分 | 降低預設 nupkg 體積（產品決策） |
| 1.0 API 凍結 | Unshipped → Shipped；可選參數多載（RS0026／RS0027）分批收斂 |
| Baseline version validator | 有穩定 NuGet 發行後啟用 `PackageValidationBaselineVersion` |

## 7. 相關文件

- [AGENTS.md](../AGENTS.md)  
- [OdfKit/PublicAPI/README.md](../OdfKit/PublicAPI/README.md)  
- [provenance/README.md](provenance/README.md)  
- [eng/README.md](../eng/README.md)  
- [ip-compliance.md](ip-compliance.md)  
