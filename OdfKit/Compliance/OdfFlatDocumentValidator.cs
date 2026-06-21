#pragma warning restore CS1591
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using OdfKit.Core;

namespace OdfKit.Compliance;

/// <summary>
/// 執行單一 XML ODF 文件的低階驗證。
/// </summary>
public static class OdfFlatDocumentValidator
{
    /// <summary>
    /// 驗證單一 XML ODF 文件串流是否符合根層級規則與選用的設定檔。
    /// </summary>
    /// <param name="stream">文件串流</param>
    /// <param name="fileName">檔案名稱</param>
    /// <param name="profile">相容性設定檔</param>
    /// <param name="culture">指定此問題生成時使用的文化特性，若為 null 則自動偵測</param>
    /// <returns>驗證結果報告</returns>
    public static OdfValidationReport Validate(
        Stream stream,
        string? fileName = null,
        OdfComplianceProfile? profile = null,
        System.Globalization.CultureInfo? culture = null)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));

        List<OdfValidationIssue> issues = [];

        // 智慧偵測語系
        System.Globalization.CultureInfo targetCulture = culture
            ?? profile?.TargetCulture
            ?? System.Globalization.CultureInfo.CurrentUICulture;

        string? profileId = profile?.Id;
        OdfDocumentKind extensionKind = OdfDocumentKindDetector.FromFileName(fileName);
        FlatRootInfo? rootInfo = ReadRootInfo(stream, fileName, profileId, issues);

        OdfDocumentKind documentKind = DetectDocumentKind(rootInfo?.MimeType, extensionKind, rootInfo?.BodyKind ?? OdfDocumentKind.Unknown);
        OdfVersion detectedVersion = rootInfo?.Version is not null
            ? ParseVersion(rootInfo.Version)
            : OdfVersion.Unknown;
        OdfSchemaSet schema = OdfSchemaRegistry.GetSchema(detectedVersion);
        if (!OdfSchemaRegistry.HasNativeSchema(detectedVersion) && detectedVersion != OdfVersion.Unknown)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Warning,
                "ODF1005",
                $"The validator is using ODF 1.4 schema to perform best-effort validation on ODF {OdfVersionInfo.ToVersionString(detectedVersion)} document.",
                fileName,
                profileId: profileId));
        }

        ValidateRoot(rootInfo, schema, fileName, profileId, issues);
        ValidateMimeType(rootInfo?.MimeType, documentKind, profile, fileName, profileId, issues);
        ValidateBodyKind(documentKind, rootInfo?.BodyKind ?? OdfDocumentKind.Unknown, fileName, profileId, issues);
        ValidateExtensionKind(extensionKind, documentKind, fileName, profileId, issues);
        ValidateProfileExtension(fileName, profile, profileId, issues);
        ValidateVersion(detectedVersion, profile, fileName, profileId, issues);
        OdfProfileRuleValidator.ValidateFlatXml(stream, fileName, profile, schema, issues);

        foreach (var issue in issues)
        {
            issue.Culture ??= targetCulture;
        }

        return new OdfValidationReport(detectedVersion, documentKind, issues);
    }

    private static FlatRootInfo? ReadRootInfo(Stream stream, string? fileName, string? profileId, List<OdfValidationIssue> issues)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreWhitespace = true,
                MaxCharactersFromEntities = 0,
                CloseInput = false
            };

            using XmlReader reader = XmlReader.Create(stream, settings);
            FlatRootInfo? rootInfo = null;
            bool insideOfficeBody = false;
            int bodyDepth = -1;

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    if (insideOfficeBody && reader.NodeType == XmlNodeType.EndElement && reader.Depth == bodyDepth)
                    {
                        return rootInfo;
                    }

                    continue;
                }

                if (rootInfo is null)
                {
                    string? mimeType = reader.GetAttribute("mimetype", OdfNamespaces.Office) ?? reader.GetAttribute("mimetype");
                    string? version = reader.GetAttribute("version", OdfNamespaces.Office) ?? reader.GetAttribute("version");
                    rootInfo = new FlatRootInfo(reader.NamespaceURI, reader.LocalName, mimeType, version, OdfDocumentKind.Unknown);
                    continue;
                }

                if (reader.NamespaceURI == OdfNamespaces.Office && reader.LocalName == "body")
                {
                    insideOfficeBody = true;
                    bodyDepth = reader.Depth;
                    if (reader.IsEmptyElement)
                    {
                        return rootInfo;
                    }
                    continue;
                }

                if (insideOfficeBody && reader.Depth == bodyDepth + 1)
                {
                    OdfDocumentKind bodyKind = OdfDocumentKind.Unknown;
                    if (reader.NamespaceURI == OdfNamespaces.Office)
                    {
                        OdfElementDefinition? bodyElement = OdfSchemaRegistry.Latest.FindElement(reader.NamespaceURI, reader.LocalName);
                        if (bodyElement is null || bodyElement.Role != OdfSchemaElementRole.BodyContent)
                        {
                            issues.Add(new OdfValidationIssue(
                                OdfIssueSeverity.Error,
                                "ODF3002",
                                $"office:body contains an unknown ODF document kind element 'office:{reader.LocalName}'.",
                                fileName,
                                "/document/body/" + reader.LocalName,
                                profileId: profileId));
                        }
                        else
                        {
                            bodyKind = OdfDocumentKindDetector.ToFlatKind(bodyElement.DocumentKind);
                        }
                    }

                    return new FlatRootInfo(rootInfo.NamespaceUri, rootInfo.LocalName, rootInfo.MimeType, rootInfo.Version, bodyKind);
                }
            }

            if (rootInfo is not null)
            {
                return rootInfo;
            }

            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Fatal,
                "ODF2000",
                "Flat ODF document does not contain a root element.",
                fileName,
                profileId: profileId));
        }
        catch (XmlException ex)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Fatal,
                "ODF2000",
                $"Flat ODF XML is not well-formed: {ex.Message}",
                fileName,
                profileId: profileId));
        }
        catch (IOException ex)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Fatal,
                "ODF2004",
                $"Flat ODF XML cannot be read: {ex.Message}",
                fileName,
                profileId: profileId));
        }

        return null;
    }

    private static void ValidateRoot(
        FlatRootInfo? rootInfo,
        OdfSchemaSet schema,
        string? fileName,
        string? profileId,
        List<OdfValidationIssue> issues)
    {
        if (rootInfo is null)
        {
            return;
        }

        if (rootInfo.NamespaceUri != OdfNamespaces.Office || rootInfo.LocalName != "document")
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF2001",
                "Flat ODF XML root must be office:document.",
                fileName,
                "/" + rootInfo.LocalName,
                profileId: profileId));
            return;
        }

        OdfElementDefinition? definition = schema.FindElement(rootInfo.NamespaceUri, rootInfo.LocalName);
        if (definition is null || definition.Role != OdfSchemaElementRole.DocumentRoot)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF3000",
                "Flat ODF XML root is not known to the selected schema metadata.",
                fileName,
                "/" + rootInfo.LocalName,
                profileId: profileId));
        }
    }

    private static void ValidateBodyKind(
        OdfDocumentKind declaredKind,
        OdfDocumentKind bodyKind,
        string? fileName,
        string? profileId,
        List<OdfValidationIssue> issues)
    {
        if (!OdfDocumentKindDetector.IsCompatibleWithBodyKind(declaredKind, bodyKind))
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF2006",
                $"Flat ODF declared kind '{declaredKind}' does not match office:body kind '{bodyKind}'.",
                fileName,
                "/document/body",
                profileId: profileId));
        }
    }

    private static void ValidateMimeType(
        string? mimeType,
        OdfDocumentKind documentKind,
        OdfComplianceProfile? profile,
        string? fileName,
        string? profileId,
        List<OdfValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF2002",
                "Flat ODF document is missing office:mimetype.",
                fileName,
                "/document",
                profileId: profileId));
            return;
        }

        if (documentKind == OdfDocumentKind.Unknown)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF2003",
                "Flat ODF document declares an unknown or unsupported office:mimetype.",
                fileName,
                "/document",
                profileId: profileId));
        }

        if (profile is not null && !profile.AllowedMimeTypes.Contains(mimeType, StringComparer.Ordinal))
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF1000",
                $"MIME type '{mimeType}' is not allowed by profile '{profile.Id}'.",
                fileName,
                "/document",
                profileId: profile.Id));
        }
    }

    private static void ValidateExtensionKind(
        OdfDocumentKind extensionKind,
        OdfDocumentKind documentKind,
        string? fileName,
        string? profileId,
        List<OdfValidationIssue> issues)
    {
        if (extensionKind == OdfDocumentKind.Unknown ||
            documentKind == OdfDocumentKind.Unknown ||
            !OdfDocumentKindDetector.IsFlatKind(extensionKind))
        {
            return;
        }

        if (extensionKind != documentKind)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Warning,
                "ODF2005",
                "Flat ODF file extension does not match office:mimetype.",
                fileName,
                "/document",
                profileId: profileId));
        }
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

    private static void ValidateVersion(
        OdfVersion detectedVersion,
        OdfComplianceProfile? profile,
        string? fileName,
        string? profileId,
        List<OdfValidationIssue> issues)
    {
        if (detectedVersion == OdfVersion.Unknown)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Warning,
                "ODF0400",
                "No office:version attribute was found in flat ODF XML.",
                fileName,
                "/document",
                OdfVersion.Odf12,
                profileId));
            return;
        }

        if (profile is not null && !profile.SupportedVersions.Contains(detectedVersion))
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF1001",
                $"ODF version '{detectedVersion}' is not allowed by profile '{profile.Id}'.",
                fileName,
                "/document",
                profile.SupportedVersions.Minimum,
                profileId));
        }
    }

    private static OdfDocumentKind DetectDocumentKind(string? mimeType, OdfDocumentKind extensionKind, OdfDocumentKind bodyKind)
    {
        OdfDocumentKind mimeKind = OdfDocumentKindDetector.FromMimeType(mimeType);
        if (mimeKind != OdfDocumentKind.Unknown)
        {
            return OdfDocumentKindDetector.ToFlatKind(mimeKind);
        }

        if (OdfDocumentKindDetector.IsFlatKind(extensionKind))
        {
            return extensionKind;
        }

        return bodyKind != OdfDocumentKind.Unknown
            ? bodyKind
            : OdfDocumentKind.Unknown;
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

    private sealed class FlatRootInfo(string namespaceUri, string localName, string? mimeType, string? version, OdfDocumentKind bodyKind)
    {
        public string NamespaceUri { get; } = namespaceUri;

        public string LocalName { get; } = localName;

        public string? MimeType { get; } = mimeType;

        public string? Version { get; } = version;

        public OdfDocumentKind BodyKind { get; } = bodyKind;
    }
}
