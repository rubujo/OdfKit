using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests animation value constraints and namespace-qualified top-level setting names.
/// 測試動畫值約束及頂層設定名稱的命名空間前綴。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfAnimationAndSettingConformanceTests
{
    /// <summary>
    /// Audio volume must be finite and within the inclusive unit interval.
    /// 音量必須為有限值，且介於包含端點的零至一之間。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void AudioVolumeUsesTheSpecifiedRange(OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            foreach (string value in new[] { "0", "1", ".5", "5e-1", " +1 ", "-0" })
                AssertValid(Validate(version, profile, $"<anim:audio anim:audio-level='{value}'/>"));
            foreach (string value in new[] { "-0.1", "1.0001", "2", "NaN", "INF", "-INF", "1E999" })
            {
                OdfValidationReport report = Validate(version, profile, $"<anim:audio anim:audio-level='{value}'/>");
                Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3101");
                Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3125" && issue.PackagePath == "content.xml");
            }
            AssertValid(Validate(version, profile, "<anim:audio/>"));
            Assert.Contains(Validate(version, profile, "<anim:audio anim:audio-level='bad' anim:id='A' xml:id='B'/>").Issues,
                issue => issue.RuleId == "ODF3126");
            Assert.Contains(Validate(version, profile, "<anim:audio anim:audio-level='not a number'/>").Issues,
                issue => issue.RuleId == "ODF3101");
        }
    }

    /// <summary>
    /// Deprecated animation IDs must agree with a simultaneously present XML ID after normalization.
    /// 已棄用的動畫識別碼經正規化後，必須與同時存在的 XML 識別碼相符。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void SimultaneousAnimationAndXmlIdsAgree(OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
            foreach (string kind in new[] { "audio", "command", "iterate", "par", "seq" })
            {
                string required = kind == "command" ? " anim:command='stop-audio'" : "";
                AssertValid(Validate(version, profile, $"<anim:{kind}{required} anim:id=' A ' xml:id='A'/>"));
                AssertValid(Validate(version, profile, $"<anim:{kind}{required} xml:id='A'/>"));
                OdfValidationReport invalid = Validate(version, profile, $"<anim:{kind}{required} anim:id='A' xml:id='B'/>");
                Assert.DoesNotContain(invalid.Issues, issue => issue.RuleId == "ODF3101");
                Assert.Contains(invalid.Issues, issue => issue.RuleId == "ODF3126");
                Assert.DoesNotContain(Validate(version, profile, $"<anim:{kind}{required} anim:id='A'/>").Issues,
                    issue => issue.RuleId == "ODF3126");
            }
    }

    /// <summary>
    /// Only item sets directly below office:settings require a bound namespace prefix.
    /// 只有 office:settings 的直接子項目集需要已繫結的命名空間前綴。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void TopLevelSettingsUseBoundPrefixesWithoutConstrainingNestedNames(OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            foreach (string name in new[] { "p:settings", "p:", "p:application:name", "xml:settings", "名:設定" })
                AssertValid(Validate(version, profile, "", Settings(version, ItemSet(name))));
            foreach (string name in new[] { "unqualified", "u:unbound", ":empty", "bad prefix:value", "xmlns:value", " p:leading" })
            {
                OdfValidationReport invalid = Validate(version, profile, "", Settings(version, ItemSet(name)));
                Assert.DoesNotContain(invalid.Issues, issue => issue.RuleId == "ODF3101");
                Assert.Contains(invalid.Issues, issue => issue.RuleId == "ODF3127" && issue.PackagePath == "settings.xml");
            }
            AssertValid(Validate(version, profile, "", Settings(version,
                "<config:config-item-set config:name='p:top'>" + ItemSet("nested") + "</config:config-item-set>")));
            AssertValid(Validate(version, profile, "", Settings(version,
                "<config:config-item-set xmlns:local='urn:test:local' config:name='local:value'>" + Item + "</config:config-item-set>")));
        }
    }

    /// <summary>
    /// Foreign content is isolated and alternate prefixes retain native validation.
    /// 外來內容會隔離處理，替代前綴仍保留原生驗證。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ForeignContentCannotAlterNativeConstraints(OdfVersion version)
    {
        OdfComplianceProfile profile = Profiles(version)[1];
        AssertValid(Validate(version, profile,
            "<anim:audio f:audio-level='2' f:id='A' xml:id='B'/><f:opaque><anim:audio anim:audio-level='2' anim:id='A' xml:id='C'/></f:opaque>"));
        string renamed = "<a:audio a:audio-level='2' a:id='A' xml:id='B'/>";
        OdfValidationReport invalid = Validate(version, profile, renamed);
        Assert.Contains(invalid.Issues, issue => issue.RuleId == "ODF3125");
        Assert.Contains(invalid.Issues, issue => issue.RuleId == "ODF3126");
        AssertValid(Validate(version, profile, "", Settings(version,
            ItemSet("p:valid") + "<f:opaque>" + ItemSet("invalid") + "</f:opaque>")));
        string alias = Settings(version, ItemSet("invalid").Replace("config:", "cfg:", StringComparison.Ordinal));
        Assert.Contains(Validate(version, profile, "", alias).Issues, issue => issue.RuleId == "ODF3127");
    }

    /// <summary>
    /// Flat and embedded XML documents run the constraints against their own entries.
    /// 單一 XML 及內嵌 XML 文件會針對各自的項目執行約束。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void FlatAndEmbeddedEntriesReceiveTheSameChecks(OdfVersion version)
    {
        string animation = "<anim:audio anim:audio-level='2' anim:id='A' xml:id='B'/>";
        string settings = "<office:settings>" + ItemSet("invalid") + "</office:settings>";
        string flat = Content(version, animation).Replace("document-content", "document", StringComparison.Ordinal)
            .Replace(" office:version=", " office:mimetype='application/vnd.oasis.opendocument.presentation' office:version=", StringComparison.Ordinal)
            .Replace("<office:body>", settings + StyleParts + "<office:body>", StringComparison.Ordinal);
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(flat));
            OdfValidationReport report = OdfValidator.Validate(input, "rules.fodp", profile);
            foreach (string rule in new[] { "ODF3125", "ODF3126", "ODF3127" })
                Assert.Contains(report.Issues, issue => issue.RuleId == rule);
            OdfValidationReport embedded = Validate(version, profile, "", embeddedContent: Content(version, animation),
                embeddedSettings: Settings(version, ItemSet("invalid")));
            Assert.Contains(embedded.Issues, issue => issue.RuleId == "ODF3125" && issue.PackagePath == "Object1/content.xml");
            Assert.Contains(embedded.Issues, issue => issue.RuleId == "ODF3126" && issue.PackagePath == "Object1/content.xml");
            Assert.Contains(embedded.Issues, issue => issue.RuleId == "ODF3127" && issue.PackagePath == "Object1/settings.xml");
        }
    }

    private const string Item = "<config:config-item config:name='value' config:type='string'>text</config:config-item>";
    private static string ItemSet(string name) => $"<config:config-item-set config:name='{name}'>{Item}</config:config-item-set>";
    private const string StyleParts = "<office:automatic-styles><style:page-layout style:name='Layout'><style:page-layout-properties/></style:page-layout></office:automatic-styles><office:master-styles><style:master-page style:name='Default' style:page-layout-name='Layout'/></office:master-styles>";
    private static string Namespaces => $"xmlns:office='{OdfNamespaces.Office}' xmlns:draw='{OdfNamespaces.Draw}' xmlns:style='{OdfNamespaces.Style}' xmlns:anim='{OdfNamespaces.Anim}' xmlns:a='{OdfNamespaces.Anim}' xmlns:config='{OdfNamespaces.Config}' xmlns:cfg='{OdfNamespaces.Config}' xmlns:p='urn:test:implementation' xmlns:名='urn:test:unicode' xmlns:f='urn:test:foreign'";
    private static string Content(OdfVersion version, string animation) =>
        $"<office:document-content {Namespaces} office:version='{OdfVersionInfo.ToVersionString(version)}'><office:body><office:presentation><draw:page draw:name='Page' draw:master-page-name='Default'>{animation}</draw:page></office:presentation></office:body></office:document-content>";
    private static string Settings(OdfVersion version, string settings) =>
        $"<office:document-settings {Namespaces} office:version='{OdfVersionInfo.ToVersionString(version)}'><office:settings>{settings}</office:settings></office:document-settings>";

    private static void AssertValid(OdfValidationReport report) => Assert.True(report.IsValid, report.ToJson());

    private static OdfValidationReport Validate(OdfVersion version, OdfComplianceProfile profile, string animation,
        string? settings = null, string? embeddedContent = null, string? embeddedSettings = null)
    {
        using var stream = new MemoryStream();
        using (OdfPackage writer = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Presentation, version, true))
        {
            writer.WriteEntry("content.xml", Encoding.UTF8.GetBytes(Content(version, animation)), "text/xml");
            string styles = $"<office:document-styles {Namespaces} office:version='{OdfVersionInfo.ToVersionString(version)}'>{StyleParts}</office:document-styles>";
            writer.WriteEntry("styles.xml", Encoding.UTF8.GetBytes(styles), "text/xml");
            if (settings is not null)
                writer.WriteEntry("settings.xml", Encoding.UTF8.GetBytes(settings), "text/xml");
            if (embeddedContent is not null)
            {
                writer.WriteEntry("Object1/content.xml", Encoding.UTF8.GetBytes(embeddedContent), "text/xml");
                writer.WriteEntry("Object1/styles.xml", Encoding.UTF8.GetBytes(styles), "text/xml");
            }
            if (embeddedSettings is not null)
                writer.WriteEntry("Object1/settings.xml", Encoding.UTF8.GetBytes(embeddedSettings), "text/xml");
            writer.Save();
        }
        stream.Position = 0;
        using OdfPackage package = OdfPackage.Open(stream);
        byte[] original = (byte[])package.ReadEntry("content.xml").Clone();
        byte[]? originalSettings = settings is null ? null : (byte[])package.ReadEntry("settings.xml").Clone();
        OdfValidationReport report = OdfPackageValidator.Validate(package, profile);
        Assert.Equal(original, package.ReadEntry("content.xml"));
        if (originalSettings is not null)
            Assert.Equal(originalSettings, package.ReadEntry("settings.xml"));
        return report;
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
