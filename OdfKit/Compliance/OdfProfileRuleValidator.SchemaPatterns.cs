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
                ValidateRootSchemaPattern(stream, entryName, profile, schema, rule, issues, closeInput: true);
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
        List<OdfValidationIssue> issues)
    {
        OdfPolicyRule? rule = FindRule(profile, "RequireSchemaPatternValidation");
        if (rule is null)
        {
            return;
        }

        ValidateRootSchemaPattern(stream, fileName, profile, schema, rule, issues, closeInput: false);
    }

    private static void ValidateRootSchemaPattern(
        Stream stream,
        string? packagePath,
        OdfComplianceProfile profile,
        OdfSchemaSet schema,
        OdfPolicyRule rule,
        List<OdfValidationIssue> issues,
        bool closeInput)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreWhitespace = true,
                MaxCharactersFromEntities = 0,
                CloseInput = closeInput
            };

            using XmlReader reader = XmlReader.Create(stream, settings);
            XDocument document = XDocument.Load(reader, LoadOptions.None);
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

            // 為了防止 foreign elements 與 attributes 干擾 schema pattern 驗證，
            // 遞迴移除不屬於 ODF 標準命名空間的元素和屬性。
            SanitizeForeignContentForSchemaValidation(document.Root);

            var body = document.Root.Element(XName.Get("body", OdfNamespaces.Office));
            if (body is not null)
            {
                if (body.Element(XName.Get("formula", OdfNamespaces.Office)) is not null ||
                    body.Element(XName.Get("database", OdfNamespaces.Office)) is not null ||
                    body.Element(XName.Get("image", OdfNamespaces.Office)) is not null ||
                    body.Element(XName.Get("chart", OdfNamespaces.Office)) is not null)
                {
                    return;
                }
            }

            // 當 schema 並非該版本的官方真實 schema 時（目前僅 ODF 1.0／Unknown，因 OASIS
            // 未發布獨立 RNG，退回以 ODF 1.4 schema 過濾近似），暫時將 root 的 version 改寫為
            // 實際 pattern 內容所屬的 1.4，以便正確匹配 pattern。原生 1.1/1.2/1.3/1.4 驗證時
            // schema 本身就是該版本的真實 schema，此處為 no-op。
            string schemaVersionString = OdfSchemaRegistry.HasNativeSchema(schema.Version)
                ? OdfVersionInfo.ToVersionString(schema.Version)
                : OdfVersionInfo.ToVersionString(OdfVersion.Odf14);
            var versionAttr = document.Root.Attribute(XName.Get("version", OdfNamespaces.Office))
                ?? document.Root.Attribute("version");
            if (versionAttr is not null && versionAttr.Value != schemaVersionString)
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
        yield return "start";

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
    }


    #endregion
}
