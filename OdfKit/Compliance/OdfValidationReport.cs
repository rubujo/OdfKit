#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdfKit.Compliance
{
    /// <summary>
    /// Represents the validation result for an ODF package or document.
    /// </summary>
    public sealed class OdfValidationReport
    {
        /// <summary>
        /// Initializes a new validation report.
        /// </summary>
        public OdfValidationReport(OdfVersion detectedVersion, OdfDocumentKind documentKind, IEnumerable<OdfValidationIssue> issues)
        {
            DetectedVersion = detectedVersion;
            DocumentKind = documentKind;
            Issues = new List<OdfValidationIssue>(issues ?? Array.Empty<OdfValidationIssue>()).AsReadOnly();
            IsValid = !Issues.Any(issue => issue.Severity == OdfIssueSeverity.Error || issue.Severity == OdfIssueSeverity.Fatal);
        }

        /// <summary>
        /// Gets whether the validated document satisfies the selected checks.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets the detected ODF version.
        /// </summary>
        public OdfVersion DetectedVersion { get; }

        /// <summary>
        /// Gets the detected ODF document kind.
        /// </summary>
        public OdfDocumentKind DocumentKind { get; }

        /// <summary>
        /// Gets all validation issues.
        /// </summary>
        public IReadOnlyList<OdfValidationIssue> Issues { get; }
    }

    /// <summary>
    /// Represents a single validation issue.
    /// </summary>
    public sealed class OdfValidationIssue
    {
        /// <summary>
        /// Initializes a new validation issue.
        /// </summary>
        public OdfValidationIssue(
            OdfIssueSeverity severity,
            string ruleId,
            string message,
            string? packagePath = null,
            string? xPath = null,
            OdfVersion? requiredVersion = null,
            string? profileId = null)
        {
            Severity = severity;
            RuleId = ruleId ?? string.Empty;
            Message = message ?? string.Empty;
            PackagePath = packagePath;
            XPath = xPath;
            RequiredVersion = requiredVersion;
            ProfileId = profileId;
        }

        /// <summary>
        /// Gets the issue severity.
        /// </summary>
        public OdfIssueSeverity Severity { get; }

        /// <summary>
        /// Gets the rule identifier.
        /// </summary>
        public string RuleId { get; }

        /// <summary>
        /// Gets the human-readable issue message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the package entry path related to this issue.
        /// </summary>
        public string? PackagePath { get; }

        /// <summary>
        /// Gets the XML path related to this issue when available.
        /// </summary>
        public string? XPath { get; }

        /// <summary>
        /// Gets the required ODF version when the issue is version-related.
        /// </summary>
        public OdfVersion? RequiredVersion { get; }

        /// <summary>
        /// Gets the compliance profile id that emitted this issue.
        /// </summary>
        public string? ProfileId { get; }
    }
}
