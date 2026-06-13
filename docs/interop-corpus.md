# Interop Corpus

本文件記錄目前 OdfKit 用來支撐相容性與 round-trip 宣稱的 corpus 類型。這不是外部檔案清單的完整快照，而是可維護的測試來源說明。fixture metadata 規則請見 [corpus-manifest.md](corpus-manifest.md)，官方 corpus 來源請見 [odf-official-corpus-sources.md](odf-official-corpus-sources.md)，ODF Toolkit / ODF Validator 對標線請見 [odf-toolkit-parity.md](odf-toolkit-parity.md)。

## Generated corpus

- `OdfFormatRoundTripTests`：建立 17 種主要 ODF extension 的最小文件，驗證 MIME、ODF 版本、document kind、載入與保存。
- `InteropCorpusTests`：針對主要封裝格式驗證公開 validator 入口、document kind、ODF 版本、保存後重新開啟，以及未知 package entry 保留。
- `OdfValidatorApiTests`：覆蓋 package 與 flat XML 驗證入口。
- `PackageRoundTripTests`：覆蓋 flat XML / ZIP package 互轉、圖片與嵌入公式 round-trip。

## ODF 1.4 positive corpus

- `CorpusComplianceTests` 以 OASIS ODF 1.4 schema provider 與最小文件樣本驗證主要 body kind。
- Positive corpus 目前重點是格式偵測、body kind、manifest 與 schema pattern 可執行性。

## Negative corpus

- `CorpusComplianceTests` 與 `ComplianceTests` 覆蓋錯誤 root、錯誤 MIME / extension、Zip Slip、manifest 不一致與 profile rule 違規。
- 驗證器應回報結構化 issue，而不是在一般錯誤文件上崩潰。

## Unknown content corpus

- `OdfPackageUnknownEntryTests` 覆蓋未知 package entries、`Configurations2`、`ObjectReplacements` 與未知 media entry 保存。
- `InteropCorpusTests` 確認主要封裝格式在 validator 與 package save round-trip 後仍保留未知 binary entry。
- `OdfUnknownXmlRoundTripTests` 覆蓋 foreign namespace、未知屬性、comments、processing instructions 與 prefix 保留。

## Security boundary corpus

- `OdfSecurityBoundaryTests` 覆蓋簽章保存 / 失效、macro sanitize、加密文件 sanitize 後重新保存。
- XML reader 與 package loader 另有 XXE、DoS 與 Zip Slip 防禦測試。

## Rendering / LibreOffice corpus

- `OdfKit.Extensions.Rendering` 與相關測試使用可替換的 LibreOffice finder。
- 這部分屬可選 rendering 擴充，不是核心 OdfKit 建立、載入、保存與驗證能力的必要條件。

## ODF Toolkit parity corpus

- OdfKit 允許用外部 ODF Validator 作為 optional baseline。
- 一般 CI 不要求 Java 或 ODF Validator JAR；設定 `ODFKIT_ODFVALIDATOR_JAR` 或 CLI `--baseline-jar` 後才執行外部比對。
- Classification mismatch 必須透過 `--baseline-exceptions` 指定的 JSON manifest 記錄為 documented exception，否則視為 parity failure。
- 外部 corpus manifest 可用 `validate-corpus` 執行，並以 fixture 的 `expected`、`kind` 與 `version` 欄位作為完成線。
- 外部 / 官方 fixture 必須提供 `sourceUri`，generated 或 OdfKit 自有樣本才可省略。
- `validate-corpus --metadata-only` 可在樣本尚未下載時檢查外部 manifest 與 baseline exception metadata。
- `eng/Test-OdfCorpus.ps1` 對外部 corpus 會先執行 metadata-only gate，再執行 fixture 驗證與可選 ODF Validator baseline。
- `validate-corpus` 會拒絕逃出 corpus root 的 fixture 路徑、重複 fixture id / path 與未知 round-trip 策略。
- `eng/Test-OdfCorpus.ps1` 與 GitHub Actions `ODF corpus` workflow 會固定驗證內建 corpus；設定 `ODFKIT_PARITY_CORPUS_ROOT` 時可同時驗證外部 corpus。
- `eng/Initialize-OdfExternalCorpus.ps1` 可建立外部 corpus manifest 與 baseline exception 範本。
- `docs/examples/external-corpus/` 提供外部 corpus manifest 與 baseline exception 範本。
- repo 內建 `tests/fixtures/corpus/manifest.json` 作為可提交 manifest 的最小範本，覆蓋
  ODT、ODS、ODP、ODG，以及 `.fodt`、`.fods`、`.fodp`、`.fodg` 四種 flat ODF 格式。

## Real-world sanitized corpus

目前 repo 內未宣稱內建大量真實世界文件 corpus。新增真實文件時，應先去識別化，並記錄來源、授權、預期驗證結果與是否允許 round-trip 後 byte-level 差異。

## Expected behavior

- 對支援的結構：建立、載入、保存、驗證應可重複執行。
- 對未知但合法的 package / XML：預設保留。
- 對不安全內容：validator 應回報 issue；sanitize API 可移除巨集與過期簽章等風險內容。
- 對無法完整語意化的高階內容：不得因保存而破壞未知資料。
