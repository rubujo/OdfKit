using System;
using System.Collections.Generic;
using System.IO;

namespace OdfKit.Compliance;

public static partial class OdfPackageValidator
{
    #region Profile Extension & Version

    private static void ValidateProfileExtension(
        string? fileName,
        OdfComplianceProfile? profile,
        string? profileId,
        List<OdfValidationIssue> issues)
    {
        if (profile is null || string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        string extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return;
        }

        if (!profile.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF1002",
                $"File extension '{extension}' is not allowed by profile '{profile.Id}'.",
                fileName,
                profileId: profileId));
        }
    }

    private static void ValidateProfileVersion(
        OdfVersion detectedVersion,
        OdfComplianceProfile? profile,
        string? profileId,
        List<OdfValidationIssue> issues,
        string? fileName = null,
        string? xPath = null)
    {
        if (profile is null || detectedVersion == OdfVersion.Unknown)
        {
            return;
        }

        if (!profile.SupportedVersions.Contains(detectedVersion))
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF1001",
                $"ODF version '{detectedVersion}' is not allowed by profile '{profile.Id}'.",
                packagePath: fileName,
                xPath: xPath,
                requiredVersion: profile.SupportedVersions.Minimum,
                profileId: profileId));
        }
    }

    private sealed class XmlRootInfo(string namespaceUri, string localName, string? version)
    {
        public string NamespaceUri { get; } = namespaceUri;

        public string LocalName { get; } = localName;

        public string? Version { get; } = version;
    }

    #endregion
}
