using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests version-specific writing-mode constraints beyond the shared schema enumeration.
/// 測試共用 schema 列舉以外的版本限定書寫方向限制。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfWritingModeConformanceTests
{
    /// <summary>
    /// Page layouts cannot inherit their own writing mode in ODF 1.4.
    /// ODF 1.4 的頁面配置不得繼承自身的書寫方向。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void PageLayoutPageValueUsesVersionSpecificRestriction(OdfVersion version)
    {
        foreach (string value in new[] { "page", " page ", "&#x9;page&#xA;" })
            foreach (OdfComplianceProfile profile in Profiles(version))
            {
                string xml = Styles(version, PageLayout(value));
                OdfValidationReport report = ValidateStyles(xml, version, profile);
                Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3101");
                if (version == OdfVersion.Odf14)
                    Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3118" && issue.PackagePath == "styles.xml");
                else
                    Assert.True(report.IsValid, report.ToJson());
            }
    }

    /// <summary>
    /// Graphic styles require identical explicit paragraph and graphic writing-mode values in ODF 1.4.
    /// ODF 1.4 圖形樣式的段落與圖形屬性須有相同的明確書寫方向值。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void GraphicStyleModesMustAgreeOnlyInReviewedVersion(OdfVersion version)
    {
        foreach (bool defaultStyle in new[] { false, true })
            foreach (OdfComplianceProfile profile in Profiles(version))
            {
                string xml = Styles(version, GraphicStyle("lr-tb", "rl-tb", defaultStyle));
                OdfValidationReport report = ValidateStyles(xml, version, profile);
                Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3101");
                if (version == OdfVersion.Odf14)
                    Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3119" && issue.PackagePath == "styles.xml");
                else
                    Assert.True(report.IsValid, report.ToJson());
            }
    }

    /// <summary>
    /// Explicit layout directions and legitimate page inheritance retain their original meaning.
    /// 明確配置方向與合法的頁面繼承保留原有語意。
    /// </summary>
    [Fact]
    public void ValidDirectionsAndOtherPagePropertiesRemainValid()
    {
        foreach (OdfComplianceProfile profile in Profiles(OdfVersion.Odf14))
        {
            foreach (string value in new[] { "lr-tb", "lr", "rl-tb", "rl", "tb-rl", "tb", "tb-lr", "sideways-rl", "sideways-lr" })
            {
                OdfValidationReport report = ValidateStyles(Styles(OdfVersion.Odf14, PageLayout(value)), OdfVersion.Odf14, profile);
                Assert.True(report.IsValid, report.ToJson());
            }
            foreach (string content in new[]
            {
                GraphicStyle("page", " page ", false),
                GraphicStyle(null, "page", false),
                GraphicStyle("page", null, true),
                "<office:styles><style:style style:name='P' style:family='paragraph'><style:paragraph-properties style:writing-mode='page'/></style:style></office:styles>",
                "<office:styles><style:style style:name='T' style:family='table'><style:table-properties style:writing-mode='page'/></style:style></office:styles>",
                "<office:styles><style:style style:name='C' style:family='table-cell'><style:table-cell-properties style:writing-mode='page'/></style:style></office:styles>",
                "<office:styles><style:style style:name='S' style:family='section'><style:section-properties style:writing-mode='page'/></style:style></office:styles>"
            })
            {
                OdfValidationReport report = ValidateStyles(Styles(OdfVersion.Odf14, content), OdfVersion.Odf14, profile);
                Assert.True(report.IsValid, report.ToJson());
            }
        }
    }

    /// <summary>
    /// Alternate prefixes and foreign attributes are distinguished by namespace.
    /// 替代前綴與外來屬性須依命名空間區分。
    /// </summary>
    [Fact]
    public void NamespaceIdentityControlsWritingModeChecks()
    {
        string renamed = Styles(OdfVersion.Odf14, PageLayout("page"))
            .Replace("<style:", "<s:", StringComparison.Ordinal).Replace("</style:", "</s:", StringComparison.Ordinal)
            .Replace(" style:", " s:", StringComparison.Ordinal).Replace("xmlns:style=", "xmlns:s=", StringComparison.Ordinal);
        OdfValidationReport native = ValidateStyles(renamed, OdfVersion.Odf14, Profiles(OdfVersion.Odf14)[0]);
        Assert.DoesNotContain(native.Issues, issue => issue.RuleId == "ODF3101");
        Assert.Contains(native.Issues, issue => issue.RuleId == "ODF3118");

        string foreign = Styles(OdfVersion.Odf14, PageLayout("page")).Replace("style:writing-mode=", "f:writing-mode=", StringComparison.Ordinal);
        OdfValidationReport ignored = ValidateStyles(foreign, OdfVersion.Odf14, Profiles(OdfVersion.Odf14)[1]);
        Assert.True(ignored.IsValid, ignored.ToJson());
    }

    /// <summary>
    /// Embedded styles and flat XML receive the same version-specific restrictions.
    /// 內嵌樣式與單一 XML 文件套用相同的版本限定限制。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void EmbeddedAndFlatStylesUseTheirDocumentVersion(OdfVersion version)
    {
        string xml = Styles(version, PageLayout("page"));
        OdfValidationReport embedded = ValidateStyles(xml, OdfVersion.Odf14, Profiles(OdfVersion.Odf14)[0], "Object1/styles.xml");
        if (version == OdfVersion.Odf14)
            Assert.Contains(embedded.Issues, issue => issue.RuleId == "ODF3118" && issue.PackagePath == "Object1/styles.xml");
        else
            Assert.DoesNotContain(embedded.Issues, issue => issue.RuleId is "ODF3118" or "ODF3101");

        string flat = xml.Replace("document-styles", "document", StringComparison.Ordinal)
            .Replace(" office:version=", " office:mimetype='application/vnd.oasis.opendocument.text' office:version=", StringComparison.Ordinal)
            .Replace("</office:document>", "<office:body><office:text/></office:body></office:document>", StringComparison.Ordinal);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(flat));
        OdfValidationReport flatReport = OdfValidator.Validate(input, "styles.fodt", Profiles(version)[0]);
        if (version == OdfVersion.Odf14)
            Assert.Contains(flatReport.Issues, issue => issue.RuleId == "ODF3118");
        else
            Assert.True(flatReport.IsValid, flatReport.ToJson());
    }

    private static string PageLayout(string value) =>
        $"<office:automatic-styles><style:page-layout style:name='L'><style:page-layout-properties style:writing-mode='{value}'/></style:page-layout></office:automatic-styles>";

    private static string GraphicStyle(string? graphic, string? paragraph, bool defaultStyle)
    {
        string element = defaultStyle ? "default-style" : "style";
        string name = defaultStyle ? string.Empty : " style:name='G'";
        string graphicValue = graphic is null ? string.Empty : $" style:writing-mode='{graphic}'";
        string paragraphValue = paragraph is null ? string.Empty : $" style:writing-mode='{paragraph}'";
        return $"<office:styles><style:{element}{name} style:family='graphic'><style:graphic-properties{graphicValue}/><style:paragraph-properties{paragraphValue}/></style:{element}></office:styles>";
    }

    private static string Styles(OdfVersion version, string content) =>
        $"<office:document-styles xmlns:office='{OdfNamespaces.Office}' xmlns:style='{OdfNamespaces.Style}' xmlns:f='urn:test:foreign' office:version='{OdfVersionInfo.ToVersionString(version)}'>{content}</office:document-styles>";

    private static OdfValidationReport ValidateStyles(string xml, OdfVersion version, OdfComplianceProfile profile, string path = "styles.xml")
    {
        using var stream = new MemoryStream();
        using (OdfPackage writer = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Text, version, true))
        {
            writer.WriteEntry(path, Encoding.UTF8.GetBytes(xml), "text/xml");
            writer.Save();
        }
        stream.Position = 0;
        using OdfPackage package = OdfPackage.Open(stream);
        byte[] original = (byte[])package.ReadEntry(path).Clone();
        OdfValidationReport report = OdfPackageValidator.Validate(package, profile);
        Assert.Equal(original, package.ReadEntry(path));
        return report;
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
