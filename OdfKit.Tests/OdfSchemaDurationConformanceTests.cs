using System.Text;
using System.Xml;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests XML Schema duration lexical rules independently of CLR duration capacity.
/// 測試 XML Schema 時間長度詞彙規則且不受 CLR 時間長度容量限制。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfSchemaDurationConformanceTests
{
    /// <summary>
    /// Duration components use ordered unsigned numbers and require fractional digits after a decimal point.
    /// 時間長度分量使用依序的無號數值且小數點後必須有數字。
    /// </summary>
    [Theory]
    [InlineData("P0Y", true)]
    [InlineData("-P0D", true)]
    [InlineData("PT0S", true)]
    [InlineData("P1Y2M3DT4H5M6.123456789S", true)]
    [InlineData("P1347M", true)]
    [InlineData("P1Y2MT2H", true)]
    [InlineData("P999999999999999999999Y", true)]
    [InlineData("P999999999999999999999M", true)]
    [InlineData("P999999999999999999999D", true)]
    [InlineData("PT999999999999999999999H", true)]
    [InlineData("PT999999999999999999999M", true)]
    [InlineData("PT999999999999999999999S", true)]
    [InlineData("-PT999999999999999999999.000000000000000000001S", true)]
    [InlineData(" \tP1D\r\n", true)]
    [InlineData("", false)]
    [InlineData("P", false)]
    [InlineData("PT", false)]
    [InlineData("P1DT", false)]
    [InlineData("+P1D", false)]
    [InlineData("P-1D", false)]
    [InlineData("p1d", false)]
    [InlineData("P1W", false)]
    [InlineData("P1.5Y", false)]
    [InlineData("PT.5S", false)]
    [InlineData("PT1.S", false)]
    [InlineData("PT1.2.3S", false)]
    [InlineData("PT1e2S", false)]
    [InlineData("P1D1Y", false)]
    [InlineData("P1Y1Y", false)]
    [InlineData("PT1M1H", false)]
    [InlineData("P1H", false)]
    [InlineData("PT1D", false)]
    [InlineData("P１D", false)]
    [InlineData("P1 D", false)]
    [InlineData("P1D\u00a0", false)]
    public void DurationLexicalSpaceHasNoClrCapacityLimit(string value, bool expected)
        => Check(value, expected);

    /// <summary>
    /// Long components remain linear scans and cannot bypass unit or fractional rules.
    /// 長分量維持線性掃描且不能繞過單位或小數規則。
    /// </summary>
    [Fact]
    public void LongDurationComponentsKeepTheirLexicalBoundaries()
    {
        string digits = new('9', 100000);
        Check("P" + digits + "Y", true);
        Check("P" + digits + "Yx", false);
        Check("P" + digits + ".0Y", false);
        Check("PT0." + digits + "S", true);
        Check("P1DT" + digits + "S", true);
    }

    private static void Check(string value, bool expected)
    {
        foreach (string type in new[] { "duration", "xsd:duration" })
        {
            OdfSchemaPatternNode data = new(OdfSchemaPatternNodeKind.Data, "exactlyOne", "", "", "", type, "", null, null, null, "http://www.w3.org/2001/XMLSchema-datatypes");
            OdfSchemaSet schema = new(OdfVersion.Odf14, new Uri("https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/"), "2004-10-28", [], attributes: null, nameClasses: null, patterns: [new OdfSchemaPatternDefinition("start", [new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "exactlyOne", "", "root", "", "", "", null, [data])])]);
            XElement xml = new("root", value);
            Assert.Equal(expected, OdfSchemaPatternValidator.ValidateElement(xml, schema, "start").IsMatch);
            Assert.Equal(value, xml.Value);
        }
    }

    /// <summary>
    /// Native metadata retains valid wide durations and rejects malformed duration values.
    /// 原生中繼資料保留合法的大範圍時間長度並拒絕格式錯誤的時間長度值。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void NativeMetadataUsesTheDurationLexicalSpace(OdfVersion version)
    {
        foreach (string value in new[] { "P999999999999999999999Y", "PT999999999999999999999.123456789S", "-P1Y2M3DT4H5M6.123456789S", " \tP1D\r\n" })
        {
            XElement xml = Document(version, value);
            foreach (OdfComplianceProfile profile in Profiles(version))
                AssertValid(Validate(xml, profile));
            byte[] bytes = Serialize(xml);
            foreach (bool stream in new[] { false, true })
            {
                using MemoryStream input = new(bytes);
                OdfNode root = stream ? OdfXmlReader.Parse(input) : OdfXmlReader.Parse(bytes);
                OdfNode property = root.Descendants().Single(node => node.NamespaceUri == OdfNamespaces.Meta && node.LocalName == "user-defined");
                Assert.Equal(value, property.TextContent);
                using MemoryStream output = new();
                OdfXmlWriter.Write(root, output);
                XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()), LoadOptions.PreserveWhitespace);
                Assert.Equal(value, saved.Descendants(XName.Get("user-defined", OdfNamespaces.Meta)).Single().Value);
                foreach (OdfComplianceProfile profile in Profiles(version))
                    AssertValid(Validate(saved, profile));
            }
        }
        foreach (string value in new[] { "PT1.S", "PT.5S", "P1W", "P1DT", "+P1D" })
            foreach (OdfComplianceProfile profile in Profiles(version))
                Assert.Contains(Validate(Document(version, value), profile).Issues, issue => issue.RuleId == "ODF3101");
    }

    private static XElement Document(OdfVersion version, string value)
    {
        XNamespace office = OdfNamespaces.Office;
        XNamespace meta = OdfNamespaces.Meta;
        return new XElement(office + "document",
            new XAttribute(XNamespace.Xmlns + "o", office), new XAttribute(XNamespace.Xmlns + "m", meta),
            new XAttribute(office + "version", OdfVersionInfo.ToVersionString(version)),
            new XAttribute(office + "mimetype", "application/vnd.oasis.opendocument.text"),
            new XElement(office + "meta", new XElement(meta + "user-defined", new XAttribute(meta + "name", "Duration"), new XAttribute(meta + "value-type", "time"), value)),
            new XElement(office + "body", new XElement(office + "text", new XElement(XName.Get("p", OdfNamespaces.Text)))));
    }

    private static OdfValidationReport Validate(XElement document, OdfComplianceProfile profile)
    {
        using MemoryStream input = new(Serialize(document));
        return OdfValidator.Validate(input, "duration.fodt", profile);
    }

    private static byte[] Serialize(XElement document)
    {
        using MemoryStream output = new();
        using (XmlWriter writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), NewLineHandling = NewLineHandling.Entitize }))
            document.Save(writer);
        return output.ToArray();
    }

    private static void AssertValid(OdfValidationReport report) => Assert.True(report.IsValid, report.ToJson());

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
