using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests the positive value constraint of relative column lengths.
/// 測試相對欄寬長度的正值限制。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfRelativeLengthConformanceTests
{
    /// <summary>
    /// Zero is invalid even though the official grammar accepts its spelling.
    /// 即使官方文法接受其拼寫，零值仍不合法。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ZeroRelativeLengthsAreRejected(OdfVersion version)
    {
        foreach (bool tableColumn in new[] { false, true })
            foreach (string value in new[] { "0*", "00*", new string('0', 120) + "*" })
                foreach (OdfComplianceProfile profile in Profiles(version))
                {
                    OdfValidationReport report = Validate(Document(version, tableColumn, value), profile);
                    Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3101");
                    Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3135");
                }
    }

    /// <summary>
    /// Positive values preserve their full lexical form and namespace aliases.
    /// 正值保留完整詞法形式及命名空間別名。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void PositiveRelativeLengthsSurviveRoundTrips(OdfVersion version)
    {
        foreach (bool tableColumn in new[] { false, true })
            foreach (string value in new[] { "1*", "001*", new string('9', 120) + "*" })
            {
                XElement xml = Document(version, tableColumn, value);
                XElement properties = xml.Descendants(XName.Get(tableColumn ? "table-column-properties" : "column", OdfNamespaces.Style)).Single();
                properties.SetAttributeValue(XName.Get("rel-width", "urn:test:foreign"), "0*");
                OdfNode dom = OdfXmlReader.Parse(Encoding.UTF8.GetBytes(xml.ToString()));
                using MemoryStream output = new();
                OdfXmlWriter.Write(dom, output);
                XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()));
                Assert.Equal(value, (string?)saved.Descendants(properties.Name).Single().Attribute(XName.Get(tableColumn ? "rel-column-width" : "rel-width", OdfNamespaces.Style)));
                Assert.True(Validate(saved, Profiles(version)[1]).IsValid);
                properties.Attribute(XName.Get("rel-width", "urn:test:foreign"))!.Remove();
                foreach (OdfComplianceProfile profile in Profiles(version))
                    Assert.True(Validate(xml, profile).IsValid);
            }
    }

    private static XElement Document(OdfVersion version, bool tableColumn, string value)
    {
        string style = tableColumn
            ? "<s:style s:name='C' s:family='table-column'><s:table-column-properties/></s:style>"
            : $"<s:style s:name='S' s:family='section'><s:section-properties><s:columns xmlns:fo='{OdfNamespaces.Fo}' fo:column-count='1'><s:column/></s:columns></s:section-properties></s:style>";
        XElement xml = XElement.Parse($"<office:document xmlns:office='{OdfNamespaces.Office}' xmlns:s='{OdfNamespaces.Style}' xmlns:text='{OdfNamespaces.Text}' office:version='{OdfVersionInfo.ToVersionString(version)}' office:mimetype='application/vnd.oasis.opendocument.text'><office:styles>{style}</office:styles><office:body><office:text><text:p/></office:text></office:body></office:document>");
        xml.Descendants(XName.Get(tableColumn ? "table-column-properties" : "column", OdfNamespaces.Style)).Single()
            .SetAttributeValue(XName.Get(tableColumn ? "rel-column-width" : "rel-width", OdfNamespaces.Style), value);
        return xml;
    }

    private static OdfValidationReport Validate(XElement xml, OdfComplianceProfile profile)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(xml.ToString()));
        return OdfValidator.Validate(stream, "relative.fodt", profile);
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
