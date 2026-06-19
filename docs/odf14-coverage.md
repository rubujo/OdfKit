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
| 文件格式偵測 | MIME、extension、`office:body` 對應 | `OdfDocumentKindDetector`、`OdfFormatInfo` | `OdfFormatRoundTripTests`、`ComplianceTests` | complete | 無。 |
| ODF 1.4 預設版本 | `office:version="1.4"`、manifest version | `OdfVersionInfo`、`OdfDocumentFactory`、`OdfSaveOptions.ForceVersion` | `OdfSaveOptionsVersionTests`、`OdfFormatRoundTripTests` | complete | 無。 |
| 封裝 manifest 完整性 | `RequireManifestIntegrity`、mimetype entry、root media type | `OdfPackage`、`OdfPackageValidator`、`OdfValidator`、`OdfKit.Cli` | `ComplianceTests`、`CorpusComplianceTests`、`OdfValidatorApiTests`、`CliTests` | complete | 無。 |
| ZIP entry 安全 | `RequireSafePackagePaths`、Zip Slip 防禦 | `OdfPackage.Open`、`OdfPackageValidator` | `CorpusComplianceTests` | complete | 無。 |
| XML 安全讀取 | 禁用 DTD、外部解析與 XML 字元量上限 | `OdfXmlReader`、`OdfLoadOptions`、validator XML readers | `SecurityComplianceTests`、`ComplianceTests` | complete | 無。 |
| 官方 ODF 1.1/1.2/1.3 schema metadata | generated 版本專屬 schema provider（各自由獨立官方 RNG 衍生） | `Odf11OfficialSchemaProvider.g.cs`、`Odf12OfficialSchemaProvider.g.cs`、`Odf13OfficialSchemaProvider.g.cs`、`OdfSchemaRegistry.GetSchema`、`OdfComplianceProfiles.OasisOdf11`/`OasisOdf13`/`IsoIec26300` | `OdfSchemaGeneratorTests`、`CorpusComplianceTests`、`tests/fixtures/corpus/manifest.json` | complete | ODF 1.1/1.2/1.3 已改為以各自官方 RNG 衍生的真實 schema 驗證；ODF 1.0 因 OASIS 從未發布獨立 RNG，仍維持以 1.4 schema 進行 best-effort 近似驗證，此為已知限制。 |
| typed DOM wrapper | ODF 1.4 schema element / attribute metadata | `OdfNodeFactory`、generated DOM wrappers、`OdfTypedDomCoverage`、typed attribute helpers、schema-specific child collections、`eng/Test-OdfTypedDomCoverage.ps1` | `TypedDomParityTests`、`OdfToolkitParityReadinessTests`、`OdfSchemaGeneratorTests`、`docs/typed-dom-coverage.md` | complete | 無。 |
| RELAX NG pattern engine | `RequireSchemaPatternValidation` | `OdfSchemaPatternValidator`、`OdfProfileRuleValidator` | `ComplianceTests`、`CorpusComplianceTests`、`tests/fixtures/corpus/manifest.json` | complete | 無。 |
| package 文件驗證 | package root、core XML、manifest、profile rules | `OdfPackageValidator`、`OdfValidator.Validate(path/package)`、`OdfKit.Cli` | `ComplianceTests`、`CorpusComplianceTests`、`OdfValidatorApiTests`、`CliTests` | complete | 無。 |
| 外部 ODF Validator baseline | ODF Toolkit / ODF Validator valid-invalid classification | `OdfExternalValidator`、CLI `validate --baseline`、`validate-corpus`、`--baseline-exceptions`、`eng/Test-OdfCorpus.ps1` | `CliTests`、`docs/odf-toolkit-parity.md`、`.github/workflows/odf-corpus.yml` | complete | 無。 |
| flat XML 文件驗證 | `office:document`、flat extension、body kind | `OdfFlatDocumentValidator`、`OdfValidator.Validate(stream)` | `ComplianceTests`、`E2ETests`、`OdfValidatorApiTests`、`tests/fixtures/corpus/manifest.json` | complete | 無。 |
| strict profile | `OASIS_ODF_1_4_Strict`、ODF namespace extension 禁止 | `OdfComplianceProfiles.OasisOdf14Strict` | `CorpusComplianceTests`、`ComplianceTests` | complete | 無。 |
| extended profile | `OASIS_ODF_1_4_Extended`、foreign extension isolation | `OdfComplianceProfiles.OasisOdf14Extended` | `CorpusComplianceTests`、`OdfUnknownXmlRoundTripTests` | complete | 無。 |
| 版本範圍 profile | ISO / EU / ROC profile version rules | `OdfComplianceProfiles` | `CorpusComplianceTests`、`ComplianceTests`、`OdfToolkitParityReadinessTests` | complete | 無。 |
| macro / script policy | `DisallowMacroByDefault`、script namespace、macro URI | `OdfProfileRuleValidator`、`OdfDocument.SanitizeMacros`、CLI `sanitize --password --output-password` | `CorpusComplianceTests`、`OdfSecurityBoundaryTests`、`CliTests` | complete | 無。 |
| 外部資源政策 | `RequireSafeExternalResourcePolicy` | `OdfProfileRuleValidator` | `CorpusComplianceTests`、`ComplianceTests` | complete | 無。 |
| unknown package entry 保真 | manifest 與未辨識 entry round-trip | `OdfPackage` | `OdfPackageUnknownEntryTests` | complete | 無。 |
| unknown XML / foreign namespace 保真 | Namespace URI + LocalName、prefix / PI / comment 保存 | `OdfNode`、`OdfXmlReader`、`OdfXmlWriter` | `OdfUnknownXmlRoundTripTests` | complete | 無。 |
| flat / ZIP 雙向互轉 | package entries、meta/settings/styles/font-face-decls | `OdfPackage` | `PackageRoundTripTests` | complete | 無。 |
| 簽章邊界 | 未編輯保存保留，內容修改移除過期簽章 | `OdfPackage`、`OdfSigner`、`OdfDocument.GetSignatureSummary`、`OdfDocument.VerifySignaturesAsync` | `DomTest`、`AdvancedSecurityTests`、`OdfSecurityBoundaryTests` | complete | 無。 |
| 加密邊界 | manifest encryption metadata、解密、重新加密保存 | `OdfEncryption`、`OdfSaveOptions`、`OdfLoadOptions`、CLI `sanitize --password --output-password` | `EncryptionTests`、`OdfSecurityBoundaryTests`、`CliTests` | complete | 無。 |
| 全格式最小 round-trip | 24 種 extension 最小 create / load / save / validate | `OdfDocumentFactory`、`OdfDocument`、format wrappers | `OdfFormatRoundTripTests`、`tests/fixtures/corpus/manifest.json`、`docs/odf-format-support.md` | complete | 無。 |

## 現階段結論

OdfKit 已具備公開 validator 入口、ODF 1.4 預設版本策略、官方 schema metadata、
profile rule validation、package / flat XML 驗證、全格式最小 round-trip、CLI 摘要與 JSON 輸出
入口，以及 unknown content 保真測試。後續重點應放在：

- 擴大更接近真實世界文件的 positive / negative corpus。
- 依 [odf-official-corpus-sources.md](odf-official-corpus-sources.md) 擴大 ODF Toolkit / ODF Validator parity matrix 的官方與真實 corpus 覆蓋。
- 擴充更多跨應用加密文件 corpus 與 policy automation。
- 繼續擴充 ODC 的完整圖表模型、ODF formula 的 MathML 語意 helper、ODI 的多影像 / 樣式 / 進階版面 API 與 ODB 的完整資料庫模型。
