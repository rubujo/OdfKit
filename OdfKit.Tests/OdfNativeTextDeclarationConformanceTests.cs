using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests annotation initials and presentation text declarations in native documents.
/// 在原生文件中測試註解作者縮寫及簡報文字宣告。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfNativeTextDeclarationConformanceTests
{
    private static readonly string[] Values = ["", " ", " \t\n ", "文字😀<&>"];
    private static readonly string[] DeclarationKinds = ["header-decl", "footer-decl"];
    private static readonly string[] PresentationNameKinds = ["header-decl", "footer-decl", "date-time-decl", "show"];
    private static readonly string[] Names = ["", "Header Name", "頁首😀\t\r\n"];

    /// <summary>
    /// Annotation initials preserve native character data in the versions that define them.
    /// 註解作者縮寫在有定義的版本中保留原生字元資料。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void AnnotationInitialsPreserveNativeValues(OdfVersion version)
    {
        foreach (string value in Values)
        {
            XElement document = Annotation(version, value);
            AssertNativeRoundTrip(document, version, XName.Get("creator-initials", OdfNamespaces.Meta), value);
        }
    }

    /// <summary>
    /// Initials reject native attributes, child elements, incorrect placement and unsupported versions.
    /// 作者縮寫拒絕原生屬性、子元素、錯誤位置及未支援的版本。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void AnnotationInitialsUseTheirDefinedContext(OdfVersion version)
    {
        if (version == OdfVersion.Odf12)
        {
            AssertSchemaInvalid(Annotation(version, "AB"), version);
            return;
        }
        foreach (int mutation in new[] { 0, 1, 2 })
        {
            XElement document = Annotation(version, "AB");
            XElement initials = document.Descendants(XName.Get("creator-initials", OdfNamespaces.Meta)).Single();
            if (mutation == 0)
                initials.SetAttributeValue(XName.Get("name", OdfNamespaces.Meta), "invalid");
            else if (mutation == 1)
                initials.Add(new XElement(XName.Get("span", OdfNamespaces.Text), "child"));
            else
            {
                initials.Remove();
                document.AddFirst(new XElement(XName.Get("meta", OdfNamespaces.Office), initials));
            }
            AssertSchemaInvalid(document, version);
        }
    }

    /// <summary>
    /// Header and footer declarations retain both their string names and text values.
    /// 頁首與頁尾宣告保留字串名稱及文字值。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void PresentationDeclarationsPreserveNamesAndText(OdfVersion version)
    {
        XNamespace presentation = OdfNamespaces.Presentation;
        foreach (string kind in DeclarationKinds)
            foreach (string name in Names)
                foreach (string value in Values)
                {
                    XElement declaration = new(presentation + kind, new XAttribute(presentation + "name", name), value);
                    XElement document = Document(version, "presentation", declaration);
                    XElement saved = AssertNativeRoundTrip(document, version, presentation + kind, value);
                    Assert.Equal(name, (string?)saved.Descendants(presentation + kind).Single().Attribute(presentation + "name"));
                }
    }

    /// <summary>
    /// Native declaration names are required, and declarations have no native child elements or other attributes.
    /// 原生宣告名稱為必要屬性，宣告沒有原生子元素或其他屬性。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void PresentationDeclarationsRejectInvalidStructure(OdfVersion version)
    {
        XNamespace presentation = OdfNamespaces.Presentation;
        foreach (string kind in DeclarationKinds)
            foreach (int mutation in new[] { 0, 1, 2, 3 })
            {
                XElement declaration = new(presentation + kind, new XAttribute(presentation + "name", "N"), "value");
                if (mutation == 0)
                {
                    declaration.Attribute(presentation + "name")!.Remove();
                    declaration.SetAttributeValue(XName.Get("name", "urn:test:foreign"), "N");
                }
                else if (mutation == 1)
                    declaration.SetAttributeValue(presentation + "source", "fixed");
                else if (mutation == 2)
                    declaration.Add(new XElement(XName.Get("span", OdfNamespaces.Text), "child"));
                XElement document = mutation == 3
                    ? Document(version, "text", new XElement(XName.Get("p", OdfNamespaces.Text), declaration))
                    : Document(version, "presentation", declaration);
                AssertSchemaInvalid(document, version);
            }
    }

    /// <summary>
    /// Presentation names use strings in all four native carriers without NCName restrictions.
    /// 簡報名稱在四種原生元素中使用字串，不加上 NCName 限制。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void PresentationNamesUseStringsAcrossTheirNativeCarriers(OdfVersion version)
    {
        OdfSchemaSet schema = OdfSchemaRegistry.GetSchema(version);
        XNamespace presentation = OdfNamespaces.Presentation;
        foreach (string kind in PresentationNameKinds)
            foreach (string name in Names)
            {
                XElement element = new(presentation + kind, new XAttribute(presentation + "name", name));
                if (kind == "date-time-decl")
                    element.SetAttributeValue(presentation + "source", "fixed");
                if (kind == "show")
                    element.SetAttributeValue(presentation + "pages", "Page1");
                else
                    element.Value = "text";
                string pattern = kind == "show" ? "presentation-show" : "presentation-decl";
                Assert.True(OdfSchemaPatternValidator.ValidateElement(element, schema, pattern).IsMatch);
                OdfNode dom = OdfXmlReader.Parse(Encoding.UTF8.GetBytes(element.ToString(SaveOptions.DisableFormatting)));
                Assert.Equal(name, dom.GetAttribute("name", OdfNamespaces.Presentation));
                using MemoryStream output = new();
                OdfXmlWriter.Write(dom, output);
                XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()), LoadOptions.PreserveWhitespace);
                Assert.Equal(name, (string?)saved.Attribute(presentation + "name"));
                Assert.True(OdfSchemaPatternValidator.ValidateElement(saved, schema, pattern).IsMatch);
                saved.Attribute(presentation + "name")!.Remove();
                saved.SetAttributeValue(XName.Get("name", "urn:test:foreign"), name);
                Assert.False(OdfSchemaPatternValidator.ValidateElement(saved, schema, pattern).IsMatch);
            }
        XElement wrong = Document(version, "text", new XElement(XName.Get("p", OdfNamespaces.Text),
            new XElement(XName.Get("span", OdfNamespaces.Text), new XAttribute(presentation + "name", "N"), "text")));
        AssertSchemaInvalid(wrong, version);
    }

    private static XElement Annotation(OdfVersion version, string value) =>
        Document(version, "text", new XElement(XName.Get("p", OdfNamespaces.Text),
            new XElement(XName.Get("annotation", OdfNamespaces.Office),
                new XElement(XName.Get("creator-initials", OdfNamespaces.Meta), value),
                new XElement(XName.Get("p", OdfNamespaces.Text), "Comment"))));

    private static XElement Document(OdfVersion version, string kind, XElement content)
    {
        XNamespace office = OdfNamespaces.Office;
        return new XElement(office + "document",
            new XAttribute(XNamespace.Xmlns + "o", office),
            new XAttribute(XNamespace.Xmlns + "t", OdfNamespaces.Text),
            new XAttribute(XNamespace.Xmlns + "p", OdfNamespaces.Presentation),
            new XAttribute(XNamespace.Xmlns + "m", OdfNamespaces.Meta),
            new XAttribute(office + "version", OdfVersionInfo.ToVersionString(version)),
            new XAttribute(office + "mimetype", "application/vnd.oasis.opendocument." + kind),
            new XElement(office + "body", new XElement(office + kind, content)));
    }

    private static XElement AssertNativeRoundTrip(XElement document, OdfVersion version, XName name, string value)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
            AssertValid(Validate(document, profile));
        byte[] bytes = Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
        foreach (bool streamReader in new[] { false, true })
        {
            using MemoryStream input = new(bytes);
            OdfNode dom = streamReader ? OdfXmlReader.Parse(input) : OdfXmlReader.Parse(bytes);
            Assert.Equal(value, dom.Descendants().Single(node => node.NamespaceUri == name.NamespaceName && node.LocalName == name.LocalName).TextContent);
            using MemoryStream output = new();
            OdfXmlWriter.Write(dom, output);
            XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()), LoadOptions.PreserveWhitespace);
            Assert.Equal(value, saved.Descendants(name).Single().Value);
            foreach (OdfComplianceProfile profile in Profiles(version))
                AssertValid(Validate(saved, profile));
        }
        using MemoryStream savedOutput = new();
        OdfXmlWriter.Write(OdfXmlReader.Parse(bytes), savedOutput);
        return XElement.Parse(Encoding.UTF8.GetString(savedOutput.ToArray()), LoadOptions.PreserveWhitespace);
    }

    private static OdfValidationReport Validate(XElement document, OdfComplianceProfile profile)
    {
        using MemoryStream input = new(Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting)));
        string path = (string?)document.Attribute(XName.Get("mimetype", OdfNamespaces.Office)) == "application/vnd.oasis.opendocument.presentation" ? "decl.fodp" : "decl.fodt";
        return OdfValidator.Validate(input, path, profile);
    }

    private static void AssertSchemaInvalid(XElement document, OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
            Assert.Contains(Validate(document, profile).Issues, issue => issue.RuleId == "ODF3101");
    }

    private static void AssertValid(OdfValidationReport report) => Assert.True(report.IsValid, report.ToJson());

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
