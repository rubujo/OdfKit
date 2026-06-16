using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Xml;
using System.Xml.Linq;
using OdfKit.Core;

namespace OdfKit.Compliance;

internal static class OdfProfileRuleValidator
{
    private static readonly string[] XmlEntries =
    [
        "content.xml",
        "styles.xml",
        "meta.xml",
        "settings.xml"
    ];

    public static void ValidatePackage(
        OdfPackage package,
        OdfComplianceProfile? profile,
        OdfSchemaSet schema,
        List<OdfValidationIssue> issues)
    {
        if (profile is null)
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
        if (profile is null || !stream.CanSeek)
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

    private static void ValidateMacroEntries(
        OdfPackage package,
        OdfComplianceProfile profile,
        List<OdfValidationIssue> issues)
    {
        OdfPolicyRule? macroRule = FindRule(profile, "DisallowMacroByDefault");
        if (macroRule is null)
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

    private class ValidationStackFrame(string namespaceUri, string localName, string qName, string xpath)
    {
        public string NamespaceUri { get; } = namespaceUri;
        public string LocalName { get; } = localName;
        public string QualifiedName { get; } = qName;
        public string XPath { get; } = xpath;

        // 追蹤此堆疊框架中已見過的子元素 QName 計數
        public Dictionary<string, int> ChildCounts { get; } = new(StringComparer.Ordinal);

        // 無障礙檢查累加器
        public bool HasAltText { get; set; }        // 若元素內找到 svg:title 或 svg:desc 則設為 true
        public bool HasTableHeaderRows { get; set; } // 若 table:table 內找到 table:table-header-rows 則設為 true
        public List<ImageInfo> ChildImages { get; } = [];
    }

    private class ImageInfo(string xpath, bool hasAltText)
    {
        public string XPath { get; } = xpath;
        public bool HasAltText { get; } = hasAltText;
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

        OdfPolicyRule? accessRule = FindRule(profile, "RequireAccessibilityMetadata");
        Stack<ValidationStackFrame> stack = [];

        using XmlReader reader = XmlReader.Create(stream, settings);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                string prefix = OdfNamespaces.GetPrefix(reader.NamespaceURI);
                if (string.IsNullOrEmpty(prefix))
                {
                    prefix = reader.Prefix;
                }
                string qName = string.IsNullOrEmpty(prefix) ? reader.LocalName : $"{prefix}:{reader.LocalName}";

                // 1. Calculate XPath
                string currentXPath;
                if (stack.Count > 0)
                {
                    var parent = stack.Peek();
                    if (!parent.ChildCounts.TryGetValue(qName, out int count))
                    {
                        count = 0;
                    }
                    count++;
                    parent.ChildCounts[qName] = count;
                    currentXPath = $"{parent.XPath}/{qName}[{count}]";
                }
                else
                {
                    currentXPath = $"/{qName}[1]";
                }

                // 2. Perform validation checks
                ValidateOdfNamespaceElement(reader, packagePath, currentXPath, profile, schema, issues);
                ValidateOdfNamespaceAttributes(reader, packagePath, currentXPath, profile, schema, issues);
                ValidateForeignExtensionElement(reader, packagePath, currentXPath, profile, issues);
                ValidateMacroOrScriptElement(reader, packagePath, currentXPath, profile, issues);
                ValidateMacroOrScriptAttributes(reader, packagePath, currentXPath, profile, issues);
                ValidateExternalResourceAttributes(reader, packagePath, currentXPath, profile, issues);

                // 3. Process accessibility metadata accumulation
                if (accessRule is not null && stack.Count > 0)
                {
                    var parent = stack.Peek();
                    if (reader.NamespaceURI == OdfNamespaces.Svg &&
                        (reader.LocalName == "title" || reader.LocalName == "desc"))
                    {
                        parent.HasAltText = true;
                    }
                    else if (reader.NamespaceURI == OdfNamespaces.Table &&
                             reader.LocalName == "table-header-rows" &&
                             parent.LocalName == "table" && parent.NamespaceUri == OdfNamespaces.Table)
                    {
                        parent.HasTableHeaderRows = true;
                    }
                }

                // 4. Handle stack push / empty elements
                if (reader.IsEmptyElement)
                {
                    if (accessRule is not null && reader.NamespaceURI == OdfNamespaces.Draw && reader.LocalName == "image")
                    {
                        if (stack.Count > 0 && stack.Peek().LocalName == "frame" && stack.Peek().NamespaceUri == OdfNamespaces.Draw)
                        {
                            stack.Peek().ChildImages.Add(new ImageInfo(currentXPath, hasAltText: false));
                        }
                        else
                        {
                            // 無替代文字的獨立圖片
                            issues.Add(new OdfValidationIssue(
                                accessRule.DefaultSeverity,
                                accessRule.Id,
                                "Image is missing alternative text (svg:title or svg:desc).",
                                packagePath,
                                currentXPath,
                                profileId: profile.Id));
                        }
                    }
                }
                else
                {
                    stack.Push(new ValidationStackFrame(reader.NamespaceURI, reader.LocalName, qName, currentXPath));
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
            {
                if (stack.Count == 0)
                    continue;
                var frame = stack.Pop();

                if (accessRule is not null)
                {
                    if (frame.NamespaceUri == OdfNamespaces.Draw && frame.LocalName == "image")
                    {
                        if (stack.Count > 0 && stack.Peek().LocalName == "frame" && stack.Peek().NamespaceUri == OdfNamespaces.Draw)
                        {
                            stack.Peek().ChildImages.Add(new ImageInfo(frame.XPath, frame.HasAltText));
                        }
                        else if (!frame.HasAltText)
                        {
                            // 無替代文字的獨立圖片
                            issues.Add(new OdfValidationIssue(
                                accessRule.DefaultSeverity,
                                accessRule.Id,
                                "Image is missing alternative text (svg:title or svg:desc).",
                                packagePath,
                                frame.XPath,
                                profileId: profile.Id));
                        }
                    }
                    else if (frame.NamespaceUri == OdfNamespaces.Draw && frame.LocalName == "frame")
                    {
                        if (!frame.HasAltText)
                        {
                            foreach (var img in frame.ChildImages)
                            {
                                if (!img.HasAltText)
                                {
                                    issues.Add(new OdfValidationIssue(
                                        accessRule.DefaultSeverity,
                                        accessRule.Id,
                                        "Image is missing alternative text (svg:title or svg:desc) on both the draw:image and its parent draw:frame.",
                                        packagePath,
                                        img.XPath,
                                        profileId: profile.Id));
                                }
                            }
                        }
                    }
                    else if (frame.NamespaceUri == OdfNamespaces.Table && frame.LocalName == "table")
                    {
                        if (!frame.HasTableHeaderRows)
                        {
                            issues.Add(new OdfValidationIssue(
                                accessRule.DefaultSeverity,
                                accessRule.Id,
                                "Table is missing a table:table-header-rows child element.",
                                packagePath,
                                frame.XPath,
                                profileId: profile.Id));
                        }
                    }
                }
            }
        }
    }

    private static OdfVersion? FindMinimumElementVersion(string namespaceUri, string localName)
    {
        OdfVersion[] versions =
        [
            OdfVersion.Odf10,
            OdfVersion.Odf11,
            OdfVersion.Odf12,
            OdfVersion.Odf13,
            OdfVersion.Odf14
        ];
        foreach (var version in versions)
        {
            var schema = OdfSchemaRegistry.GetSchema(version);
            if (schema is not null && schema.ContainsElement(namespaceUri, localName))
            {
                return version;
            }
        }
        return null;
    }

    private static OdfVersion? FindMinimumAttributeVersion(string namespaceUri, string localName)
    {
        OdfVersion[] versions =
        [
            OdfVersion.Odf10,
            OdfVersion.Odf11,
            OdfVersion.Odf12,
            OdfVersion.Odf13,
            OdfVersion.Odf14
        ];
        foreach (var version in versions)
        {
            var schema = OdfSchemaRegistry.GetSchema(version);
            if (schema is not null && schema.FindAttribute(namespaceUri, localName) is not null)
            {
                return version;
            }
        }
        return null;
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
        if (rule is null)
        {
            return;
        }

        OdfVersion? requiredVersion = FindMinimumElementVersion(reader.NamespaceURI, reader.LocalName);

        issues.Add(new OdfValidationIssue(
            rule.DefaultSeverity,
            rule.Id,
            $"ODF namespace element '{{{reader.NamespaceURI}}}{reader.LocalName}' is not present in the selected schema metadata.",
            packagePath,
            xPath,
            requiredVersion: requiredVersion,
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
        if (rule is null || !reader.HasAttributes)
        {
            return;
        }

        for (int i = 0; i < reader.AttributeCount; i++)
        {
            reader.MoveToAttribute(i);
            if (IsOdfNamespace(reader.NamespaceURI) &&
                schema.FindAttribute(reader.NamespaceURI, reader.LocalName) is null)
            {
                OdfVersion? requiredVersion = FindMinimumAttributeVersion(reader.NamespaceURI, reader.LocalName);

                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    rule.Id,
                    $"ODF namespace attribute '{{{reader.NamespaceURI}}}{reader.LocalName}' is not present in the selected schema metadata.",
                    packagePath,
                    xPath + "/@" + reader.LocalName,
                    requiredVersion: requiredVersion,
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
        if (rule is null ||
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
        if (rule is null)
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

    private static void ValidateMacroOrScriptAttributes(
        XmlReader reader,
        string? packagePath,
        string xPath,
        OdfComplianceProfile profile,
        List<OdfValidationIssue> issues)
    {
        OdfPolicyRule? macroRule = FindRule(profile, "DisallowMacroByDefault");
        if (macroRule is null || !reader.HasAttributes)
        {
            return;
        }

        for (int i = 0; i < reader.AttributeCount; i++)
        {
            reader.MoveToAttribute(i);

            string attrQName = string.IsNullOrEmpty(reader.Prefix) ? reader.LocalName : $"{reader.Prefix}:{reader.LocalName}";
            bool isScriptAttr = reader.NamespaceURI == "urn:oasis:names:tc:opendocument:xmlns:script:1.0" ||
                                string.Equals(reader.Prefix, "script", StringComparison.Ordinal) ||
                                string.Equals(attrQName, "script:event-name", StringComparison.Ordinal) ||
                                (string.Equals(reader.LocalName, "event-name", StringComparison.Ordinal) && reader.NamespaceURI == "urn:oasis:names:tc:opendocument:xmlns:script:1.0") ||
                                reader.Value.Contains("vnd.sun.star.script:");

            if (isScriptAttr)
            {
                issues.Add(new OdfValidationIssue(
                    macroRule.DefaultSeverity,
                    macroRule.Id,
                    $"Profile reports script or macro attribute '{attrQName}' with value '{reader.Value}'.",
                    packagePath,
                    xPath,
                    profileId: profile.Id));
            }
        }

        reader.MoveToElement();
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
        if (rule is null || !reader.HasAttributes)
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
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            return true;
        }

        int colonIndex = value.IndexOf(':');
        if (colonIndex > 0)
        {
            // 檢查冒號前的字元是否皆為字母
            for (int i = 0; i < colonIndex; i++)
            {
                char c = value[i];
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')))
                {
                    return false;
                }
            }
            return true;
        }

        return false;
    }

    private static bool IsStandardNamespace(string namespaceUri)
    {
        return IsOdfNamespace(namespaceUri) ||
            IsKnownInfrastructureNamespace(namespaceUri) ||
            namespaceUri == "http://www.w3.org/1998/Math/MathML" ||
            namespaceUri == "http://www.w3.org/2000/xmlns/" ||
            string.IsNullOrEmpty(namespaceUri);
    }

    private static bool IsInfrastructureOrMathNamespace(string namespaceUri)
    {
        return namespaceUri == "http://www.w3.org/1999/xlink" ||
            namespaceUri == "http://purl.org/dc/elements/1.1/" ||
            namespaceUri == "http://www.w3.org/1998/Math/MathML" ||
            namespaceUri == "http://www.w3.org/XML/1998/namespace";
    }

    private static void SanitizeForeignContentForSchemaValidation(XElement element)
    {
        var defaultSchema = OdfSchemaRegistry.DefaultOdf14;

        // 移除 foreign 屬性
        var foreignAttrs = element.Attributes()
            .Where(a => !a.IsNamespaceDeclaration &&
                        !IsInfrastructureOrMathNamespace(a.Name.NamespaceName) &&
                        (!IsStandardNamespace(a.Name.NamespaceName) ||
                         defaultSchema.FindAttribute(a.Name.NamespaceName, a.Name.LocalName) is null))
            .ToList();
        foreach (var attr in foreignAttrs)
        {
            attr.Remove();
        }

        // 遞迴處理子元素並移除 foreign 子元素
        var children = element.Elements().ToList();
        foreach (var child in children)
        {
            if (IsInfrastructureOrMathNamespace(child.Name.NamespaceName))
            {
                SanitizeForeignContentForSchemaValidation(child);
            }
            else if (!IsStandardNamespace(child.Name.NamespaceName) ||
                     defaultSchema.FindElement(child.Name.NamespaceName, child.Name.LocalName) is null)
            {
                child.Remove();
            }
            else
            {
                SanitizeForeignContentForSchemaValidation(child);
            }
        }
    }
}
