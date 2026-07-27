# 互通語料庫

本文件記錄目前 OdfKit 用來支撐相容性與來回讀寫宣稱的 corpus 類型。這不是外部檔案清單的完整快照，而是可維護的測試來源說明。fixture 中繼資料規則請見 [corpus-manifest.md](corpus-manifest.md)，官方 corpus 來源請見 [odf-official-corpus-sources.md](odf-official-corpus-sources.md)，ODF Toolkit / ODF Validator 對標線請見 [odf-toolkit-parity.md](odf-toolkit-parity.md)。

## 自動產生的 corpus

- `InteropCorpusTests`：針對主要封裝格式驗證公開驗證器 API、document kind、ODF 版本、儲存後重新開啟，以及未知 package entry 保留。
- `OdfValidatorApiTests`：覆蓋 package 與 Flat XML 驗證 API。
- `PackageRoundTripTests`：覆蓋 Flat XML / ZIP package 互轉、圖片與嵌入公式來回讀寫；
  `PackageRoundTripMatrixTests.MinimalSupportedFormatRoundTrips` 建立 24 種主要 ODF extension 的最小文件，驗證 MIME、ODF 版本、
  document kind、載入與儲存；`HighLevelSavePreservesUnknownXmlForeignContentAndProcessingInstructions`
  覆蓋 foreign namespace、未知屬性、comments、processing instructions 與 prefix 保留。

## ODF 1.4 正向 corpus

- `CorpusComplianceTests` 以 OASIS ODF 1.4 schema provider 與最小文件樣本驗證主要 body kind。
- 正向 corpus 目前重點是格式偵測、body kind、manifest 與 schema pattern 可執行性。
- `tests/fixtures/corpus/generated/realistic-review-memo.fodt` 提供小型儲存庫內近實務文件樣本，
  覆蓋標題、段落、清單、表格與註解的組合。

## 負向 corpus

- `CorpusComplianceTests` 與 `ComplianceTests` 覆蓋錯誤 root、錯誤 MIME / extension、Zip Slip、manifest 不一致與 profile rule 違規。
- 驗證器應回報結構化 issue，而不是在一般錯誤文件上崩潰。
- `tests/fixtures/corpus/generated/invalid-table-inside-paragraph.fodt` 模擬轉檔工具把區塊表格塞進
  `text:p` 的錯誤結構，作為更接近轉檔失誤的負向 fixture。

## 未知內容 corpus

- `OdfPackageUnknownEntryTests` 覆蓋未知 package entries、`Configurations2`、`ObjectReplacements` 與未知 media entry 儲存。
- `InteropCorpusTests` 確認主要封裝格式在 validator 與 package save 來回讀寫後仍保留未知 binary entry。
- `PackageRoundTripTests.HighLevelSavePreservesUnknownXmlForeignContentAndProcessingInstructions`
  覆蓋 foreign namespace、未知屬性、comments、processing instructions 與 prefix 保留（見上方
  「自動產生的 corpus」段落）。

## 加密互通 corpus

- `tests/fixtures/encryption-interop/` 收錄 LibreOffice 26.2 實機產生的密碼保護 ODT：
  `libreoffice-blowfish-cfb.odt`（ODF 1.0／1.1 傳統加密）、`libreoffice-aes256-cbc.odt`
  （ODF 1.2／1.3 傳統加密）與 `libreoffice-wholesome-gcm.odt`（ODF 1.4 整包加密，
  LibreOffice 24.8 起的預設）。同目錄 `manifest.json` 記錄密碼、SHA-256、產生方式與逐項加密參數。
- `EncryptionInteropCorpusTests` 驗證三件事：以宣告密碼解密後取得預期文字、manifest 宣告的加密
  參數與檔案實際內容一致、載入後重新儲存仍保留內容。
- 素材已提交，**不需要本機 LibreOffice 即可執行**；測試標記為 `Smoke` 與 `Interop`，並掛在主 CI
  的 `core-security` 煙霧分片。互通邊界與已知缺口見
  [odf-format-support.md](odf-format-support.md)。
- OdfKit 寫入 `OdfEncryptionAlgorithm.Aes256Gcm` 時會產生 LibreOffice wholesome 封裝。反向
  實機開啟由每週 `libreoffice-interop.yml` 的雙 TFM UNO 測試守備；目前來源產生的 manifest
  則由 `odf-external-baseline.yml` 以固定 SHA-256 的 LibreOffice extended schema 與 Jing 驗證。
  兩者仍不進入無外部應用程式依賴的主 CI 煙霧分片。
- OpenPGP 外部 baseline 以臨時 GnuPG RSA 金鑰驗證 OdfKit 產生的完整 encrypted message，
  並以 Jing 驗證根層 `manifest:encrypted-key`；反向路徑強制 GnuPG 產生 LibrePGP tag 20
  AES-OCB message，驗證 OdfKit 解密與竄改拒絕。每週 LibreOffice workflow 另以同樣的臨時
  真實金鑰執行 OdfKit 寫入、LibreOffice 解密及重新儲存、OdfKit 再開啟的雙向測試。

## 安全邊界 corpus

- `OdfSecurityBoundaryTests` 覆蓋簽章儲存 / 失效、macro sanitize、加密文件 sanitize 後重新儲存。
- `eng/Test-OdfPolicy.ps1` 與 GitHub Actions `ODF policy` 工作流程會執行 `Category=Policy`
  測試，固定檢查 macro / script artifact、外部資源 policy 與加密重新儲存邊界。
- XML reader 與 package loader 另有 XXE、DoS 與 Zip Slip 防禦測試。

## 渲染 / LibreOffice corpus

- `OdfKit.Extensions.Rendering` 與相關測試使用可替換的 LibreOffice finder。
- 這部分屬可選渲染擴充，不是核心 OdfKit 建立、載入、儲存與驗證能力的必要條件。
- LibreOffice 26.x 實機互通矩陣見 [libreoffice-interop-matrix.md](libreoffice-interop-matrix.md)；
  執行 `pwsh eng/Test-LibreOfficeInterop.ps1`（需本機 LibreOffice 26.x，否則略過）。
- OOXML 視覺 golden 矩陣見 [ooxml-visual-golden-matrix.md](ooxml-visual-golden-matrix.md)；
  執行 `pwsh eng/Test-OoxmlVisualGolden.ps1`（需 Windows + Office COM + LO + Python，否則略過）。
- 渲染 backend 部署見 [rendering-backend-deployment.md](rendering-backend-deployment.md)；
  執行 `pwsh eng/Test-RenderingBackends.ps1`（Mock 單元測試，不需真實 LO）。

## ODF Toolkit 對標 corpus

- OdfKit 允許用外部 ODF Validator 作為選用 baseline。
- 獨立的 `odf-external-baseline.yml` 會在 PR、main 與手動觸發時，以固定 SHA-256 的 Jing
  20241231 驗證通用 ODF schema 適用的 flat 與 package XML streams，再以 ODF Validator 0.13.0 對適用的
  ODF 1.0～1.4 package fixtures 執行外部比對與正／負 canary。本機與自備 corpus 仍可用
  `ODFKIT_ODFVALIDATOR_JAR` 或 CLI
  `--baseline-jar` 明確啟用。
- 分類不一致必須透過 `--baseline-exceptions` 指定的 JSON manifest 記錄為 documented exception，否則視為對標失敗。
- baseline exception manifest 不能有重複項目，也不能引用外部 corpus manifest 之外的 fixture。
- 外部 corpus manifest 可用 `validate-corpus` 執行，並以 fixture 的 `expected`、`kind` 與 `version` 欄位作為完成線。
- 外部 / 官方 fixture 必須提供 `sourceUri`，generated 或 OdfKit 自有樣本才可省略。
- `validate-corpus --metadata-only` 可在樣本尚未下載時檢查外部 manifest 與 baseline exception 中繼資料。
- `eng/Test-OdfCorpus.ps1` 對外部 corpus 會先執行 metadata-only gate，再執行 fixture 驗證與可選 ODF Validator baseline。
- `eng/Test-OdfCorpus.ps1 -InternalBaselineJar` 會以版本篩選後的暫存 manifest 對 repo corpus
  執行 baseline；CI 再加上 `-InternalBaselinePackageOnly`，避免用 ZIP package loader 開啟
  flat ODF，並以 `-InternalBaselineExcludedKinds` 表達該外部工具已確認的適用性限制。預設版本
  為 1.0～1.4。Database 仍由 Jing 驗證；Formula／FormulaTemplate／FlatFormula 因通用 schema
  未定義 `office:formula` 而由 Jing 報告明列排除，並保留在內部 package gate。
- `validate-corpus` 會拒絕逃出 corpus root 的 fixture 路徑、重複 fixture id / path 與未知 round-trip 策略。
- `eng/Test-OdfCorpus.ps1` 與 GitHub Actions `ODF corpus` 工作流程會固定驗證內建 corpus；設定 `ODFKIT_PARITY_CORPUS_ROOT` 時可同時驗證外部 corpus。
- `eng/Initialize-OdfExternalCorpus.ps1` 可建立外部 corpus manifest 與 baseline exception 範本。
- `docs/examples/external-corpus/` 提供外部 corpus manifest 與 baseline exception 範本。
- 儲存庫內建 `tests/fixtures/corpus/manifest.json` 作為可提交 manifest 的最小範本，覆蓋
  ODT、ODS、ODP、ODG，以及 `.fodt`、`.fods`、`.fodp`、`.fodg` 四種 flat ODF 格式。
- `repo-generated-odf14-decorative-image`、`repo-generated-odf14-table-in-shape` 與
  `repo-generated-odf14-zero-based-list` 是小型儲存庫內 baseline-difference fixture，
  用來固定 ODF 1.4 schema 與仍以 ODF 1.2 為基準之 ODF Toolkit / ODF Validator 的分類邊界。
- 大型 ODFDOM 官方 sample parity 仍走 `docs/examples/odfdom-sample-corpus/manifest.json`
  與 `ODFKIT_PARITY_CORPUS_ROOT`，不提交第三方 corpus 實體。

## 去識別化真實世界 corpus

目前儲存庫內未宣稱內建大量真實世界文件 corpus。新增真實文件時，應先去識別化，並記錄來源、授權、預期驗證結果與是否允許來回讀寫後 byte-level 差異。

## 預期行為

- 對支援的結構：建立、載入、儲存、驗證應可重複執行。
- 對未知但合法的 package / XML：預設保留。
- 對不安全內容：validator 應回報 issue；sanitize API 可移除巨集與過期簽章等風險內容。
- 對無法完整語意化的高階內容：不得因儲存而破壞未知資料。
