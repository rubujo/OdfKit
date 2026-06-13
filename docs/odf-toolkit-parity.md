# ODF Toolkit Parity

本文件定義 OdfKit 對標 ODF Toolkit、ODFDOM 與 ODF Validator 的完成線。
此線是 OdfKit 的「官方 parity」基準；商業文件元件能力另列為成熟度路線圖，
不作為核心 ODF 規格可信度的必要條件。

## 對標來源

- ODF Toolkit：Java 模組集合，包含 ODFDOM、Simple API 與驗證工具。
- ODFDOM：以 ODF schema 為基礎的 typed DOM 與文件操作模型。
- ODF Validator：ODF Toolkit 提供的 conformance validator。
- OASIS ODF TC tooling：ODF schema 與規格資料來源。

## Parity 等級

- `complete`：OdfKit 有對應 API、測試與文件證據。
- `validated`：OdfKit 有驗證或 corpus 證據，但仍需更多文件或樣本。
- `partial`：已具備可用能力，但尚未達到 ODF Toolkit / ODFDOM 同等深度。
- `planned`：尚未有足夠程式與測試證據支撐。

## 矩陣

| Area | OdfKit surface | Baseline | Status | Completion criteria |
|---|---|---|---|---|
| Package API | `OdfPackage` | ODF Toolkit package handling | complete | 可開啟、建立、保存 ZIP / flat XML，並保留 unknown entries。 |
| Document factory | `OdfDocumentFactory`、typed wrappers | Simple API document load/create | complete | 17 種主要 extension 可最小 create / load / save / validate / round-trip。 |
| Validator API | `OdfValidator`、`OdfPackageValidator`、`OdfFlatDocumentValidator` | ODF Validator | partial | `validate-corpus` 可執行 manifest 並比對 expected classification、kind 與 version；仍需擴大官方 corpus。 |
| External baseline | `OdfExternalValidator`、CLI `--baseline` | ODF Validator CLI | validated | 可選執行 ODF Validator JAR，並支援 documented exception manifest；未設定時一般測試與 CI 不受影響。 |
| Typed DOM | generated DOM wrappers、`OdfNodeFactory` | ODFDOM | partial | 以 [typed-dom-coverage.md](typed-dom-coverage.md) 追蹤 wrapper / factory / attribute coverage，並逐步補 typed datatype。 |
| Simple high-level API | Text / Spreadsheet / Presentation / Drawing facade | ODF Toolkit Simple API | partial | ODT / ODS / ODP / ODG 常見建立、讀取與有限修改有直接 facade。 |
| Corpus | generated、positive、negative、unknown、security corpus | ODF Validator sample corpus | validated | 小型可提交 corpus 有 manifest；大型或第三方 corpus 可用 `validate-corpus` 搭配外部路徑執行。 |

## 外部 baseline 執行

核心 OdfKit 不依賴 Java。外部 ODF Validator 僅在明確啟用時執行。

```powershell
dotnet run --project tools/OdfKit.Cli -- validate sample.odt `
  --baseline odf-validator `
  --baseline-jar C:\tools\odfvalidator.jar
```

也可透過環境變數提供 JAR：

```powershell
$env:ODFKIT_ODFVALIDATOR_JAR = "C:\tools\odfvalidator.jar"
dotnet run --project tools/OdfKit.Cli -- validate sample.odt --baseline odf-validator
```

`validate` 會比較 OdfKit 與外部 validator 的 valid / invalid classification。
若分類不同且沒有列入 documented exception，exit code 為 `1`，JSON summary 的
`baselineMismatchCount` 會大於 `0`。

若使用自訂 wrapper、已知 ODF Validator 誤判或 profile 差異，需要明確提供例外清單：

```powershell
dotnet run --project tools/OdfKit.Cli -- validate sample.odt `
  --baseline odf-validator `
  --baseline-jar C:\tools\odfvalidator.jar `
  --baseline-exceptions docs\baseline-exceptions.json `
  --format json
```

例外清單格式如下：

```json
{
  "exceptions": [
    {
      "path": "samples/known-profile-difference.odt",
      "baseline": "odf-validator",
      "odfKitIsValid": true,
      "baselineIsValid": false,
      "profileId": "OASIS_ODF_1_4_Extended",
      "reason": "外部 validator 尚未接受此 ODF 1.4 profile 組合。"
    }
  ]
}
```

`path` 可以是完整相對路徑，也可以只填檔名；含 `/` 的路徑會以正斜線正規化後比對。
已記錄的差異會讓 `baselineDocumentedExceptionCount` 增加，且該檔案的
`baseline.documentedException` 為 `true`；`baseline.matchesOdfKit` 仍保留原始分類是否一致。

## Corpus 原則

- repo 只提交小型、授權清楚、去識別化或 generated 的 fixtures。
- 大型、第三方或授權不明 corpus 不提交；使用 `ODFKIT_PARITY_CORPUS_ROOT`
  指向本機資料夾。
- 每個 fixture 都要記錄來源、授權、預期 valid / invalid、ODF 版本、
  profile 與 round-trip 預期。
- 不以 byte-level identity 作為一般 round-trip 要求；除非該 fixture 明確標記。

Corpus manifest 可用 CLI 執行：

```powershell
dotnet run --project tools/OdfKit.Cli -- validate-corpus manifest.json `
  --root $env:ODFKIT_PARITY_CORPUS_ROOT `
  --baseline odf-validator `
  --baseline-jar C:\tools\odfvalidator.jar `
  --baseline-exceptions baseline-exceptions.json
```

`validate-corpus` 會把 fixture 的 `expected`、`kind` 與 `version` 欄位視為 OdfKit
corpus 完成線；外部 baseline mismatch 若未列入 documented exception，也會讓 job 失敗。

## Documented exceptions

若 OdfKit 與外部 ODF Validator 分類不同，必須記錄在 `--baseline-exceptions` 使用的 JSON manifest：

- fixture path
- OdfKit classification
- external classification
- OdfKit issue codes 或外部 output 摘要
- profile
- 暫時接受差異的原因

沒有 documented exception 的 mismatch 代表 parity 失敗。
