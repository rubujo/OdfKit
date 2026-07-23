# Corpus Manifest 規則

本文件記錄可提交 corpus 的最小中繼資料規則。新增 fixture 時，必須在同一
pull request 更新本文件或等價 manifest，避免測試樣本來源變成黑盒。

儲存庫內可直接執行的範本 manifest 位於 `tests/fixtures/corpus/manifest.json`。
外部 corpus 的範本位於 `docs/examples/external-corpus/manifest.json`，baseline 例外範本位於
`docs/examples/external-corpus/baseline-exceptions.json`。
ODFDOM 官方 sample parity 的外部範本位於
`docs/examples/odfdom-sample-corpus/manifest.json`，baseline 例外範本位於
`docs/examples/odfdom-sample-corpus/baseline-exceptions.json`。
官方來源追蹤請見 [odf-official-corpus-sources.md](odf-official-corpus-sources.md)。

## 可提交條件

- fixture 必須是產生資料、專案自有資料、授權清楚，或已去識別化且可再散布。
- fixture 應保持小型；大型真實世界 corpus 改由 `ODFKIT_PARITY_CORPUS_ROOT`
  指向本機或 CI 產物。
- 不提交含個資、敏感資料、未知授權或不可再散布內容的文件。
- 每個 fixture 必須有預期驗證結果與 round-trip 策略。

## 欄位

| 欄位 | 必填 | 說明 |
|---|---|---|
| `id` | yes | 穩定 fixture id。 |
| `path` | yes | 儲存庫內路徑或外部 corpus 相對路徑。 |
| `source` | yes | generated、OdfKit、ODF Toolkit sample、LibreOffice export、real-world sanitized 等。 |
| `sourceUri` | external | 外部或官方 corpus 的可追溯來源 URL；generated 或 OdfKit 自有樣本可省略。 |
| `license` | yes | CC0、Apache-2.0、MPL-2.0、generated-no-copyright 等。 |
| `kind` | yes | `OdfDocumentKind` 或副檔名。 |
| `version` | yes | 預期 ODF 版本。 |
| `profile` | yes | 預期驗證 profile。 |
| `expected` | yes | `valid` 或 `invalid`。 |
| `roundTrip` | yes | `preserve-unknown`、`semantic-equivalent` 或 `byte-identical`。 |
| `sha256` | recommended | fixture 檔案 SHA-256 小寫十六進位；新增 realistic / parity fixture 應宣告。 |
| `notes` | no | 差異、例外或外部 validator 注意事項。 |

## Manifest 驗證規則

- `id` 必須在同一 manifest 內唯一。
- `path` 必須在同一 manifest 內唯一，且必須是相對路徑。
- `path` 解析後不得逃出 manifest root 或 `--root` 指定的 corpus root。
- `roundTrip` 只能是 `preserve-unknown`、`semantic-equivalent` 或 `byte-identical`。
- `profile` 必須能對應到 OdfKit 內建的 compliance profile。
- `expected` 只能是 `valid` 或 `invalid`。
- `source` 若不是 generated / OdfKit 自有來源，必須提供絕對 `http(s)`
  `sourceUri`，避免 ODF Toolkit / ODF Validator parity 樣本失去來源追溯。

## 初始內建 corpus

| id | path | source | sourceUri | license | kind | version | profile | expected | roundTrip |
|---|---|---|---|---|---|---|---|---|---|
| repo-generated-minimal-flat-text | `tests/fixtures/corpus/generated/minimal-text.fodt` | generated | n/a | generated-no-copyright | FlatText | 1.4 | OASIS ODF 1.4 Extended | valid | semantic-equivalent |
| repo-generated-minimal-flat-spreadsheet | `tests/fixtures/corpus/generated/minimal-spreadsheet.fods` | generated | n/a | generated-no-copyright | FlatSpreadsheet | 1.4 | OASIS ODF 1.4 Extended | valid | semantic-equivalent |
| repo-generated-minimal-flat-presentation | `tests/fixtures/corpus/generated/minimal-presentation.fodp` | generated | n/a | generated-no-copyright | FlatPresentation | 1.4 | OASIS ODF 1.4 Extended | valid | semantic-equivalent |
| repo-generated-minimal-flat-graphics | `tests/fixtures/corpus/generated/minimal-graphics.fodg` | generated | n/a | generated-no-copyright | FlatGraphics | 1.4 | OASIS ODF 1.4 Extended | valid | semantic-equivalent |
| repo-generated-page-layout-interleave-flat-text | `tests/fixtures/corpus/generated/page-layout-interleave.fodt` | generated | n/a | generated-no-copyright | FlatText | 1.4 | OASIS ODF 1.4 Strict | valid | semantic-equivalent |
| repo-generated-page-layout-interleave-duplicate-flat-text | `tests/fixtures/corpus/generated/page-layout-interleave-duplicate.fodt` | generated | n/a | generated-no-copyright | FlatText | 1.4 | OASIS ODF 1.4 Strict | invalid | semantic-equivalent |
| repo-generated-mathml-formula | `tests/fixtures/corpus/generated/mathml-formula.odf` | generated | n/a | generated-no-copyright | Formula | 1.4 | OASIS ODF 1.4 Extended | valid | semantic-equivalent |
| repo-generated-minimal-text | `tests/fixtures/corpus/generated/minimal-text.odt` | generated | n/a | generated-no-copyright | Text | 1.4 | OASIS ODF 1.4 Extended | valid | preserve-unknown |
| repo-generated-minimal-spreadsheet | `tests/fixtures/corpus/generated/minimal-spreadsheet.ods` | generated | n/a | generated-no-copyright | Spreadsheet | 1.4 | OASIS ODF 1.4 Extended | valid | preserve-unknown |
| repo-generated-minimal-presentation | `tests/fixtures/corpus/generated/minimal-presentation.odp` | generated | n/a | generated-no-copyright | Presentation | 1.4 | OASIS ODF 1.4 Extended | valid | preserve-unknown |
| repo-generated-minimal-graphics | `tests/fixtures/corpus/generated/minimal-graphics.odg` | generated | n/a | generated-no-copyright | Graphics | 1.4 | OASIS ODF 1.4 Extended | valid | preserve-unknown |
| repo-generated-complex-annual-report | `tests/fixtures/corpus/generated/complex/complex-annual-report.odt` | generated | n/a | generated-no-copyright | Text | 1.4 | OASIS ODF 1.4 Extended | valid | preserve-unknown |
| repo-generated-complex-financial-model | `tests/fixtures/corpus/generated/complex/complex-financial-model.ods` | generated | n/a | generated-no-copyright | Spreadsheet | 1.4 | OASIS ODF 1.4 Extended | valid | preserve-unknown |
| repo-generated-complex-business-deck | `tests/fixtures/corpus/generated/complex/complex-business-deck.odp` | generated | n/a | generated-no-copyright | Presentation | 1.4 | OASIS ODF 1.4 Extended | valid | preserve-unknown |
| repo-generated-complex-flow-diagram | `tests/fixtures/corpus/generated/complex/complex-flow-diagram.odg` | generated | n/a | generated-no-copyright | Graphics | 1.4 | OASIS ODF 1.4 Extended | valid | preserve-unknown |
| generated-format-minimal | generated in `PackageRoundTripMatrixTests.MinimalSupportedFormatRoundTrips` | generated | n/a | generated-no-copyright | 24 extensions | 1.4 | OASIS ODF 1.4 Extended | valid | semantic-equivalent |
| generated-interop-package | generated in `InteropCorpusTests` | generated | n/a | generated-no-copyright | package formats | 1.4 | OASIS ODF 1.4 Extended | valid | preserve-unknown |
| generated-schema-negative | generated in `CorpusComplianceTests` | generated | n/a | generated-no-copyright | ODT / flat XML | 1.4 | OASIS ODF 1.4 Strict | invalid | semantic-equivalent |
| generated-security-boundary | generated in `OdfSecurityBoundaryTests` | generated | n/a | generated-no-copyright | package formats | mixed | policy profiles | mixed | preserve-unknown |

> 以上四列為測試執行時動態產生的樣本（未提交為實體檔案），`24 extensions` 對應
> `OdfDocumentKindDetector.SupportedFormats` 目前登記的全部副檔名數量。

`tests/fixtures/corpus/manifest.json` 實際已提交的 fixture 數量遠多於上表列出的初始集合
（目前共 266 筆，含 Chart／Database／Image／Formula／各 Template 種類、ODF 1.1／1.2／1.3
版本樣本，以及 `bulk-*` 前綴的大量規模測試樣本)；上表僅保留最初建立 corpus 機制時的種子
fixture 作為欄位用法範例，並非完整清單。欲取得目前完整、權威的 fixture 清單，請直接讀取
`tests/fixtures/corpus/manifest.json`，不要以本文件表格為準。

complex builder corpus 目前包含四個 schema-clean 正向樣本：`complex-annual-report.odt`、
`complex-financial-model.ods`、`complex-business-deck.odp` 與 `complex-flow-diagram.odg`。
這些檔案是本儲存庫原創產生資料，授權使用 `generated-no-copyright`。

## 外部 corpus 路徑

外部 corpus 不提交到儲存庫。測試或工具若支援外部 corpus，應讀取：

```powershell
$env:ODFKIT_PARITY_CORPUS_ROOT = "D:\Corpus\OdfKit"
```

外部 corpus 的 manifest 可放在該資料夾根目錄，格式需包含本文件列出的欄位。

可用 CLI 直接執行 manifest：

```powershell
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate-corpus tests\fixtures\corpus\manifest.json --format json

dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate-corpus manifest.json `
  --root $env:ODFKIT_PARITY_CORPUS_ROOT `
  --format json

dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate-corpus manifest.json `
  --metadata-only `
  --baseline-exceptions baseline-exceptions.json `
  --format json
```

儲存庫也提供 CI 與本機共用腳本：

```powershell
.\eng\Initialize-OdfExternalCorpus.ps1 -OutputRoot D:\Corpus\OdfKit

.\eng\Initialize-OdfExternalCorpus.ps1 -OutputRoot D:\Corpus\OdfKitOdfDom `
  -Template odfdom-sample-corpus

.\eng\Test-OdfCorpus.ps1
```

`Initialize-OdfExternalCorpus.ps1` 會將外部 manifest 與 baseline exception 範本複製到指定資料夾。
預設複製 ODF Validator 外部 corpus 範本；若要建立 ODFDOM 官方 sample parity
根目錄，請指定 `-Template odfdom-sample-corpus`。
`Test-OdfCorpus.ps1` 必定執行內建 corpus；若設定 `ODFKIT_PARITY_CORPUS_ROOT`，則會同時執行外部 corpus。
外部 corpus 會先以 `validate-corpus --metadata-only` 檢查 manifest 與 baseline exception 中繼資料，
再執行實體 fixture 驗證。
若再設定 `ODFKIT_ODFVALIDATOR_JAR`，外部 corpus 會加上 ODF Validator baseline。
若外部 corpus 有已驗證的暫時分類差異，可用 `-BaselineExceptions` 指向
`baseline-exceptions.json`。

若要對 repo 內 corpus 執行外部 baseline，可傳入 `-InternalBaselineJar`，並在需要時用
`-InternalBaselinePackageOnly` 限定 ZIP package，並可用 `-InternalBaselineExcludedKinds` 排除
外部工具已確認不適用的種類。腳本會在系統暫存目錄建立短生命週期的
版本篩選 manifest；預設版本集合為 ODF 1.0～1.4。CI 的
`.github/workflows/odf-external-baseline.yml` 固定使用 package-only 模式，因為 ODF Validator
CLI 以 package loader 開啟文件；獨立的 `Test-OdfRelaxNgBaseline.ps1` 則以 Jing 驗證通用
OASIS ODF schema 適用的 flat 文件與 package XML streams。`Formula`、`FormulaTemplate` 與
`FlatFormula` 使用該 schema 未定義的 `office:formula` 表示法，因此由腳本明列排除並保留在
內部 package gate；報告會輸出實際排除集合。
package-only 依 manifest 的 `kind` 是否以 `Flat` 開頭判斷，不依可能重疊的副檔名推測；例如
`.odf` 可能是 ZIP formula package，也可能是 `FlatFormula`。需要改變版本集合時使用
`-InternalBaselineVersions`。

`validate-corpus` 會讀取 `fixtures` 陣列，逐一比對 `expected` 的 `valid` / `invalid`
classification、`kind` 文件種類與 `version` ODF 版本。任一 fixture 與 manifest 宣告不一致，
或未文件化的外部 baseline mismatch，都會讓 exit code 為 `1`。

對宣告為 valid 的 fixture，CLI 也會依 `roundTrip` 實際載入、儲存並重新載入文件：

- `semantic-equivalent` 比對文件種類、ODF 版本及 `ExtractText()` 的邏輯文字結果。
- `preserve-unknown` 除上述語意外，亦逐項比對非核心 XML 的 ZIP entry 名稱與 SHA-256，
  因此圖片、內嵌物件、供應商擴充與未知 payload 遺失時會讓 corpus gate 失敗。
- `byte-identical` 另要求完整輸出位元組相同，只適合明確承諾不重寫封裝的情境。

JSON 與文字報告會輸出 round-trip 檢查數、失敗數、文字是否一致及保留 entry 數量；invalid
fixture 不執行 round-trip，避免把刻意損毀的輸入當成可儲存文件。

`--metadata-only` 只檢查 manifest 與 baseline exception manifest 的結構與中繼資料
規則，不要求 fixture 檔案存在，也不執行 OdfKit 或外部 validator。這可用於官方或授權待確認
corpus 在下載樣本前的來源、授權與 baseline 例外格式檢查。

## JSON Collaboration fixture

JSON Collaboration fixture 不屬於 ODF package corpus，因此不放入 `validate-corpus` 的 ODF manifest。
儲存庫內 JSON wire-shape fixture 位於 `tests/fixtures/collaboration/`，並由
`tests/fixtures/collaboration/manifest.json` 記錄來源、授權、TDF changes envelope 形狀、operation
覆蓋、語意狀態、預期 replay report 與 SHA-256。此 manifest 採 clean-room 對標：可使用 TDF
公開文件與 JSON wire shape 作為來源，不可複製 Java 實作。`tdf-public-docs/` 必須讓 TDF 公開 ODF
Text operation 名稱各至少有一個最小 fixture；`repo-generated/` 可放 unknown-field round-trip、
safety limit 與 strict diagnostic fixture。

## Baseline exception manifest

外部 ODF Validator 與 OdfKit 的 valid / invalid classification 若不同，必須另外記錄
baseline exception manifest，並在 CLI 加上 `--baseline-exceptions`。此 manifest 可與外部
corpus manifest 放在同一資料夾，建議命名為 `baseline-exceptions.json`。

```json
{
  "exceptions": [
    {
      "path": "relative/path/to/fixture.odt",
      "baseline": "odf-validator",
      "odfKitIsValid": true,
      "baselineIsValid": false,
      "profileId": "OASIS_ODF_1_4_Extended",
      "reason": "說明為何暫時接受這個分類差異。"
    }
  ]
}
```

`path` 若只填檔名，會對檔名比對；若包含 `/`，會以正斜線正規化後對完整路徑尾端比對。
baseline exception 不可重複，且每筆都必須能對應到同一份 corpus manifest 的某個 fixture。
沒有列入此 manifest 的 baseline mismatch 仍必須讓 parity job 失敗。
