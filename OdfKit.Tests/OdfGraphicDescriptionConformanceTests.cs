using System.Buffers;
using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests graphic accessibility text as character data in native ODF elements.
/// 測試原生 ODF 元素中的圖形無障礙文字資料。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfGraphicDescriptionConformanceTests
{
    /// <summary>
    /// Titles and descriptions preserve whitespace and character references through both readers.
    /// 標題與描述經過兩種讀取器後仍保留空白及字元參照。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void GraphicDescriptionsPreserveCharacterData(OdfVersion version)
    {
        foreach (string text in new[] { "", " ", " \t\n ", "\r", " \t\r\n ", "標題😀 <&>\n第二行" })
            foreach (bool streamReader in new[] { false, true })
                foreach (bool lazyParent in new[] { false, true })
                {
                    string xml = Document(version, text, lazyParent);
                    foreach (OdfComplianceProfile profile in Profiles(version))
                        AssertValid(Validate(xml, profile));
                    byte[] bytes = Encoding.UTF8.GetBytes(xml);
                    using MemoryStream input = new(bytes);
                    OdfNode dom = streamReader ? OdfXmlReader.Parse(input) : OdfXmlReader.Parse(bytes);
                    foreach (string name in new[] { "title", "desc" })
                        Assert.Equal(text, dom.Descendants().Single(node => node.NamespaceUri == OdfNamespaces.Svg && node.LocalName == name).TextContent);
                    using MemoryStream output = new();
                    OdfXmlWriter.Write(dom, output);
                    XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()), LoadOptions.PreserveWhitespace);
                    foreach (string name in new[] { "title", "desc" })
                        Assert.Equal(text, saved.Descendants(XName.Get(name, OdfNamespaces.Svg)).Single().Value);
                    foreach (OdfComplianceProfile profile in Profiles(version))
                        AssertValid(Validate(saved.ToString(SaveOptions.DisableFormatting), profile));
                }
    }

    /// <summary>
    /// Native descriptions allow character data and reject attributes, child elements and invalid placement.
    /// 原生描述允許文字資料，並拒絕屬性、子元素及不合法位置。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void GraphicDescriptionStructureUsesNativeNames(OdfVersion version)
    {
        XNamespace svg = OdfNamespaces.Svg;
        XNamespace text = OdfNamespaces.Text;
        foreach (string name in new[] { "title", "desc" })
            foreach (int mutation in new[] { 0, 1, 2 })
            {
                XElement xml = XElement.Parse(Document(version, "文字", false), LoadOptions.PreserveWhitespace);
                XElement description = xml.Descendants(svg + name).Single();
                if (mutation == 0)
                    description.SetAttributeValue(svg + "width", "1cm");
                else if (mutation == 1)
                    description.Add(new XElement(text + "span", "child"));
                else
                {
                    description.Remove();
                    xml.Descendants(text + "p").Single().Add(description);
                }
                foreach (OdfComplianceProfile profile in Profiles(version))
                    Assert.Contains(Validate(xml.ToString(SaveOptions.DisableFormatting), profile).Issues, issue => issue.RuleId == "ODF3101");
            }
    }

    /// <summary>
    /// XML line ending normalization precedes references while CDATA keeps literal ampersands.
    /// XML 換行正規化先於參照解碼，CDATA 則保留字面的與號。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void GraphicDescriptionXmlTextKeepsItsMeaning(OdfVersion version)
    {
        foreach (var sample in new[]
        {
            ("first\r\nsecond\rthird", "first\nsecond\nthird"),
            ("first&#xD;second", "first\rsecond"),
            ("<![CDATA[&amp;\r\n文字<&>]]>", "&amp;\n文字<&>"),
            ("before<!-- hidden &amp; -->after", "beforeafter"),
            ("before<?instruction hidden?>after", "beforeafter"),
            ("<!--hidden-->", ""),
            ("<?instruction hidden?>", "")
        })
            foreach (bool streamReader in new[] { false, true })
            {
                string xml = Document(version, "", false).Replace("<s:title></s:title>", "<s:title>" + sample.Item1 + "</s:title>");
                byte[] bytes = Encoding.UTF8.GetBytes(xml);
                using MemoryStream input = new(bytes);
                OdfNode dom = streamReader ? OdfXmlReader.Parse(input) : OdfXmlReader.Parse(bytes);
                Assert.Equal(sample.Item2, dom.Descendants().Single(node => node.NamespaceUri == OdfNamespaces.Svg && node.LocalName == "title").TextContent);
                using MemoryStream output = new();
                OdfXmlWriter.Write(dom, output);
                XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()), LoadOptions.PreserveWhitespace);
                Assert.Equal(sample.Item2, saved.Descendants(XName.Get("title", OdfNamespaces.Svg)).Single().Value);
            }
    }

    /// <summary>
    /// Each version retains the complete native parent list and text-only definitions.
    /// 各版本保留完整的原生父元素清單及純文字定義。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void GraphicDescriptionParentsMatchTheSpecification(OdfVersion version)
    {
        OdfSchemaSet schema = OdfSchemaRegistry.GetSchema(version);
        string[] expected = ["area-circle", "area-polygon", "area-rectangle", "caption", "circle", "connector", "control", "custom-shape", "ellipse", "frame", "g", "layer", "line", "measure", "page", "page-thumbnail", "path", "polygon", "polyline", "rect", "regular-polygon"];
        foreach (string name in new[] { "title", "desc" })
        {
            HashSet<XName> parents = [];
            foreach (OdfSchemaPatternNode node in schema.Patterns.Values.SelectMany(pattern => Flatten(pattern.Roots)))
                if (node.Kind == OdfSchemaPatternNodeKind.Element && node.LocalName.Length != 0 &&
                    ChildElements(node.Children, schema, []).Contains(XName.Get(name, OdfNamespaces.Svg)))
                    parents.Add(XName.Get(node.LocalName, node.NamespaceUri));
            HashSet<XName> expectedNames = expected.Select(local => XName.Get(local, OdfNamespaces.Draw)).ToHashSet();
            expectedNames.Add(XName.Get("scene", OdfNamespaces.Dr3d));
            Assert.True(expectedNames.SetEquals(parents), string.Join(", ", parents));
            foreach (string value in new[] { "", " \t\n", "文字😀\r\n" })
                Assert.True(OdfSchemaPatternValidator.ValidateElement(new XElement(XName.Get(name, OdfNamespaces.Svg), value), schema, "svg-" + name).IsMatch);
        }
    }

    /// <summary>
    /// Saving emits carriage returns as references and keeps line feeds unchanged.
    /// 儲存以參照輸出歸位字元，並保持換行字元不變。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void GraphicDescriptionSavingPreservesLineBreaks(OdfVersion version)
    {
        OdfNode dom = OdfXmlReader.Parse(Encoding.UTF8.GetBytes(Document(version, "", false)));
        const string value = "first\rsecond\nthird\r\nfourth";
        dom.Descendants().Single(node => node.NamespaceUri == OdfNamespaces.Svg && node.LocalName == "title").TextContent = value;
        using MemoryStream output = new();
        OdfXmlWriter.Write(dom, output);
        XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()), LoadOptions.PreserveWhitespace);
        Assert.Equal(value, saved.Descendants(XName.Get("title", OdfNamespaces.Svg)).Single().Value);
    }

    /// <summary>
    /// Container text excludes comments and instructions while each node retains its own value.
    /// 容器文字排除註解及處理指令，各節點則保留自身內容。
    /// </summary>
    [Fact]
    public void GraphicDescriptionTextExcludesMarkupNodes()
    {
        foreach (OdfNodeType kind in new[] { OdfNodeType.Comment, OdfNodeType.ProcessingInstruction })
        {
            OdfNode container = new(OdfNodeType.Element, "title", OdfNamespaces.Svg);
            OdfNode markup = new(kind, "instruction", "") { TextContent = "hidden" };
            container.AppendChild(markup);
            Assert.Equal("", container.TextContent);
            ArrayBufferWriter<char> buffer = new();
            Assert.True(container.TryWriteTextContent(buffer));
            Assert.Equal("", buffer.WrittenSpan.ToString());
            Assert.Equal("hidden", markup.TextContent);
            Assert.True(markup.TryWriteTextContent(buffer));
            Assert.Equal("hidden", buffer.WrittenSpan.ToString());
            container.AppendChild(new OdfNode(OdfNodeType.Text, "", "") { TextContent = "visible" });
            Assert.Equal("visible", container.TextContent);
            buffer.Clear();
            Assert.True(container.TryWriteTextContent(buffer));
            Assert.Equal("visible", buffer.WrittenSpan.ToString());
        }
    }

    private static IEnumerable<OdfSchemaPatternNode> Flatten(IEnumerable<OdfSchemaPatternNode> nodes)
    {
        foreach (OdfSchemaPatternNode node in nodes)
        {
            yield return node;
            foreach (OdfSchemaPatternNode child in Flatten(node.Children))
                yield return child;
        }
    }

    private static IEnumerable<XName> ChildElements(IEnumerable<OdfSchemaPatternNode> nodes, OdfSchemaSet schema, HashSet<string> references)
    {
        foreach (OdfSchemaPatternNode node in nodes)
        {
            if (node.Kind == OdfSchemaPatternNodeKind.Element)
            {
                if (node.LocalName.Length != 0)
                    yield return XName.Get(node.LocalName, node.NamespaceUri);
            }
            else if (node.Kind == OdfSchemaPatternNodeKind.Ref)
            {
                if (references.Add(node.ReferenceName) && schema.FindPattern(node.ReferenceName) is { } definition)
                    foreach (XName name in ChildElements(definition.Roots, schema, references))
                        yield return name;
            }
            else
                foreach (XName name in ChildElements(node.Children, schema, references))
                    yield return name;
        }
    }

    private static string Document(OdfVersion version, string text, bool lazyParent)
    {
        string escaped = text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\r", "&#xD;");
        string padding = lazyParent ? new string('x', 9000) : "";
        return $"<office:document xmlns:office='{OdfNamespaces.Office}' xmlns:text='{OdfNamespaces.Text}' xmlns:draw='{OdfNamespaces.Draw}' xmlns:s='{OdfNamespaces.Svg}' office:version='{OdfVersionInfo.ToVersionString(version)}' office:mimetype='application/vnd.oasis.opendocument.text'><office:body><office:text><text:p>{padding}<draw:rect s:width='1cm' s:height='1cm'><s:title>{escaped}</s:title><s:desc>{escaped}</s:desc></draw:rect></text:p></office:text></office:body></office:document>";
    }

    private static OdfValidationReport Validate(string xml, OdfComplianceProfile profile)
    {
        using MemoryStream input = new(Encoding.UTF8.GetBytes(xml));
        return OdfValidator.Validate(input, "graphic-description.fodt", profile);
    }

    private static void AssertValid(OdfValidationReport report) => Assert.True(report.IsValid, report.ToJson());

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
