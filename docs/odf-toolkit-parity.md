# ODF Toolkit 對標線

本文件定義 OdfKit 對標 ODF Toolkit、ODFDOM 與 ODF Validator 的完成線。
此線是 OdfKit 的「官方對標」基準；其他文件元件能力另列為成熟度路線圖，
不作為核心 ODF 規格可信度的必要條件。

## 對標來源

- ODF Toolkit：Java 模組集合，包含 ODFDOM、Simple API 與驗證工具。
- ODFDOM：以 ODF schema 為基礎的 typed DOM 與文件操作模型。
- ODF Validator：ODF Toolkit 提供的 conformance validator。
- OASIS ODF TC tooling：ODF schema 與規格資料來源。

官方 corpus 來源與 baseline 命名規則請見 [odf-official-corpus-sources.md](odf-official-corpus-sources.md)。
ODF 1.4（2025-12 OASIS Standard）四份正式規格文本逐章稽核結論見
[ODF 1.4 逐章稽核紀錄](odf14-gap-audit.md)。

## 對標等級

- `evidence-verified`：OdfKit 對列出的工作流程已有對應 API、測試與文件證據。
- `validated`：OdfKit 有驗證或 corpus 證據，但仍需更多文件或樣本。
- `partial`：已具備可用能力，但尚未達到 ODF Toolkit / ODFDOM 同等深度。
- `planned`：尚未有足夠程式與測試證據支撐。

## 矩陣

| 範圍 | OdfKit 表面 | Baseline | 狀態 | 完成條件 |
|---|---|---|---|---|
| Package API | `OdfPackage` | ODF Toolkit package handling | evidence-verified | 可開啟、建立、保存 ZIP / Flat XML，並保留 unknown entries。 |
| 文件工廠 | `OdfDocumentFactory`、typed wrappers | Simple API document load/create | evidence-verified | 24 種主要 extension 可最小 create / load / save / validate / round-trip。 |
| Validator API | `OdfValidator`、`OdfPackageValidator`、`OdfFlatDocumentValidator` | ODF Validator 的分類與診斷工作流 | evidence-verified | 內建 package、官方 RNG 衍生 schema metadata／pattern 與 profile gate；`validate-corpus` 可比對 expected classification、kind 與 version。核心不是可載入任意 RNG 的通用 validator。 |
| 外部 baseline | `OdfExternalValidator`、CLI `--baseline` | ODF Validator CLI | evidence-verified | 獨立 CI 以固定版本與 SHA-256 對標 repo 內版本屬於 ODF 1.1～1.4 的所有 package fixtures；`validate` 與 `validate-corpus` 都會把未文件化 baseline mismatch 視為失敗，並支援 documented exception manifest。 |
| Typed DOM | generated DOM wrappers、`OdfNodeFactory`、`OdfTypedDomCoverage`、typed attribute helpers、schema-specific child collections | ODFDOM | evidence-verified | 以 CLI `typed-dom-coverage`、`eng/Test-OdfTypedDomCoverage.ps1` 與 CI artifact 追蹤 child relation coverage；generated wrappers 已包含常用 datatype typed property、2,000+ schema-specific child collection property，且 repo 內已有完整型別與符合 ODFDOM-style sample traversal 的測試。 |
| Simple high-level API | Text / Spreadsheet / Presentation / Drawing facade | ODF Toolkit Simple API | evidence-verified | ODT / ODS / ODP / ODG 常見建立、讀取（如 presentation page、MathML formula object 支援）、複雜樣式、公式、加密、樞紐分析表與條件格式有直接外觀層，並具備完整 `[Fact]`／`[Theory]` 測試套件驗證。 |
| Corpus | generated、positive、negative、unknown、security corpus | ODF Validator sample corpus | evidence-verified | repo 內已有封裝與 flat 主要格式的可執行 manifest 範本，包含 ODF 1.1/1.2/1.3/1.4 及負向驗證；大型或第三方 corpus 可用 `validate-corpus` 搭配外部路徑執行。 |
| Foreign extension policy | extended profile warning、unknown XML round-trip、macro sanitization 邊界 | ODFDOM extension preservation | evidence-verified | 以 [foreign-extension-policy.md](foreign-extension-policy.md) 文件化 foreign namespace 隔離、保存與淨化邊界。 |

## 外部 baseline 執行

核心 OdfKit 不依賴 Java。獨立的 `odf-external-baseline.yml` 固定使用 Jing 20241231、
ODF Validator 0.13.0 與 Java 11。Jing 直接以 repo 內 OASIS ODF 1.1～1.4 schema 驗證該
通用 schema 適用的 flat 文件與 package XML streams；Formula／FormulaTemplate／FlatFormula
因使用 schema 未定義的 `office:formula` 表示法而由報告明列排除，仍由內部 package gate
覆蓋。ODF Validator 則對適用的 ZIP package 執行分類對標，並跑
真實 JAR 正／負 package canary。Database 與 Formula／FormulaTemplate 的 ODF Validator 0.13.0
上游限制不會變成 allowlist：它們只從該工具集合排除；Database 仍由 Jing 覆蓋。
兩條外部 baseline 都是阻擋 gate。

外部工具的供應鏈資料集中於 `eng/external-tools.json`。每個 CI cache key 都包含工具來源、
cache revision、版本與完整 SHA-256，沒有寬鬆 fallback key；安裝腳本在 cache 命中後仍驗證
archive 與每個必要 JAR 的內容雜湊，已存在但不符時立即失敗，
cache miss 才以暫存檔下載、驗證後移入正式路徑。異常 cache 需調查後明確遞增
`cacheRevision`，不可靜默覆寫。cache 只保存工具，不保存驗證輸出或暫存 corpus manifest。

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

- 儲存庫只提交小型、授權清楚、去識別化或 generated 的 fixtures。
- 大型、第三方或授權不明 corpus 不提交；使用 `ODFKIT_PARITY_CORPUS_ROOT`
  指向本機資料夾。
- 外部 corpus 可從 `docs/examples/external-corpus/manifest.json` 複製範本開始，
  並以 `docs/examples/external-corpus/baseline-exceptions.json` 記錄暫時分類差異。
- ODFDOM 官方 sample parity 已釘選於 `docs/examples/odfdom-sample-corpus/manifest.json`：
  鎖定 `tdf/odftoolkit v0.13.0`（commit `b926a6134a2fee782076500dfc02c47c2d651cff`）四個
  官方 sample（Text／Spreadsheet／Presentation／Graphics，皆 ODF 1.2），已完成 Apache-2.0
  授權審核並填入實際 `sha256`；若要擴充更多 fixture 或改版到更新的上游 release，依同一份
  manifest 的欄位格式（`sourceUri`、授權審核狀態、expected classification、round-trip 策略、
  `sha256`）新增或更新項目，並同步調整
  `OdfKit.Tests/DocsAndCorpusContractTests.cs.ExternalOdfDomSampleCorpusTemplateCanBeMetadataValidatedByCli`
  的釘選 commit／雜湊斷言。
- 每個 fixture 都要記錄來源、授權、預期 valid / invalid、ODF 版本、
  profile 與 round-trip 預期。
- Corpus manifest 會拒絕重複 `id` / `path`、未知 `roundTrip` 策略與逃出 corpus root 的路徑。
- 不以 byte-level identity 作為一般來回讀寫要求；除非該 fixture 明確標記。

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
`--metadata-only` 可在樣本檔案尚未存在時檢查來源 URI、授權欄位、profile、來回讀寫
策略與 baseline exception manifest 格式。

## 已記錄例外

若 OdfKit 與外部 ODF Validator 分類不同，必須記錄在 `--baseline-exceptions` 使用的 JSON manifest：

- fixture path
- OdfKit classification
- external classification
- OdfKit 問題代碼或外部 output 摘要
- profile
- 暫時接受差異的原因

baseline exception 不可重複，且每筆都必須對應到同一份 corpus manifest 的 fixture；
過期或孤立的 exception 會讓 `validate-corpus` 失敗。

沒有 documented exception 的 mismatch 代表對標失敗。
