using System.Text;
using System.Xml;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests the relationship between configuration item type declarations and their stored values.
/// 測試設定項目型別宣告與保存值之間的關係。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfConfigDatatypeConformanceTests
{
    private static readonly (string Type, string Value, bool Valid)[] Cases =
    [
        ("boolean", "true", true), ("boolean", "false", true), ("boolean", " \ttrue\r\n", true), ("boolean", "1", false), ("boolean", "0", false), ("boolean", "True", false),
        ("short", "-32768", true), ("short", "32767", true), ("short", "+001", true), ("short", "32768", false), ("short", "-32769", false),
        ("int", "-2147483648", true), ("int", "2147483647", true), ("int", "2147483648", false), ("int", "1.0", false),
        ("long", "-9223372036854775808", true), ("long", "9223372036854775807", true), ("long", "9223372036854775808", false), ("long", "1e2", false),
        ("double", "INF", true), ("double", "-INF", true), ("double", "NaN", true), ("double", "1e1000", true), ("double", "Infinity", false), ("double", "nan", false), ("double", "1\u00a0", false),
        ("string", "", true), ("string", " \t\r\n文字😀<&>", true), ("string", "123", true),
        ("datetime", "2026-10-16T12:30:00Z", true), ("datetime", "-0001-01-01T24:00:00Z", true), ("datetime", "12026-01-01T12:30:00.123456789+14:00", true),
        ("datetime", "2026-10-16", false), ("datetime", "0000-01-01T00:00:00", false), ("datetime", "2026-10-16T12:30:00+14:01", false),
        ("base64Binary", "", true), ("base64Binary", "Zg==", true), ("base64Binary", "Zm8=", true), ("base64Binary", " Z \tg\r\n= = ", true),
        ("base64Binary", "Zh==", false), ("base64Binary", "Zm9=", false), ("base64Binary", "Zg", false), ("base64Binary", "Zg==\u00a0", false)
    ];

    /// <summary>
    /// All eight declared setting types constrain values beyond the text-only official item schema.
    /// 宣告的八種設定型別對值施加官方純文字項目 schema 以外的限制。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12, "boolean")]
    [InlineData(OdfVersion.Odf13, "boolean")]
    [InlineData(OdfVersion.Odf14, "boolean")]
    [InlineData(OdfVersion.Odf12, "short")]
    [InlineData(OdfVersion.Odf13, "short")]
    [InlineData(OdfVersion.Odf14, "short")]
    [InlineData(OdfVersion.Odf12, "int")]
    [InlineData(OdfVersion.Odf13, "int")]
    [InlineData(OdfVersion.Odf14, "int")]
    [InlineData(OdfVersion.Odf12, "long")]
    [InlineData(OdfVersion.Odf13, "long")]
    [InlineData(OdfVersion.Odf14, "long")]
    [InlineData(OdfVersion.Odf12, "double")]
    [InlineData(OdfVersion.Odf13, "double")]
    [InlineData(OdfVersion.Odf14, "double")]
    [InlineData(OdfVersion.Odf12, "string")]
    [InlineData(OdfVersion.Odf13, "string")]
    [InlineData(OdfVersion.Odf14, "string")]
    [InlineData(OdfVersion.Odf12, "datetime")]
    [InlineData(OdfVersion.Odf13, "datetime")]
    [InlineData(OdfVersion.Odf14, "datetime")]
    [InlineData(OdfVersion.Odf12, "base64Binary")]
    [InlineData(OdfVersion.Odf13, "base64Binary")]
    [InlineData(OdfVersion.Odf14, "base64Binary")]
    public void DeclaredTypesConstrainNativeSettingValues(OdfVersion version, string type)
    {
        foreach (var item in Cases.Where(item => item.Type == type))
            foreach (bool padded in new[] { false, true })
            {
                string declared = padded ? " \t" + type + "\r\n" : type;
                XElement node = Item(declared, item.Value);
                Assert.True(OdfSchemaPatternValidator.ValidateElement(node, OdfSchemaRegistry.GetSchema(version), "config-config-item").IsMatch);
                foreach (bool nested in new[] { false, true })
                {
                    XElement xml = Document(version, node, nested);
                    foreach (OdfComplianceProfile profile in Profiles(version))
                    {
                        OdfValidationReport report = Validate(xml, profile);
                        Assert.Equal(item.Valid, report.IsValid);
                        if (!item.Valid)
                            Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3142");
                    }
                    if (item.Valid)
                        RoundTrip(xml, declared, item.Value, version);
                }
            }
    }

    /// <summary>
    /// Configuration items require their name and type and cannot contain child elements.
    /// 設定項目要求名稱與型別且不能包含子元素。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void SettingModelsRetainRequiredAttributesAndNativeCarriers(OdfVersion version)
    {
        XNamespace config = OdfNamespaces.Config;
        XElement noType = Item("string", "text");
        noType.Attribute(config + "type")!.Remove();
        XElement noName = Item("string", "text");
        noName.Attribute(config + "name")!.Remove();
        XElement child = Item("string", "text");
        child.Add(new XElement(XName.Get("span", OdfNamespaces.Text), "child"));
        XElement wrongCarrier = Item("string", "");
        wrongCarrier.Name = config + "config-item-set";
        foreach (XElement node in new[] { noType, noName, child, wrongCarrier, Item("dateTime", "2026-10-16T00:00:00"), Item("", ""), Item("integer", "1") })
            foreach (OdfComplianceProfile profile in Profiles(version))
                Assert.Contains(Validate(Document(version, node, false), profile).Issues, issue => issue.RuleId == "ODF3101");
    }

    /// <summary>
    /// Foreign names cannot activate native setting datatype rules.
    /// 外部名稱不能啟動原生設定型別規則。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void SettingDatatypeRulesUseExpandedXmlNames(OdfVersion version)
    {
        XNamespace config = OdfNamespaces.Config;
        XElement element = Item("boolean", "invalid");
        element.Name = XName.Get("config-item", "urn:foreign");
        XElement attribute = Item("boolean", "invalid");
        attribute.Attribute(config + "type")!.Remove();
        attribute.SetAttributeValue(XName.Get("type", "urn:foreign"), "boolean");
        foreach (XElement node in new[] { element, attribute })
            foreach (OdfComplianceProfile profile in Profiles(version))
                Assert.DoesNotContain(Validate(Document(version, node, false), profile).Issues, issue => issue.RuleId == "ODF3142");
    }

    /// <summary>
    /// Base64 values enforce XML whitespace, padding and unused-bit constraints.
    /// Base64 值遵守 XML 空白、填補及未使用位元限制。
    /// </summary>
    [Theory]
    [InlineData("", true)]
    [InlineData("Zg==", true)]
    [InlineData("Zm8=", true)]
    [InlineData("Zm9v", true)]
    [InlineData(" Z \tg\r\n= = ", true)]
    [InlineData("Zh==", false)]
    [InlineData("Zm9=", false)]
    [InlineData("Zg", false)]
    [InlineData("====", false)]
    [InlineData("Zg==\u00a0", false)]
    public void Base64BinaryRespectsItsLexicalSpace(string value, bool expected)
        => CheckBinary(value, expected);

    /// <summary>
    /// Long Base64 values have no mail line-length restriction and retain strict final-bit checks.
    /// 長 Base64 值沒有郵件行長限制且維持嚴格的末端位元檢查。
    /// </summary>
    [Fact]
    public void LongBase64ValuesKeepTheirLexicalBoundaries()
    {
        string value = new('A', 40000);
        CheckBinary(value, true);
        CheckBinary(value + "Zh==", false);
        CheckBinary(value + "\u00a0", false);
        CheckBinary(string.Join(" \t\r\n", Enumerable.Repeat("AAAA", 10000)), true);
    }

    private static void CheckBinary(string value, bool expected)
    {
        foreach (string type in new[] { "base64Binary", "xsd:base64Binary" })
        {
            OdfSchemaPatternNode data = new(OdfSchemaPatternNodeKind.Data, "exactlyOne", "", "", "", type, "", null, null, null, "http://www.w3.org/2001/XMLSchema-datatypes");
            OdfSchemaSet schema = new(OdfVersion.Odf14, new Uri("https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/"), "2004-10-28", [], attributes: null, nameClasses: null, patterns: [new OdfSchemaPatternDefinition("start", [new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "exactlyOne", "", "root", "", "", "", null, [data])])]);
            Assert.Equal(expected, OdfSchemaPatternValidator.ValidateElement(new XElement("root", value), schema, "start").IsMatch);
        }
    }

    private static XElement Item(string type, string value)
    {
        XNamespace config = OdfNamespaces.Config;
        return new XElement(config + "config-item", new XAttribute(config + "name", "Setting"), new XAttribute(config + "type", type), value);
    }

    private static XElement Document(OdfVersion version, XElement item, bool nested)
    {
        XNamespace office = OdfNamespaces.Office;
        XNamespace config = OdfNamespaces.Config;
        XElement content = nested
            ? new XElement(config + "config-item-map-indexed", new XAttribute(config + "name", "Map"), new XElement(config + "config-item-map-entry", new XElement(item)))
            : new XElement(item);
        return new XElement(office + "document",
            new XAttribute(XNamespace.Xmlns + "o", office), new XAttribute(XNamespace.Xmlns + "c", config), new XAttribute(XNamespace.Xmlns + "ooo", "http://openoffice.org/2004/office"),
            new XAttribute(office + "version", OdfVersionInfo.ToVersionString(version)), new XAttribute(office + "mimetype", "application/vnd.oasis.opendocument.text"),
            new XElement(office + "settings", new XElement(config + "config-item-set", new XAttribute(config + "name", "ooo:configuration-settings"), content)),
            new XElement(office + "body", new XElement(office + "text", new XElement(XName.Get("p", OdfNamespaces.Text)))));
    }

    private static void RoundTrip(XElement xml, string type, string value, OdfVersion version)
    {
        byte[] bytes = Serialize(xml);
        foreach (bool stream in new[] { false, true })
        {
            using MemoryStream input = new(bytes);
            OdfNode root = stream ? OdfXmlReader.Parse(input) : OdfXmlReader.Parse(bytes);
            OdfNode item = root.Descendants().Single(node => node.NamespaceUri == OdfNamespaces.Config && node.LocalName == "config-item");
            Assert.Equal(type, item.GetAttribute("type", OdfNamespaces.Config));
            Assert.Equal(value, item.TextContent);
            using MemoryStream output = new();
            OdfXmlWriter.Write(root, output);
            XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()), LoadOptions.PreserveWhitespace);
            Assert.Equal(value, saved.Descendants(XName.Get("config-item", OdfNamespaces.Config)).Single().Value);
            foreach (OdfComplianceProfile profile in Profiles(version))
                Assert.True(Validate(saved, profile).IsValid);
        }
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
        return OdfValidator.Validate(input, "config-types.fodt", profile);
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
