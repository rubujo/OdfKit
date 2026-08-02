# ODF Profile 來源

本文件記錄 OdfKit 內建 Profile 的來源、權威層級與驗證狀態。這份文件描述程式碼目前採用的 Profile 中繼資料，不代表外部政策來源的即時狀態。

## 狀態欄位

- `AuthorityLevel`：規則的權威層級。`Normative` 表示可直接追溯到規格或法規；`Compatibility` 表示用於互通或工具相容性；`Draft` 表示仍等待有效官方來源確認。
- `VerificationStatus`：來源確認程度。`VerifiedOfficial` 表示來源明確且直接；`OfficialButIndirect` 表示有官方來源但此 profile 是 OdfKit 的相容性映射；`CompatibilityOnly` 表示僅作工具相容性檢查；`NeedsActiveSource` 表示需要重新確認有效來源。
- `SupportedVersions`：Profile 接受的 ODF 版本範圍。

## 內建 Profile

| Profile | 來源 | 來源日期 | AuthorityLevel | VerificationStatus | SupportedVersions | 備註 |
|---|---|---:|---|---|---|---|
| `OASIS_ODF_1_0` | `https://docs.oasis-open.org/office/v1.0/OpenDocument-v1.0-os.pdf` | `2005-05-01` | `Normative` | `VerifiedOfficial` | `1.0` | 官方 OASIS ODF 1.0 標準。 |
| `OASIS_ODF_1_1` | `https://docs.oasis-open.org/office/v1.1/OS/OpenDocument-schema-v1.1.rng` | `2007-02-01` | `Normative` | `VerifiedOfficial` | `1.1` | 官方 OASIS ODF 1.1 標準。 |
| `OASIS_ODF_1_2` | `https://docs.oasis-open.org/office/v1.2/os/OpenDocument-v1.2-os-part1.html` | `2011-09-29` | `Normative` | `VerifiedOfficial` | `1.2` | 官方 OASIS ODF 1.2 標準。 |
| `OASIS_ODF_1_2_Strict` | `https://docs.oasis-open.org/office/v1.2/os/OpenDocument-v1.2-os-part1.html` | `2011-09-29` | `Normative` | `VerifiedOfficial` | `1.2` | 官方 OASIS ODF 1.2 嚴格一致性規範。 |
| `OASIS_ODF_1_2_Extended` | `https://docs.oasis-open.org/office/v1.2/os/OpenDocument-v1.2-os-part1.html` | `2011-09-29` | `Normative` | `VerifiedOfficial` | `1.2` | 官方 OASIS ODF 1.2 擴充一致性規範。 |
| `OASIS_ODF_1_3` | `https://docs.oasis-open.org/office/OpenDocument/v1.3/os/schemas/OpenDocument-v1.3-schema.rng` | `2021-04-27` | `Normative` | `VerifiedOfficial` | `1.3` | 官方 OASIS ODF 1.3 標準。 |
| `OASIS_ODF_1_3_Strict` | `https://docs.oasis-open.org/office/OpenDocument/v1.3/os/` | `2021-04-27` | `Normative` | `VerifiedOfficial` | `1.3` | 官方 OASIS ODF 1.3 嚴格一致性規範。 |
| `OASIS_ODF_1_3_Extended` | `https://docs.oasis-open.org/office/OpenDocument/v1.3/os/` | `2021-04-27` | `Normative` | `VerifiedOfficial` | `1.3` | 官方 OASIS ODF 1.3 擴充一致性規範。 |
| `OASIS_ODF_1_4_Strict` | `https://docs.oasis-open.org/office/OpenDocument/v1.4/os/` | `2025-10-06` | `Normative` | `VerifiedOfficial` | `1.4` | 官方 OASIS ODF 1.4 嚴格標準；OASIS Standard 文件日期為 2025-10-06，見 [odf14-gap-audit.md](odf14-gap-audit.md)。 |
| `OASIS_ODF_1_4_Extended` | `https://docs.oasis-open.org/office/OpenDocument/v1.4/os/` | `2025-10-06` | `Normative` | `VerifiedOfficial` | `1.4` | 官方 OASIS ODF 1.4 擴充標準；OASIS Standard 文件日期為 2025-10-06，見 [odf14-gap-audit.md](odf14-gap-audit.md)。 |
| `ISO_IEC_26300_2006` | `https://www.iso.org/standard/43485.html` | `2006-11-30` | `Normative` | `VerifiedOfficial` | `1.0` | ISO/IEC 26300:2006 標準（對應 ODF 1.0）。 |
| `ISO_IEC_26300_2015` | `https://www.iso.org/standard/66363.html` | `2015` | `Normative` | `VerifiedOfficial` | `1.2` | ISO/IEC 26300:2015 標準（對應 ODF 1.2 基準）。 |
| `ISO_IEC_26300_2025` | `https://www.iso.org/standard/81404.html` | `2025` | `Normative` | `VerifiedOfficial` | `1.3` | ISO/IEC 26300:2025 標準（對應 ODF 1.3 基準）。 |
| `EU_InteroperableEurope` | `https://eur-lex.europa.eu/eli/reg/2024/903/oj` | `2024-03-13` | `Compatibility` | `OfficialButIndirect` | `all-known` | 歐盟公共部門互通性規範映射；來源為一般互通性法規，不是 ODF 專屬 profile。 |
| `EU_OfficeDocumentExchange` | `https://eur-lex.europa.eu/eli/reg/2024/903/oj` | `2024-03-13` | `Compatibility` | `OfficialButIndirect` | `all-known` | 歐盟可編輯辦公室文件交換相容性規範。 |
| `ROC_Taiwan_ODF_CNS15251` | `https://www.cnsonline.com.tw/?node=detail&generalno=15251-1&classno=X5018` | `2019-09-05` | `Normative` | `VerifiedOfficial` | `1.2` | 中華民國（臺灣）CNS 15251 ODF 1.2 國家標準；主來源為第 1 部綱要，並包含 [第 2 部 OpenFormula](https://www.cnsonline.com.tw/?node=detail&generalno=15251-2&classno=X5018) 與[第 3 部套件](https://www.cnsonline.com.tw/?node=detail&generalno=15251-3&classno=X5018)。三部分皆於 2019-09-05 修訂、2025-03-10 確認；原 CNS 15251 已廢止。 |
| `ROC_Taiwan_GovernmentODFTools` | `https://moda.gov.tw/digital-affairs/digital-service/app-services/248` | `null` | `Compatibility` | `CompatibilityOnly` | `all-known` | 中華民國 (臺灣) 政府 ODF 文件應用工具相容性規範。 |
| `DE_Government_ODF` | `https://www.it-planungsrat.de/fileadmin/beschluesse/2026/Beschluss_2026_03_Deutschland-Stack_Standards.pdf` | `2026` | `Normative` | `VerifiedOfficial` | `all-known` | Deutschland-Stack 標準附件直接列出 ODF，但未限定 ODF 版本；本 Profile 因此不宣稱特定版本。 |
| `FR_Government_ODF_RGI` | `https://www.numerique.gouv.fr/offre-accompagnement/reference-interoperabilite-rgi/` | `null` | `Draft` | `NeedsActiveSource` | `1.2` | 法國 RGI 來源入口；仍需確認目前有效且直接列出 ODF 的官方條目。 |
| `NO_Government_ODF` | `https://lovdata.no/forskrift/2009-09-25-1222` | `2009-09-25` | `Normative` | `VerifiedOfficial` | `1.2` | 挪威政府 ODF 文件相容性規範。 |
| `BR_Government_ODF_ePING` | `https://www.gov.br/governodigital/pt-br/infraestrutura-nacional-de-dados/interoperabilidade/padroes-de-interoperabilidade` | `2025` | `Draft` | `NeedsActiveSource` | `all-known` | 巴西 e-PING 來源入口；仍需確認目前有效且直接列出 ODF 的官方條目。 |
| `US_NARA_ODF` | `https://www.archives.gov/records-mgmt/policy/transfer-guidance-tables.html` | `null` | `Compatibility` | `OfficialButIndirect` | `all-known` | 美國 NARA 電子檔案移轉格式接受規則映射，不是完整 ODF 規格 profile。 |
| `NATO_ODF` | `https://nhqc3s.hq.nato.int/apps/architecture/nisp/pdf/NISP-Vol3-v15-release.pdf` | `2024` | `Normative` | `VerifiedOfficial` | `1.2` | NATO Interoperability Standards and Profiles 第 3 卷直接列出 ISO/IEC 26300-1～26300-3:2015。 |
| `PT_Government_ODF_RNID` | `https://files.dre.pt/1s/2018/01/00400/0012100127.pdf` | `2018-01-05` | `Normative` | `VerifiedOfficial` | `1.2` | 葡萄牙 RNID 表 II 將 ODF 1.2 列為可編輯文件的強制規格。 |
| `BE_Government_ODF` | `https://bosa.belgium.be/` | `null` | `Draft` | `NeedsActiveSource` | `all-known` | 比利時 BOSA 來源入口；ODF 義務源自 2006-06-23 部長會議決議與 2007 年聯邦備忘錄，但 BOSA 站內查無直接列出 ODF 的現行條目（`dt.bosa.be` 的開放標準頁目前不可達），仍需確認有效官方條目。 |
| `IT_Government_ODF_CAD` | `https://www.agid.gov.it/` | `null` | `Draft` | `NeedsActiveSource` | `1.2` | AgID 舊版附件可證明 ODF 使用，但本次找不到直接且現行的 ODF 1.2 規範來源，故不得宣稱已驗證。 |
| `UK_Government_ODF_1_2` | `https://www.gov.uk/government/publications/open-standards-for-government/sharing-or-collaborating-with-government-documents` | `2026-01-29` | `Normative` | `VerifiedOfficial` | `1.2` | 英國政府 ODF 1.2 相容性規範；官方頁面於 2026-01-29 更新後仍明定使用 ODF 1.2。 |
| `NL_Government_ODF` | `https://www.forumstandaardisatie.nl/open-standaarden/odf` | `null` | `Normative` | `VerifiedOfficial` | `1.2` | 荷蘭政府 ODF 1.2 相容性規範。 |
| `SK_Government_ODF` | `https://mirri.gov.sk/sekcie/informatizacia/dokumenty/standardy-isvs/` | `null` | `Normative` | `VerifiedOfficial` | `1.2` | 斯洛伐克 ITVS 標準與官方文件將可編輯 ODF 文件上限明定為 1.2；不再錯誤宣稱所有版本。 |
| `DK_Government_ODF` | `https://digst.dk/it-loesninger/standarder/` | `null` | `Draft` | `NeedsActiveSource` | `1.2` | 本次未找到現行且直接限定 ODF 1.2 的官方規範條目，故降級等待有效來源。 |
| `MY_Government_ODF` | `https://www.digital.gov.my/` | `null` | `Draft` | `NeedsActiveSource` | `1.2` | 馬來西亞數位政府來源入口；仍需確認目前有效且直接列出 ODF 的官方條目。 |
| `KR_Government_ODF` | `https://www.mois.go.kr/` | `null` | `Draft` | `NeedsActiveSource` | `1.2` | 本次未找到 MOIS 直接且現行的 KS X ISO/IEC 26300 規範條目，故不得維持已驗證狀態。 |
| `ZA_Government_ODF` | `https://www.dpsa.gov.za/` | `null` | `Draft` | `NeedsActiveSource` | `all-known` | 可找到 2007 年採用 ODF 的歷史證據，但本次未找到現行官方規範與版本邊界，故降級等待有效來源。 |

## 維護規則

- 若 Profile 的 `SourceUrl`、`SourceDate`、`AuthorityLevel`、`VerificationStatus` 或 `SupportedVersions` 變更，必須同步更新本文件與相關測試。
- `NeedsActiveSource` 的 Profile 不得在文件中標示為 official、verified 或 normative。
- `CompatibilityOnly` 的 Profile 不得用於宣稱法規合規，只能描述為工具或流程相容性檢查。
- `all-known` 表示目前程式碼接受所有已建模的 ODF 版本，不代表外部來源明確批准所有版本。

## 2026-08-02 Profile 增刪稽核

- **不新增 Profile**：本次找到的最新官方更新（例如英國於 2026-01-29 更新 ODF 1.2 指引、德國
  2026 Deutschland-Stack 納入 ODF）都屬既有國家／組織 Profile 的來源更新，沒有形成新的
  ODF 技術一致性邊界。
- **不刪除公開成員**：既有 `ItGovernmentOdf`、`DkGovernmentOdf`、`KrGovernmentOdf` 與
  `ZaGovernmentOdf` 仍代表已知政策脈絡，但缺少可直接驗證的現行 ODF 規範。為保留呼叫端相容性，
  本次將其降為 `Draft`／`NeedsActiveSource`，而非讓程式繼續宣稱已驗證或直接移除公開 API。
- **來源更新而非新增**：CNS 15251 改指現行的 15251-1～15251-3；德國、NATO、葡萄牙及
  斯洛伐克改用直接官方來源，並收斂其實際版本邊界。
- **不以泛稱建立 Profile**：僅提到 ODF、只連到機關首頁、產品支援矩陣或非現行歷史採用證據，
  均不足以新增 `VerifiedOfficial` Profile。
