using System.Text;
using System.Xml;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests source-bound native style attributes across shared carriers and datatype families.
/// 測試依來源綁定的原生樣式屬性及其共同承載元素與型別族。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfStyleAttributeConformanceTests
{
    /// <summary>
    /// Native style fields preserve data, reject invalid datatype values and retain their declared carriers.
    /// 原生樣式欄位保存資料、拒絕不合法型別值並維持宣告的承載元素。
    /// </summary>
    [Theory]
    [InlineData("ODFG-6017aeeeea225bca8f7d", "draw:caption-gap", "distance", "graphic-properties")]
    [InlineData("ODFG-8c2252db64b38e865204", "draw:contrast", "percent", "graphic-properties")]
    [InlineData("ODFG-c5f53067fe51d912cedd", "draw:decimal-places", "nonNegativeInteger", "graphic-properties")]
    [InlineData("ODFG-61955906a711a19e2f98", "draw:gamma", "percent", "graphic-properties")]
    [InlineData("ODFG-f431c40a535ff86264ec", "draw:guide-overhang", "length", "graphic-properties")]
    [InlineData("ODFG-cd27dc5c421921b30ce2", "draw:image-opacity", "zeroToHundredPercent", "graphic-properties")]
    [InlineData("ODFG-cc7b5467ea8bf2b6a202", "draw:line-distance", "distance", "graphic-properties")]
    [InlineData("ODFG-be650e6675245ec1174a", "draw:marker-end", "styleNameRef", "graphic-properties")]
    [InlineData("ODFG-a816bc1f67db46f55da9", "draw:marker-end-width", "length", "graphic-properties")]
    [InlineData("ODFG-ac863a4b3ff189feacc6", "draw:marker-start", "styleNameRef", "graphic-properties")]
    [InlineData("ODFG-05b57affea01f42c76b4", "draw:marker-start-width", "length", "graphic-properties")]
    [InlineData("ODFG-eac472bf48d1f27ded0c", "draw:shadow-color", "color", "graphic-properties")]
    [InlineData("ODFG-114d97699533313a07bc", "draw:stroke-dash", "styleNameRef", "graphic-properties")]
    [InlineData("ODFG-972a4f9f1d8504a990b9", "draw:symbol-color", "color", "graphic-properties")]
    [InlineData("ODFG-3c966fbd4cea896a30c0", "style:background-transparency", "zeroToHundredPercent", "graphic-properties")]
    [InlineData("ODFG-3c966fbd4cea896a30c0", "style:background-transparency", "zeroToHundredPercent", "paragraph-properties")]
    [InlineData("ODFG-35c03561acdbe555d083", "style:column-width", "positiveLength", "table-column-properties")]
    [InlineData("ODFG-b86c2c43fa71fbb84745", "style:diagonal-bl-tr", "string", "table-cell-properties")]
    [InlineData("ODFG-eb87395ef6e4bbfc4f80", "style:diagonal-tl-br", "string", "table-cell-properties")]
    [InlineData("ODFG-6abb5ef4030885c8f494", "style:layout-grid-base-height", "length", "page-layout-properties")]
    [InlineData("ODFG-49094b03362ec97f8c17", "style:layout-grid-color", "color", "page-layout-properties")]
    [InlineData("ODFG-5ec149fbf03119aee823", "style:min-row-height", "nonNegativeLength", "table-row-properties")]
    [InlineData("ODFG-594e3044c05ba2572ed4", "style:row-height", "positiveLength", "table-row-properties")]
    [InlineData("ODFG-bf456bd2a7d69b4fe3d8", "style:text-combine-start-char", "character", "text-properties")]
    [InlineData("ODFG-ca72942763fe88b37880", "style:text-combine-end-char", "character", "text-properties")]
    [InlineData("ODFG-af92248dc705aa7c2911", "style:text-scale", "percent", "text-properties")]
    [InlineData("ODFG-9c7bbbffef319a3802c3", "svg:stroke-color", "color", "graphic-properties")]
    [InlineData("ODFG-b705100c40db02d3021b", "svg:stroke-width", "length", "graphic-properties")]
    public void SourceDeclaredAttributesKeepTheirNativeModels(string groupId, string attribute, string datatype, string carrier)
    {
        (string[] valid, string[] invalid) = Values(datatype, attribute);
        foreach (OdfVersion version in Versions)
        {
            foreach (string? value in valid.Prepend(null))
            {
                XElement xml = Document(version, attribute, value, carrier, true);
                AssertValid(xml, version, groupId);
                byte[] bytes = Serialize(xml);
                foreach (bool stream in new[] { false, true })
                {
                    using MemoryStream input = new(bytes);
                    OdfNode root = stream ? OdfXmlReader.Parse(input) : OdfXmlReader.Parse(bytes);
                    OdfElement property = Assert.IsAssignableFrom<OdfElement>(root.Descendants().Single(node => node.NamespaceUri == OdfNamespaces.Style && node.LocalName == carrier));
                    XName name = Qualified(attribute);
                    Assert.Equal(value, property.GetAttributeValue(name.LocalName, name.NamespaceName, version));
                    if (value is null)
                        property.RemoveAttribute(name.LocalName, name.NamespaceName);
                    else
                        property.SetAttributeValue(name.LocalName, name.NamespaceName, value, attribute.Split(':')[0], version);
                    using MemoryStream output = new();
                    OdfXmlWriter.Write(root, output);
                    XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()), LoadOptions.PreserveWhitespace);
                    Assert.Equal(value, (string?)saved.Descendants(XName.Get(carrier, OdfNamespaces.Style)).Single().Attribute(name));
                    AssertValid(saved, version, groupId);
                }
            }
            foreach (string value in invalid)
                foreach (OdfComplianceProfile profile in Profiles(version))
                    Assert.Contains(Validate(Document(version, attribute, value, carrier, true), profile).Issues, issue => issue.RuleId == "ODF3101");
            XElement wrongCarrier = Document(version, attribute, null, carrier, true);
            wrongCarrier.Descendants(XName.Get("p", OdfNamespaces.Text)).Single().SetAttributeValue(Qualified(attribute), valid[0]);
            foreach (OdfComplianceProfile profile in Profiles(version))
                Assert.Contains(Validate(wrongCarrier, profile).Issues, issue => issue.RuleId == "ODF3101");
        }
    }

    /// <summary>
    /// Resource references resolve to the required marker or stroke-dash kind rather than merely valid NCNames.
    /// 資源參照解析至要求的標記或虛線種類，而不僅是合法 NCName。
    /// </summary>
    [Theory]
    [InlineData("draw:marker-start", "Dash1")]
    [InlineData("draw:marker-end", "Dash1")]
    [InlineData("draw:stroke-dash", "Marker1")]
    public void ResourceReferencesRequireTheCorrectTarget(string attribute, string wrongKind)
    {
        foreach (OdfVersion version in Versions)
            foreach (string target in new[] { "MissingResource", wrongKind })
                foreach (OdfComplianceProfile profile in Profiles(version))
                {
                    XElement xml = Document(version, attribute, target, "graphic-properties", true);
                    Assert.False(Validate(xml, profile).IsValid, attribute + " = " + target);
                }
    }

    /// <summary>
    /// Finite typed lengths serialize as decimal lexical values accepted by native length attributes.
    /// 有限型別化長度序列化為原生長度屬性可接受的十進位詞彙值。
    /// </summary>
    [Theory]
    [InlineData(1E-100)]
    [InlineData(1E100)]
    [InlineData(double.Epsilon)]
    [InlineData(double.MaxValue)]
    [InlineData(1.2345678901234567)]
    public void TypedLengthsKeepValidDecimalLexicalValues(double value)
    {
        string lexical = OdfLength.FromCentimeters(value).ToString();
        foreach (OdfVersion version in Versions)
            AssertValid(Document(version, "style:column-width", lexical, "table-column-properties", false), version, "typed-length");
        Assert.True(OdfLength.TryParse(lexical, out OdfLength parsed));
        Assert.Equal(value, parsed.Value);
    }

    private static readonly OdfVersion[] Versions = [OdfVersion.Odf12, OdfVersion.Odf13, OdfVersion.Odf14];

    /// <summary>
    /// Package references may use resources declared in styles.xml while their automatic style is in content.xml.
    /// 封裝參照可使用 styles.xml 宣告的資源，而自動樣式位於 content.xml。
    /// </summary>
    [Theory]
    [InlineData("draw:marker-start")]
    [InlineData("draw:marker-end")]
    [InlineData("draw:stroke-dash")]
    public void PackageResourcesResolveAcrossContentAndStyles(string attribute)
    {
        foreach (OdfVersion version in Versions)
        {
            string target = attribute == "draw:stroke-dash" ? "Dash1" : "Marker1";
            XElement template = Document(version, attribute, target, "graphic-properties", true);
            using TextDocument document = TextDocument.Create();
            document.TargetVersion = version;
            OdfNode body = document.ContentDom.Descendants().Single(node => node.NamespaceUri == OdfNamespaces.Office && node.LocalName == "body");
            OdfNode contentRoot = body.Parent!;
            OdfNode? automatic = contentRoot.Children.FirstOrDefault(node => node.NamespaceUri == OdfNamespaces.Office && node.LocalName == "automatic-styles");
            if (automatic is null)
            {
                automatic = OdfNodeFactory.CreateElement("automatic-styles", OdfNamespaces.Office, "office");
                contentRoot.InsertBefore(automatic, body);
            }
            XElement style = template.Descendants(XName.Get("style", OdfNamespaces.Style)).Single();
            style.SetAttributeValue(XName.Get("name", OdfNamespaces.Style), "ResourceStyle");
            automatic.AppendChild(Fragment(style));
            OdfNode styles = document.StylesDom.Descendants().Single(node => node.NamespaceUri == OdfNamespaces.Office && node.LocalName == "styles");
            foreach (XElement resource in template.Descendants().Where(node => node.Name.NamespaceName == OdfNamespaces.Draw && node.Name.LocalName is "marker" or "stroke-dash"))
                styles.AppendChild(Fragment(resource));
            using MemoryStream output = new();
            document.SaveToStream(output);
            using OdfPackage package = OdfPackage.Open(new MemoryStream(output.ToArray()));
            using (Stream content = package.GetEntryStream("content.xml"))
                Assert.Contains(XElement.Load(content).Descendants(XName.Get("graphic-properties", OdfNamespaces.Style)), node => (string?)node.Attribute(Qualified(attribute)) == target);
            using (Stream definitions = package.GetEntryStream("styles.xml"))
                Assert.Contains(XElement.Load(definitions).Descendants(XName.Get(attribute == "draw:stroke-dash" ? "stroke-dash" : "marker", OdfNamespaces.Draw)), node => (string?)node.Attribute(XName.Get("name", OdfNamespaces.Draw)) == target);
            foreach (OdfComplianceProfile profile in Profiles(version))
            {
                OdfValidationReport report = OdfPackageValidator.Validate(package, profile);
                Assert.True(report.IsValid, report.ToJson());
            }
        }
    }

    /// <summary>
    /// Inline embedded documents cannot borrow the outer document's named resources.
    /// 行內內嵌文件不能借用外層文件的具名資源。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void InlineResourceScopesRemainIndependent(OdfVersion version)
    {
        foreach (bool ownResources in new[] { false, true })
        {
            XElement outer = Document(version, "draw:marker-start", "Marker1", "graphic-properties", true);
            XElement inner = Document(version, "draw:marker-start", "Marker1", "graphic-properties", ownResources);
            inner.Descendants(XName.Get("style", OdfNamespaces.Style)).Single().SetAttributeValue(XName.Get("name", OdfNamespaces.Style), "InnerStyle");
            outer.Descendants(XName.Get("p", OdfNamespaces.Text)).Single().Add(
                new XElement(XName.Get("frame", OdfNamespaces.Draw),
                    new XAttribute(XName.Get("width", OdfNamespaces.Svg), "2cm"), new XAttribute(XName.Get("height", OdfNamespaces.Svg), "2cm"),
                    new XElement(XName.Get("object", OdfNamespaces.Draw), inner)));
            foreach (OdfComplianceProfile profile in Profiles(version))
            {
                OdfValidationReport report = Validate(outer, profile);
                if (ownResources)
                    Assert.True(report.IsValid, report.ToJson());
                else
                    Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3143");
            }
        }
    }

    /// <summary>
    /// Text-combine characters remain stored both when active and when their display condition is inactive.
    /// 文字合併字元在啟用或顯示條件未啟用時皆維持保存。
    /// </summary>
    [Theory]
    [InlineData("style:text-combine-start-char")]
    [InlineData("style:text-combine-end-char")]
    public void TextCombineCharacterDataDoesNotDependOnActivation(string attribute)
    {
        foreach (OdfVersion version in Versions)
            foreach (string mode in new[] { "none", "lines" })
            {
                XElement xml = Document(version, attribute, "字", "text-properties", false);
                xml.Descendants(XName.Get("text-properties", OdfNamespaces.Style)).Single().SetAttributeValue(XName.Get("text-combine", OdfNamespaces.Style), mode);
                AssertValid(xml, version, attribute);
            }
    }

    private static OdfNode Fragment(XElement element)
    {
        OdfNode parsed = OdfXmlReader.Parse(Encoding.UTF8.GetBytes(element.ToString(SaveOptions.DisableFormatting)));
        return parsed;
    }

    private static (string[] Valid, string[] Invalid) Values(string type, string attribute) => type switch
    {
        "distance" or "length" or "positiveLength" or "nonNegativeLength" => (["2cm", "1.25pt"], ["not-a-length", "2bogus", "1e3cm"]),
        "percent" when attribute == "draw:contrast" => (["-50%", "0%", "50.25%", "200%"], ["not-percent", "50", "NaN%"]),
        "percent" => (["0%", "50.25%", "200%"], ["not-percent", "50", "NaN%"]),
        "zeroToHundredPercent" => (["0%", "50.25%", "100%"], ["-1%", "101%", "50"]),
        "nonNegativeInteger" => (["0", "2", "999999999999999999999"], ["-1", "1.5", "1e2"]),
        "color" => (["#000000", "#12AbEf"], ["#fff", "red", "inherit", "#12GGFF", "#1234567", " #123456 "]),
        "string" => (["", " \t\r\n文字😀<&>", "0.1pt solid #112233"], []),
        "character" => (["A", "字", "😀", " "], ["", "AB", "😀A"]),
        "styleNameRef" when attribute == "draw:stroke-dash" => (["", "Dash1", " \tDash1\r\n"], ["bad name", "1bad"]),
        "styleNameRef" => (["", "Marker1", " \tMarker1\r\n"], ["bad name", "1bad"]),
        _ => throw new InvalidOperationException()
    };

    private static XName Qualified(string name)
    {
        string[] parts = name.Split(':');
        string uri = parts[0] switch
        {
            "style" => OdfNamespaces.Style,
            "draw" => OdfNamespaces.Draw,
            "svg" => OdfNamespaces.Svg,
            _ => throw new InvalidOperationException()
        };
        return XName.Get(parts[1], uri);
    }

    private static XElement Document(OdfVersion version, string attribute, string? value, string carrier, bool resources)
    {
        XNamespace office = OdfNamespaces.Office;
        XNamespace style = OdfNamespaces.Style;
        XNamespace draw = OdfNamespaces.Draw;
        XNamespace svg = OdfNamespaces.Svg;
        XElement styles = new(office + "styles");
        XElement automatic = new(office + "automatic-styles");
        XElement property = new(style + carrier);
        if (value is not null)
            property.SetAttributeValue(Qualified(attribute), value);
        if (attribute is "style:text-combine-start-char" or "style:text-combine-end-char")
            property.SetAttributeValue(style + "text-combine", "lines");
        if (carrier == "page-layout-properties")
            automatic.Add(new XElement(style + "page-layout", new XAttribute(style + "name", "P1"), property));
        else
            styles.Add(new XElement(style + "style", new XAttribute(style + "name", "S1"), new XAttribute(style + "family", carrier[..^"-properties".Length]), property));
        if (resources)
        {
            styles.Add(new XElement(draw + "marker", new XAttribute(draw + "name", "Marker1"), new XAttribute(svg + "viewBox", "0 0 100 100"), new XAttribute(svg + "d", "M0 0L100 100")));
            styles.Add(new XElement(draw + "stroke-dash", new XAttribute(draw + "name", "Dash1"), new XAttribute(draw + "style", "rect"), new XAttribute(draw + "dots1", "1"), new XAttribute(draw + "dots1-length", "0.1cm"), new XAttribute(draw + "distance", "0.2cm")));
        }
        return new XElement(office + "document",
            new XAttribute(XNamespace.Xmlns + "o", office), new XAttribute(XNamespace.Xmlns + "s", style), new XAttribute(XNamespace.Xmlns + "d", draw), new XAttribute(XNamespace.Xmlns + "v", svg),
            new XAttribute(office + "version", OdfVersionInfo.ToVersionString(version)), new XAttribute(office + "mimetype", "application/vnd.oasis.opendocument.text"),
            styles, automatic, new XElement(office + "body", new XElement(office + "text", new XElement(XName.Get("p", OdfNamespaces.Text)))));
    }

    private static byte[] Serialize(XElement xml)
    {
        using MemoryStream output = new();
        using (XmlWriter writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), NewLineHandling = NewLineHandling.Entitize }))
            xml.Save(writer);
        return output.ToArray();
    }

    private static OdfValidationReport Validate(XElement xml, OdfComplianceProfile profile)
    {
        using MemoryStream input = new(Serialize(xml));
        return OdfValidator.Validate(input, "style-attributes.fodt", profile);
    }

    private static void AssertValid(XElement xml, OdfVersion version, string context)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            OdfValidationReport report = Validate(xml, profile);
            Assert.True(report.IsValid, context + " " + report.ToJson());
        }
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
