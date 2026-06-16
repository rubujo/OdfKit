using System;
using System.Collections.Generic;
using System.IO;
using OdfKit.Core;

namespace OdfKit.Compliance;

public static partial class OdfPackageValidator
{
    #region MIME Type & Document Kind

    private static void ValidateMimeType(
        string? mimeType,
        OdfDocumentKind mimeKind,
        OdfComplianceProfile? profile,
        string? profileId,
        List<OdfValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF0001",
                "ODF package is missing the mimetype entry.",
                "mimetype",
                profileId: profileId));
            return;
        }

        if (mimeKind == OdfDocumentKind.Unknown)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF0002",
                "ODF package declares an unknown or unsupported OpenDocument MIME type.",
                "mimetype",
                profileId: profileId));
        }

        if (profile is not null && !profile.AllowedMimeTypes.Contains(mimeType, StringComparer.Ordinal))
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF1000",
                $"MIME type '{mimeType}' is not allowed by profile '{profile.Id}'.",
                "mimetype",
                profileId: profile.Id));
        }
    }

    private static void ValidateMimeTypeEntry(
        OdfPackage package,
        string? profileId,
        List<OdfValidationIssue> issues)
    {
        if (!package.HasEntry("mimetype"))
        {
            return;
        }

        if (package.EntryOrder.Count > 0 &&
            !string.Equals(package.EntryOrder[0], "mimetype", StringComparison.Ordinal))
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF0003",
                "ODF package mimetype entry must be the first ZIP entry.",
                "mimetype",
                profileId: profileId));
        }

        if (package.Entries.TryGetValue("mimetype", out OdfPackageEntry? mimeEntry) &&
            mimeEntry.WasStoredInZip is false)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF0004",
                "ODF package mimetype entry must be stored without compression.",
                "mimetype",
                profileId: profileId));
        }
    }

    private static void ValidateBodyKind(
        OdfDocumentKind declaredKind,
        OdfDocumentKind bodyKind,
        string? profileId,
        List<OdfValidationIssue> issues)
    {
        if (!OdfDocumentKindDetector.IsCompatibleWithBodyKind(declaredKind, bodyKind))
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF0500",
                $"ODF MIME document kind '{declaredKind}' does not match content.xml office:body kind '{bodyKind}'.",
                "content.xml",
                "/document-content/body",
                profileId: profileId));
        }
    }

    private static void ValidateExtensionKind(
        OdfDocumentKind extensionKind,
        OdfDocumentKind mimeKind,
        string? fileName,
        string? profileId,
        List<OdfValidationIssue> issues)
    {
        if (extensionKind == OdfDocumentKind.Unknown ||
            mimeKind == OdfDocumentKind.Unknown ||
            OdfDocumentKindDetector.IsFlatKind(extensionKind) ||
            extensionKind == mimeKind)
        {
            return;
        }

        issues.Add(new OdfValidationIssue(
            OdfIssueSeverity.Warning,
            "ODF0501",
            "ODF file extension does not match the package mimetype.",
            fileName,
            profileId: profileId));
    }


    #endregion
}
