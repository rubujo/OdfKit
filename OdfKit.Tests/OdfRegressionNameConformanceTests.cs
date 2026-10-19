using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests regression names as strings, including empty and absent values.
/// 測試迴歸名稱的字串語意，包含空值及未指定值。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfRegressionNameConformanceTests
{
    /// <summary>
    /// Names preserve string whitespace through DOM reading and writing.
    /// 名稱經 DOM 讀取及寫入後保留字串空白。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void NamesPreserveStringValues(OdfVersion version)
    {
        foreach (string? value in new string?[] { null, "", "  趨勢線  A  ", "line\tname\nnext\rrow", "<curve>&\"'" })
        {
            XElement xml = Document(version);
            XElement properties = xml.Descendants(XName.Get("chart-properties", OdfNamespaces.Style)).Single();
            if (value is not null)
                properties.SetAttributeValue(XName.Get("regression-name", OdfNamespaces.Chart), value);
            OdfNode root = OdfXmlReader.Parse(Encoding.UTF8.GetBytes(xml.ToString()));
            using var output = new MemoryStream();
            OdfXmlWriter.Write(root, output);
            XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()));
            string? actual = (string?)saved.Descendants(XName.Get("chart-properties", OdfNamespaces.Style)).Single().Attribute(XName.Get("regression-name", OdfNamespaces.Chart));
            Assert.Equal(value, actual);
            foreach (OdfComplianceProfile profile in Profiles(version))
            {
                using var input = new MemoryStream(output.ToArray());
                OdfValidationReport report = OdfValidator.Validate(input, "name.fodt", profile);
                Assert.True(report.IsValid, report.ToJson());
            }
        }
    }

    /// <summary>
    /// The native name belongs to chart properties regardless of its prefix.
    /// 原生名稱不依前綴判定，且僅能出現在圖表屬性中。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void NamesRespectNativePlacement(OdfVersion version)
    {
        XElement xml = Document(version);
        xml.SetAttributeValue(XNamespace.Xmlns + "alias", OdfNamespaces.Chart);
        xml.Descendants(XName.Get("p", OdfNamespaces.Text)).Single().SetAttributeValue(XName.Get("regression-name", OdfNamespaces.Chart), "wrong placement");
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(xml.ToString()));
            Assert.Contains(OdfValidator.Validate(input, "name.fodt", profile).Issues, issue => issue.RuleId == "ODF3101");
        }
    }

    private static XElement Document(OdfVersion version) => XElement.Parse($"<office:document xmlns:office='{OdfNamespaces.Office}' xmlns:text='{OdfNamespaces.Text}' xmlns:style='{OdfNamespaces.Style}' xmlns:c='{OdfNamespaces.Chart}' office:version='{OdfVersionInfo.ToVersionString(version)}' office:mimetype='application/vnd.oasis.opendocument.text'><office:styles><style:style style:name='Regression' style:family='chart'><style:chart-properties/></style:style></office:styles><office:body><office:text><text:p/></office:text></office:body></office:document>");
    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version == OdfVersion.Odf13
        ? [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended]
        : [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended];
}
