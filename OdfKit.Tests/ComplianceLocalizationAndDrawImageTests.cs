using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

public partial class ComplianceTests
{
    [Fact]
    public void SchemaPatternAttributeFrontierHandlesManyIndependentOptionals()
    {
        const string ns = "urn:example:attributes";
        OdfSchemaPatternNode[] optionalAttributes = Enumerable.Range(0, 30)
            .Select(index => new OdfSchemaPatternNode(
                OdfSchemaPatternNodeKind.Optional,
                "optional",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                nameClasses: null,
                children:
                [
                    new OdfSchemaPatternNode(
                        OdfSchemaPatternNodeKind.Attribute,
                        "optional",
                        ns,
                        $"value-{index}",
                        string.Empty,
                        string.Empty,
                        string.Empty)
                ]))
            .ToArray();
        var pattern = new OdfSchemaPatternDefinition(
            "root",
            [
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element,
                    "exactlyOne",
                    ns,
                    "root",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    nameClasses: null,
                    children: optionalAttributes)
            ]);
        var schema = new OdfSchemaSet(
            OdfVersion.Odf12,
            new Uri("https://example.invalid/attributes.rng"),
            "generated",
            [],
            [],
            [],
            [pattern]);
        XElement element = new(
            XName.Get("root", ns),
            Enumerable.Range(0, 30).Select(index => new XAttribute(XName.Get($"value-{index}", ns), index)));

        Assert.True(OdfSchemaPatternValidator.ValidateElement(element, schema, "root").IsMatch);
    }

    [Fact]
    public void SchemaPatternInterleaveFlattensReferencedInterleaveBranches()
    {
        const string ns = "urn:example:interleave";
        static OdfSchemaPatternNode Element(string localName) => new(
            OdfSchemaPatternNodeKind.Element,
            "exactlyOne",
            ns,
            localName,
            string.Empty,
            string.Empty,
            string.Empty);

        var inner = new OdfSchemaPatternDefinition(
            "inner",
            [
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Interleave,
                    "exactlyOne",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    nameClasses: null,
                    children:
                    [
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.ZeroOrMore,
                            "zeroOrMore",
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            nameClasses: null,
                            children: [Element("a")]),
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.ZeroOrMore,
                            "zeroOrMore",
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            nameClasses: null,
                            children: [Element("b")])
                    ])
            ]);
        var root = new OdfSchemaPatternDefinition(
            "root",
            [
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element,
                    "exactlyOne",
                    ns,
                    "root",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    nameClasses: null,
                    children:
                    [
                        new OdfSchemaPatternNode(
                            OdfSchemaPatternNodeKind.Interleave,
                            "exactlyOne",
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            nameClasses: null,
                            children:
                            [
                                new OdfSchemaPatternNode(
                                    OdfSchemaPatternNodeKind.Ref,
                                    "optional",
                                    string.Empty,
                                    string.Empty,
                                    "inner",
                                    string.Empty,
                                    string.Empty),
                                new OdfSchemaPatternNode(
                                    OdfSchemaPatternNodeKind.Optional,
                                    "optional",
                                    string.Empty,
                                    string.Empty,
                                    string.Empty,
                                    string.Empty,
                                    string.Empty,
                                    nameClasses: null,
                                    children: [Element("divider")])
                            ])
                    ])
            ]);
        var schema = new OdfSchemaSet(
            OdfVersion.Odf12,
            new Uri("https://example.invalid/interleave.rng"),
            "generated",
            [],
            [],
            [],
            [inner, root]);
        XElement element = new(
            XName.Get("root", ns),
            new XElement(XName.Get("a", ns)),
            new XElement(XName.Get("divider", ns)),
            new XElement(XName.Get("b", ns)));

        Assert.True(OdfSchemaPatternValidator.ValidateElement(element, schema, "root").IsMatch);
    }

    [Fact]
    public void Odf12DocumentStylesPatternAcceptsOptionalSections()
    {
        XNamespace office = OdfNamespaces.Office;
        OdfSchemaSet schema = OdfSchemaRegistry.GetSchema(OdfVersion.Odf12);
        string[] sectionNames = ["font-face-decls", "styles", "automatic-styles", "master-styles"];

        foreach (string sectionName in sectionNames)
        {
            XElement singleSection = new(
                office + "document-styles",
                new XAttribute(office + "version", "1.2"),
                new XElement(office + sectionName));
            Assert.True(OdfSchemaPatternValidator.ValidateElement(
                singleSection,
                schema,
                "office-document-styles").IsMatch,
                $"An empty office:{sectionName} section must match.");
        }

        XElement allSections = new(
            office + "document-styles",
            new XAttribute(office + "version", "1.2"),
            sectionNames.Select(sectionName => new XElement(office + sectionName)));
        Assert.True(OdfSchemaPatternValidator.ValidateElement(
            allSections,
            schema,
            "office-document-styles").IsMatch,
            "The ordered optional styles sections must match.");
    }

    [Fact]
    public void Odf12DocumentMetaPatternAcceptsOptionalMetaContent()
    {
        XNamespace office = OdfNamespaces.Office;
        XNamespace meta = OdfNamespaces.Meta;
        XNamespace dc = OdfNamespaces.Dc;
        XElement documentMeta = new(
            office + "document-meta",
            new XAttribute(office + "version", "1.2"),
            new XElement(
                office + "meta",
                new XElement(meta + "generator", "ODFDOM"),
                new XElement(
                    meta + "user-defined",
                    new XAttribute(meta + "name", "License"),
                    new XAttribute(meta + "value-type", "string"),
                    "Apache-2.0"),
                new XElement(dc + "creator", "ODFDOM"),
                new XElement(dc + "date", "2026-07-22T00:00:00"),
                new XElement(meta + "editing-cycles", "1"),
                new XElement(meta + "editing-duration", "PT0S")));

        OdfSchemaSet schema = OdfSchemaRegistry.GetSchema(OdfVersion.Odf12);
        XElement rootOnly = new(
            office + "document-meta",
            new XAttribute(office + "version", "1.2"));
        Assert.True(OdfSchemaPatternValidator.ValidateElement(
            rootOnly,
            schema,
            "office-document-meta").IsMatch,
            "A document-meta root without the optional office:meta element must match.");
        XElement emptyMeta = new(
            office + "document-meta",
            new XAttribute(office + "version", "1.2"),
            new XElement(office + "meta"));
        Assert.True(OdfSchemaPatternValidator.ValidateElement(
            emptyMeta,
            schema,
            "office-document-meta").IsMatch,
            "An empty office:meta element must match.");
        foreach (XElement child in documentMeta.Element(office + "meta")!.Elements())
        {
            XElement singleChild = new(
                office + "document-meta",
                new XAttribute(office + "version", "1.2"),
                new XElement(office + "meta", new XElement(child)));
            Assert.True(OdfSchemaPatternValidator.ValidateElement(
                singleChild,
                schema,
                "office-document-meta").IsMatch,
                $"A standalone {child.Name} metadata child must match.");
        }

        OdfSchemaPatternValidationResult result = OdfSchemaPatternValidator.ValidateElement(
            documentMeta,
            schema,
            "office-document-meta");

        Assert.True(result.IsMatch, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
    }

    [Fact]
    public void SchemaPatternNameProbeHonorsDirectChildNameClass()
    {
        var nameClass = new OdfSchemaNameClass(
            OdfSchemaNameClassKind.Name,
            OdfNamespaces.Office,
            "document-styles",
            isExcept: false);
        var nameNode = new OdfSchemaPatternNode(
            OdfSchemaPatternNodeKind.Name,
            "exactlyOne",
            OdfNamespaces.Office,
            "document-styles",
            string.Empty,
            string.Empty,
            string.Empty,
            [nameClass]);
        var elementNode = new OdfSchemaPatternNode(
            OdfSchemaPatternNodeKind.Element,
            "exactlyOne",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            nameClasses: null,
            children:
            [
                new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Choice,
                    "exactlyOne",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    nameClasses: null,
                    children: [nameNode])
            ]);
        var pattern = new OdfSchemaPatternDefinition("office-document-styles", [elementNode]);
        var schema = new OdfSchemaSet(
            OdfVersion.Odf12,
            new Uri("https://example.invalid/child-name-class.rng"),
            "generated",
            elements: [],
            attributes: [],
            nameClasses: [],
            patterns: [pattern]);

        Assert.True(OdfSchemaPatternValidator.PatternMatchesElementName(
            pattern,
            new XElement(XName.Get("document-styles", OdfNamespaces.Office)),
            schema));
        Assert.False(OdfSchemaPatternValidator.PatternMatchesElementName(
            pattern,
            new XElement(XName.Get("document-meta", OdfNamespaces.Office)),
            schema));
    }

    /// <summary>
    /// 迴歸測試：<c>draw:image</c> 的內容模型為
    /// <c>Choice(common-draw-data-attlist | office-binary-data)</c> 後接 <c>draw-text</c>。
    /// 當圖片以 <c>xlink:href</c> 外部參照（而非內嵌 <c>office:binary-data</c>）表示時，
    /// 屬性剝離邏輯曾誤將整個 choice 當作純屬性節點移除其中一個分支，使僅存的元素內容分支
    /// （office-binary-data）變成強制要求，導致任何使用外部參照的合法圖片都被誤判為不合規。
    /// </summary>
    [Fact]
    public void SchemaPatternValidator_DrawImageWithExternalHref_IsValid()
    {
        XNamespace draw = OdfNamespaces.Draw;
        XNamespace xlink = OdfNamespaces.XLink;

        var validImage = new XElement(draw + "image",
            new XAttribute(xlink + "href", "Pictures/sample.png"),
            new XAttribute(xlink + "type", "simple"),
            new XAttribute(xlink + "show", "embed"),
            new XAttribute(xlink + "actuate", "onLoad"));

        OdfSchemaSet schema = OdfSchemaRegistry.Odf14;
        OdfSchemaPatternValidationResult result =
            OdfSchemaPatternValidator.ValidateElement(validImage, schema, "draw-image");

        Assert.True(result.IsMatch);
    }

    /// <summary>
    /// 迴歸測試的反例：確保上述修正沒有讓驗證變得過度寬鬆——缺少必填 <c>xlink:href</c>
    /// 且沒有內嵌 <c>office:binary-data</c> 的 <c>draw:image</c> 仍必須判定為不合規。
    /// </summary>
    [Fact]
    public void SchemaPatternValidator_DrawImageMissingHrefAndBinaryData_IsInvalid()
    {
        XNamespace draw = OdfNamespaces.Draw;
        XNamespace xlink = OdfNamespaces.XLink;

        var brokenImage = new XElement(draw + "image",
            new XAttribute(xlink + "type", "simple"));

        OdfSchemaSet schema = OdfSchemaRegistry.Odf14;
        OdfSchemaPatternValidationResult result =
            OdfSchemaPatternValidator.ValidateElement(brokenImage, schema, "draw-image");

        Assert.False(result.IsMatch);
    }

    [Fact]
    public void LocalizerResolvesCorrectTranslationsForRegisteredLanguages()
    {
        var enFix = OdfLocalizer.GetSuggestedFix("RequireAccessibilityMetadata", new CultureInfo("en"));
        Assert.Contains("alternative text", enFix);

        var zhFix = OdfLocalizer.GetSuggestedFix("RequireAccessibilityMetadata", new CultureInfo("zh-TW"));
        Assert.Contains("為圖片加入 svg:title/svg:desc", zhFix);

        var deFix = OdfLocalizer.GetSuggestedFix("RequireAccessibilityMetadata", new CultureInfo("de"));
        Assert.Contains("Alternativtext", deFix);
    }

    [Fact]
    public void LocalizerCorrectlyFallsBackToParentCulture()
    {
        var atFix = OdfLocalizer.GetSuggestedFix("RequireAccessibilityMetadata", new CultureInfo("de-AT"));
        Assert.Contains("Alternativtext", atFix);

        var ptBrFix = OdfLocalizer.GetSuggestedFix("RequireAccessibilityMetadata", new CultureInfo("pt-BR"));
        Assert.Contains("texto alternativo", ptBrFix);
    }

    [Fact]
    public void LocalizerResolvesNewCulturesAndParentFallbacks()
    {
        var jaFix = OdfLocalizer.GetSuggestedFix("RequireAccessibilityMetadata", new CultureInfo("ja-JP"));
        Assert.Contains("代替テキスト", jaFix);

        var esFix = OdfLocalizer.GetSuggestedFix("RequireAccessibilityMetadata", new CultureInfo("es-MX"));
        Assert.Contains("texto alternativo", esFix);

        var csFix = OdfLocalizer.GetSuggestedFix("RequireAccessibilityMetadata", new CultureInfo("cs-CZ"));
        Assert.Contains("alternativní text", csFix);

        var plFix = OdfLocalizer.GetSuggestedFix("RequireAccessibilityMetadata", new CultureInfo("pl-PL"));
        Assert.Contains("tekst alternatywny", plFix);

        var ptBrFix = OdfLocalizer.GetSuggestedFix("RequireAccessibilityMetadata", new CultureInfo("pt-BR"));
        Assert.Contains("texto alternativo", ptBrFix);

        var ptPtFix = OdfLocalizer.GetSuggestedFix("RequireAccessibilityMetadata", new CultureInfo("pt-PT"));
        Assert.Contains("texto alternativo", ptPtFix);
    }

    [Fact]
    public void LocalizerFallsBackToEnglishForUnregisteredCulture()
    {
        var fix = OdfLocalizer.GetSuggestedFix("RequireAccessibilityMetadata", new CultureInfo("tr-TR"));
        Assert.Contains("alternative text", fix);
    }

    [Fact]
    public void BuiltInComplianceSuggestedFixesResolveForAllSupportedCultures()
    {
        string[] cultures = ["en", "zh-TW", "de", "fr", "nl", "nb", "pt", "it", "sk", "da", "ms", "ko", "ja", "es", "cs", "pl", "pt-BR"];
        var ruleIds = new System.Collections.Generic.SortedSet<string>(StringComparer.Ordinal);
        foreach (OdfComplianceProfile profile in OdfComplianceProfiles.BuiltIn)
        {
            foreach (var rule in profile.Rules)
            {
                ruleIds.Add(rule.Id);
            }
        }

        foreach (string cultureName in cultures)
        {
            var culture = new CultureInfo(cultureName);
            foreach (string ruleId in ruleIds)
            {
                string fix = OdfLocalizer.GetSuggestedFix(ruleId, culture);

                Assert.NotEmpty(fix);
                Assert.NotEqual(ruleId, fix);
            }
        }
    }

    [Fact]
    public void ValidatorAutoDetectsLanguageBasedOnProfileTargetCulture()
    {
        // 使用含有事件監聽器（巨集）的內容，以觸發 DisallowMacroByDefault 規則。
        string content = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:script=\"urn:oasis:names:tc:opendocument:xmlns:script:1.0\" office:version=\"1.2\">" +
                         "<office:scripts><office:event-listeners><script:event-listener script:event-name=\"dom-click\" script:language=\"ooo:script\" script:macro-name=\"MyMacro\" /></office:event-listeners></office:scripts>" +
                         "<office:body><office:text/></office:body></office:document-content>";
        using MemoryStream ms = new(Encoding.UTF8.GetBytes(content));

        OdfValidationReport reportDe = OdfFlatDocumentValidator.Validate(ms, new OdfValidationOptions { FileName = "document.fodt", Profile = OdfComplianceProfiles.DeGovernmentOdf });

        // 德國 Profile 自動偵測德文，應包含德文 SuggestedFix。
        Assert.Contains(reportDe.Issues, issue => issue.RuleId == "DisallowMacroByDefault" && (issue.SuggestedFix.Contains("Entfernen") || issue.SuggestedFix.Contains("Makros")));

        ms.Position = 0;
        OdfValidationReport reportTw = OdfFlatDocumentValidator.Validate(ms, new OdfValidationOptions { FileName = "document.fodt", Profile = OdfComplianceProfiles.RocTaiwanOdfCns15251 });

        // 臺灣 CNS 15251 Profile 自動偵測繁中，應包含繁中 SuggestedFix。
        Assert.Contains(reportTw.Issues, issue => issue.RuleId == "DisallowMacroByDefault" && (issue.SuggestedFix.Contains("移除") || issue.SuggestedFix.Contains("巨集")));
    }
}
