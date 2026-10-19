using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests the minimum moving-average period beyond the schema's positiveInteger type.
/// 測試超出 schema 正整數型別限制的移動平均最小週期。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfChartRegressionConformanceTests
{
    /// <summary>
    /// The period is at least two without adding an upper integer bound.
    /// 週期至少為二，且不額外加入整數上限。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void RegressionPeriodsRequireAtLeastTwo(OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            foreach (string value in new[] { "1", "+1", "0001", "+0001", " 1 ", "0000000000000000000000000000001" })
            {
                OdfValidationReport report = Validate(version, profile, $"chart:regression-period='{value}'");
                Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3101");
                Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3133");
            }
            foreach (string value in new[] { "2", "+2", "0002", " 2 ", new string('9', 120) })
                AssertValid(Validate(version, profile, $"chart:regression-period='{value}'"));
            AssertValid(Validate(version, profile, ""));
            foreach (string value in new[] { "0", "-1", "1.5", "１", "1\u00a0" })
                Assert.Contains(Validate(version, profile, $"chart:regression-period='{value}'").Issues, issue => issue.RuleId == "ODF3101");
        }
    }

    /// <summary>
    /// Native attributes retain the value constraint independently of the active regression type.
    /// 原生屬性值限制不隨目前使用的迴歸種類改變。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void RegressionPeriodsRespectNamespacesAndValueDomain(OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            Assert.Contains(Validate(version, profile, "cp:regression-period='1'", "linear").Issues, issue => issue.RuleId == "ODF3133");
            AssertValid(Validate(version, profile, "cp:regression-period='2'", "linear"));
        }
        AssertValid(Validate(version, Profiles(version)[1], "f:regression-period='1'"));
    }

    /// <summary>
    /// ODF 1.2 rejects the later attribute through its own grammar.
    /// ODF 1.2 由自身 grammar 拒絕後版屬性。
    /// </summary>
    [Fact]
    public void LegacyVersionsKeepTheirOwnAttributeGrammar()
    {
        OdfValidationReport report = Validate(OdfVersion.Odf12, OdfComplianceProfiles.OasisOdf12Strict, "chart:regression-period='2'", "linear");
        Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3101");
        Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3133");
    }

    private static OdfValidationReport Validate(OdfVersion version, OdfComplianceProfile profile, string attributes, string kind = "moving-average")
    {
        string xml = $"<office:document xmlns:office='{OdfNamespaces.Office}' xmlns:text='{OdfNamespaces.Text}' xmlns:style='{OdfNamespaces.Style}' xmlns:chart='{OdfNamespaces.Chart}' xmlns:cp='{OdfNamespaces.Chart}' xmlns:f='urn:test:foreign' office:version='{OdfVersionInfo.ToVersionString(version)}' office:mimetype='application/vnd.oasis.opendocument.text'><office:styles><style:style style:name='Regression' style:family='chart'><style:chart-properties chart:regression-type='{kind}' {attributes}/></style:style></office:styles><office:body><office:text><text:p/></office:text></office:body></office:document>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        return OdfValidator.Validate(stream, "regression.fodt", profile);
    }

    private static void AssertValid(OdfValidationReport report) => Assert.True(report.IsValid, report.ToJson());
    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version == OdfVersion.Odf13
        ? [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended]
        : [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended];
}
