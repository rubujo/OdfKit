using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests semantic style placement and conditional attribute requirements.
/// 測試語意樣式位置與條件式屬性要求。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfStyleCooccurrenceConformanceTests
{
    /// <summary>
    /// Ruby grid height requires an explicitly false standard grid mode.
    /// 注音網格高度要求明示為假的標準網格模式。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void RubyHeightRequiresNonstandardGridMode(OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            foreach (string mode in new[] { "false", " false " })
                AssertValid(Validate(version, profile, Layout($"style:layout-grid-ruby-height='1mm' style:layout-grid-standard-mode='{mode}'")));
            foreach (string mode in new[] { "", " style:layout-grid-standard-mode='true'" })
                AssertSemanticError(Validate(version, profile, Layout("style:layout-grid-ruby-height='1mm'" + mode)), "ODF3131");
            Assert.Contains(Validate(version, profile, Layout("style:layout-grid-ruby-height='1mm' style:layout-grid-standard-mode='0'")).Issues, issue => issue.RuleId == "ODF3101");
            AssertValid(Validate(version, profile, Layout("style:layout-grid-standard-mode='true'")));
        }
    }

    /// <summary>
    /// Visible area attributes are restricted to automatic styles.
    /// 可見區域屬性僅能用在自動樣式。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void VisibleAreaRequiresAutomaticStyle(OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
            foreach (string name in new[] { "left", "top", "width", "height" })
            {
                string properties = $"<style:graphic-properties draw:visible-area-{name}='1cm'/>";
                string style = $"<style:style style:name='Graphic' style:family='graphic'>{properties}</style:style>";
                AssertValid(Validate(version, profile, "<office:automatic-styles>" + style + "</office:automatic-styles>"));
                AssertSemanticError(Validate(version, profile, "<office:styles>" + style + "</office:styles>"), "ODF3132");
                AssertSemanticError(Validate(version, profile, "<office:styles><style:default-style style:family='graphic'>" + properties + "</style:default-style></office:styles>"), "ODF3132");
            }
    }

    /// <summary>
    /// Conditional sections require a condition attribute, including explicit empty values.
    /// 條件區段要求條件屬性，明示空值仍視為屬性存在。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ConditionalSectionsRequireConditionPresence(OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            Assert.Contains(Validate(version, profile, "", "<text:section text:name='S' text:display='condition'><text:p/></text:section>").Issues, issue => issue.RuleId == "ODF3101");
            AssertValid(Validate(version, profile, "", "<text:section text:name='S' text:display='condition' text:condition=''><text:p/></text:section>"));
            AssertValid(Validate(version, profile, "", "<text:section text:name='S' text:display='none'><text:p/></text:section>"));
        }
    }

    /// <summary>
    /// Namespace aliases and foreign attributes do not change native constraint scopes.
    /// 命名空間別名與外來屬性不改變原生限制範圍。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void AliasesAndForeignAttributesKeepTheirScopes(OdfVersion version)
    {
        OdfComplianceProfile profile = Profiles(version)[1];
        AssertValid(Validate(version, profile, "<office:styles><style:style style:name='G' style:family='graphic'><style:graphic-properties f:visible-area-left='1cm'/></style:style></office:styles>"));
        AssertSemanticError(Validate(version, profile, "<office:styles><s:style s:name='G' s:family='graphic'><s:graphic-properties d:visible-area-left='1cm'/></s:style></office:styles>"), "ODF3132");
        AssertSemanticError(Validate(version, profile, Layout("style:layout-grid-ruby-height='1mm' f:layout-grid-standard-mode='false'")), "ODF3131");
        string separate = "<office:automatic-styles><style:page-layout style:name='A'><style:page-layout-properties style:layout-grid-ruby-height='1mm'/></style:page-layout><style:page-layout style:name='B'><style:page-layout-properties style:layout-grid-standard-mode='false'/></style:page-layout></office:automatic-styles>";
        AssertSemanticError(Validate(version, profile, separate), "ODF3131");
    }

    private static string Layout(string attributes) => $"<office:automatic-styles><style:page-layout style:name='Layout'><style:page-layout-properties {attributes}/></style:page-layout></office:automatic-styles>";

    private static OdfValidationReport Validate(OdfVersion version, OdfComplianceProfile profile, string styles, string body = "<text:p/>")
    {
        string xml = $"<office:document xmlns:office='{OdfNamespaces.Office}' xmlns:text='{OdfNamespaces.Text}' xmlns:style='{OdfNamespaces.Style}' xmlns:s='{OdfNamespaces.Style}' xmlns:draw='{OdfNamespaces.Draw}' xmlns:d='{OdfNamespaces.Draw}' xmlns:f='urn:test:foreign' office:version='{OdfVersionInfo.ToVersionString(version)}' office:mimetype='application/vnd.oasis.opendocument.text'>{styles}<office:body><office:text>{body}</office:text></office:body></office:document>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        return OdfValidator.Validate(stream, "styles.fodt", profile);
    }

    private static void AssertValid(OdfValidationReport report) => Assert.True(report.IsValid, report.ToJson());
    private static void AssertSemanticError(OdfValidationReport report, string rule)
    {
        Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3101");
        Assert.Contains(report.Issues, issue => issue.RuleId == rule);
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
