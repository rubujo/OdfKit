#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using OdfKit.Core;

namespace OdfKit.Compliance
{
    /// <summary>
    /// Performs low-level validation for flat XML ODF documents.
    /// </summary>
    public static class OdfFlatDocumentValidator
    {
        /// <summary>
        /// Validates a flat XML ODF document stream against root-level rules and an optional profile.
        /// </summary>
        public static OdfValidationReport Validate(Stream stream, string? fileName = null, OdfComplianceProfile? profile = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var issues = new List<OdfValidationIssue>();
            string? profileId = profile?.Id;
            OdfDocumentKind extensionKind = OdfDocumentKindDetector.FromFileName(fileName);
            FlatRootInfo? rootInfo = ReadRootInfo(stream, fileName, profileId, issues);

            OdfDocumentKind documentKind = DetectDocumentKind(rootInfo?.MimeType, extensionKind, rootInfo?.BodyKind ?? OdfDocumentKind.Unknown);
            OdfVersion detectedVersion = rootInfo?.Version != null
                ? ParseVersion(rootInfo.Version)
                : OdfVersion.Unknown;
            OdfSchemaSet schema = OdfSchemaRegistry.GetSchema(detectedVersion);

            ValidateRoot(rootInfo, schema, fileName, profileId, issues);
            ValidateMimeType(rootInfo?.MimeType, documentKind, profile, fileName, profileId, issues);
            ValidateBodyKind(documentKind, rootInfo?.BodyKind ?? OdfDocumentKind.Unknown, fileName, profileId, issues);
            ValidateExtensionKind(extensionKind, documentKind, fileName, profileId, issues);
            ValidateProfileExtension(fileName, profile, profileId, issues);
            ValidateVersion(detectedVersion, profile, fileName, profileId, issues);
            OdfProfileRuleValidator.ValidateFlatXml(stream, fileName, profile, schema, issues);

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

                    if (rootInfo == null)
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
                            if (bodyElement == null || bodyElement.Role != OdfSchemaElementRole.BodyContent)
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

                if (rootInfo != null)
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
            if (rootInfo == null)
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
            if (definition == null || definition.Role != OdfSchemaElementRole.DocumentRoot)
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

            if (profile != null && !profile.AllowedMimeTypes.Contains(mimeType, StringComparer.Ordinal))
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
            if (profile == null || string.IsNullOrWhiteSpace(fileName))
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

            if (profile != null && !profile.SupportedVersions.Contains(detectedVersion))
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

        private sealed class FlatRootInfo
        {
            public FlatRootInfo(string namespaceUri, string localName, string? mimeType, string? version, OdfDocumentKind bodyKind)
            {
                NamespaceUri = namespaceUri;
                LocalName = localName;
                MimeType = mimeType;
                Version = version;
                BodyKind = bodyKind;
            }

            public string NamespaceUri { get; }

            public string LocalName { get; }

            public string? MimeType { get; }

            public string? Version { get; }

            public OdfDocumentKind BodyKind { get; }
        }
    }
}
