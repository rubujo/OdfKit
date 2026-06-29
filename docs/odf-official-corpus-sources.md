# ODF Official Corpus Sources

本文件記錄 OdfKit 外部 parity corpus 的官方來源與採集規則。它不是授權同意書；
新增任何第三方 fixture 前，仍必須逐項確認授權與可再散布條件。

## 來源登錄

| Source | URL | Role | Redistribute in repo |
|---|---|---|---|
| ODF Toolkit website | `https://odftoolkit.org/` | ODF Toolkit 與 ODFDOM 官方入口。 | no |
| ODF Validator documentation | `https://odftoolkit.org/conformance/ODFValidator.html` | ODF Toolkit Validator CLI / WAR 使用說明。 | no |
| ODF Toolkit source | `https://github.com/tdf/odftoolkit` | ODF Toolkit、ODFDOM 與 Validator 原始碼來源。 | no |
| ODF Validator Maven metadata | `https://mvnrepository.com/artifact/org.odftoolkit/odfvalidator` | 檢查可取得版本與 artifact 名稱。 | no |
| OPF odf-validator | `https://github.com/openpreserve/odf-validator` | 獨立保存用途 validator；名稱相近但不是 ODF Toolkit baseline。 | no |
| OASIS ODF 1.1 RNG schema | `https://docs.oasis-open.org/office/v1.1/OS/OpenDocument-schema-v1.1.rng` | OASIS Standard（2007-02-01）官方獨立 RELAX NG schema，`OdfSchemaRegistry`／`OdfComplianceProfiles.OasisOdf11` 用以產生真實 ODF 1.1 schema。 | yes（已下載至 `tools/OdfSchemaGenerator/schemas/OpenDocument-schema-v1.1.rng`） |
| OASIS ODF 1.2 RNG schema | `https://docs.oasis-open.org/office/v1.2/os/OpenDocument-v1.2-os-schema.rng` | OASIS Standard（2011-09-29）官方獨立 RELAX NG schema，供 `ISO_IEC_26300` profile 使用真實 ODF 1.2 schema。 | yes（已下載至 `tools/OdfSchemaGenerator/schemas/OpenDocument-v1.2-os-schema.rng`） |
| OASIS ODF 1.3 RNG schema | `https://docs.oasis-open.org/office/OpenDocument/v1.3/os/schemas/OpenDocument-v1.3-schema.rng` | OASIS Standard（2021-04-27）官方獨立 RELAX NG schema，`OdfComplianceProfiles.OasisOdf13` 用以產生真實 ODF 1.3 schema。 | yes（已下載至 `tools/OdfSchemaGenerator/schemas/OpenDocument-v1.3-schema.rng`） |
| OASIS ODF 1.0 規格文件 | `https://docs.oasis-open.org/office/v1.0/OpenDocument-v1.0-os.pdf` | 已查證：OASIS 官方目錄（`docs.oasis-open.org/office/v1.0/`）僅提供規格 PDF，從未發布獨立 RNG schema 檔案。因此 ODF 1.0 沒有真實 schema 可用，`OdfSchemaRegistry` 維持以 ODF 1.4 schema 進行 best-effort 近似驗證，屬於已知限制。 | no（無 RNG 可下載） |
| ODFDOM 官方 sample（釘選版本） | `https://github.com/tdf/odftoolkit/blob/b926a6134a2fee782076500dfc02c47c2d651cff/odfdom/src/main/resources/` | 已審核：釘選 `tdf/odftoolkit` 釋出版本 `v0.13.0`（commit `b926a6134a2fee782076500dfc02c47c2d651cff`），`odfdom` 子模組與 repo 根目錄同採 Apache-2.0 授權（已逐一核對 `LICENSE` 與 `odfdom/LICENSE.txt` 內容一致）。涵蓋 `OdfTextDocument.odt`／`OdfSpreadsheetDocument.ods`／`OdfPresentationDocument.odp`／`OdfGraphicsDocument.odg` 四個官方 sample，皆為 ODF 1.2（`office:version="1.2"`）。詳見 [docs/examples/odfdom-sample-corpus/manifest.json](examples/odfdom-sample-corpus/manifest.json)。 | no（不提交實體檔案，僅引用 sourceUri／sha256；需放置於 `ODFKIT_PARITY_CORPUS_ROOT`） |

## 採集流程

1. 先用 `eng/Initialize-OdfExternalCorpus.ps1 -OutputRoot <path>` 建立外部 corpus root。
2. 將官方或授權允許的樣本放在外部 root 內，不直接提交到 repo。
3. 依 `docs/examples/external-corpus/manifest.json` 填入每個 fixture 的 `id`、`path`、
   `source`、`sourceUri`、`license`、`kind`、`version`、`profile`、`expected` 與 `roundTrip`。
   ODFDOM 官方 sample parity 已釘選至 `docs/examples/odfdom-sample-corpus/manifest.json`
   所記載的 `tdf/odftoolkit v0.13.0`（commit `b926a6134a2fee782076500dfc02c47c2d651cff`）；
   若日後要更新到更新的上游版本，須重新審核該版本 `odfdom/LICENSE.txt` 是否仍為
   Apache-2.0、重新下載四個官方 sample 並計算 `sha256`，再同步更新此處與
   `OdfKit.Tests/DocsAndCorpusContractTests.cs` 的釘選 commit／雜湊斷言。
4. 若 ODF Toolkit Validator 與 OdfKit classification 不一致，先確認是否為 OdfKit bug。
   只有確認為暫時接受的 baseline 差異時，才記錄到 `baseline-exceptions.json`。
5. 在樣本尚未下載或授權仍待審核時，可先用 `validate-corpus --metadata-only`
   檢查 manifest 與 baseline exception metadata。
6. 使用 `eng/Test-OdfCorpus.ps1` 執行內建與外部 corpus。

## Baseline 命名

本 repo 的 `--baseline odf-validator` 指的是 ODF Toolkit / TDF ODF Validator。
OPF 的 `odf-validator` 是另一套工具，不應混入同一份 baseline manifest；若未來要支援，
必須新增獨立 baseline 名稱與 documented exception 集合。

## 完成線

外部 corpus 進入 parity matrix 前，必須同時具備：

- 可追溯來源 URL。
- manifest `sourceUri` 必須指向官方來源或可審核的上游樣本頁面。
- 授權與可再散布判斷。
- `validate-corpus --metadata-only` 可先通過 metadata gate。
- `validate-corpus` 可執行 manifest。
- 若啟用 ODF Toolkit Validator，必須有 baseline 結果或 documented exception。
- 對每個 fixture 記錄 expected classification、kind、version 與 profile。
