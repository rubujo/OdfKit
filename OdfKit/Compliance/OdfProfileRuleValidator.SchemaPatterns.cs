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

        DrawingResourceReferences references = new();
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
                    package.LoadOptions.MaxXmlCharactersInDocument,
                    references);
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
        references.Validate(profile, rule, issues);
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

        DrawingResourceReferences references = new();
        ValidateRootSchemaPattern(stream, fileName, profile, schema, rule, issues, closeInput: false, maxXmlCharacters, references);
        references.Validate(profile, rule, issues);
    }

    private static void ValidateRootSchemaPattern(
        Stream stream,
        string? packagePath,
        OdfComplianceProfile profile,
        OdfSchemaSet schema,
        OdfPolicyRule rule,
        List<OdfValidationIssue> issues,
        bool closeInput,
        long maxXmlCharacters,
        DrawingResourceReferences references)
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
                SanitizeForeignContentForSchemaValidation(
                    document.Root, schema, TextPermittedElements.GetValue(schema, BuildTextPermittedElements));
            }

            ValidateMeasureFieldScopes(document.Root, packagePath, profile, schema, rule, issues);
            ValidateWritingModes(document.Root, packagePath, profile, schema, rule, issues);
            ValidateIndexSources(document.Root, packagePath, profile, schema, rule, issues);
            ValidateFormLabels(document.Root, packagePath, profile, schema, rule, issues);
            ValidateAnimationAttributes(document.Root, packagePath, profile, schema, rule, issues);
            ValidateSettingNames(document.Root, packagePath, profile, schema, rule, issues);
            ValidateScopedAttributeConstraints(document.Root, packagePath, profile, schema, rule, issues);
            ValidateStyleCooccurrence(document.Root, packagePath, profile, schema, rule, issues);
            ValidateChartRegressionPeriods(document.Root, packagePath, profile, schema, rule, issues);
            references.Collect(document.Root, packagePath, schema.Version);

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

    private static void ValidateMeasureFieldScopes(
        XElement root,
        string? packagePath,
        OdfComplianceProfile profile,
        OdfSchemaSet schema,
        OdfPolicyRule rule,
        List<OdfValidationIssue> issues)
    {
        if (schema.Version is not (OdfVersion.Odf12 or OdfVersion.Odf13 or OdfVersion.Odf14))
            return;

        XName measureObject = XName.Get("measure", OdfNamespaces.Draw);
        XName embeddedDocument = XName.Get("document", OdfNamespaces.Office);
        foreach (XElement field in root.Descendants(XName.Get("measure", OdfNamespaces.Text)))
        {
            XElement? ancestor = field.Parent;
            // 內嵌文件不能借用外層文件的尺寸標註物件；XML 深度已由讀取器限制。
            while (ancestor is not null && ancestor.Name != measureObject && ancestor.Name != embeddedDocument)
                ancestor = ancestor.Parent;

            if (ancestor?.Name != measureObject)
            {
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3117",
                    "A text:measure field must be contained in the text of a draw:measure object.",
                    packagePath,
                    "/" + root.Name.LocalName + "//text:measure",
                    profileId: profile.Id));
            }
        }
    }

    private static void ValidateWritingModes(
        XElement root,
        string? packagePath,
        OdfComplianceProfile profile,
        OdfSchemaSet schema,
        OdfPolicyRule rule,
        List<OdfValidationIssue> issues)
    {
        if (schema.Version != OdfVersion.Odf14)
            return;

        XName writingMode = XName.Get("writing-mode", OdfNamespaces.Style);
        foreach (XElement properties in root.Descendants(XName.Get("page-layout-properties", OdfNamespaces.Style)))
        {
            if (properties.Attribute(writingMode)?.Value.Trim(' ', '\t', '\r', '\n') == "page")
            {
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3118",
                    "An ODF 1.4 page layout must not use the writing-mode value 'page'.",
                    packagePath,
                    "/" + root.Name.LocalName + "//style:page-layout-properties/@style:writing-mode",
                    profileId: profile.Id));
            }
        }

        XName namedStyle = XName.Get("style", OdfNamespaces.Style);
        XName defaultStyle = XName.Get("default-style", OdfNamespaces.Style);
        foreach (XElement style in root.Descendants())
        {
            if ((style.Name != namedStyle && style.Name != defaultStyle) ||
                style.Attribute(XName.Get("family", OdfNamespaces.Style))?.Value.Trim(' ', '\t', '\r', '\n') != "graphic")
                continue;

            string? graphic = style.Element(XName.Get("graphic-properties", OdfNamespaces.Style))?
                .Attribute(writingMode)?.Value.Trim(' ', '\t', '\r', '\n');
            string? paragraph = style.Element(XName.Get("paragraph-properties", OdfNamespaces.Style))?
                .Attribute(writingMode)?.Value.Trim(' ', '\t', '\r', '\n');
            if (graphic is not null && paragraph is not null && graphic != paragraph)
            {
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3119",
                    "An ODF 1.4 graphic style must use identical explicit writing-mode values in its graphic and paragraph properties.",
                    packagePath,
                    "/" + root.Name.LocalName + "//style:" + style.Name.LocalName,
                    profileId: profile.Id));
            }
        }
    }

    private static void ValidateIndexSources(
        XElement root,
        string? packagePath,
        OdfComplianceProfile profile,
        OdfSchemaSet schema,
        OdfPolicyRule rule,
        List<OdfValidationIssue> issues)
    {
        if (schema.Version is not (OdfVersion.Odf12 or OdfVersion.Odf13 or OdfVersion.Odf14))
            return;

        XNamespace text = OdfNamespaces.Text;
        foreach (XElement source in root.Descendants())
        {
            if (source.Name == text + "illustration-index-source" || source.Name == text + "table-index-source")
            {
                string? captions = source.Attribute(text + "use-caption")?.Value.Trim(' ', '\t', '\r', '\n');
                if ((captions is null or "true") && source.Attribute(text + "caption-sequence-name") is null)
                {
                    issues.Add(new OdfValidationIssue(
                        rule.DefaultSeverity,
                        "ODF3120",
                        "A caption-based index source must specify text:caption-sequence-name, including when captions are enabled by default.",
                        packagePath,
                        "/" + root.Name.LocalName + "//text:" + source.Name.LocalName,
                        profileId: profile.Id));
                }
            }
            else if (source.Name == text + "table-of-content-source")
            {
                CheckUnique(source, "table-of-content-entry-template", "outline-level", numeric: true, "ODF3121");
                CheckUnique(source, "index-source-styles", "outline-level", numeric: true, "ODF3123");
            }
            else if (source.Name == text + "bibliography-source")
            {
                CheckUnique(source, "bibliography-entry-template", "bibliography-type", numeric: false, "ODF3122");
            }
            else if (source.Name == text + "user-index-source")
            {
                CheckUnique(source, "index-source-styles", "outline-level", numeric: true, "ODF3123");
            }
        }

        void CheckUnique(XElement source, string childName, string keyName, bool numeric, string ruleId)
        {
            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (XElement child in source.Elements(text + childName))
            {
                string? key = child.Attribute(text + keyName)?.Value.Trim(' ', '\t', '\r', '\n');
                if (numeric && key is not null)
                    key = NormalizePositiveInteger(key);
                if (key is null || key.Length == 0 || seen.Add(key))
                    continue;

                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    ruleId,
                    $"An index source must not contain multiple '{childName}' entries with the same '{keyName}' value.",
                    packagePath,
                    "/" + root.Name.LocalName + "//text:" + source.Name.LocalName + "/text:" + childName,
                    profileId: profile.Id));
            }
        }
    }

    private static void ValidateFormLabels(
        XElement root,
        string? packagePath,
        OdfComplianceProfile profile,
        OdfSchemaSet schema,
        OdfPolicyRule rule,
        List<OdfValidationIssue> issues)
    {
        if (schema.Version is not (OdfVersion.Odf12 or OdfVersion.Odf13 or OdfVersion.Odf14))
            return;
        XNamespace form = OdfNamespaces.Form;
        Dictionary<string, XElement> labels = new(StringComparer.Ordinal);
        foreach (XElement label in root.Descendants(form + "fixed-text"))
        {
            string? targets = label.Attribute(form + "for")?.Value;
            if (targets is null)
                continue;
            bool duplicate = false;
            foreach (string item in targets.Split(','))
            {
                string target = item.Trim(' ', '\t', '\r', '\n');
                if (target.Length == 0)
                    continue;
                if (labels.TryGetValue(target, out XElement? previous))
                    duplicate |= !ReferenceEquals(previous, label);
                else
                    labels.Add(target, label);
            }
            if (duplicate)
            {
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3124",
                    "A form control must not be associated with more than one fixed-text label.",
                    packagePath,
                    "/" + root.Name.LocalName + "//form:fixed-text/@form:for",
                    profileId: profile.Id));
            }
        }
    }

    private static void ValidateAnimationAttributes(
        XElement root,
        string? packagePath,
        OdfComplianceProfile profile,
        OdfSchemaSet schema,
        OdfPolicyRule rule,
        List<OdfValidationIssue> issues)
    {
        if (schema.Version is not (OdfVersion.Odf12 or OdfVersion.Odf13 or OdfVersion.Odf14))
            return;
        XNamespace anim = OdfNamespaces.Anim;
        foreach (XElement element in root.Descendants())
        {
            if (element.Name.Namespace != anim)
                continue;
            if (element.Name.LocalName == "audio" && element.Attribute(anim + "audio-level") is { } volume)
            {
                double level;
                bool parsed = true;
                try
                {
                    level = XmlConvert.ToDouble(volume.Value);
                }
                catch (FormatException)
                {
                    // 不合法的 double 詞法由 grammar 回報；仍需檢查獨立的識別碼義務。
                    level = 0;
                    parsed = false;
                }
                catch (OverflowException)
                {
                    level = double.PositiveInfinity;
                }
                if (parsed && (double.IsNaN(level) || level < 0 || level > 1))
                {
                    issues.Add(new OdfValidationIssue(
                        rule.DefaultSeverity,
                        "ODF3125",
                        "Animation audio volume must be a finite value from 0 through 1, inclusive.",
                        packagePath,
                        "/" + root.Name.LocalName + "//anim:audio/@anim:audio-level",
                        profileId: profile.Id));
                }
            }
            if (element.Name.LocalName is not ("audio" or "command" or "iterate" or "par" or "seq"))
                continue;
            string? legacyId = element.Attribute(anim + "id")?.Value.Trim(' ', '\t', '\r', '\n');
            string? xmlId = element.Attribute(XNamespace.Xml + "id")?.Value.Trim(' ', '\t', '\r', '\n');
            if (legacyId is not null && xmlId is not null && legacyId != xmlId)
            {
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3126",
                    "Simultaneous anim:id and xml:id attributes must identify the same animation element.",
                    packagePath,
                    "/" + root.Name.LocalName + "//anim:" + element.Name.LocalName + "/@anim:id",
                    profileId: profile.Id));
            }
        }
    }

    private static void ValidateSettingNames(
        XElement root,
        string? packagePath,
        OdfComplianceProfile profile,
        OdfSchemaSet schema,
        OdfPolicyRule rule,
        List<OdfValidationIssue> issues)
    {
        if (schema.Version is not (OdfVersion.Odf12 or OdfVersion.Odf13 or OdfVersion.Odf14))
            return;
        XNamespace config = OdfNamespaces.Config;
        foreach (XElement settings in root.Descendants(XName.Get("settings", OdfNamespaces.Office)))
            foreach (XElement itemSet in settings.Elements(config + "config-item-set"))
            {
                string? name = itemSet.Attribute(config + "name")?.Value;
                if (name is null)
                    continue;
                int separator = name.IndexOf(':');
                bool valid = false;
                if (separator > 0)
                {
                    string prefix = name.Substring(0, separator);
                    try
                    {
                        XmlConvert.VerifyNCName(prefix);
                        valid = prefix != "xmlns" && (itemSet.Annotation<OdfSchemaNamespaceContext>() is { } originalContext
                            ? originalContext.ResolvePrefix(prefix)
                            : itemSet.GetNamespaceOfPrefix(prefix)) is not null;
                    }
                    catch (XmlException)
                    {
                        // 只驗證前綴；冒號後是應用程式定義的 string，不額外施加 QName 限制。
                    }
                }
                if (!valid)
                {
                    issues.Add(new OdfValidationIssue(
                        rule.DefaultSeverity,
                        "ODF3127",
                        "A config-item-set directly within office:settings must have a name beginning with a bound namespace prefix and a colon.",
                        packagePath,
                        "/" + root.Name.LocalName + "//office:settings/config:config-item-set/@config:name",
                        profileId: profile.Id));
                }
            }
    }

    private static void ValidateStyleCooccurrence(
        XElement root,
        string? packagePath,
        OdfComplianceProfile profile,
        OdfSchemaSet schema,
        OdfPolicyRule rule,
        List<OdfValidationIssue> issues)
    {
        if (schema.Version is not (OdfVersion.Odf12 or OdfVersion.Odf13 or OdfVersion.Odf14))
            return;
        XNamespace style = OdfNamespaces.Style;
        XNamespace draw = OdfNamespaces.Draw;
        foreach (XElement element in root.Descendants())
        {
            if (element.Name == style + "page-layout-properties" &&
                element.Attribute(style + "layout-grid-ruby-height") is not null &&
                element.Attribute(style + "layout-grid-standard-mode")?.Value.Trim(' ', '\t', '\r', '\n') != "false")
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3131",
                    "A ruby grid height requires layout-grid-standard-mode explicitly set to false on the same page-layout-properties element.",
                    packagePath,
                    "/" + root.Name.LocalName + "//style:page-layout-properties/@style:layout-grid-ruby-height",
                    profileId: profile.Id));
            if (element.Name == style + "graphic-properties" &&
                (element.Attribute(draw + "visible-area-left") is not null ||
                 element.Attribute(draw + "visible-area-top") is not null ||
                 element.Attribute(draw + "visible-area-width") is not null ||
                 element.Attribute(draw + "visible-area-height") is not null) &&
                (element.Parent?.Name != style + "style" ||
                 element.Parent.Parent?.Name != XName.Get("automatic-styles", OdfNamespaces.Office)))
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3132",
                    "Visible area attributes may only be assigned to automatic styles.",
                    packagePath,
                    "/" + root.Name.LocalName + "//style:graphic-properties",
                    profileId: profile.Id));
        }
    }

    private static void ValidateChartRegressionPeriods(
        XElement root,
        string? packagePath,
        OdfComplianceProfile profile,
        OdfSchemaSet schema,
        OdfPolicyRule rule,
        List<OdfValidationIssue> issues)
    {
        if (schema.Version is not (OdfVersion.Odf13 or OdfVersion.Odf14))
            return;
        XNamespace chart = OdfNamespaces.Chart;
        foreach (XElement properties in root.Descendants(XName.Get("chart-properties", OdfNamespaces.Style)))
            if (properties.Attribute(chart + "regression-period") is { } period &&
                NormalizePositiveInteger(period.Value.Trim(' ', '\t', '\r', '\n')) == "1")
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3133",
                    "The minimum value of chart:regression-period is two.",
                    packagePath,
                    "/" + root.Name.LocalName + "//style:chart-properties/@chart:regression-period",
                    profileId: profile.Id));
    }

    private static string? NormalizePositiveInteger(string value)
    {
        int start = value.Length > 0 && value[0] == '+' ? 1 : 0;
        if (start == value.Length)
            return null;
        for (int index = start; index < value.Length; index++)
        {
            if (value[index] < '0' || value[index] > '9')
                return null;
        }
        while (start < value.Length && value[start] == '0')
            start++;
        // positiveInteger 的無效零值由 grammar 回報，不新增整數大小或位數限制。
        return start == value.Length ? null : value.Substring(start);
    }

    private static void ValidateScopedAttributeConstraints(
        XElement root,
        string? packagePath,
        OdfComplianceProfile profile,
        OdfSchemaSet schema,
        OdfPolicyRule rule,
        List<OdfValidationIssue> issues)
    {
        if (schema.Version is not (OdfVersion.Odf12 or OdfVersion.Odf13 or OdfVersion.Odf14))
            return;
        XNamespace style = OdfNamespaces.Style;
        XNamespace text = OdfNamespaces.Text;
        XNamespace table = OdfNamespaces.Table;
        foreach (XElement element in root.Descendants())
        {
            // 設定項目的 RNG 只有 text；宣告型別與內容的對應限制由規範文字補足。
            if (element.Name == XName.Get("config-item", OdfNamespaces.Config) &&
                element.Attribute(XName.Get("type", OdfNamespaces.Config)) is XAttribute settingType &&
                !OdfSchemaPatternValidator.MatchesConfigurationValue(settingType.Value, element.Value))
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3142",
                    "A config:config-item value must match the datatype declared by config:type.",
                    packagePath,
                    "/" + root.Name.LocalName + "//config:config-item",
                    profileId: profile.Id));
            // value-range 的 RNG 共用 string 宣告；規範文字仍要求 value 使用 double。
            if (element.Name == XName.Get("value-range", OdfNamespaces.Form) &&
                element.Attribute(XName.Get("value", OdfNamespaces.Form)) is XAttribute rangeValue &&
                !OdfSchemaPatternValidator.IsXmlFloatingPoint(rangeValue.Value.Trim(' ', '\t', '\r', '\n')))
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3141",
                    "The form:value attribute of form:value-range must have the double datatype.",
                    packagePath,
                    "/" + root.Name.LocalName + "//form:value-range/@form:value",
                    profileId: profile.Id));
            // 框架的直接內容共用框架描述；文字方塊內的獨立表格仍保留自身描述。
            if (element.Name.Namespace == table && element.Name.LocalName is "title" or "desc" &&
                element.Parent is XElement parentTable && parentTable.Name == table + "table" &&
                parentTable.Parent?.Name == XName.Get("frame", OdfNamespaces.Draw))
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3138",
                    "A table that is a direct child of draw:frame must use the frame's svg:title and svg:desc instead of table:title or table:desc.",
                    packagePath,
                    "/" + root.Name.LocalName + "//table:table/table:" + element.Name.LocalName,
                    profileId: profile.Id));
            if (element.Name.Namespace == text && element.Name.LocalName is "alphabetical-index-mark" or "alphabetical-index-mark-start" &&
                element.Attribute(text + "key2") is not null && element.Attribute(text + "key1") is null)
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3137",
                    "An alphabetical index mark with a secondary key must also specify its primary key.",
                    packagePath,
                    "/" + root.Name.LocalName + "//" + element.Name.LocalName + "/@text:key2",
                    profileId: profile.Id));
            XAttribute? relativeLength = element.Name == style + "column"
                ? element.Attribute(style + "rel-width")
                : element.Name == style + "table-column-properties"
                    ? element.Attribute(style + "rel-column-width")
                    : null;
            if (relativeLength is not null && IsZeroRelativeLength(relativeLength.Value))
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3135",
                    "A relativeLength value must contain a positive integer before the asterisk.",
                    packagePath,
                    "/" + root.Name.LocalName + "//" + element.Name.LocalName + "/@" + relativeLength.Name.LocalName,
                    profileId: profile.Id));
            foreach (XAttribute attribute in element.Attributes())
                if (ViolatesAngleDatatype(attribute) && schema.ContainsElement(element.Name.NamespaceName, element.Name.LocalName))
                    issues.Add(new OdfValidationIssue(
                        rule.DefaultSeverity,
                        "ODF3136",
                        "An angle value must contain a number with an optional adjacent deg, grad or rad unit.",
                        packagePath,
                        "/" + root.Name.LocalName + "//" + element.Name.LocalName + "/@" + attribute.Name.LocalName,
                        profileId: profile.Id));
        }
        XNamespace meta = OdfNamespaces.Meta;
        HashSet<string> metadataNames = new(StringComparer.Ordinal);
        foreach (XElement property in root.Descendants(meta + "user-defined"))
            if (property.Attribute(meta + "name") is { } name && !metadataNames.Add(name.Value))
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3134",
                    "User-defined metadata names must be unique within the document instance.",
                    packagePath,
                    "/" + root.Name.LocalName + "//meta:user-defined/@meta:name",
                    profileId: profile.Id));
        XNamespace form = OdfNamespaces.Form;
        foreach (XElement container in root.Descendants(form + "form"))
        {
            HashSet<string> defaultSelections = new(StringComparer.Ordinal);
            HashSet<string> currentSelections = new(StringComparer.Ordinal);
            foreach (XElement radio in container.Elements(form + "radio"))
            {
                XAttribute? name = radio.Attribute(form + "name");
                if (name is null)
                    continue;
                if (radio.Attribute(form + "selected")?.Value.Trim(' ', '\t', '\r', '\n') == "true" && !defaultSelections.Add(name.Value))
                    issues.Add(new OdfValidationIssue(
                        rule.DefaultSeverity,
                        "ODF3139",
                        "Radio buttons with the same name within a form must not have more than one default selection.",
                        packagePath,
                        "/" + root.Name.LocalName + "//form:radio/@form:selected",
                        profileId: profile.Id));
                if (radio.Attribute(form + "current-selected")?.Value.Trim(' ', '\t', '\r', '\n') == "true" && !currentSelections.Add(name.Value))
                    issues.Add(new OdfValidationIssue(
                        rule.DefaultSeverity,
                        "ODF3140",
                        "Radio buttons with the same name within a form must not have more than one current selection.",
                        packagePath,
                        "/" + root.Name.LocalName + "//form:radio/@form:current-selected",
                        profileId: profile.Id));
            }
        }
        foreach (XElement container in root.Descendants(form + "properties"))
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (XElement property in container.Elements())
            {
                if (property.Name != form + "property" && property.Name != form + "list-property")
                    continue;
                if (property.Attribute(form + "property-name") is { } name && !names.Add(name.Value))
                    issues.Add(new OdfValidationIssue(
                        rule.DefaultSeverity,
                        "ODF3128",
                        "Property names must be unique across property and list-property children of the same form:properties element.",
                        packagePath,
                        "/" + root.Name.LocalName + "//form:properties/*/@form:property-name",
                        profileId: profile.Id));
            }
        }
        XNamespace draw = OdfNamespaces.Draw;
        HashSet<string> pageNames = new(StringComparer.Ordinal);
        foreach (XElement page in root.Descendants(draw + "page"))
            if (page.Attribute(draw + "name") is { } name && !pageNames.Add(name.Value))
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3129",
                    "Drawing page names must be unique within the document instance.",
                    packagePath,
                    "/" + root.Name.LocalName + "//draw:page/@draw:name",
                    profileId: profile.Id));
        if (schema.Version == OdfVersion.Odf12)
            return;
        XNamespace dr3d = OdfNamespaces.Dr3d;
        foreach (XElement element in root.Descendants())
        {
            if (element.Attribute(draw + "z-index") is null || element.Parent is not { } parent ||
                parent.Name != draw + "g" && parent.Name != dr3d + "scene")
                continue;
            bool isShape = element.Name.Namespace == draw
                ? element.Name.LocalName is "caption" or "circle" or "connector" or "control" or "custom-shape" or
                    "ellipse" or "frame" or "g" or "line" or "measure" or "page-thumbnail" or "path" or "polygon" or
                    "polyline" or "rect" or "regular-polygon"
                : element.Name.Namespace == dr3d && element.Name.LocalName is "cube" or "extrude" or "rotate" or "scene" or "sphere";
            if (isShape)
                issues.Add(new OdfValidationIssue(
                    rule.DefaultSeverity,
                    "ODF3130",
                    "Shapes that are direct children of draw:g or dr3d:scene must not have a draw:z-index attribute in ODF 1.3 or later.",
                    packagePath,
                    "/" + root.Name.LocalName + "//" + element.Name.LocalName + "/@draw:z-index",
                    profileId: profile.Id));
        }
    }

    private static bool ViolatesAngleDatatype(XAttribute attribute)
    {
        string ns = attribute.Name.NamespaceName;
        string name = attribute.Name.LocalName;
        bool single = ns == OdfNamespaces.Draw && name is "start-angle" or "end-angle" or "angle" or "text-rotate-angle" or "rotation" or "caption-angle" ||
            ns == OdfNamespaces.Style && name is "rotation-angle" or "text-rotation-angle" ||
            ns == OdfNamespaces.Dr3d && name is "shadow-slant" or "end-angle" ||
            ns == OdfNamespaces.Chart && name == "angle-offset";
        if (single)
            return !OdfKit.DOM.OdfAngle.TryParse(attribute.Value, out _);
        if (ns != OdfNamespaces.Draw || name is not ("extrusion-rotation-angle" or "extrusion-skew"))
            return false;
        string[] tokens = attribute.Value.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length != 2)
            return false;
        return !OdfKit.DOM.OdfAngle.TryParse(tokens[1], out _) ||
            name == "extrusion-rotation-angle" && !OdfKit.DOM.OdfAngle.TryParse(tokens[0], out _);
    }

    private static bool IsZeroRelativeLength(string value)
    {
        if (value.Length < 2 || value[value.Length - 1] != '*')
            return false;
        for (int index = 0; index < value.Length - 1; index++)
            if (value[index] != '0')
                return false;
        return true;
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
