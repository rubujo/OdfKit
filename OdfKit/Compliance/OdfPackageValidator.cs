using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Xml;
using OdfKit.Core;

namespace OdfKit.Compliance;

/// <summary>
/// 在完整結構描述驗證可用之前，執行套件層級的 ODF 驗證。
/// </summary>
public static class OdfPackageValidator
{
    private static readonly Dictionary<string, string> ExpectedRootNames = new(StringComparer.Ordinal)
    {
        ["content.xml"] = "document-content",
        ["styles.xml"] = "document-styles",
        ["meta.xml"] = "document-meta",
        ["settings.xml"] = "document-settings"
    };

    private static readonly Dictionary<string, string> ExpectedManifestMediaTypes = new(StringComparer.Ordinal)
    {
        ["content.xml"] = "text/xml",
        ["styles.xml"] = "text/xml",
        ["meta.xml"] = "text/xml",
        ["settings.xml"] = "text/xml",
        ["META-INF/documentsignatures.xml"] = "text/xml"
    };

    /// <summary>
    /// 驗證 ODF 套件是否符合套件層級規則與選用的設定檔。
    /// </summary>
    /// <param name="package">ODF 套件</param>
    /// <param name="profile">相容性設定檔</param>
    /// <param name="fileName">檔案名稱</param>
    /// <returns>驗證結果報告</returns>
    public static OdfValidationReport Validate(
        OdfPackage package,
        OdfComplianceProfile? profile = null,
        string? fileName = null)
    {
        if (package is null) throw new ArgumentNullException(nameof(package));

        List<OdfValidationIssue> issues = [];
        string? profileId = profile?.Id;
        string? mimeType = package.MimeType;
        OdfDocumentKind mimeKind = OdfDocumentKindDetector.FromMimeType(mimeType);
        OdfDocumentKind extensionKind = OdfDocumentKindDetector.FromFileName(fileName);
        OdfDocumentKind bodyKind = DetectBodyKind(package, issues, profileId);
        OdfDocumentKind documentKind = mimeKind != OdfDocumentKind.Unknown ? mimeKind : bodyKind;

        ValidateMimeType(mimeType, documentKind, profile, profileId, issues);
        ValidateMimeTypeEntry(package, profileId, issues);
        ValidateBodyKind(mimeKind, bodyKind, profileId, issues);
        ValidateExtensionKind(extensionKind, mimeKind, fileName, profileId, issues);
        ValidateEntryPaths(package, profileId, issues);
        ValidateManifest(package, mimeType, profileId, issues);

        OdfVersion detectedVersion = DetectVersion(package, issues, profileId);
        OdfSchemaSet schema = OdfSchemaRegistry.GetSchema(detectedVersion);
        ValidateXmlRoots(package, schema, issues, profileId);
        ValidateProfileExtension(fileName, profile, profileId, issues);
        ValidateProfileVersion(detectedVersion, profile, profileId, issues, fileName, "/office:document-content[1]");
        OdfProfileRuleValidator.ValidatePackage(package, profile, schema, issues);

        return new OdfValidationReport(detectedVersion, documentKind, issues);
    }

    private static void ValidateMimeType(
        string? mimeType,
        OdfDocumentKind documentKind,
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

        if (documentKind == OdfDocumentKind.Unknown)
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

        return path.Split('/').All(part => part.Length > 0 && part != "." && part != "..");
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
                profileId: profileId));
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
                    profileId: profileId));
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
                    profileId: profileId));
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
                profileId: profileId));
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
                profileId: profileId));
        }
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
            if (!info.HasChecksumType) missing.Add("checksum-type");
            if (!info.HasChecksum || info.Checksum.Length == 0) missing.Add("checksum");
            if (!info.HasAlgorithmName) missing.Add("algorithm-name");
            if (!info.HasInitialisationVector || info.InitialisationVector.Length == 0) missing.Add("initialisation-vector");
            if (!info.HasKeyDerivationName) missing.Add("key-derivation-name");
            if (!info.HasIterationCount || info.IterationCount <= 0) missing.Add("iteration-count");
            if (!info.HasSalt || info.Salt.Length == 0) missing.Add("salt");

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

    private static OdfVersion DetectVersion(OdfPackage package, List<OdfValidationIssue> issues, string? profileId)
    {
        foreach (string entryName in (string[])["content.xml", "styles.xml", "meta.xml", "settings.xml"])
        {
            if (!package.HasEntry(entryName))
            {
                continue;
            }

            XmlRootInfo? rootInfo = ReadRootInfo(package, entryName, issues, profileId);
            if (rootInfo?.Version is not null)
            {
                return ParseVersion(rootInfo.Version);
            }
        }

        issues.Add(new OdfValidationIssue(
            OdfIssueSeverity.Warning,
            "ODF0400",
            "No office:version attribute was found in core ODF XML entries.",
            requiredVersion: OdfVersion.Odf12,
            profileId: profileId));
        return OdfVersion.Unknown;
    }

    private static OdfDocumentKind DetectBodyKind(OdfPackage package, List<OdfValidationIssue> issues, string? profileId)
    {
        if (!package.HasEntry("content.xml"))
        {
            return OdfDocumentKind.Unknown;
        }

        try
        {
            using Stream stream = package.GetEntryStream("content.xml");
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreWhitespace = true,
                MaxCharactersFromEntities = 0
            };

            using XmlReader reader = XmlReader.Create(stream, settings);
            bool insideOfficeBody = false;
            int bodyDepth = -1;

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element &&
                    reader.NamespaceURI == OdfNamespaces.Office &&
                    reader.LocalName == "body")
                {
                    insideOfficeBody = true;
                    bodyDepth = reader.Depth;
                    if (reader.IsEmptyElement)
                    {
                        return OdfDocumentKind.Unknown;
                    }
                    continue;
                }

                if (insideOfficeBody && reader.NodeType == XmlNodeType.Element && reader.Depth == bodyDepth + 1)
                {
                    if (reader.NamespaceURI != OdfNamespaces.Office)
                    {
                        return OdfDocumentKind.Unknown;
                    }

                    OdfElementDefinition? bodyElement = OdfSchemaRegistry.Latest.FindElement(reader.NamespaceURI, reader.LocalName);
                    if (bodyElement is null || bodyElement.Role != OdfSchemaElementRole.BodyContent)
                    {
                        issues.Add(new OdfValidationIssue(
                            OdfIssueSeverity.Error,
                            "ODF3002",
                            $"office:body contains an unknown ODF document kind element 'office:{reader.LocalName}'.",
                            "content.xml",
                            "/document-content/body/" + reader.LocalName,
                            profileId: profileId));
                        return OdfDocumentKind.Unknown;
                    }

                    return bodyElement.DocumentKind;
                }

                if (insideOfficeBody && reader.NodeType == XmlNodeType.EndElement && reader.Depth == bodyDepth)
                {
                    return OdfDocumentKind.Unknown;
                }
            }
        }
        catch (XmlException ex)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Fatal,
                "ODF0301",
                $"ODF XML entry is not well-formed: {ex.Message}",
                "content.xml",
                profileId: profileId));
        }
        catch (IOException ex)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Fatal,
                "ODF0302",
                $"ODF XML entry cannot be read: {ex.Message}",
                "content.xml",
                profileId: profileId));
        }
        catch (SecurityException ex)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Fatal,
                "ODF0303",
                $"ODF XML entry failed security validation: {ex.Message}",
                "content.xml",
                profileId: profileId));
        }

        return OdfDocumentKind.Unknown;
    }

    private static void ValidateXmlRoots(
        OdfPackage package,
        OdfSchemaSet schema,
        List<OdfValidationIssue> issues,
        string? profileId)
    {
        foreach (var expected in GetExpectedXmlRootEntries(package))
        {
            XmlRootInfo? rootInfo = ReadRootInfo(package, expected.Key, issues, profileId);
            if (rootInfo is null)
            {
                continue;
            }

            if (rootInfo.NamespaceUri != OdfNamespaces.Office || rootInfo.LocalName != expected.Value)
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Error,
                    "ODF0300",
                    $"'{expected.Key}' has root element '{{{rootInfo.NamespaceUri}}}{rootInfo.LocalName}', expected office:{expected.Value}.",
                    expected.Key,
                    "/" + rootInfo.LocalName,
                    profileId: profileId));
                continue;
            }

            OdfElementDefinition? definition = schema.FindElement(rootInfo.NamespaceUri, rootInfo.LocalName);
            if (definition is null || definition.Role != OdfSchemaElementRole.DocumentRoot)
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Error,
                    "ODF3000",
                    $"'{expected.Key}' uses an ODF root element not known to the selected schema metadata.",
                    expected.Key,
                    "/" + rootInfo.LocalName,
                    profileId: profileId));
            }
        }
    }

    private static IEnumerable<KeyValuePair<string, string>> GetExpectedXmlRootEntries(OdfPackage package)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (var expected in ExpectedRootNames)
        {
            if (package.HasEntry(expected.Key) && seen.Add(expected.Key))
            {
                yield return expected;
            }
        }

        foreach (string entryName in package.Entries.Keys.OrderBy(name => name, StringComparer.Ordinal))
        {
            string normalized = entryName.Replace('\\', '/');
            string? expectedRoot = GetEmbeddedXmlRootName(normalized);
            if (expectedRoot is not null && seen.Add(normalized))
            {
                yield return new KeyValuePair<string, string>(normalized, expectedRoot);
            }
        }
    }

    private static string? GetEmbeddedXmlRootName(string entryName)
    {
        if (entryName.StartsWith("META-INF/", StringComparison.Ordinal))
        {
            return null;
        }

        foreach (var expected in ExpectedRootNames)
        {
            if (entryName.EndsWith("/" + expected.Key, StringComparison.Ordinal))
            {
                return expected.Value;
            }
        }

        return null;
    }

    private static XmlRootInfo? ReadRootInfo(OdfPackage package, string entryName, List<OdfValidationIssue> issues, string? profileId)
    {
        try
        {
            using Stream stream = package.GetEntryStream(entryName);
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreWhitespace = true,
                MaxCharactersFromEntities = 0
            };

            using XmlReader reader = XmlReader.Create(stream, settings);
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                string? version = reader.GetAttribute("version", OdfNamespaces.Office) ?? reader.GetAttribute("version");
                return new XmlRootInfo(reader.NamespaceURI, reader.LocalName, version);
            }
        }
        catch (XmlException ex)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Fatal,
                "ODF0301",
                $"ODF XML entry is not well-formed: {ex.Message}",
                entryName,
                profileId: profileId));
        }
        catch (IOException ex)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Fatal,
                "ODF0302",
                $"ODF XML entry cannot be read: {ex.Message}",
                entryName,
                profileId: profileId));
        }
        catch (SecurityException ex)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Fatal,
                "ODF0303",
                $"ODF XML entry failed security validation: {ex.Message}",
                entryName,
                profileId: profileId));
        }

        return null;
    }

    private static OdfVersion ParseVersion(string version)
    {
        return version switch
        {
            "1.0" => OdfVersion.Odf10,
            "1.1" => OdfVersion.Odf11,
            "1.2" => OdfVersion.Odf12,
            "1.3" => OdfVersion.Odf13,
            "1.4" => OdfVersion.Odf14,
            _ => OdfVersion.Unknown
        };
    }

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
        Console.WriteLine($"DEBUG: ValidateProfileVersion detectedVersion={detectedVersion}, profile={profile?.Id}, fileName={fileName}, xPath={xPath}");
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
}
