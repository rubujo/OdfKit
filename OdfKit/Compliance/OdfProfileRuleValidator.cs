using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Xml;
using System.Xml.Linq;
using OdfKit.Core;

namespace OdfKit.Compliance
{
    internal static class OdfProfileRuleValidator
    {
        private static readonly string[] XmlEntries =
        {
            "content.xml",
            "styles.xml",
            "meta.xml",
            "settings.xml"
        };

        public static void ValidatePackage(
            OdfPackage package,
            OdfComplianceProfile? profile,
            OdfSchemaSet schema,
            List<OdfValidationIssue> issues)
        {
            if (profile == null)
            {
                return;
            }

            ValidateMacroEntries(package, profile, issues);
            ValidatePackageSchemaPatterns(package, profile, schema, issues);

            foreach (string entryName in GetProfileXmlEntries(package))
            {
                if (!package.HasEntry(entryName))
                {
                    continue;
                }

                try
                {
                    using Stream stream = package.GetEntryStream(entryName);
                    ScanXml(stream, entryName, profile, schema, issues, closeInput: true);
                }
                catch (IOException ex)
                {
                    issues.Add(new OdfValidationIssue(
                        OdfIssueSeverity.Fatal,
                        "ODF0302",
                        $"ODF XML entry cannot be read for profile checks: {ex.Message}",
                        entryName,
                        profileId: profile.Id));
                }
                catch (SecurityException ex)
                {
                    issues.Add(new OdfValidationIssue(
                        OdfIssueSeverity.Fatal,
                        "ODF0303",
                        $"ODF XML entry failed security validation during profile checks: {ex.Message}",
                        entryName,
                        profileId: profile.Id));
                }
            }
        }

        public static void ValidateFlatXml(
            Stream stream,
            string? fileName,
            OdfComplianceProfile? profile,
            OdfSchemaSet schema,
            List<OdfValidationIssue> issues)
        {
            if (profile == null || !stream.CanSeek)
            {
                return;
            }

            long originalPosition = stream.Position;
            stream.Position = 0;

            try
            {
                ScanXml(stream, fileName, profile, schema, issues, closeInput: false);
                stream.Position = 0;
                ValidateFlatSchemaPattern(stream, fileName, profile, schema, issues);
            }
            finally
            {
                stream.Position = originalPosition;
            }
        }

        private static void ValidatePackageSchemaPatterns(
            OdfPackage package,
            OdfComplianceProfile profile,
            OdfSchemaSet schema,
            List<OdfValidationIssue> issues)
        {
            OdfPolicyRule? rule = FindRule(profile, "RequireSchemaPatternValidation");
            if (rule == null)
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
            var seen = new HashSet<string>(StringComparer.Ordinal);
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
            if (rule == null)
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
                if (document.Root == null)
                {
                    issues.Add(new OdfValidationIssue(
                        rule.DefaultSeverity,
                        "ODF3102",
                        "ODF XML document does not contain a root element for schema pattern validation.",
                        packagePath,
                        profileId: profile.Id));
                    return;
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
                    if (pattern != null)
                    {
                        OdfSchemaPatternDefinition? defaultPattern = defaultSchema.FindPattern(name);
                        bool isCustom = defaultPattern == null || pattern != defaultPattern;

                        if (isCustom && OdfSchemaPatternValidator.PatternMatchesElementName(pattern, document.Root, schema))
                        {
                            targetPatternName = name;
                            break;
                        }
                    }
                }

                if (targetPatternName == null)
                {
                    foreach (string name in patternNames)
                    {
                        OdfSchemaPatternDefinition? pattern = schema.FindPattern(name);
                        if (pattern != null && OdfSchemaPatternValidator.PatternMatchesElementName(pattern, document.Root, schema))
                        {
                            targetPatternName = name;
                            break;
                        }
                    }
                }

                if (targetPatternName == null)
                {
                    targetPatternName = patternNames[0];
                }

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
            var patternNames = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string candidate in GetRootPatternCandidates(root, packagePath))
            {
                if (seen.Add(candidate) && schema.FindPattern(candidate) != null)
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
            if (packagePath != null)
            {
                string? fileName = packagePath.Replace('\\', '/').Split('/').LastOrDefault();
                fileStem = fileName == null
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

        private static void ValidateMacroEntries(
            OdfPackage package,
            OdfComplianceProfile profile,
            List<OdfValidationIssue> issues)
        {
            OdfPolicyRule? macroRule = FindRule(profile, "DisallowMacroByDefault");
            if (macroRule == null)
            {
                return;
            }

            foreach (string entryName in package.Entries.Keys)
            {
                if (entryName.StartsWith("Basic/", StringComparison.OrdinalIgnoreCase) ||
                    entryName.StartsWith("Scripts/", StringComparison.OrdinalIgnoreCase) ||
                    entryName.EndsWith("macrosignatures.xml", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new OdfValidationIssue(
                        macroRule.DefaultSeverity,
                        macroRule.Id,
                        "Profile reports embedded macro or script package content.",
                        entryName,
                        profileId: profile.Id));
                }
            }
        }

        private static void ScanXml(
            Stream stream,
            string? packagePath,
            OdfComplianceProfile profile,
            OdfSchemaSet schema,
            List<OdfValidationIssue> issues,
            bool closeInput)
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
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                string xPath = "/" + reader.LocalName;
                ValidateOdfNamespaceElement(reader, packagePath, xPath, profile, schema, issues);
                ValidateOdfNamespaceAttributes(reader, packagePath, xPath, profile, schema, issues);
                ValidateForeignExtensionElement(reader, packagePath, xPath, profile, issues);
                ValidateMacroOrScriptElement(reader, packagePath, xPath, profile, issues);
                ValidateExternalResourceAttributes(reader, packagePath, xPath, profile, issues);
            }
        }

        private static void ValidateOdfNamespaceElement(
            XmlReader reader,
            string? packagePath,
            string xPath,
            OdfComplianceProfile profile,
            OdfSchemaSet schema,
            List<OdfValidationIssue> issues)
        {
            if (!IsOdfNamespace(reader.NamespaceURI) || schema.ContainsElement(reader.NamespaceURI, reader.LocalName))
            {
                return;
            }

            OdfPolicyRule? rule = FindRule(profile, "DisallowInvalidOdfNamespaceExtensions") ??
                FindRule(profile, "RequireOdfNamespaceValidity");
            if (rule == null)
            {
                return;
            }

            issues.Add(new OdfValidationIssue(
                rule.DefaultSeverity,
                rule.Id,
                $"ODF namespace element '{{{reader.NamespaceURI}}}{reader.LocalName}' is not present in the selected schema metadata.",
                packagePath,
                xPath,
                profileId: profile.Id));
        }

        private static void ValidateOdfNamespaceAttributes(
            XmlReader reader,
            string? packagePath,
            string xPath,
            OdfComplianceProfile profile,
            OdfSchemaSet schema,
            List<OdfValidationIssue> issues)
        {
            OdfPolicyRule? rule = FindRule(profile, "DisallowInvalidOdfNamespaceExtensions") ??
                FindRule(profile, "RequireOdfNamespaceValidity");
            if (rule == null || !reader.HasAttributes)
            {
                return;
            }

            for (int i = 0; i < reader.AttributeCount; i++)
            {
                reader.MoveToAttribute(i);
                if (IsOdfNamespace(reader.NamespaceURI) &&
                    schema.FindAttribute(reader.NamespaceURI, reader.LocalName) == null)
                {
                    issues.Add(new OdfValidationIssue(
                        rule.DefaultSeverity,
                        rule.Id,
                        $"ODF namespace attribute '{{{reader.NamespaceURI}}}{reader.LocalName}' is not present in the selected schema metadata.",
                        packagePath,
                        xPath + "/@" + reader.LocalName,
                        profileId: profile.Id));
                }
            }

            reader.MoveToElement();
        }

        private static void ValidateForeignExtensionElement(
            XmlReader reader,
            string? packagePath,
            string xPath,
            OdfComplianceProfile profile,
            List<OdfValidationIssue> issues)
        {
            OdfPolicyRule? rule = FindRule(profile, "RequireForeignExtensionIsolation");
            if (rule == null ||
                string.IsNullOrEmpty(reader.NamespaceURI) ||
                IsOdfNamespace(reader.NamespaceURI) ||
                IsKnownInfrastructureNamespace(reader.NamespaceURI))
            {
                return;
            }

            issues.Add(new OdfValidationIssue(
                rule.DefaultSeverity,
                rule.Id,
                $"Foreign extension element '{{{reader.NamespaceURI}}}{reader.LocalName}' should remain isolated and removable.",
                packagePath,
                xPath,
                profileId: profile.Id));
        }

        private static void ValidateMacroOrScriptElement(
            XmlReader reader,
            string? packagePath,
            string xPath,
            OdfComplianceProfile profile,
            List<OdfValidationIssue> issues)
        {
            OdfPolicyRule? macroRule = FindRule(profile, "DisallowMacroByDefault");
            OdfPolicyRule? resourceRule = FindRule(profile, "RequireSafeExternalResourcePolicy");
            OdfPolicyRule? rule = macroRule ?? resourceRule;
            if (rule == null)
            {
                return;
            }

            bool isScriptElement = reader.LocalName.IndexOf("script", StringComparison.OrdinalIgnoreCase) >= 0 ||
                reader.LocalName.IndexOf("event-listener", StringComparison.OrdinalIgnoreCase) >= 0 ||
                reader.NamespaceURI.IndexOf(":script:", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isScriptElement)
            {
                return;
            }

            issues.Add(new OdfValidationIssue(
                rule.DefaultSeverity,
                rule.Id,
                "Profile reports script or event-listener XML content.",
                packagePath,
                xPath,
                profileId: profile.Id));
        }

        private static void ValidateExternalResourceAttributes(
            XmlReader reader,
            string? packagePath,
            string xPath,
            OdfComplianceProfile profile,
            List<OdfValidationIssue> issues)
        {
            OdfPolicyRule? rule = FindRule(profile, "RequireSafeExternalResourcePolicy") ??
                FindRule(profile, "RequireCrossBorderInteroperability");
            if (rule == null || !reader.HasAttributes)
            {
                return;
            }

            for (int i = 0; i < reader.AttributeCount; i++)
            {
                reader.MoveToAttribute(i);
                if (reader.NamespaceURI == OdfNamespaces.XLink &&
                    string.Equals(reader.LocalName, "href", StringComparison.Ordinal) &&
                    IsExternalReference(reader.Value))
                {
                    issues.Add(new OdfValidationIssue(
                        rule.DefaultSeverity,
                        rule.Id,
                        $"Profile reports external resource reference '{reader.Value}'.",
                        packagePath,
                        xPath,
                        profileId: profile.Id));
                }
            }

            reader.MoveToElement();
        }

        private static OdfPolicyRule? FindRule(OdfComplianceProfile profile, string id)
        {
            return profile.Rules.FirstOrDefault(rule => string.Equals(rule.Id, id, StringComparison.Ordinal));
        }

        private static bool IsOdfNamespace(string namespaceUri)
        {
            return namespaceUri.StartsWith("urn:oasis:names:tc:opendocument:xmlns:", StringComparison.Ordinal);
        }

        private static bool IsKnownInfrastructureNamespace(string namespaceUri)
        {
            return namespaceUri == OdfNamespaces.XLink ||
                namespaceUri == OdfNamespaces.Dc ||
                namespaceUri == OdfNamespaces.Ds ||
                namespaceUri == "http://www.w3.org/2001/XMLSchema-instance";
        }

        private static bool IsExternalReference(string value)
        {
            return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("//", StringComparison.Ordinal);
        }
    }
}
