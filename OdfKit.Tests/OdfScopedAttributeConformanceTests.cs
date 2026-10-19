using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests container-local property names, document page names and versioned shape ordering.
/// 測試容器區域屬性名稱、文件頁面名稱及分版的形狀排序限制。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfScopedAttributeConformanceTests
{
    /// <summary>
    /// Property and list-property names share one uniqueness scope per direct container.
    /// 屬性與清單屬性的名稱在每個直接容器內共用唯一性範圍。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void PropertyNamesAreUniqueWithinTheirContainer(OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            AssertValid(Validate(Form(Properties(Property("A") + List("a") + Property(" A "))), version, profile));
            AssertValid(Validate(Form(Properties(Property("A"))) + Form(Properties(List("A"))), version, profile));
            foreach (string repeated in new[] { Property("A"), List("A") })
            {
                OdfValidationReport report = Validate(Form(Properties(Property("A") + repeated)), version, profile);
                Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3101");
                Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3128");
            }
        }
    }

    /// <summary>
    /// Page names are unique across pages while unrelated drawing names remain independent.
    /// 頁面名稱在各頁間唯一，其他繪圖名稱維持獨立。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void PageNamesAreUniqueWithinTheirDocument(OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            AssertValid(Validate(Page("A", Rect("draw:name='A'")) + Page("a") + Page(" A "), version, profile, true));
            AssertValid(Validate("<draw:page draw:master-page-name='Default'/><draw:page draw:master-page-name='Default'/>", version, profile, true));
            foreach (string name in new[] { "A", "" })
            {
                OdfValidationReport report = Validate(Page(name) + Page(name), version, profile, true);
                Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3101");
                Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3129");
            }
            AssertValid(Validate(Page("A"), version, profile, true, embedded: Content(Page("A"), version, true)));
        }
    }

    /// <summary>
    /// The grouped-shape z-index prohibition starts in ODF 1.3 and applies to direct parents.
    /// 群組形狀的 z-index 禁止條件從 ODF 1.3 起適用，並依直接父元素判斷。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void GroupedShapeOrderingRespectsTheVersion(OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            AssertValid(Validate(Page("A", Rect("draw:z-index='1'") + "<draw:g>" + Rect("") + "</draw:g>"), version, profile, true));
            foreach (string group in new[]
            {
                "<draw:g>" + Rect("draw:z-index='1'") + "</draw:g>",
                "<dr3d:scene><dr3d:cube draw:z-index='1'/></dr3d:scene>"
            })
            {
                OdfValidationReport report = Validate(Page("A", group), version, profile, true);
                Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3101");
                if (version == OdfVersion.Odf12)
                    Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3130");
                else
                    Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3130");
            }
        }
    }

    /// <summary>
    /// Foreign properties and shapes do not participate in native uniqueness scopes.
    /// 外來屬性及形狀不參與原生唯一性範圍。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ForeignScopesAndAliasesRemainDistinct(OdfVersion version)
    {
        OdfComplianceProfile profile = Profiles(version)[1];
        string isolated = Form(Properties(Property("A") + "<f:opaque>" + Property("A") + "</f:opaque>"));
        AssertValid(Validate(isolated, version, profile));
        string alias = Form(Properties(Property("A") + List("A"))).Replace("form:", "frm:", StringComparison.Ordinal);
        Assert.Contains(Validate(alias, version, profile).Issues, issue => issue.RuleId == "ODF3128");
        AssertValid(Validate(Page("A") + "<f:opaque>" + Page("A") + "</f:opaque>", version, profile, true));
        AssertValid(Validate(Page("A", "<draw:g>" + Rect("f:z-index='1'") + "</draw:g>"), version, profile, true));
    }

    /// <summary>
    /// Flat and embedded entries execute property uniqueness against their own XML tree.
    /// 單一 XML 與內嵌項目會對各自的 XML 樹執行屬性唯一性檢查。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void FlatAndEmbeddedEntriesHaveTheirOwnScopes(OdfVersion version)
    {
        string forms = Form(Properties(Property("A") + List("A")));
        string xml = Content(forms, version, false);
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            string flat = xml.Replace("document-content", "document", StringComparison.Ordinal)
                .Replace(" office:version=", " office:mimetype='application/vnd.oasis.opendocument.text' office:version=", StringComparison.Ordinal);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(flat));
            Assert.Contains(OdfValidator.Validate(stream, "properties.fodt", profile).Issues, issue => issue.RuleId == "ODF3128");
            OdfValidationReport report = Validate("", version, profile, embedded: xml);
            Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3128" && issue.PackagePath == "Object1/content.xml");
        }
    }

    private static string Property(string name) => $"<form:property form:property-name='{name}' office:value-type='string' office:string-value='text'/>";
    private static string List(string name) => $"<form:list-property form:property-name='{name}' office:value-type='string'/>";
    private static string Properties(string children) => $"<form:properties>{children}</form:properties>";
    private static string Form(string children) => $"<form:form>{children}</form:form>";
    private static string Rect(string attributes) => $"<draw:rect svg:width='1cm' svg:height='1cm' {attributes}/>";
    private static string Page(string name, string children = "") => $"<draw:page draw:name='{name}' draw:master-page-name='Default'>{children}</draw:page>";
    private static string Namespaces => $"xmlns:office='{OdfNamespaces.Office}' xmlns:text='{OdfNamespaces.Text}' xmlns:form='{OdfNamespaces.Form}' xmlns:frm='{OdfNamespaces.Form}' xmlns:draw='{OdfNamespaces.Draw}' xmlns:dr3d='{OdfNamespaces.Dr3d}' xmlns:svg='{OdfNamespaces.Svg}' xmlns:style='{OdfNamespaces.Style}' xmlns:f='urn:test:foreign'";
    private const string StyleParts = "<office:automatic-styles><style:page-layout style:name='Layout'><style:page-layout-properties/></style:page-layout></office:automatic-styles><office:master-styles><style:master-page style:name='Default' style:page-layout-name='Layout'/></office:master-styles>";
    private static string Content(string children, OdfVersion version, bool presentation) =>
        $"<office:document-content {Namespaces} office:version='{OdfVersionInfo.ToVersionString(version)}'><office:body>" +
        (presentation ? $"<office:presentation>{children}</office:presentation>" : $"<office:text><office:forms>{children}</office:forms><text:p/></office:text>") +
        "</office:body></office:document-content>";

    private static OdfValidationReport Validate(string children, OdfVersion version, OdfComplianceProfile profile,
        bool presentation = false, string? embedded = null)
    {
        using var stream = new MemoryStream();
        using (OdfPackage writer = OdfDocumentFactory.CreatePackage(stream, presentation ? OdfDocumentKind.Presentation : OdfDocumentKind.Text, version, true))
        {
            writer.WriteEntry("content.xml", Encoding.UTF8.GetBytes(Content(children, version, presentation)), "text/xml");
            string styles = $"<office:document-styles {Namespaces} office:version='{OdfVersionInfo.ToVersionString(version)}'>{StyleParts}</office:document-styles>";
            writer.WriteEntry("styles.xml", Encoding.UTF8.GetBytes(styles), "text/xml");
            if (embedded is not null)
            {
                writer.WriteEntry("Object1/content.xml", Encoding.UTF8.GetBytes(embedded), "text/xml");
                writer.WriteEntry("Object1/styles.xml", Encoding.UTF8.GetBytes(styles), "text/xml");
            }
            writer.Save();
        }
        stream.Position = 0;
        using OdfPackage package = OdfPackage.Open(stream);
        byte[] original = (byte[])package.ReadEntry("content.xml").Clone();
        OdfValidationReport report = OdfPackageValidator.Validate(package, profile);
        Assert.Equal(original, package.ReadEntry("content.xml"));
        return report;
    }

    private static void AssertValid(OdfValidationReport report) => Assert.True(report.IsValid, report.ToJson());
    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
