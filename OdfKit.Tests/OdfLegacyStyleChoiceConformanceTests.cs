using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests alternative style families in the official schemas, including legacy combined definitions.
/// 測試官方 schema 的替代樣式家族，包含舊版合併定義。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfLegacyStyleChoiceConformanceTests
{
    /// <summary>
    /// A style selects one family while retaining that family's content restrictions.
    /// 樣式選取一個家族，且保留該家族的內容限制。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf10)]
    [InlineData(OdfVersion.Odf11)]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void FamilyAlternativesKeepTheirAttributeAndContentBranches(OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            foreach (string body in new[] { "<style:style style:name='P' style:family='paragraph'/>", "<style:style style:name='P' style:family='paragraph'><style:paragraph-properties/><style:text-properties/></style:style>", "<style:style style:name='P' style:family='text'><style:text-properties/></style:style>" })
                AssertValid(Validate(Document(version, body), profile));
            foreach (string body in new[] { "<style:style style:name='P' style:family='text'><style:paragraph-properties/></style:style>", "<style:style style:name='P'/>", "<style:style style:name='P' style:family='not-a-family'/>", "<style:style style:family='paragraph'/>" })
                Assert.Contains(Validate(Document(version, body), profile).Issues, issue => issue.RuleId == "ODF3101");
        }
    }

    /// <summary>
    /// Declared legacy paragraph transparency values survive DOM round trips.
    /// 已宣告的舊版段落透明度值經 DOM 往返後仍保留。
    /// </summary>
    [Fact]
    public void LegacyParagraphTransparencySurvivesRoundTrips()
    {
        foreach (string? value in new string?[] { null, "0%", "25.5%", "100%", "-1%", "125%" })
        {
            XElement xml = Document(OdfVersion.Odf11, "<style:style style:name='P' style:family='paragraph'><style:paragraph-properties/></style:style>");
            XElement properties = xml.Descendants(XName.Get("paragraph-properties", OdfNamespaces.Style)).Single();
            properties.SetAttributeValue(XName.Get("background-transparency", OdfNamespaces.Style), value);
            OdfNode dom = OdfXmlReader.Parse(Encoding.UTF8.GetBytes(xml.ToString()));
            using var output = new MemoryStream();
            OdfXmlWriter.Write(dom, output);
            XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()));
            Assert.Equal(value, (string?)saved.Descendants(XName.Get("paragraph-properties", OdfNamespaces.Style)).Single().Attribute(XName.Get("background-transparency", OdfNamespaces.Style)));
            AssertValid(Validate(saved, OdfComplianceProfiles.OasisOdf11));
        }
    }

    private static XElement Document(OdfVersion version, string styles) => XElement.Parse($"<office:document xmlns:office='{OdfNamespaces.Office}' xmlns:style='{OdfNamespaces.Style}' xmlns:text='{OdfNamespaces.Text}' office:version='{OdfVersionInfo.ToVersionString(version)}' office:mimetype='application/vnd.oasis.opendocument.text'><office:styles>{styles}</office:styles><office:body><office:text><text:p/></office:text></office:body></office:document>");
    private static OdfValidationReport Validate(XElement xml, OdfComplianceProfile profile)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml.ToString()));
        return OdfValidator.Validate(stream, "choice.fodt", profile);
    }
    private static void AssertValid(OdfValidationReport report) => Assert.True(report.IsValid, report.ToJson());
    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf10 => [OdfComplianceProfiles.OasisOdf10],
        OdfVersion.Odf11 => [OdfComplianceProfiles.OasisOdf11],
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
