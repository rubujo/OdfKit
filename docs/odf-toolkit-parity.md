# ODF Toolkit Parity

本文件定義 OdfKit 對標 ODF Toolkit、ODFDOM 與 ODF Validator 的完成線。
此線是 OdfKit 的「官方 parity」基準；其他文件元件能力另列為成熟度路線圖，
不作為核心 ODF 規格可信度的必要條件。

## 對標來源

- ODF Toolkit：Java 模組集合，包含 ODFDOM、Simple API 與驗證工具。
- ODFDOM：以 ODF schema 為基礎的 typed DOM 與文件操作模型。
- ODF Validator：ODF Toolkit 提供的 conformance validator。
- OASIS ODF TC tooling：ODF schema 與規格資料來源。

官方 corpus 來源與 baseline 命名規則請見 [odf-official-corpus-sources.md](odf-official-corpus-sources.md)。

## Parity 等級

- `complete`：OdfKit 有對應 API、測試與文件證據。
- `validated`：OdfKit 有驗證或 corpus 證據，但仍需更多文件或樣本。
- `partial`：已具備可用能力，但尚未達到 ODF Toolkit / ODFDOM 同等深度。
- `planned`：尚未有足夠程式與測試證據支撐。

## 矩陣

| Area | OdfKit surface | Baseline | Status | Completion criteria |
|---|---|---|---|---|
| Package API | `OdfPackage` | ODF Toolkit package handling | complete | 可開啟、建立、保存 ZIP / flat XML，並保留 unknown entries。 |
| Document factory | `OdfDocumentFactory`、typed wrappers | Simple API document load/create | complete | 24 種主要 extension 可最小 create / load / save / validate / round-trip。 |
| Validator API | `OdfValidator`、`OdfPackageValidator`、`OdfFlatDocumentValidator` | ODF Validator | complete | `validate-corpus` 可執行 manifest 並比對 expected classification、kind 與 version；已包含官方 1.1/1.2/1.3/1.4 真實 RNG 驗證並已於 CI 中執行。 |
| External baseline | `OdfExternalValidator`、CLI `--baseline` | ODF Validator CLI | complete | 可選執行 ODF Validator JAR；`validate` 與 `validate-corpus` 都會把未文件化 baseline mismatch 視為失敗，並支援 documented exception manifest。 |
| Typed DOM | generated DOM wrappers、`OdfNodeFactory`、`OdfTypedDomCoverage`、typed attribute helpers、schema-specific child collections | ODFDOM | complete | 以 [typed-dom-coverage.md](typed-dom-coverage.md)、CLI `typed-dom-coverage` 與 CI artifact 追蹤 child relation coverage；generated wrappers 已包含常用 datatype typed property、2,000+ schema-specific child collection property，且 repo 內已有完整型別與符合 ODFDOM-style sample traversal 的測試。 |
| Simple high-level API | Text / Spreadsheet / Presentation / Drawing facade | ODF Toolkit Simple API | complete | ODT / ODS / ODP / ODG 常見建立、讀取（如 presentation page、MathML formula object 支援）、複雜樣式、公式、加密、樞紐分析表與條件格式有直接之 facade，並具備完整 `[Fact]`／`[Theory]` 測試套件驗證，詳見 [testing-strategy.md](testing-strategy.md)。 |
| Corpus | generated、positive、negative、unknown、security corpus | ODF Validator sample corpus | complete | repo 內已有封裝與 flat 主要格式的可執行 manifest 範本，包含 ODF 1.1/1.2/1.3/1.4 及負向驗證；大型或第三方 corpus 可用 `validate-corpus` 搭配外部路徑執行。 |
| Foreign extension policy | extended profile warning、unknown XML round-trip、macro sanitization 邊界 | ODFDOM extension preservation | complete | 以 [foreign-extension-policy.md](foreign-extension-policy.md) 文件化 foreign namespace 隔離、保存與淨化邊界。 |

## 外部 baseline 執行

核心 OdfKit 不依賴 Java。外部 ODF Validator 僅在明確啟用時執行。

```powershell
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate sample.odt `
  --baseline odf-validator `
  --baseline-jar C:\tools\odfvalidator.jar
```

也可透過環境變數提供 JAR：

```powershell
$env:ODFKIT_ODFVALIDATOR_JAR = "C:\tools\odfvalidator.jar"
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate sample.odt --baseline odf-validator
```

`validate` 會比較 OdfKit 與外部 validator 的 valid / invalid classification。
若分類不同且沒有列入 documented exception，exit code 為 `1`，JSON summary 的
`baselineMismatchCount` 會大於 `0`。

若使用自訂 wrapper、已知 ODF Validator 誤判或 profile 差異，需要明確提供例外清單：

```powershell
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate sample.odt `
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
- 外部 corpus 可從 `docs/examples/external-corpus/manifest.json` 複製範本開始，
  並以 `docs/examples/external-corpus/baseline-exceptions.json` 記錄暫時分類差異。
- ODFDOM 官方 sample parity 可從 `docs/examples/odfdom-sample-corpus/manifest.json`
  複製範本開始；此範本要求 `sourceUri`、授權審核狀態、expected classification、
  round-trip 策略與 `sha256` 欄位格式先通過 metadata gate。
- 每個 fixture 都要記錄來源、授權、預期 valid / invalid、ODF 版本、
  profile 與 round-trip 預期。
- Corpus manifest 會拒絕重複 `id` / `path`、未知 `roundTrip` 策略與逃出 corpus root 的路徑。
- 不以 byte-level identity 作為一般 round-trip 要求；除非該 fixture 明確標記。

Corpus manifest 可用 CLI 執行：

```powershell
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate-corpus tests\fixtures\corpus\manifest.json --format json

.\eng\Test-OdfCorpus.ps1

.\eng\Initialize-OdfExternalCorpus.ps1 -OutputRoot D:\Corpus\OdfKit

.\eng\Initialize-OdfExternalCorpus.ps1 -OutputRoot D:\Corpus\OdfKitOdfDom `
  -Template odfdom-sample-corpus

dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate-corpus manifest.json `
  --root $env:ODFKIT_PARITY_CORPUS_ROOT `
  --baseline odf-validator `
  --baseline-jar C:\tools\odfvalidator.jar `
  --baseline-exceptions baseline-exceptions.json

dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate-corpus manifest.json `
  --metadata-only `
  --baseline-exceptions baseline-exceptions.json `
  --format json
```

`validate-corpus` 會把 fixture 的 `expected`、`kind` 與 `version` 欄位視為 OdfKit
corpus 完成線；外部 baseline mismatch 若未列入 documented exception，也會讓 job 失敗。
`--metadata-only` 可在樣本檔案尚未存在時檢查來源 URI、授權欄位、profile、round-trip
策略與 baseline exception manifest 格式。

## Documented exceptions

若 OdfKit 與外部 ODF Validator 分類不同，必須記錄在 `--baseline-exceptions` 使用的 JSON manifest：

- fixture path
- OdfKit classification
- external classification
- OdfKit 問題代碼或外部 output 摘要
- profile
- 暫時接受差異的原因

baseline exception 不可重複，且每筆都必須對應到同一份 corpus manifest 的 fixture；
過期或孤立的 exception 會讓 `validate-corpus` 失敗。

沒有 documented exception 的 mismatch 代表 parity 失敗。
