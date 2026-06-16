# ODF Profile Sources

本文件記錄 OdfKit 內建 profile 的來源、權威層級與驗證狀態。這份文件描述
程式碼目前採用的 profile metadata，不代表外部政策來源的即時狀態。

## 狀態欄位

- `AuthorityLevel`：規則的權威層級。`Normative` 表示可直接追溯到規格或法規；
  `Compatibility` 表示用於互通或工具相容性；`Draft` 表示仍等待有效官方來源確認。
- `VerificationStatus`：來源確認程度。`VerifiedOfficial` 表示來源明確且直接；
  `OfficialButIndirect` 表示有官方來源但此 profile 是 OdfKit 的相容性映射；
  `CompatibilityOnly` 表示僅作工具相容性檢查；`NeedsActiveSource` 表示需要重新確認有效來源。
- `SupportedVersions`：profile 接受的 ODF 版本範圍。

## 內建 Profile

| Profile | Source | Source date | AuthorityLevel | VerificationStatus | SupportedVersions | Notes |
|---|---|---:|---|---|---|---|
| `OASIS_ODF_1_4_Strict` | `https://docs.oasis-open.org/office/OpenDocument/v1.4/os/` | `2025-10-06` | `Normative` | `VerifiedOfficial` | `1.4` | 嚴格一致性，不允許 ODF namespace 偽裝擴充。 |
| `OASIS_ODF_1_4_Extended` | `https://docs.oasis-open.org/office/OpenDocument/v1.4/os/` | `2025-10-06` | `Normative` | `VerifiedOfficial` | `1.4` | 允許 foreign namespace，但要求可隔離與可移除。 |
| `OASIS_ODF_1_1` | `https://docs.oasis-open.org/office/v1.1/OS/OpenDocument-schema-v1.1.rng` | `2007-02-01` | `Normative` | `VerifiedOfficial` | `1.1` | 以 OASIS 官方獨立 RNG 衍生的真實 ODF 1.1 schema 驗證，非從 1.4 過濾的近似值。 |
| `OASIS_ODF_1_3` | `https://docs.oasis-open.org/office/OpenDocument/v1.3/os/schemas/OpenDocument-v1.3-schema.rng` | `2021-04-27` | `Normative` | `VerifiedOfficial` | `1.3` | 以 OASIS 官方獨立 RNG 衍生的真實 ODF 1.3 schema 驗證，非從 1.4 過濾的近似值。 |
| `ISO_IEC_26300` | `https://www.iso.org/standard/66363.html` | `2015` | `Normative` | `VerifiedOfficial` | `1.2` | 對應 ODF 1.2 baseline；自 schema pattern 改採真實的官方 ODF 1.2 RNG（`https://docs.oasis-open.org/office/v1.2/os/OpenDocument-v1.2-os-schema.rng`）後，不再是過去從 1.4 schema 過濾出的近似值。 |
| `EU_InteroperableEurope` | `https://eur-lex.europa.eu/eli/reg/2024/903/oj` | `2024-03-13` | `Normative` | `VerifiedOfficial` | `all-known` | 以互通性與可檢查 metadata / accessibility 規則呈現。 |
| `EU_OfficeDocumentExchange` | `https://eur-lex.europa.eu/eli/reg/2024/903/oj` | `2024-03-13` | `Compatibility` | `OfficialButIndirect` | `all-known` | OdfKit 的辦公室文件交換相容性映射，不宣稱為獨立官方 ODF profile。 |
| `ROC_Taiwan_ODF_CNS15251` | `null` | `null` | `Draft` | `NeedsActiveSource` | `all-known` | 等待可追溯的有效 CNS15251 / ODF mapping 來源後才能升級。 |
| `ROC_Taiwan_GovernmentODFTools` | `https://moda.gov.tw/digital-affairs/digital-service/app-services/248` | `null` | `Compatibility` | `CompatibilityOnly` | `all-known` | 工具相容性 profile，不等同 CNS15251 規範本身。 |

## 維護規則

- 若 profile 的 `SourceUri`、`SourceDate`、`AuthorityLevel`、`VerificationStatus`
  或 `SupportedVersions` 變更，必須同步更新本文件與相關測試。
- `NeedsActiveSource` 的 profile 不得在文件中標示為 official、verified 或 normative。
- `CompatibilityOnly` 的 profile 不得用於宣稱法規合規，只能描述為工具或流程相容性檢查。
- `all-known` 表示目前程式碼接受所有已建模的 ODF 版本，不代表外部來源明確批准所有版本。
