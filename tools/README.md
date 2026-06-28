# OdfKit 工具總覽

本目錄收錄 OdfKit 的命令列工具與開發用產生器。它們**不是**可發佈套件，
主要用於驗證、轉換、schema 產生、corpus 產生與 trimming 煙霧測試。

## 工具一覽

| 工具 | 主要用途 | 執行方式 |
|------|----------|----------|
| `OdfKit.Cli` | 驗證、資訊查詢、sanitize、flat XML / CSV 轉換 | `dotnet run --project tools/OdfKit.Cli --framework net10.0 -- ...` |
| `OdfSchemaGenerator` | 從 OASIS RNG 產生 schema metadata、provider 與 DOM wrapper 輔助輸出 | `dotnet run --project tools/OdfSchemaGenerator -- ...` |
| `OdfCorpusGenerator` | 依支援格式批次產生 corpus fixtures 並更新 manifest | `dotnet run --project tools/OdfCorpusGenerator -- [repo-root]` |
| `OdfKit.TrimSmoke` | Native AOT / trimming API 根煙霧測試 | `dotnet run --project tools/OdfKit.TrimSmoke` |

## 1. OdfKit.Cli

### 可用命令

| 命令 | 用途 |
|------|------|
| `validate` | 驗證單一檔案或資料夾中的 ODF 文件 |
| `validate-corpus` | 依 manifest 驗證 corpus |
| `info` | 顯示 package / MIME / 版本 / entry 摘要 |
| `metadata` | 顯示文件中介資料 |
| `sanitize` | 重新輸出並清理文件，可搭配密碼與加密選項 |
| `typed-dom-coverage` | 輸出 typed DOM 覆蓋報告 |
| `convert-flat` | ZIP ODF 與 Flat XML 之間轉換 |
| `convert-csv` | ODS / FODS 與 CSV 之間轉換 |
| `pack` | 將 Flat XML 重新封裝為 ZIP ODF |

### 常用範例

```powershell
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate file.odt
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- validate samples --recursive --format json
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- info file.ods
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- metadata file.odt
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- sanitize input.odt sanitized.odt
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- typed-dom-coverage --format json
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- convert-flat input.odt output.fodt
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- convert-csv input.ods output.csv
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- pack input.fodt output.odt
```

更完整的 corpus 與對標情境請見：

- [docs/corpus-manifest.md](../docs/corpus-manifest.md)
- [docs/odf-toolkit-parity.md](../docs/odf-toolkit-parity.md)
- [docs/typed-dom-coverage.md](../docs/typed-dom-coverage.md)

## 2. OdfSchemaGenerator

`OdfSchemaGenerator` 會讀取 OASIS RELAX NG schema，輸出可供 OdfKit 使用的
中繼資料或輔助程式碼。

### 主要選項

```text
Usage: OdfSchemaGenerator [--format json|csharp|csharp-provider|dom-wrappers]
                          [--output <file>]
                          [--output-directory <directory>]
                          [--class-name <name>]
                          [--source-url <uri>]
                          [--source-date <date>]
                          [--version 1.0|1.1|1.2|1.3|1.4]
                          <schema.rng>
```

### 範例

```powershell
dotnet run --project tools/OdfSchemaGenerator -- `
  --format json `
  --version 1.4 `
  --output artifacts\odf14-schema.json `
  tools\OdfSchemaGenerator\schemas\OpenDocument-v1.3-schema.rng
```

相關背景請見 [docs/odf-official-corpus-sources.md](../docs/odf-official-corpus-sources.md)
與 [docs/typed-dom-coverage.md](../docs/typed-dom-coverage.md)。

## 3. OdfCorpusGenerator

`OdfCorpusGenerator` 會依 `OdfDocumentKindDetector.SupportedFormats` 與既定
scenario 批次產生 corpus fixtures，並更新 `tests/fixtures/corpus/manifest.json`。

```powershell
dotnet run --project tools/OdfCorpusGenerator
dotnet run --project tools/OdfCorpusGenerator -- D:\Dev\Project\Application\OdfKit
```

輸出位置：

- `tests/fixtures/corpus/generated/bulk/`
- `tests/fixtures/corpus/manifest.json`

相關契約請見 [docs/corpus-manifest.md](../docs/corpus-manifest.md)。

## 4. OdfKit.TrimSmoke

`OdfKit.TrimSmoke` 是開發期 smoke test，會直接觸及主要公開 API 根，驗證
trimming / Native AOT 情境下的基本可用性。

```powershell
pwsh eng/Test-TrimSmoke.ps1 -Configuration Release
dotnet run --project tools/OdfKit.TrimSmoke
```

標準驗證使用 `eng/Test-TrimSmoke.ps1`，它會以 `PublishTrimmed` 發佈並執行裁剪後的
`OdfKit.TrimSmoke.exe`。`-PublishAot` 僅作為 Native AOT 研究入口；BouncyCastle OpenPGP
路徑目前仍透過 trimming guard 保留必要組件，不宣稱完整 Native AOT 支援。

成功時會輸出類似：

```text
TrimSmoke OK: 15 API 根通過
```

## 5. 與 samples 的分工

- `tools/` 側重 **驗證、轉換、產生與工程煙霧測試**
- `samples/` 側重 **使用情境展示與輸出成果範例**

若要先看 API 示範，請讀 [samples/README.md](../samples/README.md)。
