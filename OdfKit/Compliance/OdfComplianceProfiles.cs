#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdfKit.Compliance
{
    /// <summary>
    /// Provides built-in ODF compliance profiles used by package validation.
    /// </summary>
    public static class OdfComplianceProfiles
    {
        private static readonly string[] PackagedExtensions =
        {
            ".odt", ".ott", ".odm",
            ".ods", ".ots",
            ".odp", ".otp",
            ".odg", ".otg",
            ".odc", ".odf", ".odi", ".odb"
        };

        private static readonly string[] FlatExtensions =
        {
            ".fodt", ".fods", ".fodp", ".fodg"
        };

        private static readonly string[] OpenDocumentMimeTypes =
        {
            "application/vnd.oasis.opendocument.text",
            "application/vnd.oasis.opendocument.text-template",
            "application/vnd.oasis.opendocument.text-master",
            "application/vnd.oasis.opendocument.spreadsheet",
            "application/vnd.oasis.opendocument.spreadsheet-template",
            "application/vnd.oasis.opendocument.presentation",
            "application/vnd.oasis.opendocument.presentation-template",
            "application/vnd.oasis.opendocument.graphics",
            "application/vnd.oasis.opendocument.graphics-template",
            "application/vnd.oasis.opendocument.chart",
            "application/vnd.oasis.opendocument.formula",
            "application/vnd.oasis.opendocument.image",
            "application/vnd.oasis.opendocument.database"
        };

        private static readonly OdfPolicyRule[] StandardRules =
        {
            Rule("RequireOdfNamespaceValidity", "ODF namespace elements and attributes must be valid for the selected ODF version.", OdfIssueSeverity.Error),
            Rule("RequireManifestIntegrity", "The package manifest must describe the package root and all stored payload entries.", OdfIssueSeverity.Error),
            Rule("RequireSafePackagePaths", "Package entries must use safe relative ZIP paths with forward slash separators.", OdfIssueSeverity.Fatal),
            Rule("RequireDeclaredOdfVersion", "ODF XML root elements should declare the office:version attribute.", OdfIssueSeverity.Warning)
        };

        /// <summary>
        /// Gets the OASIS OpenDocument v1.4 strict conformance profile.
        /// </summary>
        public static OdfComplianceProfile OasisOdf14Strict { get; } = new OdfComplianceProfile(
            "OASIS_ODF_1_4_Strict",
            "International",
            "OASIS",
            new Uri("https://docs.oasis-open.org/office/OpenDocument/v1.4/os/"),
            "2025-10-06",
            OdfPolicyAuthorityLevel.Normative,
            OdfProfileVerificationStatus.VerifiedOfficial,
            OdfVersionRange.Exact(OdfVersion.Odf14),
            PackagedExtensions.Concat(FlatExtensions),
            OpenDocumentMimeTypes,
            StandardRules.Concat(new[]
            {
                Rule("DisallowInvalidOdfNamespaceExtensions", "Strict conformance does not allow non-schema ODF namespace extensions.", OdfIssueSeverity.Error)
            }));

        /// <summary>
        /// Gets the OASIS OpenDocument v1.4 extended conformance profile.
        /// </summary>
        public static OdfComplianceProfile OasisOdf14Extended { get; } = new OdfComplianceProfile(
            "OASIS_ODF_1_4_Extended",
            "International",
            "OASIS",
            new Uri("https://docs.oasis-open.org/office/OpenDocument/v1.4/os/"),
            "2025-10-06",
            OdfPolicyAuthorityLevel.Normative,
            OdfProfileVerificationStatus.VerifiedOfficial,
            OdfVersionRange.Exact(OdfVersion.Odf14),
            PackagedExtensions.Concat(FlatExtensions),
            OpenDocumentMimeTypes,
            StandardRules.Concat(new[]
            {
                Rule("RequireForeignExtensionIsolation", "Foreign extensions must use non-ODF namespaces and remain removable.", OdfIssueSeverity.Warning)
            }));

        /// <summary>
        /// Gets the ISO/IEC 26300 profile, initially anchored to ODF 1.2.
        /// </summary>
        public static OdfComplianceProfile IsoIec26300 { get; } = new OdfComplianceProfile(
            "ISO_IEC_26300",
            "International",
            "ISO/IEC JTC 1",
            new Uri("https://www.iso.org/standard/66363.html"),
            "2015",
            OdfPolicyAuthorityLevel.Normative,
            OdfProfileVerificationStatus.VerifiedOfficial,
            OdfVersionRange.Exact(OdfVersion.Odf12),
            PackagedExtensions.Concat(FlatExtensions),
            OpenDocumentMimeTypes,
            StandardRules.Concat(new[]
            {
                Rule("RequireIso26300Odf12Compatibility", "Documents must remain compatible with the published ISO/IEC 26300 ODF 1.2 baseline.", OdfIssueSeverity.Error)
            }));

        /// <summary>
        /// Gets the EU public sector interoperability profile.
        /// </summary>
        public static OdfComplianceProfile EuInteroperableEurope { get; } = new OdfComplianceProfile(
            "EU_InteroperableEurope",
            "European Union",
            "European Union",
            new Uri("https://eur-lex.europa.eu/eli/reg/2024/903/oj"),
            "2024-03-13",
            OdfPolicyAuthorityLevel.Normative,
            OdfProfileVerificationStatus.VerifiedOfficial,
            OdfVersionRange.AllKnown,
            PackagedExtensions.Concat(FlatExtensions),
            OpenDocumentMimeTypes,
            new[]
            {
                Rule("RequireOpenStandardDocumentFormat", "Editable public-sector documents should use formats implementable from public standards.", OdfIssueSeverity.Warning),
                Rule("RequireCrossBorderInteroperability", "Core document content must not require one vendor-private extension to be read.", OdfIssueSeverity.Warning),
                Rule("RequireForeignExtensionIsolation", "Foreign extensions must use non-ODF namespaces and remain removable.", OdfIssueSeverity.Warning),
                Rule("RequireMachineReadableMetadata", "Title, language, dates, authorship, and document type metadata should be machine-readable.", OdfIssueSeverity.Warning),
                Rule("RequireAccessibilityMetadata", "Language, alternative text, table headers, and reading order should be inspectable.", OdfIssueSeverity.Warning)
            });

        /// <summary>
        /// Gets the EU editable office document exchange compatibility profile.
        /// </summary>
        public static OdfComplianceProfile EuOfficeDocumentExchange { get; } = new OdfComplianceProfile(
            "EU_OfficeDocumentExchange",
            "European Union",
            "European Union and member-state policies",
            new Uri("https://eur-lex.europa.eu/eli/reg/2024/903/oj"),
            "2024-03-13",
            OdfPolicyAuthorityLevel.Compatibility,
            OdfProfileVerificationStatus.OfficialButIndirect,
            OdfVersionRange.AllKnown,
            PackagedExtensions.Concat(FlatExtensions),
            OpenDocumentMimeTypes,
            new[]
            {
                Rule("RequireOpenStandardDocumentFormat", "Editable office documents should remain based on publicly implementable standards.", OdfIssueSeverity.Warning),
                Rule("AllowPolicyScopedOdfPreference", "ODF preference must be backed by a specific institution, member-state, procurement, or interoperability source.", OdfIssueSeverity.Info),
                Rule("AllowPdfPdfAForFinalDocuments", "Final non-editable publications may use PDF or PDF/A alongside editable sources.", OdfIssueSeverity.Info),
                Rule("RequireCrossBorderInteroperability", "Core document content must be readable without a single vendor-private extension.", OdfIssueSeverity.Warning)
            });

        /// <summary>
        /// Gets the Republic of China CNS15251 draft tracking profile.
        /// </summary>
        public static OdfComplianceProfile RocTaiwanOdfCns15251 { get; } = new OdfComplianceProfile(
            "ROC_Taiwan_ODF_CNS15251",
            "Republic of China (Taiwan)",
            "Government ODF-CNS15251 policy",
            null,
            null,
            OdfPolicyAuthorityLevel.Draft,
            OdfProfileVerificationStatus.NeedsActiveSource,
            OdfVersionRange.AllKnown,
            PackagedExtensions.Concat(FlatExtensions),
            OpenDocumentMimeTypes,
            new[]
            {
                Rule("RequireCns15251Mapping", "Profile metadata must identify the ODF to CNS15251 mapping once an active official source is confirmed.", OdfIssueSeverity.Warning),
                Rule("RequireTraditionalChineseMetadataSupport", "Traditional Chinese metadata, language tags, CJK text, and font names must round-trip without damage.", OdfIssueSeverity.Warning),
                Rule("PreferEditableOdfForGovernmentDocuments", "Editable government documents should target ODF for creation, preservation, and exchange.", OdfIssueSeverity.Info),
                Rule("AllowPdfForFinalPublication", "Final non-editable publications may use PDF or PDF/A alongside editable ODF sources.", OdfIssueSeverity.Info),
                Rule("DisallowMacroByDefault", "Government exchange profiles should reject macro and script content by default.", OdfIssueSeverity.Warning),
                Rule("PreserveCjkLayoutFeatures", "Vertical layout, ruby, East Asian grids, CJK spacing, and fallback font information must be preserved.", OdfIssueSeverity.Warning),
                Rule("RequireSafeExternalResourcePolicy", "External images, objects, remote links, and event handlers should be reported by default.", OdfIssueSeverity.Warning)
            });

        /// <summary>
        /// Gets the Republic of China government ODF tools compatibility profile.
        /// </summary>
        public static OdfComplianceProfile RocTaiwanGovernmentOdfTools { get; } = new OdfComplianceProfile(
            "ROC_Taiwan_GovernmentODFTools",
            "Republic of China (Taiwan)",
            "Ministry of Digital Affairs",
            new Uri("https://moda.gov.tw/digital-affairs/digital-service/app-services/248"),
            null,
            OdfPolicyAuthorityLevel.Compatibility,
            OdfProfileVerificationStatus.CompatibilityOnly,
            OdfVersionRange.AllKnown,
            PackagedExtensions.Concat(FlatExtensions),
            OpenDocumentMimeTypes,
            new[]
            {
                Rule("RequireGovernmentToolCompatibility", "Documents should remain compatible with government ODF application tools.", OdfIssueSeverity.Warning),
                Rule("RequireTraditionalChineseMetadataSupport", "Traditional Chinese metadata, language tags, CJK text, and font names must round-trip without damage.", OdfIssueSeverity.Warning),
                Rule("DisallowMacroByDefault", "Government exchange profiles should reject macro and script content by default.", OdfIssueSeverity.Warning),
                Rule("RequireSafeExternalResourcePolicy", "External images, objects, remote links, and event handlers should be reported by default.", OdfIssueSeverity.Warning)
            });

        /// <summary>
        /// Gets all built-in compliance profiles.
        /// </summary>
        public static IReadOnlyList<OdfComplianceProfile> BuiltIn { get; } = new[]
        {
            OasisOdf14Strict,
            OasisOdf14Extended,
            IsoIec26300,
            EuInteroperableEurope,
            EuOfficeDocumentExchange,
            RocTaiwanOdfCns15251,
            RocTaiwanGovernmentOdfTools
        };

        /// <summary>
        /// Finds a built-in profile by its stable identifier.
        /// </summary>
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
}
