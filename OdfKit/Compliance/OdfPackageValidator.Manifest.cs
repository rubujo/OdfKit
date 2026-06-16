using System;
using System.Collections.Generic;
using OdfKit.Core;

namespace OdfKit.Compliance;

public static partial class OdfPackageValidator
{
    #region Entry Paths & Manifest

    private static void ValidateEntryPaths(OdfPackage package, string? profileId, List<OdfValidationIssue> issues)
    {
        foreach (string duplicateEntryName in package.DuplicateEntryNames)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF0202",
                "Package contains duplicate ZIP entry names, which makes ODF payload resolution ambiguous.",
                duplicateEntryName,
                profileId: profileId));
        }

        foreach (string entryName in package.Entries.Keys)
        {
            if (!IsSafeEntryPath(entryName))
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Fatal,
                    "ODF0200",
                    "Package entry path is not a safe relative ODF ZIP path.",
                    entryName,
                    profileId: profileId));
            }
        }

        foreach (string manifestPath in package.Manifest.Keys)
        {
            if (manifestPath == "/")
            {
                continue;
            }

            if (!IsSafeEntryPath(manifestPath))
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Fatal,
                    "ODF0201",
                    "Manifest path is not a safe relative ODF ZIP path.",
                    manifestPath,
                    profileId: profileId));
            }
        }
    }

    private static bool IsSafeEntryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.StartsWith("/", StringComparison.Ordinal) ||
            path.Contains("\\") ||
            path.Contains(":") ||
            path.Contains("//"))
        {
            return false;
        }

        string normalized = path.EndsWith("/", StringComparison.Ordinal)
            ? path.Substring(0, path.Length - 1)
            : path;

        return normalized.Length > 0 &&
            normalized.Split('/').All(part => part.Length > 0 && part != "." && part != "..");
    }

    private static void ValidateManifest(
        OdfPackage package,
        string? mimeType,
        string? profileId,
        List<OdfValidationIssue> issues)
    {
        if (!package.HasEntry("META-INF/manifest.xml"))
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF0100",
                "ODF package is missing META-INF/manifest.xml.",
                "META-INF/manifest.xml",
                profileId: profileId));
            return;
        }

        ValidateManifestRoot(package.ManifestRootInfo, profileId, issues);

        foreach (string duplicateManifestPath in package.DuplicateManifestPaths)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF0107",
                "Manifest contains duplicate file-entry full-path values, which makes package metadata ambiguous.",
                duplicateManifestPath,
                profileId: profileId));
        }

        foreach (OdfManifestFileEntryIssue fileEntryIssue in package.ManifestFileEntryIssues)
        {
            string issuePath = string.IsNullOrWhiteSpace(fileEntryIssue.FullPath)
                ? "META-INF/manifest.xml"
                : fileEntryIssue.FullPath!;

            if (fileEntryIssue.MissingFullPath)
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Error,
                    "ODF0108",
                    "Manifest file-entry is missing manifest:full-path.",
                    "META-INF/manifest.xml",
                    profileId: profileId));
            }

            if (fileEntryIssue.MissingMediaType)
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Error,
                    "ODF0109",
                    "Manifest file-entry is missing manifest:media-type.",
                    issuePath,
                    profileId: profileId));
            }

            if (fileEntryIssue.InvalidFullPath)
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Fatal,
                    "ODF0201",
                    "Manifest path is not a safe relative ODF ZIP path.",
                    issuePath,
                    profileId: profileId));
            }
        }

        if (!package.Manifest.ContainsKey("/"))
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF0101",
                "Manifest is missing the root file-entry '/'.",
                "META-INF/manifest.xml",
                profileId: profileId));
        }
        else if (!string.IsNullOrWhiteSpace(mimeType) &&
            !string.Equals(package.Manifest["/"], mimeType, StringComparison.Ordinal))
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF0103",
                "Manifest root media type does not match the package mimetype entry.",
                "META-INF/manifest.xml",
                profileId: profileId,
                details: ManifestDetails(
                    entryPath: "/",
                    expectedMediaType: mimeType,
                    actualMediaType: package.Manifest["/"])));
        }

        foreach (string entryName in package.Entries.Keys)
        {
            if (entryName == "mimetype" || entryName == "META-INF/manifest.xml")
            {
                continue;
            }

            if (!package.Manifest.ContainsKey(entryName))
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Warning,
                    "ODF0102",
                    "Package entry is not listed in META-INF/manifest.xml.",
                    entryName,
                    profileId: profileId,
                    details: ManifestDetails(
                        entryPath: entryName,
                        expectedManifestEntry: "present",
                        actualManifestEntry: "missing")));
            }
        }

        foreach (string manifestPath in package.Manifest.Keys)
        {
            if (manifestPath != "/" &&
                !manifestPath.EndsWith("/", StringComparison.Ordinal) &&
                string.IsNullOrEmpty(package.Manifest[manifestPath]))
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Error,
                    "ODF0106",
                    "Manifest directory file-entry must use a trailing slash in full-path.",
                    manifestPath,
                    profileId: profileId));
            }

            if (manifestPath == "/" ||
                manifestPath.EndsWith("/", StringComparison.Ordinal) ||
                manifestPath == "META-INF/manifest.xml")
            {
                continue;
            }

            if (!IsSafeEntryPath(manifestPath))
            {
                continue;
            }

            if (!package.HasEntry(manifestPath))
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Error,
                    "ODF0104",
                    "Manifest file-entry points to a package entry that does not exist.",
                    manifestPath,
                    profileId: profileId,
                    details: ManifestDetails(
                        entryPath: manifestPath,
                        expectedPackageEntry: "present",
                        actualPackageEntry: "missing")));
            }
        }

        foreach (var expected in ExpectedManifestMediaTypes)
        {
            if (!package.Manifest.TryGetValue(expected.Key, out string? declaredMediaType) ||
                string.Equals(declaredMediaType, expected.Value, StringComparison.Ordinal))
            {
                continue;
            }

            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF0105",
                $"Manifest media type for '{expected.Key}' must be '{expected.Value}'.",
                expected.Key,
                profileId: profileId,
                details: ManifestDetails(
                    entryPath: expected.Key,
                    expectedMediaType: expected.Value,
                    actualMediaType: declaredMediaType)));
        }

        ValidateEncryptionMetadata(package, profileId, issues);

        if (package.HasEntry("META-INF/documentsignatures.xml") &&
            !package.Manifest.ContainsKey("META-INF/documentsignatures.xml"))
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF0110",
                "Digital signature entry must be listed in META-INF/manifest.xml.",
                "META-INF/documentsignatures.xml",
                profileId: profileId,
                details: ManifestDetails(
                    entryPath: "META-INF/documentsignatures.xml",
                    expectedManifestEntry: "present",
                    actualManifestEntry: "missing")));
        }
    }

    private static IReadOnlyDictionary<string, string?> ManifestDetails(
        string entryPath,
        string? expectedMediaType = null,
        string? actualMediaType = null,
        string? expectedManifestEntry = null,
        string? actualManifestEntry = null,
        string? expectedPackageEntry = null,
        string? actualPackageEntry = null)
    {
        Dictionary<string, string?> details = new(StringComparer.Ordinal)
        {
            ["entryPath"] = entryPath
        };

        if (expectedMediaType is not null)
            details["expectedMediaType"] = expectedMediaType;
        if (actualMediaType is not null)
            details["actualMediaType"] = actualMediaType;
        if (expectedManifestEntry is not null)
            details["expectedManifestEntry"] = expectedManifestEntry;
        if (actualManifestEntry is not null)
            details["actualManifestEntry"] = actualManifestEntry;
        if (expectedPackageEntry is not null)
            details["expectedPackageEntry"] = expectedPackageEntry;
        if (actualPackageEntry is not null)
            details["actualPackageEntry"] = actualPackageEntry;

        return details;
    }

    private static void ValidateEncryptionMetadata(
        OdfPackage package,
        string? profileId,
        List<OdfValidationIssue> issues)
    {
        foreach (var entry in package.Entries.Values)
        {
            OdfEncryptionInfo? info = entry.EncryptionInfo;
            if (info is null)
            {
                continue;
            }

            List<string> missing = [];
            if (!info.HasChecksumType)
                missing.Add("checksum-type");
            if (!info.HasChecksum || info.Checksum.Length == 0)
                missing.Add("checksum");
            if (!info.HasAlgorithmName)
                missing.Add("algorithm-name");
            if (!info.HasInitialisationVector || info.InitialisationVector.Length == 0)
                missing.Add("initialisation-vector");
            if (!info.HasKeyDerivationName)
                missing.Add("key-derivation-name");
            if (!info.HasIterationCount || info.IterationCount <= 0)
                missing.Add("iteration-count");
            if (!info.HasSalt || info.Salt.Length == 0)
                missing.Add("salt");

            if (missing.Count == 0)
            {
                continue;
            }

            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF0120",
                "Encrypted manifest file-entry is missing encryption metadata: " + string.Join(", ", missing) + ".",
                entry.Name,
                profileId: profileId));
        }
    }

    private static void ValidateManifestRoot(
        OdfManifestRootInfo? rootInfo,
        string? profileId,
        List<OdfValidationIssue> issues)
    {
        if (rootInfo is null)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Fatal,
                "ODF0111",
                "Manifest XML does not contain a root element.",
                "META-INF/manifest.xml",
                profileId: profileId));
            return;
        }

        if (rootInfo.NamespaceUri != OdfNamespaces.Manifest || rootInfo.LocalName != "manifest")
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF0111",
                "Manifest XML root must be manifest:manifest.",
                "META-INF/manifest.xml",
                "/" + rootInfo.LocalName,
                profileId: profileId));
        }

        if (string.IsNullOrWhiteSpace(rootInfo.Version))
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Warning,
                "ODF0112",
                "Manifest XML root is missing manifest:version.",
                "META-INF/manifest.xml",
                "/" + rootInfo.LocalName,
                requiredVersion: OdfVersion.Odf12,
                profileId: profileId));
        }
    }


    #endregion
}
