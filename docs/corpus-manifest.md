# Corpus Manifest

本文件記錄可提交 corpus 的最小 metadata 規則。新增 fixture 時，必須在同一
pull request 更新本文件或等價 manifest，避免測試樣本來源變成黑盒。

## 可提交條件

- fixture 必須是 generated、專案自有、授權清楚，或已去識別化且可再散布。
- fixture 應保持小型；大型真實世界 corpus 改由 `ODFKIT_PARITY_CORPUS_ROOT`
  指向本機或 CI artifact。
- 不提交含個資、商業機密、未知授權或不可再散布內容的文件。
- 每個 fixture 必須有預期驗證結果與 round-trip 策略。

## 欄位

| Field | Required | Description |
|---|---|---|
| `id` | yes | 穩定 fixture id。 |
| `path` | yes | repo 內路徑或外部 corpus 相對路徑。 |
| `source` | yes | generated、OdfKit、ODF Toolkit sample、LibreOffice export、real-world sanitized 等。 |
| `license` | yes | CC0、Apache-2.0、MPL-2.0、generated-no-copyright 等。 |
| `kind` | yes | `OdfDocumentKind` 或 extension。 |
| `version` | yes | 預期 ODF 版本。 |
| `profile` | yes | 預期驗證 profile。 |
| `expected` | yes | `valid` 或 `invalid`。 |
| `roundTrip` | yes | `preserve-unknown`、`semantic-equivalent` 或 `byte-identical`。 |
| `notes` | no | 差異、例外或外部 validator 注意事項。 |

## 初始內建 corpus

| id | path | source | license | kind | version | profile | expected | roundTrip |
|---|---|---|---|---|---|---|---|---|
| generated-format-minimal | generated in `OdfFormatRoundTripTests` | generated | generated-no-copyright | 17 extensions | 1.4 | OASIS ODF 1.4 Extended | valid | semantic-equivalent |
| generated-interop-package | generated in `InteropCorpusTests` | generated | generated-no-copyright | package formats | 1.4 | OASIS ODF 1.4 Extended | valid | preserve-unknown |
| generated-schema-negative | generated in `CorpusComplianceTests` | generated | generated-no-copyright | ODT / flat XML | 1.4 | OASIS ODF 1.4 Strict | invalid | semantic-equivalent |
| generated-security-boundary | generated in `OdfSecurityBoundaryTests` | generated | generated-no-copyright | package formats | mixed | policy profiles | mixed | preserve-unknown |

## 外部 corpus 路徑

外部 corpus 不提交到 repo。測試或工具若支援外部 corpus，應讀取：

```powershell
$env:ODFKIT_PARITY_CORPUS_ROOT = "D:\Corpus\OdfKit"
```

外部 corpus 的 manifest 可放在該資料夾根目錄，格式需包含本文件列出的欄位。

可用 CLI 直接執行 manifest：

```powershell
dotnet run --project tools/OdfKit.Cli -- validate-corpus manifest.json `
  --root $env:ODFKIT_PARITY_CORPUS_ROOT `
  --format json
```

`validate-corpus` 會讀取 `fixtures` 陣列，逐一比對 `expected` 的 `valid` / `invalid`
classification。任一 fixture 與 expected 不一致，或未文件化的外部 baseline mismatch，
都會讓 exit code 為 `1`。

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
沒有列入此 manifest 的 baseline mismatch 仍必須讓 parity job 失敗。
