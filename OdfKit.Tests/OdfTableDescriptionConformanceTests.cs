using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests table descriptions and their frame-dependent prose constraints.
/// 測試表格描述及其依框架脈絡適用的規範文字限制。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfTableDescriptionConformanceTests
{
    private static readonly string[] Kinds = ["title", "desc"];
    private static readonly string[] Values = ["", " ", "\t\n", "表格😀<&>"];

    /// <summary>
    /// Tables in text flow retain their own title and description character data.
    /// 文字流程中的表格保留自身標題及描述的字元資料。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void TextFlowTablesPreserveTheirDescriptions(OdfVersion version)
    {
        foreach (string kind in Kinds)
            foreach (string value in Values)
            {
                XElement document = Document(version, Table(new XElement(XName.Get(kind, OdfNamespaces.Table), value)));
                AssertValid(document, version);
                AssertRoundTrip(document, XName.Get(kind, OdfNamespaces.Table), value, version);
            }
    }

    /// <summary>
    /// Tables used as frame content use the frame's SVG description instead of their own.
    /// 作為框架內容的表格使用框架的 SVG 描述，不使用自身描述。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void DirectFrameTablesRejectTheirOwnDescriptions(OdfVersion version)
    {
        foreach (string kind in Kinds)
        {
            XElement frame = Frame(Table(new XElement(XName.Get(kind, OdfNamespaces.Table), "Table description")));
            Assert.True(OdfSchemaPatternValidator.ValidateElement(frame, OdfSchemaRegistry.GetSchema(version), "draw-frame").IsMatch);
            XElement document = Document(version, new XElement(XName.Get("p", OdfNamespaces.Text), frame));
            foreach (OdfComplianceProfile profile in Profiles(version))
                Assert.Contains(Validate(document, profile).Issues, issue => issue.RuleId == "ODF3138");
        }
    }

    /// <summary>
    /// Frame descriptions remain optional and preserve their text when supplied.
    /// 框架描述維持可選，提供時則保留其文字。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void FrameDescriptionsUseSvgWithoutBecomingRequired(OdfVersion version)
    {
        AssertValid(Document(version, new XElement(XName.Get("p", OdfNamespaces.Text), Frame(Table()))), version);
        foreach (string kind in Kinds)
            foreach (string value in Values)
            {
                XElement frame = Frame(Table());
                frame.Add(new XElement(XName.Get(kind, OdfNamespaces.Svg), value));
                XElement document = Document(version, new XElement(XName.Get("p", OdfNamespaces.Text), frame));
                AssertValid(document, version);
                AssertRoundTrip(document, XName.Get(kind, OdfNamespaces.Svg), value, version);
            }
    }

    /// <summary>
    /// Tables inside a text box keep their own descriptions rather than describing an alternative frame representation.
    /// 文字方塊內的表格保留自身描述，不將其視為框架的替代表示。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void NestedTextFlowTablesRetainTheirOwnDescriptions(OdfVersion version)
    {
        foreach (string kind in Kinds)
        {
            XElement table = Table(new XElement(XName.Get(kind, OdfNamespaces.Table), "Inner table"));
            XElement frame = Frame(new XElement(XName.Get("text-box", OdfNamespaces.Draw), table));
            AssertValid(Document(version, new XElement(XName.Get("p", OdfNamespaces.Text), frame)), version);
        }
    }

    /// <summary>
    /// The native table description model rejects attributes, nested elements and incorrect placement.
    /// 原生表格描述模型拒絕屬性、巢狀元素及錯誤位置。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void TableDescriptionsEnforceTheirNativeContentModel(OdfVersion version)
    {
        foreach (string kind in Kinds)
            foreach (int mutation in new[] { 0, 1, 2 })
            {
                XElement description = new(XName.Get(kind, OdfNamespaces.Table), "Text");
                if (mutation == 0)
                    description.SetAttributeValue(XName.Get("name", OdfNamespaces.Table), "invalid");
                else if (mutation == 1)
                    description.Add(new XElement(XName.Get("span", OdfNamespaces.Text), "invalid"));
                XElement document = Document(version, mutation == 2 ? description : Table(description));
                foreach (OdfComplianceProfile profile in Profiles(version))
                    Assert.Contains(Validate(document, profile).Issues, issue => issue.RuleId == "ODF3101");
            }
    }

    /// <summary>
    /// Foreign elements named frame do not activate the native frame constraint.
    /// 名為 frame 的外來元素不會啟用原生框架限制。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ForeignFrameNamesDoNotActivateNativeConstraints(OdfVersion version)
    {
        foreach (string kind in Kinds)
        {
            XElement foreign = new(XName.Get("frame", "urn:test:foreign"), Table(new XElement(XName.Get(kind, OdfNamespaces.Table), "Text")));
            XElement document = Document(version, new XElement(XName.Get("p", OdfNamespaces.Text), foreign));
            foreach (OdfComplianceProfile profile in Profiles(version))
                Assert.DoesNotContain(Validate(document, profile).Issues, issue => issue.RuleId == "ODF3138");
        }
    }

    private static XElement Table(XElement? description = null)
    {
        XNamespace table = OdfNamespaces.Table;
        return new XElement(table + "table", new XAttribute(table + "name", "T1"), description,
            new XElement(table + "table-column"),
            new XElement(table + "table-row", new XElement(table + "table-cell", new XElement(XName.Get("p", OdfNamespaces.Text), "Cell"))));
    }

    private static XElement Frame(XElement content) => new(XName.Get("frame", OdfNamespaces.Draw),
        new XAttribute(XName.Get("width", OdfNamespaces.Svg), "1cm"),
        new XAttribute(XName.Get("height", OdfNamespaces.Svg), "1cm"), content);

    private static XElement Document(OdfVersion version, XElement content)
    {
        XNamespace office = OdfNamespaces.Office;
        return new XElement(office + "document",
            new XAttribute(XNamespace.Xmlns + "o", office),
            new XAttribute(XNamespace.Xmlns + "t", OdfNamespaces.Table),
            new XAttribute(XNamespace.Xmlns + "d", OdfNamespaces.Draw),
            new XAttribute(XNamespace.Xmlns + "s", OdfNamespaces.Svg),
            new XAttribute(XNamespace.Xmlns + "p", OdfNamespaces.Text),
            new XAttribute(office + "version", OdfVersionInfo.ToVersionString(version)),
            new XAttribute(office + "mimetype", "application/vnd.oasis.opendocument.text"),
            new XElement(office + "body", new XElement(office + "text", content)));
    }

    private static void AssertRoundTrip(XElement document, XName name, string value, OdfVersion version)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
        foreach (bool stream in new[] { false, true })
        {
            using MemoryStream input = new(bytes);
            OdfNode dom = stream ? OdfXmlReader.Parse(input) : OdfXmlReader.Parse(bytes);
            Assert.Equal(value, dom.Descendants().Single(node => node.NamespaceUri == name.NamespaceName && node.LocalName == name.LocalName).TextContent);
            using MemoryStream output = new();
            OdfXmlWriter.Write(dom, output);
            XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()), LoadOptions.PreserveWhitespace);
            Assert.Equal(value, saved.Descendants(name).Single().Value);
            AssertValid(saved, version);
        }
    }

    private static OdfValidationReport Validate(XElement document, OdfComplianceProfile profile)
    {
        using MemoryStream input = new(Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting)));
        return OdfValidator.Validate(input, "table.fodt", profile);
    }

    private static void AssertValid(XElement document, OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            OdfValidationReport report = Validate(document, profile);
            Assert.True(report.IsValid, report.ToJson());
        }
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
