# ODF 1.4 覆蓋矩陣

本文件記錄 OdfKit 目前對 ODF 1.4 驗證與 API 覆蓋的事實狀態。狀態以
目前程式碼、測試與文件證據為準，不把後續路線圖目標預先標為完成。

## 狀態標記

- `complete`：已有公開 API、實作與直接測試證據。
- `validated`：已有 validator 或 corpus 測試證據，但 API 或文件仍可擴充。
- `partial`：已有部分支援，但仍有明確缺口。
- `planned`：尚未有足夠程式與測試證據支撐。

## 矩陣

| Spec area | Schema pattern / rule | API surface | Tests | Status | Gaps |
|---|---|---|---|---|---|
| 文件格式偵測 | MIME、extension、`office:body` 對應 | `OdfDocumentKindDetector`、`OdfFormatInfo` | `OdfFormatRoundTripTests`、`ComplianceTests` | complete | 仍需在發行文件中說明 template / flat 變體限制。 |
| ODF 1.4 預設版本 | `office:version="1.4"`、manifest version | `OdfVersionInfo`、`OdfDocumentFactory`、`OdfSaveOptions.ForceVersion` | `OdfSaveOptionsVersionTests`、`OdfFormatRoundTripTests` | complete | 舊版文件的完整 schema 差異仍屬 best-effort。 |
| 封裝 manifest 完整性 | `RequireManifestIntegrity`、mimetype entry、root media type | `OdfPackage`、`OdfPackageValidator`、`OdfValidator`、`OdfKit.Cli` | `ComplianceTests`、`CorpusComplianceTests`、`OdfValidatorApiTests`、`CliTests` | validated | CLI 已提供摘要；manifest 差異報告仍可擴充。 |
| ZIP entry 安全 | `RequireSafePackagePaths`、Zip Slip 防禦 | `OdfPackage.Open`、`OdfPackageValidator` | `CorpusComplianceTests` | complete | 無。 |
| XML 安全讀取 | 禁用 DTD 與外部解析 | `OdfXmlReader`、validator XML readers | `SecurityComplianceTests`、`ComplianceTests` | validated | 仍可補更多大型 XML DoS corpus。 |
| 官方 ODF 1.4 schema metadata | generated ODF 1.4 schema provider | `Odf14OfficialSchemaProvider.g.cs`、`OdfSchemaRegistry` | `ComplianceTests`、`OdfSchemaGeneratorTests` | validated | 需在 V2/V3 擴大 positive / negative corpus。 |
| typed DOM wrapper | ODF 1.4 schema element / attribute metadata | `OdfNodeFactory`、generated DOM wrappers、`OdfTypedDomCoverage`、typed attribute helpers、`eng/Test-OdfTypedDomCoverage.ps1` | `TypedDomParityTests`、`OdfToolkitParityReadinessTests`、`OdfSchemaGeneratorTests`、`docs/typed-dom-coverage.md` | partial | 已有 wrapper / factory / attribute coverage guard、schema-to-wrapper report、CI artifact、常用 datatype helper、generator typed datatype 輸出與重產生後的 integer / decimal / dateTime / style family property；尚需擴大 datatype 類別與 ODFDOM sample parity。 |
| RELAX NG pattern engine | `RequireSchemaPatternValidation` | `OdfSchemaPatternValidator`、`OdfProfileRuleValidator` | `ComplianceTests`、`CorpusComplianceTests` | partial | engine 已可執行，但仍需更多官方 corpus 覆蓋複雜 content model。 |
| package 文件驗證 | package root、core XML、manifest、profile rules | `OdfPackageValidator`、`OdfValidator.Validate(path/package)`、`OdfKit.Cli` | `ComplianceTests`、`CorpusComplianceTests`、`OdfValidatorApiTests`、`CliTests` | complete | CLI 已提供文字與 JSON 驗證輸出。 |
| 外部 ODF Validator baseline | ODF Toolkit / ODF Validator valid-invalid classification | `OdfExternalValidator`、CLI `validate --baseline`、`validate-corpus`、`--baseline-exceptions`、`eng/Test-OdfCorpus.ps1` | `CliTests`、`docs/odf-toolkit-parity.md`、`.github/workflows/odf-corpus.yml` | validated | 已可執行 manifest、比對 kind / version 並記錄 documented exceptions；尚需加入官方與真實 corpus 對照結果。 |
| flat XML 文件驗證 | `office:document`、flat extension、body kind | `OdfFlatDocumentValidator`、`OdfValidator.Validate(stream)` | `ComplianceTests`、`E2ETests`、`OdfValidatorApiTests`、`tests/fixtures/corpus/manifest.json` | complete | flat embedded object corpus 仍可增加。 |
| strict profile | `OASIS_ODF_1_4_Strict`、ODF namespace extension 禁止 | `OdfComplianceProfiles.OasisOdf14Strict` | `CorpusComplianceTests`、`ComplianceTests` | validated | 需補更多官方 strict negative case。 |
| extended profile | `OASIS_ODF_1_4_Extended`、foreign extension isolation | `OdfComplianceProfiles.OasisOdf14Extended` | `CorpusComplianceTests`、`OdfUnknownXmlRoundTripTests` | validated | foreign content 的可移除性目前以規則與 round-trip 測試支撐，尚未有完整文件化策略。 |
| 版本範圍 profile | ISO / EU / ROC profile version rules | `OdfComplianceProfiles` | `CorpusComplianceTests`、`ComplianceTests` | validated | profile 來源文件與驗證狀態仍需在發行文件中更明確標示。 |
| macro / script policy | `DisallowMacroByDefault`、script namespace、macro URI | `OdfProfileRuleValidator`、`OdfDocument.SanitizeMacros`、CLI `sanitize` | `CorpusComplianceTests`、`OdfSecurityBoundaryTests`、`CliTests` | complete | 加密 sanitize 的 CLI 與 UX 文件仍待補。 |
| 外部資源政策 | `RequireSafeExternalResourcePolicy` | `OdfProfileRuleValidator` | `CorpusComplianceTests`、`ComplianceTests` | validated | 還可補更多 remote image/object corpus。 |
| unknown package entry 保真 | manifest 與未辨識 entry round-trip | `OdfPackage` | `OdfPackageUnknownEntryTests` | complete | 無。 |
| unknown XML / foreign namespace 保真 | Namespace URI + LocalName、prefix / PI / comment 保存 | `OdfNode`、`OdfXmlReader`、`OdfXmlWriter` | `OdfUnknownXmlRoundTripTests` | complete | 無。 |
| flat / ZIP 雙向互轉 | package entries、meta/settings/styles/font-face-decls | `OdfPackage` | `PackageRoundTripTests` | complete | 更大型文件 corpus 尚未加入。 |
| 簽章邊界 | 未編輯保存保留，內容修改移除過期簽章 | `OdfPackage`、`OdfSigner` | `DomTest`、`AdvancedSecurityTests`、`OdfSecurityBoundaryTests` | complete | 高階文件層尚未提供簽章狀態查詢摘要 API。 |
| 加密邊界 | manifest encryption metadata、解密、重新加密保存 | `OdfEncryption`、`OdfSaveOptions`、`OdfLoadOptions` | `EncryptionTests`、`OdfSecurityBoundaryTests` | validated | 加密 sanitize 的 CLI 與 UX 文件仍待補。 |
| 全格式最小 round-trip | 17 種 extension 最小 create / load / save / validate | `OdfDocumentFactory`、`OdfDocument`、format wrappers | `OdfFormatRoundTripTests`、`tests/fixtures/corpus/manifest.json`、`docs/odf-format-support.md` | complete | 高階語意 API 依格式成熟度不同，ODC / ODF / ODI / ODB 仍是最小 story。 |

## 現階段結論

OdfKit 已具備公開 validator 入口、ODF 1.4 預設版本策略、官方 schema metadata、
profile rule validation、package / flat XML 驗證、全格式最小 round-trip、CLI 摘要與 JSON 輸出
入口，以及 unknown content 保真測試。後續重點應放在：

- 擴大更接近真實世界文件的 positive / negative corpus。
- 依 [odf-official-corpus-sources.md](odf-official-corpus-sources.md) 擴大 ODF Toolkit / ODF Validator parity matrix 的官方與真實 corpus 覆蓋。
- 補齊加密 sanitize CLI UX 與更多 policy automation。
- 繼續擴充 ODC、ODF formula、ODI 與 ODB 的高階語意 API。
