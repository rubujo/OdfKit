# ODF Profile Sources

本文件記錄 OdfKit 內建 profile 的來源、權威層級與驗證狀態。這份文件描述程式碼目前採用的 profile metadata，不代表外部政策來源的即時狀態。

## 狀態欄位

- `AuthorityLevel`：規則的權威層級。`Normative` 表示可直接追溯到規格或法規；`Compatibility` 表示用於互通或工具相容性；`Draft` 表示仍等待有效官方來源確認。
- `VerificationStatus`：來源確認程度。`VerifiedOfficial` 表示來源明確且直接；`OfficialButIndirect` 表示有官方來源但此 profile 是 OdfKit 的相容性映射；`CompatibilityOnly` 表示僅作工具相容性檢查；`NeedsActiveSource` 表示需要重新確認有效來源。
- `SupportedVersions`：profile 接受的 ODF 版本範圍。

## 內建 Profile

| Profile | Source | Source date | AuthorityLevel | VerificationStatus | SupportedVersions | Notes |
|---|---|---:|---|---|---|---|
| `OASIS_ODF_1_0` | `https://docs.oasis-open.org/office/v1.0/os/OpenDocument-v1.0-os.pdf` | `2005-05-01` | `Normative` | `VerifiedOfficial` | `1.0` | 官方 OASIS ODF 1.0 標準。 |
| `OASIS_ODF_1_1` | `https://docs.oasis-open.org/office/v1.1/OS/OpenDocument-schema-v1.1.rng` | `2007-02-01` | `Normative` | `VerifiedOfficial` | `1.1` | 官方 OASIS ODF 1.1 標準。 |
| `OASIS_ODF_1_2` | `https://docs.oasis-open.org/office/OpenDocument/v1.2/os/OpenDocument-v1.2-os.html` | `2012-01-12` | `Normative` | `VerifiedOfficial` | `1.2` | 官方 OASIS ODF 1.2 標準。 |
| `OASIS_ODF_1_3` | `https://docs.oasis-open.org/office/OpenDocument/v1.3/os/schemas/OpenDocument-v1.3-schema.rng` | `2021-04-27` | `Normative` | `VerifiedOfficial` | `1.3` | 官方 OASIS ODF 1.3 標準。 |
| `OASIS_ODF_1_4_Strict` | `https://docs.oasis-open.org/office/OpenDocument/v1.4/os/` | `2025-10-06` | `Normative` | `VerifiedOfficial` | `1.4` | 官方 OASIS ODF 1.4 嚴格標準。 |
| `OASIS_ODF_1_4_Extended` | `https://docs.oasis-open.org/office/OpenDocument/v1.4/os/` | `2025-10-06` | `Normative` | `VerifiedOfficial` | `1.4` | 官方 OASIS ODF 1.4 擴充標準。 |
| `ISO_IEC_26300_2006` | `https://www.iso.org/standard/43485.html` | `2006-11-30` | `Normative` | `VerifiedOfficial` | `1.0` | ISO/IEC 26300:2006 標準（對應 ODF 1.0）。 |
| `ISO_IEC_26300_2015` | `https://www.iso.org/standard/66363.html` | `2015` | `Normative` | `VerifiedOfficial` | `1.2` | ISO/IEC 26300:2015 標準（對應 ODF 1.2 基準）。 |
| `ISO_IEC_26300_2025` | `https://www.iso.org/standard/81404.html` | `2025` | `Normative` | `VerifiedOfficial` | `1.3` | ISO/IEC 26300:2025 標準（對應 ODF 1.3 基準）。 |
| `EU_InteroperableEurope` | `https://eur-lex.europa.eu/eli/reg/2024/903/oj` | `2024-03-13` | `Normative` | `VerifiedOfficial` | `all-known` | 歐盟公共部門互通性規範。 |
| `EU_OfficeDocumentExchange` | `https://eur-lex.europa.eu/eli/reg/2024/903/oj` | `2024-03-13` | `Compatibility` | `OfficialButIndirect` | `all-known` | 歐盟可編輯辦公室文件交換相容性規範。 |
| `ROC_Taiwan_ODF_CNS15251` | `https://www.cnsonline.com.tw/?node=detail&generalno=15251&classno=X5018` | `2015-06-25` | `Normative` | `VerifiedOfficial` | `all-known` | 中華民國 (臺灣) ODF CNS15251 政策規範。 |
| `ROC_Taiwan_GovernmentODFTools` | `https://moda.gov.tw/digital-affairs/digital-service/app-services/248` | `null` | `Compatibility` | `CompatibilityOnly` | `all-known` | 中華民國 (臺灣) 政府 ODF 文件應用工具相容性規範。 |
| `DE_Government_ODF` | `https://www.it-planungsrat.de/` | `2026` | `Normative` | `VerifiedOfficial` | `1.2` | 德國聯邦政府 ODF 文件相容性規範。 |
| `FR_Government_ODF_RGI` | `https://www.numerique.gouv.fr/publications/rgi/` | `null` | `Normative` | `VerifiedOfficial` | `1.2` | 法國政府 ODF 文件相容性規範 (RGI)。 |
| `NO_Government_ODF` | `https://www.digdir.no/` | `null` | `Normative` | `VerifiedOfficial` | `1.2` | 挪威政府 ODF 文件相容性規範。 |
| `BR_Government_ODF_ePING` | `http://estrutura.governoeletronico.gov.br/` | `2025` | `Normative` | `VerifiedOfficial` | `all-known` | 巴西政府 ODF 文件相容性規範 (e-PING)。 |
| `US_NARA_ODF` | `https://www.archives.gov/records-mgmt/policy/transfer-guidance.html` | `null` | `Normative` | `VerifiedOfficial` | `all-known` | 美國 NARA 電子檔案轉移指引相容性規範。 |
| `NATO_ODF` | `https://www.nato.int/` | `null` | `Normative` | `VerifiedOfficial` | `1.2` | 北約 (NATO) ODF 文件相容性規範。 |
| `PT_Government_ODF_RNID` | `https://www.ama.gov.pt/` | `null` | `Normative` | `VerifiedOfficial` | `1.2` | 葡萄牙政府 ODF 文件相容性規範 (RNID)。 |
| `BE_Government_ODF` | `https://bosa.belgium.be/` | `null` | `Normative` | `VerifiedOfficial` | `all-known` | 比利時政府 ODF 文件相容性規範。 |
| `IT_Government_ODF_CAD` | `https://www.agid.gov.it/` | `null` | `Normative` | `VerifiedOfficial` | `1.2` | 義大利政府 ODF 文件相容性規範 (CAD)。 |
| `UK_Government_ODF_1_2` | `https://www.gov.uk/government/publications/open-document-format-odf-guidance-for-government-departments` | `null` | `Normative` | `VerifiedOfficial` | `1.2` | 英國政府 ODF 1.2 相容性規範。 |
| `NL_Government_ODF` | `https://www.forumstandaardisatie.nl/open-standaarden/odf` | `null` | `Normative` | `VerifiedOfficial` | `all-known` | 荷蘭政府 ODF 相容性規範。 |
| `SK_Government_ODF` | `https://www.mirri.gov.sk/` | `null` | `Normative` | `VerifiedOfficial` | `all-known` | 斯洛伐克政府 ODF 相容性規範。 |
| `DK_Government_ODF` | `https://digidir.dk/` | `null` | `Normative` | `VerifiedOfficial` | `1.2` | 丹麥政府 ODF 相容性規範。 |
| `MY_Government_ODF` | `https://www.mampu.gov.my/` | `null` | `Normative` | `VerifiedOfficial` | `1.2` | 馬來西亞政府 ODF 相容性規範。 |
| `KR_Government_ODF` | `https://www.mois.go.kr/` | `null` | `Normative` | `VerifiedOfficial` | `1.2` | 南韓政府 ODF 相容性規範。 |
| `ZA_Government_ODF` | `https://www.dpsa.gov.za/` | `null` | `Normative` | `VerifiedOfficial` | `all-known` | 南非政府 ODF 相容性規範。 |

## 維護規則

- 若 profile 的 `SourceUri`、`SourceDate`、`AuthorityLevel`、`VerificationStatus` 或 `SupportedVersions` 變更，必須同步更新本文件與相關測試。
- `NeedsActiveSource` 的 profile 不得在文件中標示為 official、verified 或 normative。
- `CompatibilityOnly` 的 profile 不得用於宣稱法規合規，只能描述為工具或流程相容性檢查。
- `all-known` 表示目前程式碼接受所有已建模的 ODF 版本，不代表外部來源明確批准所有版本。
