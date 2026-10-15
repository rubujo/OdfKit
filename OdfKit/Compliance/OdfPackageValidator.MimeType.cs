using System;
using System.Collections.Generic;
using System.IO;
using OdfKit.Core;

namespace OdfKit.Compliance;
/// <summary>
/// Provides the OdfPackageValidator API.
/// 提供 OdfPackageValidator API。
/// </summary>

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
        OdfVersion version,
        string? profileId,
        List<OdfValidationIssue> issues)
    {
        if (package.IsFlatXml)
        {
            return;
        }

        if (!package.HasEntry("mimetype"))
        {
            return;
        }

        package.Entries.TryGetValue("mimetype", out OdfPackageEntry? mimeEntry);
        if (mimeEntry?.WasFirstLocalEntryInZip is false ||
            (mimeEntry?.WasFirstLocalEntryInZip is null && package.EntryOrder.Count > 0 &&
             !string.Equals(package.EntryOrder[0], "mimetype", StringComparison.Ordinal)))
        {
            bool isRecommendation = version is OdfVersion.Odf10 or OdfVersion.Odf11;
            issues.Add(new OdfValidationIssue(
                isRecommendation ? OdfIssueSeverity.Warning : OdfIssueSeverity.Error,
                "ODF0003",
                isRecommendation
                    ? "ODF package mimetype entry should be the first ZIP entry."
                    : "ODF package mimetype entry must be the first ZIP entry.",
                "mimetype",
                profileId: profileId));
        }

        if (mimeEntry?.WasStoredInZip is false)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF0004",
                "ODF package mimetype entry must be stored without compression.",
                "mimetype",
                profileId: profileId));
        }

        if (mimeEntry?.LocalHeaderExtraFieldLength > 0)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF0005",
                "ODF package mimetype entry must not have a ZIP local header extra field.",
                "mimetype",
                profileId: profileId));
        }

        if (mimeEntry is not null)
        {
            ReadOnlySpan<byte> bytes = mimeEntry.GetBytesSpan();
            string expected = package.MimeType ?? string.Empty;
            bool exactAscii = bytes.Length == expected.Length;
            for (int index = 0; exactAscii && index < bytes.Length; index++)
                exactAscii = bytes[index] <= 0x7f && bytes[index] == expected[index];

            if (!exactAscii)
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Error,
                    "ODF0006",
                    "ODF package mimetype entry must contain exactly the ASCII media type without a byte order mark or surrounding whitespace.",
                    "mimetype",
                    profileId: profileId));
            }
        }
    }

    private static void ValidateZipCompressionMethods(
        OdfPackage package,
        OdfVersion version,
        string? profileId,
        List<OdfValidationIssue> issues)
    {
        if (package.IsFlatXml || version < OdfVersion.Odf12)
            return;

        foreach (OdfPackageEntry entry in package.Entries.Values)
        {
            if (entry.ZipCompressionMethod is { } method && method is not 0 and not 8)
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Error,
                    "ODF0007",
                    "ODF package entries must use ZIP STORED or DEFLATED compression.",
                    entry.Name,
                    profileId: profileId));
            }
            else if (entry.ZipCompressionMetadataMissing)
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Warning,
                    "ODF0008",
                    "The original ZIP compression method is unavailable; compression conformance was not verified for this entry.",
                    entry.Name,
                    profileId: profileId));
            }
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
