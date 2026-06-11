using System;
using System.Collections.Generic;

namespace OdfKit.Compliance
{
    /// <summary>
    /// Identifies an OpenDocument Format version supported by OdfKit.
    /// </summary>
    public enum OdfVersion
    {
        /// <summary>
        /// The version could not be detected.
        /// </summary>
        Unknown,

        /// <summary>
        /// OpenDocument Format 1.0.
        /// </summary>
        Odf10,

        /// <summary>
        /// OpenDocument Format 1.1.
        /// </summary>
        Odf11,

        /// <summary>
        /// OpenDocument Format 1.2 / ISO/IEC 26300:2015.
        /// </summary>
        Odf12,

        /// <summary>
        /// OpenDocument Format 1.3.
        /// </summary>
        Odf13,

        /// <summary>
        /// OpenDocument Format 1.4.
        /// </summary>
        Odf14
    }

    /// <summary>
    /// Identifies the ODF document kind represented by a package or flat XML document.
    /// </summary>
    public enum OdfDocumentKind
    {
        /// <summary>
        /// The document kind could not be detected.
        /// </summary>
        Unknown,

        /// <summary>
        /// An ODF text document.
        /// </summary>
        Text,

        /// <summary>
        /// An ODF text template.
        /// </summary>
        TextTemplate,

        /// <summary>
        /// An ODF master text document.
        /// </summary>
        TextMaster,

        /// <summary>
        /// An ODF spreadsheet document.
        /// </summary>
        Spreadsheet,

        /// <summary>
        /// An ODF spreadsheet template.
        /// </summary>
        SpreadsheetTemplate,

        /// <summary>
        /// An ODF presentation document.
        /// </summary>
        Presentation,

        /// <summary>
        /// An ODF presentation template.
        /// </summary>
        PresentationTemplate,

        /// <summary>
        /// An ODF graphics document.
        /// </summary>
        Graphics,

        /// <summary>
        /// An ODF graphics template.
        /// </summary>
        GraphicsTemplate,

        /// <summary>
        /// An ODF chart document.
        /// </summary>
        Chart,

        /// <summary>
        /// An ODF formula document.
        /// </summary>
        Formula,

        /// <summary>
        /// An ODF image document.
        /// </summary>
        Image,

        /// <summary>
        /// An ODF database document.
        /// </summary>
        Database,

        /// <summary>
        /// A flat XML ODF text document.
        /// </summary>
        FlatText,

        /// <summary>
        /// A flat XML ODF spreadsheet document.
        /// </summary>
        FlatSpreadsheet,

        /// <summary>
        /// A flat XML ODF presentation document.
        /// </summary>
        FlatPresentation,

        /// <summary>
        /// A flat XML ODF graphics document.
        /// </summary>
        FlatGraphics
    }

    /// <summary>
    /// Indicates how severe a validation issue is.
    /// </summary>
    public enum OdfIssueSeverity
    {
        /// <summary>
        /// Informational finding.
        /// </summary>
        Info,

        /// <summary>
        /// Non-fatal warning.
        /// </summary>
        Warning,

        /// <summary>
        /// Error that makes the document invalid for the selected profile.
        /// </summary>
        Error,

        /// <summary>
        /// Fatal issue that prevents reliable validation.
        /// </summary>
        Fatal
    }

    /// <summary>
    /// Describes the authority level of a compliance profile.
    /// </summary>
    public enum OdfPolicyAuthorityLevel
    {
        /// <summary>
        /// Draft profile whose normative source still needs confirmation.
        /// </summary>
        Draft,

        /// <summary>
        /// Compatibility profile derived from tool or deployment behavior.
        /// </summary>
        Compatibility,

        /// <summary>
        /// Recommended profile backed by policy guidance.
        /// </summary>
        Recommended,

        /// <summary>
        /// Normative profile backed by a standard or binding requirement.
        /// </summary>
        Normative
    }

    /// <summary>
    /// Tracks how strongly a profile source has been verified.
    /// </summary>
    public enum OdfProfileVerificationStatus
    {
        /// <summary>
        /// The profile is backed by a directly verified official source.
        /// </summary>
        VerifiedOfficial,

        /// <summary>
        /// The profile is backed by an official but indirect source.
        /// </summary>
        OfficialButIndirect,

        /// <summary>
        /// The profile needs an active official source before becoming normative.
        /// </summary>
        NeedsActiveSource,

        /// <summary>
        /// The profile is a compatibility profile, not a normative standard.
        /// </summary>
        CompatibilityOnly
    }

    /// <summary>
    /// Represents a supported inclusive ODF version range.
    /// </summary>
    public sealed class OdfVersionRange
    {
        /// <summary>
        /// Initializes a new version range.
        /// </summary>
        public OdfVersionRange(OdfVersion minimum, OdfVersion maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }

        /// <summary>
        /// Gets the minimum supported version.
        /// </summary>
        public OdfVersion Minimum { get; }

        /// <summary>
        /// Gets the maximum supported version.
        /// </summary>
        public OdfVersion Maximum { get; }

        /// <summary>
        /// Gets a range for all ODF versions known to this library.
        /// </summary>
        public static OdfVersionRange AllKnown { get; } = new OdfVersionRange(OdfVersion.Odf10, OdfVersion.Odf14);

        /// <summary>
        /// Creates a range containing exactly one version.
        /// </summary>
        public static OdfVersionRange Exact(OdfVersion version) => new OdfVersionRange(version, version);

        /// <summary>
        /// Returns true when the supplied version is inside this range.
        /// </summary>
        public bool Contains(OdfVersion version)
        {
            if (version == OdfVersion.Unknown)
            {
                return false;
            }

            return version >= Minimum && version <= Maximum;
        }
    }

    /// <summary>
    /// Describes a policy or validation rule declared by a compliance profile.
    /// </summary>
    public sealed class OdfPolicyRule
    {
        /// <summary>
        /// Initializes a new policy rule.
        /// </summary>
        public OdfPolicyRule(string id, string description, OdfIssueSeverity defaultSeverity = OdfIssueSeverity.Warning)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Rule id cannot be empty.", nameof(id));
            Id = id;
            Description = description ?? string.Empty;
            DefaultSeverity = defaultSeverity;
        }

        /// <summary>
        /// Gets the stable rule identifier.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the rule description.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the default severity when this rule reports an issue.
        /// </summary>
        public OdfIssueSeverity DefaultSeverity { get; }
    }

    /// <summary>
    /// Describes an ODF compliance profile and its policy metadata.
    /// </summary>
    public sealed class OdfComplianceProfile
    {
        /// <summary>
        /// Initializes a new compliance profile.
        /// </summary>
        public OdfComplianceProfile(
            string id,
            string jurisdiction,
            string authority,
            Uri? sourceUrl,
            string? sourceDate,
            OdfPolicyAuthorityLevel authorityLevel,
            OdfProfileVerificationStatus verificationStatus,
            OdfVersionRange supportedVersions,
            IEnumerable<string> allowedExtensions,
            IEnumerable<string> allowedMimeTypes,
            IEnumerable<OdfPolicyRule> rules)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Profile id cannot be empty.", nameof(id));
            Id = id;
            Jurisdiction = jurisdiction ?? string.Empty;
            Authority = authority ?? string.Empty;
            SourceUrl = sourceUrl;
            SourceDate = sourceDate;
            AuthorityLevel = authorityLevel;
            VerificationStatus = verificationStatus;
            SupportedVersions = supportedVersions ?? throw new ArgumentNullException(nameof(supportedVersions));
            AllowedExtensions = new List<string>(allowedExtensions ?? Array.Empty<string>()).AsReadOnly();
            AllowedMimeTypes = new List<string>(allowedMimeTypes ?? Array.Empty<string>()).AsReadOnly();
            Rules = new List<OdfPolicyRule>(rules ?? Array.Empty<OdfPolicyRule>()).AsReadOnly();
        }

        /// <summary>
        /// Gets the stable profile identifier.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the jurisdiction for this profile.
        /// </summary>
        public string Jurisdiction { get; }

        /// <summary>
        /// Gets the authority name for this profile.
        /// </summary>
        public string Authority { get; }

        /// <summary>
        /// Gets the official source URL when available.
        /// </summary>
        public Uri? SourceUrl { get; }

        /// <summary>
        /// Gets the source date as an ISO date string when known.
        /// </summary>
        public string? SourceDate { get; }

        /// <summary>
        /// Gets the profile authority level.
        /// </summary>
        public OdfPolicyAuthorityLevel AuthorityLevel { get; }

        /// <summary>
        /// Gets the source verification status.
        /// </summary>
        public OdfProfileVerificationStatus VerificationStatus { get; }

        /// <summary>
        /// Gets the ODF versions supported by this profile.
        /// </summary>
        public OdfVersionRange SupportedVersions { get; }

        /// <summary>
        /// Gets the allowed file extensions.
        /// </summary>
        public IReadOnlyList<string> AllowedExtensions { get; }

        /// <summary>
        /// Gets the allowed MIME types.
        /// </summary>
        public IReadOnlyList<string> AllowedMimeTypes { get; }

        /// <summary>
        /// Gets the profile rules.
        /// </summary>
        public IReadOnlyList<OdfPolicyRule> Rules { get; }
    }
}
