using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests native frame and axis names as strings rather than XML identifiers.
/// 測試原生框架及座標軸名稱為字串，而非 XML 識別碼。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfFrameAndAxisNameConformanceTests
{
    /// <summary>
    /// Names preserve empty, absent, Unicode and whitespace values through DOM round trips.
    /// 名稱經 DOM 往返後保留空值、未指定值、Unicode 及空白。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12, "frame")]
    [InlineData(OdfVersion.Odf13, "frame")]
    [InlineData(OdfVersion.Odf14, "frame")]
    [InlineData(OdfVersion.Odf12, "axis")]
    [InlineData(OdfVersion.Odf13, "axis")]
    [InlineData(OdfVersion.Odf14, "axis")]
    public void NamesPreserveTheirStringIdentity(OdfVersion version, string kind)
    {
        foreach (string? value in new string?[] { null, "", "  name:名稱  ", "line\tname\nnext\rrow", "<name>&\"'" })
        {
            XElement xml = Document(version, kind);
            (XName elementName, XName attributeName) = Names(kind);
            xml.Descendants(elementName).Single().SetAttributeValue(attributeName, value);
            OdfNode dom = OdfXmlReader.Parse(Encoding.UTF8.GetBytes(xml.ToString()));
            using var output = new MemoryStream();
            OdfXmlWriter.Write(dom, output);
            XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()));
            Assert.Equal(value, (string?)saved.Descendants(elementName).Single().Attribute(attributeName));
            foreach (OdfComplianceProfile profile in Profiles(version))
                AssertValid(Validate(saved, profile));
        }
    }

    /// <summary>
    /// Known native name attributes remain restricted to their declared element.
    /// 已知原生名稱屬性仍限定於其宣告的元素。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12, "frame")]
    [InlineData(OdfVersion.Odf13, "frame")]
    [InlineData(OdfVersion.Odf14, "frame")]
    [InlineData(OdfVersion.Odf12, "axis")]
    [InlineData(OdfVersion.Odf13, "axis")]
    [InlineData(OdfVersion.Odf14, "axis")]
    public void NativeNamePlacementIsChecked(OdfVersion version, string kind)
    {
        XElement xml = Document(version, kind);
        xml.SetAttributeValue(Names(kind).Attribute, "wrong element");
        foreach (OdfComplianceProfile profile in Profiles(version))
            Assert.Contains(Validate(xml, profile).Issues, issue => issue.RuleId == "ODF3101");
    }

    private static (XName Element, XName Attribute) Names(string kind) => kind == "frame"
        ? (XName.Get("floating-frame", OdfNamespaces.Draw), XName.Get("frame-name", OdfNamespaces.Draw))
        : (XName.Get("axis", OdfNamespaces.Chart), XName.Get("name", OdfNamespaces.Chart));
    private static XElement Document(OdfVersion version, string kind)
    {
        string body = kind == "frame"
            ? "<office:text><text:p><d:frame d:name='Frame'><d:floating-frame xlink:type='simple' xlink:href='about:blank'/></d:frame></text:p></office:text>"
            : "<office:chart><c:chart c:class='c:bar'><c:plot-area><c:axis c:dimension='x'/></c:plot-area></c:chart></office:chart>";
        string mime = kind == "frame" ? "text" : "chart";
        return XElement.Parse($"<office:document xmlns:office='{OdfNamespaces.Office}' xmlns:text='{OdfNamespaces.Text}' xmlns:d='{OdfNamespaces.Draw}' xmlns:c='{OdfNamespaces.Chart}' xmlns:xlink='{OdfNamespaces.XLink}' office:version='{OdfVersionInfo.ToVersionString(version)}' office:mimetype='application/vnd.oasis.opendocument.{mime}'><office:body>{body}</office:body></office:document>");
    }
    private static OdfValidationReport Validate(XElement xml, OdfComplianceProfile profile)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml.ToString()));
        string fileName = xml.Attribute(XName.Get("mimetype", OdfNamespaces.Office))?.Value == "application/vnd.oasis.opendocument.chart" ? "names.fodc" : "names.fodt";
        return OdfValidator.Validate(stream, fileName, profile);
    }
    private static void AssertValid(OdfValidationReport report) => Assert.True(report.IsValid, report.ToJson());
    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
