using System.Text;
using System.Xml;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests native user-defined metadata content models, datatypes, and numeric producer behavior.
/// 測試原生自訂中繼資料內容模型、型別及數值產生器行為。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfMetadataValueConformanceTests
{
    private static readonly (string? Type, string Value, bool Valid)[] Cases =
    [
        (null, "", true), (null, " \t\r\n文字😀<&>", true), ("string", " \t\r\n文字😀<&>", true),
        ("float", "1.25", true), ("float", "INF", true), ("float", "-INF", true), ("float", "NaN", true), ("float", "1E1000", true),
        ("date", "2024-02-29", true), ("date", "12026-01-01T12:00:00+14:00", true), ("date", "-0001-01-01", true),
        ("time", "P999999999999999999999Y", true), ("time", "-P1Y2M3DT4H5M6.123456789S", true),
        ("boolean", "true", true), ("boolean", "false", true), ("boolean", " \ttrue\r\n", true),
        (" float ", "INF", true), (" \tdate\r\n", "2024-02-29", true), (" time ", "P1D", true), (" boolean ", "true", true), (" string ", "文字", true),
        ("float", "Infinity", false), ("float", "nan", false), ("float", "", false),
        ("date", "2023-02-29", false), ("date", "0000-01-01", false), ("date", "12:00:00", false),
        ("time", "PT1.S", false), ("time", "PT.5S", false), ("time", "12:00:00", false),
        ("boolean", "0", false), ("boolean", "1", false), ("boolean", "True", false), ("number", "1", false),
        ("", "text", false), (" ", "text", false), ("Float", "1", false)
    ];

    /// <summary>
    /// Metadata value types constrain character data without changing the stored lexical text.
    /// 中繼資料值型別限制字元資料且不改寫保存的詞彙文字。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void TypedMetadataContentUsesNativeDatatypeModels(OdfVersion version)
    {
        foreach (var item in Cases)
        {
            XElement xml = Document(version, item.Type, item.Value);
            foreach (OdfComplianceProfile profile in Profiles(version))
            {
                OdfValidationReport report = Validate(xml, profile);
                Assert.Equal(item.Valid, report.IsValid);
                if (!item.Valid)
                    Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3101");
            }
            if (!item.Valid)
                continue;
            byte[] bytes = Serialize(xml);
            foreach (bool stream in new[] { false, true })
            {
                using MemoryStream input = new(bytes);
                OdfNode root = stream ? OdfXmlReader.Parse(input) : OdfXmlReader.Parse(bytes);
                OdfNode property = root.Descendants().Single(node => node.NamespaceUri == OdfNamespaces.Meta && node.LocalName == "user-defined");
                Assert.Equal(item.Value, property.TextContent);
                Assert.Equal(item.Type, property.GetAttribute("value-type", OdfNamespaces.Meta));
                using MemoryStream output = new();
                OdfXmlWriter.Write(root, output);
                XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()), LoadOptions.PreserveWhitespace);
                XElement savedProperty = saved.Descendants(XName.Get("user-defined", OdfNamespaces.Meta)).Single();
                Assert.Equal(item.Value, savedProperty.Value);
                Assert.Equal(item.Type, (string?)savedProperty.Attribute(XName.Get("value-type", OdfNamespaces.Meta)));
                foreach (OdfComplianceProfile profile in Profiles(version))
                    Assert.True(Validate(saved, profile).IsValid);
            }
        }
    }

    /// <summary>
    /// User-defined metadata requires its native parent and name and cannot contain child elements.
    /// 自訂中繼資料要求原生父元素及名稱且不能包含子元素。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void MetadataContentKeepsItsNativeStructure(OdfVersion version)
    {
        XNamespace meta = OdfNamespaces.Meta;
        XNamespace office = OdfNamespaces.Office;
        XElement missing = Document(version, null, "text");
        missing.Descendants(meta + "user-defined").Single().Attribute(meta + "name")!.Remove();
        XElement child = Document(version, null, "text");
        child.Descendants(meta + "user-defined").Single().Add(new XElement(XName.Get("span", OdfNamespaces.Text), "child"));
        XElement extraAttribute = Document(version, null, "text");
        extraAttribute.Descendants(meta + "user-defined").Single().SetAttributeValue(meta + "unknown", "not-declared");
        XElement wrongParent = Document(version, null, "text");
        XElement property = wrongParent.Descendants(meta + "user-defined").Single();
        property.Remove();
        wrongParent.Element(office + "body")!.Element(office + "text")!.Add(property);
        foreach (XElement xml in new[] { missing, child, extraAttribute, wrongParent })
            foreach (OdfComplianceProfile profile in Profiles(version))
                Assert.Contains(Validate(xml, profile).Issues, issue => issue.RuleId == "ODF3101");
    }

    /// <summary>
    /// Numeric metadata producers emit XML Schema special values and retain them after package reload.
    /// 數值中繼資料產生器輸出 XML Schema 特殊值並在封裝重新載入後保留數值。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void NumericMetadataProducersUseXmlSchemaLexicalValues(OdfVersion version)
    {
        foreach (double value in new[] { double.PositiveInfinity, double.NegativeInfinity, double.NaN, double.MaxValue, double.MinValue, 0d, -0d, 1E100, 1E-100 })
        {
            using TextDocument document = TextDocument.Create();
            document.TargetVersion = version;
            document.SetCustomProperty("Value", value);
            using MemoryStream output = new();
            document.SaveToStream(output);
            using TextDocument loaded = TextDocument.Load(new MemoryStream(output.ToArray()));
            foreach (OdfComplianceProfile profile in Profiles(version))
            {
                OdfValidationReport report = OdfPackageValidator.Validate(loaded.Package, profile);
                Assert.True(report.IsValid, report.ToJson());
            }
            Assert.Equal(value, loaded.FindCustomProperty<double>("Value"));
            Assert.Equal(value, Assert.IsType<double>(loaded.GetAllCustomProperties()["Value"]));
        }
    }

    /// <summary>
    /// Reading standard infinity tokens cannot silently replace them with zero.
    /// 讀取標準無限大詞彙不能靜默替換為零。
    /// </summary>
    [Theory]
    [InlineData("INF", double.PositiveInfinity)]
    [InlineData("-INF", double.NegativeInfinity)]
    [InlineData("NaN", double.NaN)]
    public void StandardSpecialValuesRemainNumeric(string lexical, double expected)
    {
        foreach (string type in new[] { "float", " \tfloat\r\n" })
            foreach (string value in new[] { lexical, " \t" + lexical + "\r\n" })
            {
                using TextDocument document = TextDocument.Create();
                document.SetCustomProperty("Value", 0d);
                OdfNode property = document.MetaDom.Descendants().Single(node => node.NamespaceUri == OdfNamespaces.Meta && node.LocalName == "user-defined");
                property.TextContent = value;
                property.SetAttribute("value-type", OdfNamespaces.Meta, type, "meta");
                Assert.Equal(expected, document.FindCustomProperty<double>("Value"));
                Assert.Equal(expected, Assert.IsType<double>(document.GetAllCustomProperties()["Value"]));
            }
    }

    /// <summary>
    /// Omitted metadata types remain strings, and type declarations cannot move to unrelated metadata elements.
    /// 未指定中繼資料型別時維持字串，且型別宣告不能移至無關中繼資料元素。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void MetadataTypeDefaultsAndCarrierRestrictions(OdfVersion version)
    {
        using TextDocument document = TextDocument.Create();
        document.TargetVersion = version;
        document.SetCustomProperty("Value", "123");
        OdfNode property = document.MetaDom.Descendants().Single(node => node.NamespaceUri == OdfNamespaces.Meta && node.LocalName == "user-defined");
        property.RemoveAttribute("value-type", OdfNamespaces.Meta);
        Assert.Equal("123", Assert.IsType<string>(document.FindCustomProperty("Value")));
        Assert.Equal("123", Assert.IsType<string>(document.GetAllCustomProperties()["Value"]));
        using MemoryStream output = new();
        document.SaveToStream(output);
        using TextDocument loaded = TextDocument.Load(new MemoryStream(output.ToArray()));
        Assert.Equal("123", Assert.IsType<string>(loaded.FindCustomProperty("Value")));
        foreach (OdfComplianceProfile profile in Profiles(version))
            Assert.True(OdfPackageValidator.Validate(loaded.Package, profile).IsValid);
        XNamespace meta = OdfNamespaces.Meta;
        XElement wrongCarrier = Document(version, null, "text");
        XElement generator = wrongCarrier.Descendants(meta + "user-defined").Single();
        generator.Name = meta + "generator";
        generator.Attribute(meta + "name")!.Remove();
        generator.SetAttributeValue(meta + "value-type", "string");
        foreach (OdfComplianceProfile profile in Profiles(version))
            Assert.Contains(Validate(wrongCarrier, profile).Issues, issue => issue.RuleId == "ODF3101");
    }

    private static XElement Document(OdfVersion version, string? type, string value)
    {
        XNamespace office = OdfNamespaces.Office;
        XNamespace meta = OdfNamespaces.Meta;
        XElement property = new(meta + "user-defined", new XAttribute(meta + "name", "Value"), value);
        if (type is not null)
            property.SetAttributeValue(meta + "value-type", type);
        return new XElement(office + "document",
            new XAttribute(XNamespace.Xmlns + "o", office), new XAttribute(XNamespace.Xmlns + "m", meta),
            new XAttribute(office + "version", OdfVersionInfo.ToVersionString(version)), new XAttribute(office + "mimetype", "application/vnd.oasis.opendocument.text"),
            new XElement(office + "meta", property), new XElement(office + "body", new XElement(office + "text", new XElement(XName.Get("p", OdfNamespaces.Text)))));
    }

    private static byte[] Serialize(XElement document)
    {
        using MemoryStream output = new();
        using (XmlWriter writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), NewLineHandling = NewLineHandling.Entitize }))
            document.Save(writer);
        return output.ToArray();
    }

    private static OdfValidationReport Validate(XElement document, OdfComplianceProfile profile)
    {
        using MemoryStream input = new(Serialize(document));
        return OdfValidator.Validate(input, "metadata-values.fodt", profile);
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
