# 產品品質閘門（Corpus／互通／效能）

本文件把 **產品與互通** 相關的本機／CI 可執行檢查收斂成單一入口。  
可維護性閘門（PublicAPI、RS、格式化、語系）見 [maintainability.md](maintainability.md)。

## 分層建議

| 層級 | 何時跑 | 指令 |
|------|--------|------|
| **A. 每次提交前** | 函式庫／測試變更 | 見下方「提交前最小集合」 |
| **B. 每日或 PR** | 有環境時 | corpus、policy、typed DOM 覆蓋 |
| **C. 發版前** | tag／NuGet 前 | 全表 + pack + 可選 LibreOffice／OOXML 視覺 |

### 提交前最小集合

```powershell
pwsh eng/Format-Safe.ps1
dotnet build OdfKit/OdfKit.csproj -c Release
dotnet test OdfKit.Tests/OdfKit.Tests.csproj -c Release --framework net10.0 `
  --filter "FullyQualifiedName!~LibreOffice&FullyQualifiedName!~InteropCorpus&FullyQualifiedName!~OfficeGui"
```

可選（文件／在地化變更時建議加上）：

```powershell
pwsh eng/Test-BilingualXmlDocs.ps1 -FailOnNewIssues
pwsh eng/Test-OneLineXmlSummary.ps1 -FailOnIssues
pwsh eng/Test-LocalizerKeyParity.ps1 -FailOnIssues
pwsh eng/Generate-LocalizerExceptionsFromJson.ps1 -VerifyOnly
```

## Corpus 與互通

| 腳本 | 說明 |
|------|------|
| `pwsh eng/Test-OdfCorpus.ps1` | 內建 corpus（`tests/fixtures/corpus/manifest.json`）；可設 `ODFKIT_PARITY_CORPUS_ROOT` 併跑外部 corpus |
| `pwsh eng/Initialize-OdfExternalCorpus.ps1 -OutputRoot <path>` | 初始化外部 corpus 目錄與 manifest 範本 |
| `pwsh eng/Test-LibreOfficeInterop.ps1` | LibreOffice headless 實機互通（需本機安裝 soffice） |
| `pwsh eng/Test-OoxmlVisualGolden.ps1` | OOXML 轉換視覺 golden |
| `pwsh eng/Test-OdfPolicy.ps1` | 巨集淨化、外部資源 policy、加密邊界等 |
| `pwsh eng/Test-RenderingBackends.ps1` | Rendering 擴充單元測試 |
| `pwsh eng/Test-OfficeGuiSmoke.ps1` | 可選 GUI 煙霧（環境依賴較重） |

規則與契約：

- [corpus-manifest.md](corpus-manifest.md)
- [odf-official-corpus-sources.md](odf-official-corpus-sources.md)
- [ci-cd.md](ci-cd.md)

## 效能基線

| 腳本 | 說明 |
|------|------|
| `pwsh eng/Benchmark-Regression.ps1` | 短迭代 DomInsert 與 `eng/baselines/performance-baselines.json` 比對；超容許回歸非零結束 |
| `pwsh eng/Benchmark-Performance.ps1` | 效能相關單元測試與簡易計時 |
| `pwsh eng/Benchmark-Stable.ps1` | 較長 stable profile |
| `pwsh eng/Benchmark-BaselineReport.ps1` | 產生 Markdown 效能報告 |
| `pwsh eng/Benchmark-Competitive.ps1` | 競爭對比量測（若適用） |

發版前建議至少跑一次 `Benchmark-Regression.ps1`（可在 CI 設 `continue-on-error` 視環境穩定度而定）。

## Sample 與文件體驗

| 項目 | 說明 |
|------|------|
| `dotnet run samples/Sample.cs` | 全功能示範（含 options API 片段） |
| Smoke | `$env:ODFKIT_SAMPLE_SMOKE_ONLY='true'` 略過擴充轉檔展示 |
| 入門 | [getting-started.md](getting-started.md)、[samples/README.md](../samples/README.md) |
| 食譜 | [cookbook.md](cookbook.md) |

## 與 API 形狀（B 類）的關係

高頻多可選參數改 options 後，sample 與 corpus 測試應使用新表面：

- `OdfRichTextRunOptions`／`OdsRowWriteOptions`／`OdfValidationOptions`
- `OdfFlatXmlWriteOptions`／`OdfSchemaRegistrationOptions`

細節見 [public-api-optional-parameters.md](public-api-optional-parameters.md)。

## 最近本機閘門紀錄（v0.0.1 完滿）

| 日期 | 層級 | 結果摘要 |
|------|------|----------|
| 2026-07-09 | A | `dotnet test` net10.0（排除 LibreOffice／InteropCorpus／OfficeGui）：通過 2082、略過 1、失敗 0 |
| 2026-07-09 | B | `Test-OdfPolicy.ps1`：32 通過；`Test-OdfCorpus.ps1`：內建 corpus 通過（未設 `ODFKIT_PARITY_CORPUS_ROOT`，略過外部 corpus） |
| 2026-07-09 | C | `Benchmark-Regression.ps1`：DomInsert 未超 +40% 容許（基準 123.9 µs／量測 159.6 µs） |
| 2026-07-09 | 可維護 | 雙語 missing 0、一行 summary OK、Localizer 574 keys × 12、optional expand dry-run 0 |

發版 tag 前建議再跑一次本表；LibreOffice／OOXML 視覺 golden 仍依環境選跑。

## 相關

- [eng/README.md](../eng/README.md)
- [maintainability.md](maintainability.md)
- [ip-compliance.md](ip-compliance.md)
