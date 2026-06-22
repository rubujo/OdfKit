using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Xml;
using OdfKit.Core;

namespace OdfKit.Compliance;

public static partial class OdfPackageValidator
{
    #region Version Detection & XML Roots

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
        string? profileId,
        OdfDocumentKind documentKind = OdfDocumentKind.Unknown)
    {
        bool isFormulaKind = documentKind is OdfDocumentKind.Formula or OdfDocumentKind.FormulaTemplate or OdfDocumentKind.FlatFormula;

        foreach (var expected in GetExpectedXmlRootEntries(package))
        {
            XmlRootInfo? rootInfo = ReadRootInfo(package, expected.Key, issues, profileId);
            if (rootInfo is null)
            {
                continue;
            }

            // ODF 公式文件（application/vnd.oasis.opendocument.formula）的 content.xml 採用真實
            // LibreOffice 慣用的特殊封裝慣例：根節點直接是裸 MathML math:math 元素，並不包裹於
            // office:document-content 結構內。此為該文件類型獨有的合法形狀，非結構錯誤。
            if (isFormulaKind &&
                expected.Key == "content.xml" &&
                rootInfo.NamespaceUri == "http://www.w3.org/1998/Math/MathML" &&
                rootInfo.LocalName == "math")
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


    #endregion
}
