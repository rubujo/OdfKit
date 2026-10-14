using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Xml;
using System.Xml.Linq;
using OdfKit.Core;

namespace OdfKit.Compliance;

internal static partial class OdfProfileRuleValidator
{
    #region Schema Pattern Validation

    private static void ValidatePackageSchemaPatterns(
        OdfPackage package,
        OdfComplianceProfile profile,
        OdfSchemaSet schema,
        List<OdfValidationIssue> issues)
    {
        OdfPolicyRule? rule = FindRule(profile, "RequireSchemaPatternValidation");
        if (rule is null)
        {
            return;
        }

        foreach (string entryName in GetProfileXmlEntries(package))
        {
            if (!package.HasEntry(entryName))
            {
                continue;
            }

            try
            {
                using Stream stream = package.GetEntryStream(entryName);
                ValidateRootSchemaPattern(
                    stream,
                    entryName,
                    profile,
                    schema,
                    rule,
                    issues,
                    closeInput: true,
                    package.LoadOptions.MaxXmlCharactersInDocument);
            }
            catch (IOException ex)
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Fatal,
                    "ODF0302",
                    $"ODF XML entry cannot be read for schema pattern checks: {ex.Message}",
                    entryName,
                    profileId: profile.Id));
            }
            catch (SecurityException ex)
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Fatal,
                    "ODF0303",
                    $"ODF XML entry failed security validation during schema pattern checks: {ex.Message}",
                    entryName,
                    profileId: profile.Id));
            }
        }
    }

    private static IEnumerable<string> GetProfileXmlEntries(OdfPackage package)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string entryName in XmlEntries)
        {
            if (package.HasEntry(entryName) && seen.Add(entryName))
            {
                yield return entryName;
            }
        }

        foreach (string entryName in package.Entries.Keys.OrderBy(name => name, StringComparer.Ordinal))
        {
            string normalized = entryName.Replace('\\', '/');
            if (IsEmbeddedOdfXmlEntry(normalized) && seen.Add(normalized))
            {
                yield return normalized;
            }
        }
    }

    private static bool IsEmbeddedOdfXmlEntry(string entryName)
    {
        if (entryName.StartsWith("META-INF/", StringComparison.Ordinal))
        {
            return false;
        }

        return entryName.EndsWith("/content.xml", StringComparison.Ordinal) ||
            entryName.EndsWith("/styles.xml", StringComparison.Ordinal) ||
            entryName.EndsWith("/meta.xml", StringComparison.Ordinal) ||
            entryName.EndsWith("/settings.xml", StringComparison.Ordinal);
    }

    private static void ValidateFlatSchemaPattern(
        Stream stream,
        string? fileName,
        OdfComplianceProfile profile,
        OdfSchemaSet schema,
        List<OdfValidationIssue> issues,
        long maxXmlCharacters)
    {
        OdfPolicyRule? rule = FindRule(profile, "RequireSchemaPatternValidation");
        if (rule is null)
        {
            return;
        }

        ValidateRootSchemaPattern(stream, fileName, profile, schema, rule, issues, closeInput: false, maxXmlCharacters);
    }

    private static void ValidateRootSchemaPattern(
        Stream stream,
        string? packagePath,
        OdfComplianceProfile profile,
        OdfSchemaSet schema,
        OdfPolicyRule rule,
        List<OdfValidationIssue> issues,
        bool closeInput,
        long maxXmlCharacters)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreWhitespace = true,
                MaxCharactersFromEntities = 0,
                MaxCharactersInDocument = maxXmlCharacters > 0 ? maxXmlCharacters : 0,
                CloseInput = closeInput
            };

            using XmlReader reader = XmlReader.Create(stream, settings);
            XDocument document = XDocument.Load(new OdfKit.Core.OdfDepthLimitedXmlReader(reader), LoadOptions.None);
            if (document.Root is null)
            {
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3102",
                    "ODF XML document does not contain a root element for schema pattern validation.",
                    packagePath,
                    profileId: profile.Id));
                return;
            }

            string? declaredVersion = document.Root.Attribute(XName.Get("version", OdfNamespaces.Office))?.Value;
            schema = ResolveEntrySchema(packagePath, declaredVersion, schema);
            if (packagePath is not null && IsEmbeddedOdfXmlEntry(packagePath) &&
                declaredVersion is not null && !OdfVersionInfo.TryParseVersionString(declaredVersion, out _))
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Error,
                    "ODF0401",
                    "Embedded ODF XML entry declares an unrecognized office:version.",
                    packagePath,
                    "/" + document.Root.Name.LocalName + "/@office:version",
                    profileId: profile.Id));
            }

            // 裸 MathML 沒有內建的完整 grammar；保留互通形狀，但明確回報未驗證的範圍。
            if (document.Root.Name.NamespaceName == "http://www.w3.org/1998/Math/MathML" &&
                document.Root.Name.LocalName == "math")
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Warning,
                    "ODF3103",
                    "MathML content was not validated against a complete MathML schema.",
                    packagePath,
                    "/math",
                    profileId: profile.Id));
                return;
            }

            // Extended 才能先處理 foreign content；Strict 必須直接比對原始 grammar，
            // 由 schema 決定哪些擴充位置合法，不可先刪除不合法屬性讓它通過。
            if (FindRule(profile, "RequireForeignExtensionIsolation") is not null)
            {
                SanitizeForeignContentForSchemaValidation(document.Root);
            }

            var body = document.Root.Element(XName.Get("body", OdfNamespaces.Office));
            if (body is not null)
            {
                if (body.Element(XName.Get("formula", OdfNamespaces.Office)) is not null)
                {
                    // office:formula 是 SDK 的相容形狀，官方 Formula package 使用裸 MathML。
                    // 此豁免不適用於官方 grammar 已定義的 database、image 或 chart。
                    foreach (XElement child in body.Elements().ToList())
                    {
                        if (child.Name == XName.Get("formula", OdfNamespaces.Office))
                        {
                            issues.Add(new OdfValidationIssue(
                                OdfIssueSeverity.Warning,
                                "ODF3103",
                                $"Schema validation excludes the '{child.Name.LocalName}' body content; the document envelope is still validated.",
                                packagePath,
                                "/" + document.Root.Name.LocalName + "/body/" + child.Name.LocalName,
                                profileId: profile.Id));
                            child.ReplaceWith(new XElement(XName.Get("text", OdfNamespaces.Office)));
                        }
                    }
                }
            }

            // 當 schema 並非該版本的官方真實 schema 時（目前僅 Unknown），暫時將 root 的
            // version 改寫為實際 pattern 內容所屬的 1.4，以便正確匹配 pattern。
            // 原生 1.0～1.4 驗證時 schema 本身就是該版本的真實 schema，此處為 no-op。
            string schemaVersionString = OdfSchemaRegistry.HasNativeSchema(schema.Version)
                ? OdfVersionInfo.ToVersionString(schema.Version)
                : OdfVersionInfo.ToVersionString(OdfVersion.Odf14);
            var versionAttr = document.Root.Attribute(XName.Get("version", OdfNamespaces.Office))
                ?? document.Root.Attribute("version");
            if (!OdfSchemaRegistry.HasNativeSchema(schema.Version) &&
                versionAttr is not null && versionAttr.Value != schemaVersionString)
            {
                versionAttr.Value = schemaVersionString;
            }

            List<string> patternNames = ResolveRootPatternNames(schema, document.Root, packagePath);
            if (patternNames.Count == 0)
            {
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3100",
                    $"No schema pattern is available for root element '{{{document.Root.Name.NamespaceName}}}{document.Root.Name.LocalName}'.",
                    packagePath,
                    "/" + document.Root.Name.LocalName,
                    profileId: profile.Id));
                return;
            }

            OdfSchemaSet defaultSchema = OdfSchemaRegistry.DefaultOdf14;
            string? targetPatternName = null;

            foreach (string name in patternNames)
            {
                OdfSchemaPatternDefinition? pattern = schema.FindPattern(name);
                if (pattern is not null)
                {
                    OdfSchemaPatternDefinition? defaultPattern = defaultSchema.FindPattern(name);
                    bool isCustom = defaultPattern is null || pattern != defaultPattern;

                    if (isCustom && OdfSchemaPatternValidator.PatternMatchesElementName(pattern, document.Root, schema))
                    {
                        targetPatternName = name;
                        break;
                    }
                }
            }

            if (targetPatternName is null)
            {
                foreach (string name in patternNames)
                {
                    OdfSchemaPatternDefinition? pattern = schema.FindPattern(name);
                    if (pattern is not null && OdfSchemaPatternValidator.PatternMatchesElementName(pattern, document.Root, schema))
                    {
                        targetPatternName = name;
                        break;
                    }
                }
            }

            targetPatternName ??= patternNames[0];

            OdfSchemaPatternValidationResult result = OdfSchemaPatternValidator.ValidateElement(
                document.Root,
                schema,
                targetPatternName);

            if (result.IsMatch)
            {
                return;
            }

            foreach (OdfValidationIssue issue in result.Issues)
            {
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    issue.RuleId,
                    issue.Message,
                    packagePath,
                    "/" + document.Root.Name.LocalName,
                    profileId: profile.Id));
            }
        }
        catch (XmlException ex)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Fatal,
                "ODF0301",
                $"ODF XML entry is not well-formed during schema pattern checks: {ex.Message}",
                packagePath,
                profileId: profile.Id));
        }
    }

    private static OdfSchemaSet ResolveEntrySchema(string? packagePath, string? declaredVersion, OdfSchemaSet schema)
    {
        return packagePath is not null && IsEmbeddedOdfXmlEntry(packagePath) &&
            OdfVersionInfo.TryParseVersionString(declaredVersion, out OdfVersion version)
            ? OdfSchemaRegistry.GetSchema(version) : schema;
    }

    private static List<string> ResolveRootPatternNames(OdfSchemaSet schema, XElement root, string? packagePath)
    {
        List<string> patternNames = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string candidate in GetRootPatternCandidates(root, packagePath))
        {
            if (seen.Add(candidate) && schema.FindPattern(candidate) is not null)
            {
                patternNames.Add(candidate);
            }
        }

        return patternNames;
    }

    private static IEnumerable<string> GetRootPatternCandidates(XElement root, string? packagePath)
    {
        string localName = root.Name.LocalName;
        yield return localName;

        if (root.Name.NamespaceName == OdfNamespaces.Office)
        {
            yield return "office-" + localName;
        }

        string fileStem = string.Empty;
        if (packagePath is not null)
        {
            string? fileName = packagePath.Replace('\\', '/').Split('/').LastOrDefault();
            fileStem = fileName is null
                ? string.Empty
                : Path.GetFileNameWithoutExtension(fileName) ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(fileStem))
        {
            yield return "document-" + fileStem;
            yield return "office-document-" + fileStem;
            yield return fileStem;
        }

        yield return "root";
        yield return "start";
    }


    #endregion
}
