#pragma warning disable CS1591
#pragma warning restore CS1591

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OdfKit.Compliance;

/// <summary>
/// 提供用於封裝驗證的內建 ODF 合規性規範。
/// </summary>
public static class OdfComplianceProfiles
{
    private static readonly string[] PackagedExtensions = [
        ".odt", ".ott", ".odm", ".oth",
        ".ods", ".ots",
        ".odp", ".otp",
        ".odg", ".otg",
        ".odc", ".otc", ".odf", ".otf", ".odi", ".oti", ".odb"
    ];

    private static readonly string[] FlatExtensions = [
        ".fodt", ".fods", ".fodp", ".fodg",
        ".fodc", ".fdf", ".fodi"
    ];

    private static readonly string[] OpenDocumentMimeTypes = [
        "application/vnd.oasis.opendocument.text",
        "application/vnd.oasis.opendocument.text-template",
        "application/vnd.oasis.opendocument.text-master",
        "application/vnd.oasis.opendocument.text-web",
        "application/vnd.oasis.opendocument.spreadsheet",
        "application/vnd.oasis.opendocument.spreadsheet-template",
        "application/vnd.oasis.opendocument.presentation",
        "application/vnd.oasis.opendocument.presentation-template",
        "application/vnd.oasis.opendocument.graphics",
        "application/vnd.oasis.opendocument.graphics-template",
        "application/vnd.oasis.opendocument.chart",
        "application/vnd.oasis.opendocument.chart-template",
        "application/vnd.oasis.opendocument.formula",
        "application/vnd.oasis.opendocument.formula-template",
        "application/vnd.oasis.opendocument.image",
        "application/vnd.oasis.opendocument.image-template",
        "application/vnd.oasis.opendocument.database"
    ];

    private static readonly OdfPolicyRule[] StandardRules = [
        Rule("RequireOdfNamespaceValidity", "ODF namespace elements and attributes must be valid for the selected ODF version.", OdfIssueSeverity.Error),
        Rule("RequireManifestIntegrity", "The package manifest must describe the package root and all stored payload entries.", OdfIssueSeverity.Error),
        Rule("RequireSafePackagePaths", "Package entries must use safe relative ZIP paths with forward slash separators.", OdfIssueSeverity.Fatal),
        Rule("RequireDeclaredOdfVersion", "ODF XML root elements should declare the office:version attribute.", OdfIssueSeverity.Warning)
    ];

    /// <summary>
    /// 取得 OASIS OpenDocument v1.0 一致性規範。
    /// </summary>
    public static OdfComplianceProfile OasisOdf10 { get; } = new(
        "OASIS_ODF_1_0",
        "International",
        "OASIS",
        new Uri("https://docs.oasis-open.org/office/v1.0/os/OpenDocument-v1.0-os.pdf"),
        "2005-05-01",
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.Exact(OdfVersion.Odf10),
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [.. StandardRules, Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error)],
        null);

    /// <summary>
    /// 取得 OASIS OpenDocument v1.1 一致性規範。
    /// </summary>
    public static OdfComplianceProfile OasisOdf11 { get; } = new(
        "OASIS_ODF_1_1",
        "International",
        "OASIS",
        new Uri("https://docs.oasis-open.org/office/v1.1/OS/OpenDocument-schema-v1.1.rng"),
        "2007-02-01",
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.Exact(OdfVersion.Odf11),
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [.. StandardRules, Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error)],
        null);

    /// <summary>
    /// 取得 OASIS OpenDocument v1.2 一致性規範。
    /// </summary>
    public static OdfComplianceProfile OasisOdf12 { get; } = new(
        "OASIS_ODF_1_2",
        "International",
        "OASIS",
        new Uri("https://docs.oasis-open.org/office/OpenDocument/v1.2/os/OpenDocument-v1.2-os.html"),
        "2012-01-12",
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.Exact(OdfVersion.Odf12),
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [.. StandardRules, Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error)],
        null);

    /// <summary>
    /// 取得 OASIS OpenDocument v1.3 一致性規範。
    /// </summary>
    public static OdfComplianceProfile OasisOdf13 { get; } = new(
        "OASIS_ODF_1_3",
        "International",
        "OASIS",
        new Uri("https://docs.oasis-open.org/office/OpenDocument/v1.3/os/schemas/OpenDocument-v1.3-schema.rng"),
        "2021-04-27",
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.Exact(OdfVersion.Odf13),
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [.. StandardRules, Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error)],
        null);

    /// <summary>
    /// 取得 OASIS OpenDocument v1.4 嚴格 (Strict) 一致性規範。
    /// </summary>
    public static OdfComplianceProfile OasisOdf14Strict { get; } = new(
        "OASIS_ODF_1_4_Strict",
        "International",
        "OASIS",
        new Uri("https://docs.oasis-open.org/office/OpenDocument/v1.4/os/"),
        "2025-10-06",
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.Exact(OdfVersion.Odf14),
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [.. StandardRules, Rule("DisallowInvalidOdfNamespaceExtensions", "Strict conformance does not allow non-schema ODF namespace extensions.", OdfIssueSeverity.Error), Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error)],
        null);

    /// <summary>
    /// 取得 OASIS OpenDocument v1.4 擴充 (Extended) 一致性規範。
    /// </summary>
    public static OdfComplianceProfile OasisOdf14Extended { get; } = new(
        "OASIS_ODF_1_4_Extended",
        "International",
        "OASIS",
        new Uri("https://docs.oasis-open.org/office/OpenDocument/v1.4/os/"),
        "2025-10-06",
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.Exact(OdfVersion.Odf14),
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [.. StandardRules, Rule("RequireForeignExtensionIsolation", "Foreign extensions must use non-ODF namespaces and remain removable.", OdfIssueSeverity.Warning), Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error)],
        null);

    /// <summary>
    /// 取得 ISO/IEC 26300:2006 一致性規範（對應於 ODF 1.0）。
    /// </summary>
    public static OdfComplianceProfile IsoIec26300_2006 { get; } = new(
        "ISO_IEC_26300_2006",
        "International",
        "ISO/IEC JTC 1",
        new Uri("https://www.iso.org/standard/43485.html"),
        "2006-11-30",
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.Exact(OdfVersion.Odf10),
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [.. StandardRules, Rule("RequireIso26300Odf10Compatibility", "Documents must remain compatible with the published ISO/IEC 26300:2006 baseline.", OdfIssueSeverity.Error)],
        null);

    /// <summary>
    /// 取得 ISO/IEC 26300:2015 一致性規範（對應於 ODF 1.2 基準）。
    /// </summary>
    public static OdfComplianceProfile IsoIec26300_2015 { get; } = new(
        "ISO_IEC_26300_2015",
        "International",
        "ISO/IEC JTC 1",
        new Uri("https://www.iso.org/standard/66363.html"),
        "2015",
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.Exact(OdfVersion.Odf12),
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [.. StandardRules, Rule("RequireIso26300Odf12Compatibility", "Documents must remain compatible with the published ISO/IEC 26300:2015 baseline.", OdfIssueSeverity.Error)],
        null);

    /// <summary>
    /// 取得 ISO/IEC 26300:2025 一致性規範（對應於 ODF 1.3 基準）。
    /// </summary>
    public static OdfComplianceProfile IsoIec26300_2025 { get; } = new(
        "ISO_IEC_26300_2025",
        "International",
        "ISO/IEC JTC 1",
        new Uri("https://www.iso.org/standard/81404.html"),
        "2025",
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.Exact(OdfVersion.Odf13),
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [.. StandardRules, Rule("RequireIso26300Odf13Compatibility", "Documents must remain compatible with the published ISO/IEC 26300:2025 baseline.", OdfIssueSeverity.Error)],
        null);

    /// <summary>
    /// 取得歐盟公共部門互通性規範。
    /// </summary>
    public static OdfComplianceProfile EuInteroperableEurope { get; } = new(
        "EU_InteroperableEurope",
        "European Union",
        "European Union",
        new Uri("https://eur-lex.europa.eu/eli/reg/2024/903/oj"),
        "2024-03-13",
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.AllKnown,
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [
            .. StandardRules,
            Rule("RequireOpenStandardDocumentFormat", "Editable public-sector documents should use formats implementable from public standards.", OdfIssueSeverity.Warning),
            Rule("RequireCrossBorderInteroperability", "Core document content must not require one vendor-private extension to be read.", OdfIssueSeverity.Warning),
            Rule("RequireForeignExtensionIsolation", "Foreign extensions must use non-ODF namespaces and remain removable.", OdfIssueSeverity.Warning),
            Rule("RequireMachineReadableMetadata", "Title, language, dates, authorship, and document type metadata should be machine-readable.", OdfIssueSeverity.Warning),
            Rule("RequireAccessibilityMetadata", "Language, alternative text, table headers, and reading order should be inspectable.", OdfIssueSeverity.Warning)
        ],
        new CultureInfo("en"));

    /// <summary>
    /// 取得歐盟可編輯辦公室文件交換相容性規範。
    /// </summary>
    public static OdfComplianceProfile EuOfficeDocumentExchange { get; } = new(
        "EU_OfficeDocumentExchange",
        "European Union",
        "European Union and member-state policies",
        new Uri("https://eur-lex.europa.eu/eli/reg/2024/903/oj"),
        "2024-03-13",
        OdfPolicyAuthorityLevel.Compatibility,
        OdfProfileVerificationStatus.OfficialButIndirect,
        OdfVersionRange.AllKnown,
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [
            .. StandardRules,
            Rule("RequireOpenStandardDocumentFormat", "Editable office documents should remain based on publicly implementable standards.", OdfIssueSeverity.Warning),
            Rule("AllowPolicyScopedOdfPreference", "ODF preference must be backed by a specific institution, member-state, procurement, or interoperability source.", OdfIssueSeverity.Info),
            Rule("AllowPdfPdfAForFinalDocuments", "Final non-editable publications may use PDF or PDF/A alongside editable sources.", OdfIssueSeverity.Info),
            Rule("RequireCrossBorderInteroperability", "Core document content must be readable without a single vendor-private extension.", OdfIssueSeverity.Warning)
        ],
        new CultureInfo("en"));

    /// <summary>
    /// 取得中華民國 (臺灣) ODF CNS15251 政策規範。
    /// </summary>
    public static OdfComplianceProfile RocTaiwanOdfCns15251 { get; } = new(
        "ROC_Taiwan_ODF_CNS15251",
        "Republic of China (Taiwan)",
        "Government ODF-CNS15251 policy",
        new Uri("https://www.cnsonline.com.tw/?node=detail&generalno=15251&classno=X5018"),
        "2015-06-25",
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.AllKnown,
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [
            .. StandardRules,
            Rule("RequireCns15251Mapping", "Profile metadata must identify the ODF to CNS15251 mapping once an active official source is confirmed.", OdfIssueSeverity.Warning),
            Rule("RequireTraditionalChineseMetadataSupport", "Traditional Chinese metadata, language tags, CJK text, and font names must round-trip without damage.", OdfIssueSeverity.Warning),
            Rule("PreferEditableOdfForGovernmentDocuments", "Editable government documents should target ODF for creation, preservation, and exchange.", OdfIssueSeverity.Info),
            Rule("AllowPdfForFinalPublication", "Final non-editable publications may use PDF or PDF/A alongside editable ODF sources.", OdfIssueSeverity.Info),
            Rule("DisallowMacroByDefault", "Government exchange profiles should reject macro and script content by default.", OdfIssueSeverity.Warning),
            Rule("PreserveCjkLayoutFeatures", "Vertical layout, ruby, East Asian grids, CJK spacing, and fallback font information must be preserved.", OdfIssueSeverity.Warning),
            Rule("RequireSafeExternalResourcePolicy", "External images, objects, remote links, and event handlers should be reported by default.", OdfIssueSeverity.Warning),
            Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error)
        ],
        new CultureInfo("zh-TW"));

    /// <summary>
    /// 取得中華民國 (臺灣) 政府 ODF 文件應用工具相容性規範。
    /// </summary>
    public static OdfComplianceProfile RocTaiwanGovernmentOdfTools { get; } = new(
        "ROC_Taiwan_GovernmentODFTools",
        "Republic of China (Taiwan)",
        "Ministry of Digital Affairs",
        new Uri("https://moda.gov.tw/digital-affairs/digital-service/app-services/248"),
        null,
        OdfPolicyAuthorityLevel.Compatibility,
        OdfProfileVerificationStatus.CompatibilityOnly,
        OdfVersionRange.AllKnown,
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [
            .. StandardRules,
            Rule("RequireGovernmentToolCompatibility", "Documents should remain compatible with government ODF application tools.", OdfIssueSeverity.Warning),
            Rule("RequireTraditionalChineseMetadataSupport", "Traditional Chinese metadata, language tags, CJK text, and font names must round-trip without damage.", OdfIssueSeverity.Warning),
            Rule("DisallowMacroByDefault", "Government exchange profiles should reject macro and script content by default.", OdfIssueSeverity.Warning),
            Rule("RequireSafeExternalResourcePolicy", "External images, objects, remote links, and event handlers should be reported by default.", OdfIssueSeverity.Warning),
            Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error)
        ],
        new CultureInfo("zh-TW"));

    /// <summary>
    /// 取得德國聯邦政府 ODF 文件相容性規範。
    /// </summary>
    public static OdfComplianceProfile DeGovernmentOdf { get; } = new(
        "DE_Government_ODF",
        "Germany",
        "Federal IT Council (IT-Planungsrat) / Deutschland-Stack",
        new Uri("https://www.it-planungsrat.de/"),
        "2026",
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.Exact(OdfVersion.Odf12),
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [
            .. StandardRules,
            Rule("RequireDeutschlandStackCompatibility", "Documents must comply with Germany's Deutschland-Stack office application standard.", OdfIssueSeverity.Warning),
            Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error),
            Rule("RequireForeignExtensionIsolation", "Foreign extensions must use non-ODF namespaces and remain removable.", OdfIssueSeverity.Warning),
            Rule("DisallowMacroByDefault", "Government exchange profiles should reject macro and script content by default.", OdfIssueSeverity.Warning)
        ],
        new CultureInfo("de"));

    /// <summary>
    /// 取得法國政府 ODF 文件相容性規範。
    /// </summary>
    public static OdfComplianceProfile FrGovernmentOdf { get; } = new(
        "FR_Government_ODF_RGI",
        "France",
        "DINUM (Direction interministérielle du numérique) / RGI",
        new Uri("https://www.numerique.gouv.fr/publications/rgi/"),
        null,
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.Exact(OdfVersion.Odf12),
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [
            .. StandardRules,
            Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error),
            Rule("RequireForeignExtensionIsolation", "Foreign extensions must use non-ODF namespaces and remain removable.", OdfIssueSeverity.Warning),
            Rule("DisallowMacroByDefault", "Government exchange profiles should reject macro and script content by default.", OdfIssueSeverity.Warning)
        ],
        new CultureInfo("fr"));

    /// <summary>
    /// 取得挪威政府 ODF 文件相容性規範。
    /// </summary>
    public static OdfComplianceProfile NoGovernmentOdf { get; } = new(
        "NO_Government_ODF",
        "Norway",
        "Digitalisation Agency (Digitaliseringsdirektoratet) / Standardiseringsforskriften",
        new Uri("https://www.digdir.no/"),
        null,
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.Exact(OdfVersion.Odf12),
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [
            .. StandardRules,
            Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error),
            Rule("RequireAccessibilityMetadata", "Language, alternative text, table headers, and reading order should be inspectable.", OdfIssueSeverity.Error),
            Rule("RequireSafeExternalResourcePolicy", "External images, objects, remote links, and event handlers should be reported by default.", OdfIssueSeverity.Warning),
            Rule("DisallowMacroByDefault", "Government exchange profiles should reject macro and script content by default.", OdfIssueSeverity.Warning)
        ],
        new CultureInfo("nb"));

    /// <summary>
    /// 取得巴西政府 ODF 文件相容性規範。
    /// </summary>
    public static OdfComplianceProfile BrGovernmentOdf { get; } = new(
        "BR_Government_ODF_ePING",
        "Brazil",
        "e-PING (Padrões de Interoperabilidade de Governo Eletrônico)",
        new Uri("http://estrutura.governoeletronico.gov.br/"),
        "2025",
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.AllKnown,
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [
            .. StandardRules,
            Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error),
            Rule("DisallowMacroByDefault", "Government exchange profiles should reject macro and script content by default.", OdfIssueSeverity.Warning)
        ],
        new CultureInfo("pt"));

    /// <summary>
    /// 取得美國 NARA 電子檔案轉移指引相容性規範。
    /// </summary>
    public static OdfComplianceProfile UsNaraOdf { get; } = new(
        "US_NARA_ODF",
        "United States",
        "National Archives and Records Administration (NARA)",
        new Uri("https://www.archives.gov/records-mgmt/policy/transfer-guidance.html"),
        null,
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.AllKnown,
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [
            .. StandardRules,
            Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error),
            Rule("RequireSafeExternalResourcePolicy", "External images, objects, remote links, and event handlers should be reported by default.", OdfIssueSeverity.Warning),
            Rule("DisallowMacroByDefault", "Government exchange profiles should reject macro and script content by default.", OdfIssueSeverity.Warning)
        ],
        new CultureInfo("en"));

    /// <summary>
    /// 取得北約 (NATO) ODF 文件相容性規範。
    /// </summary>
    public static OdfComplianceProfile NatoOdf { get; } = new(
        "NATO_ODF",
        "NATO",
        "NATO Standardization Office (NSO) / STANAG",
        new Uri("https://www.nato.int/"),
        null,
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.Exact(OdfVersion.Odf12),
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [
            .. StandardRules,
            Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error),
            Rule("DisallowMacroByDefault", "Government exchange profiles should reject macro and script content by default.", OdfIssueSeverity.Warning),
            Rule("RequireCrossBorderInteroperability", "Core document content must not require one vendor-private extension to be read.", OdfIssueSeverity.Warning)
        ],
        new CultureInfo("en"));

    /// <summary>
    /// 取得葡萄牙政府 ODF 文件相容性規範。
    /// </summary>
    public static OdfComplianceProfile PtGovernmentOdf { get; } = new(
        "PT_Government_ODF_RNID",
        "Portugal",
        "AMA (Agência para a Modernização Administrativa) / RNID",
        new Uri("https://www.ama.gov.pt/"),
        null,
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.Exact(OdfVersion.Odf12),
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [
            .. StandardRules,
            Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error),
            Rule("DisallowMacroByDefault", "Government exchange profiles should reject macro and script content by default.", OdfIssueSeverity.Warning),
            Rule("RequireForeignExtensionIsolation", "Foreign extensions must use non-ODF namespaces and remain removable.", OdfIssueSeverity.Warning)
        ],
        new CultureInfo("pt"));

    /// <summary>
    /// 取得比利時政府 ODF 文件相容性規範。
    /// </summary>
    public static OdfComplianceProfile BeGovernmentOdf { get; } = new(
        "BE_Government_ODF",
        "Belgium",
        "BOSA (Federal Public Service Policy and Support)",
        new Uri("https://bosa.belgium.be/"),
        null,
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.AllKnown,
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [
            .. StandardRules,
            Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error),
            Rule("DisallowMacroByDefault", "Government exchange profiles should reject macro and script content by default.", OdfIssueSeverity.Warning)
        ],
        new CultureInfo("nl"));

    /// <summary>
    /// 取得義大利政府 ODF 文件相容性規範。
    /// </summary>
    public static OdfComplianceProfile ItGovernmentOdf { get; } = new(
        "IT_Government_ODF_CAD",
        "Italy",
        "AgID (Agenzia per l'Italia Digitale) / CAD",
        new Uri("https://www.agid.gov.it/"),
        null,
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.Exact(OdfVersion.Odf12),
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [
            .. StandardRules,
            Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error),
            Rule("RequireAccessibilityMetadata", "Language, alternative text, table headers, and reading order should be inspectable.", OdfIssueSeverity.Warning),
            Rule("DisallowMacroByDefault", "Government exchange profiles should reject macro and script content by default.", OdfIssueSeverity.Warning)
        ],
        new CultureInfo("it"));

    /// <summary>
    /// 取得英國政府 ODF 1.2 相容性規範。
    /// </summary>
    public static OdfComplianceProfile UkGovernmentOdf12 { get; } = new(
        "UK_Government_ODF_1_2",
        "United Kingdom",
        "UK Government Cabinet Office",
        new Uri("https://www.gov.uk/government/publications/open-document-format-odf-guidance-for-government-departments"),
        null,
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.Exact(OdfVersion.Odf12),
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [
            .. StandardRules,
            Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error),
            Rule("RequireAccessibilityMetadata", "Language, alternative text, table headers, and reading order should be inspectable.", OdfIssueSeverity.Warning),
            Rule("DisallowMacroByDefault", "Government exchange profiles should reject macro and script content by default.", OdfIssueSeverity.Warning)
        ],
        new CultureInfo("en"));

    /// <summary>
    /// 取得荷蘭政府 ODF 相容性規範。
    /// </summary>
    public static OdfComplianceProfile NlGovernmentOdf { get; } = new(
        "NL_Government_ODF",
        "Netherlands",
        "Forum Standaardisatie",
        new Uri("https://www.forumstandaardisatie.nl/open-standaarden/odf"),
        null,
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.AllKnown,
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [
            .. StandardRules,
            Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error),
            Rule("RequireOpenStandardDocumentFormat", "Editable public-sector documents should use formats implementable from public standards.", OdfIssueSeverity.Warning),
            Rule("DisallowMacroByDefault", "Government exchange profiles should reject macro and script content by default.", OdfIssueSeverity.Warning)
        ],
        new CultureInfo("nl"));

    /// <summary>
    /// 取得斯洛伐克政府 ODF 相容性規範。
    /// </summary>
    public static OdfComplianceProfile SkGovernmentOdf { get; } = new(
        "SK_Government_ODF",
        "Slovakia",
        "Ministry of Investments, Regional Development and Informatization",
        new Uri("https://www.mirri.gov.sk/"),
        null,
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.AllKnown,
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [
            .. StandardRules,
            Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error),
            Rule("DisallowMacroByDefault", "Government exchange profiles should reject macro and script content by default.", OdfIssueSeverity.Warning)
        ],
        new CultureInfo("sk"));

    /// <summary>
    /// 取得丹麥政府 ODF 相容性規範。
    /// </summary>
    public static OdfComplianceProfile DkGovernmentOdf { get; } = new(
        "DK_Government_ODF",
        "Denmark",
        "Danish Agency for Digitisation (Digitaliseringsstyrelsen)",
        new Uri("https://digidir.dk/"),
        null,
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.Exact(OdfVersion.Odf12),
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [
            .. StandardRules,
            Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error),
            Rule("DisallowMacroByDefault", "Government exchange profiles should reject macro and script content by default.", OdfIssueSeverity.Warning)
        ],
        new CultureInfo("da"));

    /// <summary>
    /// 取得馬來西亞政府 ODF 相容性規範。
    /// </summary>
    public static OdfComplianceProfile MyGovernmentOdf { get; } = new(
        "MY_Government_ODF",
        "Malaysia",
        "Malaysian Administrative Modernisation and Management Planning Unit (MAMPU)",
        new Uri("https://www.mampu.gov.my/"),
        null,
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.Exact(OdfVersion.Odf12),
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [
            .. StandardRules,
            Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error),
            Rule("DisallowMacroByDefault", "Government exchange profiles should reject macro and script content by default.", OdfIssueSeverity.Warning)
        ],
        new CultureInfo("ms"));

    /// <summary>
    /// 取得南韓政府 ODF 相容性規範。
    /// </summary>
    public static OdfComplianceProfile KrGovernmentOdf { get; } = new(
        "KR_Government_ODF",
        "South Korea",
        "Ministry of the Interior and Safety (MOIS) / KS X ISO/IEC 26300",
        new Uri("https://www.mois.go.kr/"),
        null,
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.Exact(OdfVersion.Odf12),
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [
            .. StandardRules,
            Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error),
            Rule("DisallowMacroByDefault", "Government exchange profiles should reject macro and script content by default.", OdfIssueSeverity.Warning)
        ],
        new CultureInfo("ko"));

    /// <summary>
    /// 取得南非政府 ODF 相容性規範。
    /// </summary>
    public static OdfComplianceProfile ZaGovernmentOdf { get; } = new(
        "ZA_Government_ODF",
        "South Africa",
        "Department of Public Service and Administration (DPSA) / SAGA",
        new Uri("https://www.dpsa.gov.za/"),
        null,
        OdfPolicyAuthorityLevel.Normative,
        OdfProfileVerificationStatus.VerifiedOfficial,
        OdfVersionRange.AllKnown,
        [.. PackagedExtensions, .. FlatExtensions],
        OpenDocumentMimeTypes,
        [
            .. StandardRules,
            Rule("RequireSchemaPatternValidation", "ODF XML entries must validate against the normative schema patterns.", OdfIssueSeverity.Error),
            Rule("DisallowMacroByDefault", "Government exchange profiles should reject macro and script content by default.", OdfIssueSeverity.Warning)
        ],
        new CultureInfo("en"));

    /// <summary>
    /// 取得所有內建的合規性規範清單。
    /// </summary>
    public static IReadOnlyList<OdfComplianceProfile> BuiltIn { get; } = [
        OasisOdf10,
        OasisOdf11,
        OasisOdf12,
        OasisOdf13,
        OasisOdf14Strict,
        OasisOdf14Extended,
        IsoIec26300_2006,
        IsoIec26300_2015,
        IsoIec26300_2025,
        EuInteroperableEurope,
        EuOfficeDocumentExchange,
        RocTaiwanOdfCns15251,
        RocTaiwanGovernmentOdfTools,
        DeGovernmentOdf,
        FrGovernmentOdf,
        NoGovernmentOdf,
        BrGovernmentOdf,
        UsNaraOdf,
        NatoOdf,
        PtGovernmentOdf,
        BeGovernmentOdf,
        ItGovernmentOdf,
        UkGovernmentOdf12,
        NlGovernmentOdf,
        SkGovernmentOdf,
        DkGovernmentOdf,
        MyGovernmentOdf,
        KrGovernmentOdf,
        ZaGovernmentOdf
    ];

    /// <summary>
    /// 根據識別碼尋找特定的內建合規性規範。
    /// </summary>
    /// <param name="id">規範的唯一識別碼</param>
    /// <returns>若找到對應的規範則傳回該執行個體；否則傳回 null</returns>
    public static OdfComplianceProfile? Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return BuiltIn.FirstOrDefault(profile => string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private static OdfPolicyRule Rule(string id, string description, OdfIssueSeverity severity)
    {
        return new OdfPolicyRule(id, description, severity);
    }
}
