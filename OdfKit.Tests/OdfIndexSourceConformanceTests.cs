using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests conditional attributes and source-local uniqueness in document indexes.
/// 測試文件索引的條件式屬性及來源範圍內的唯一性。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfIndexSourceConformanceTests
{
    /// <summary>
    /// Caption-based indexes require a sequence name, including when the default enables captions.
    /// 使用圖表標題的索引須有序列名稱，包含依預設值啟用圖表標題的情況。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void CaptionSequencePresenceUsesEffectiveCaptionSetting(OdfVersion version)
    {
        foreach (string kind in new[] { "illustration-index", "table-index" })
            foreach (OdfComplianceProfile profile in Profiles(version))
            {
                foreach (string attributes in new[] { "", " text:use-caption='true'", " text:use-caption=' true '" })
                {
                    OdfValidationReport report = ValidateBody(Index(kind, "", attributes), version, profile);
                    Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3101");
                    Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3120" && issue.PackagePath == "content.xml");
                    OdfValidationReport present = ValidateBody(Index(kind, "", attributes + " text:caption-sequence-name=''"), version, profile);
                    Assert.True(present.IsValid, present.ToJson());
                }
                OdfValidationReport names = ValidateBody(Index(kind, "", " text:use-caption='false'"), version, profile);
                Assert.True(names.IsValid, names.ToJson());
            }
    }

    /// <summary>
    /// Templates and source style groups use their typed key values within each source.
    /// 範本及來源樣式群組在各來源內依型別值判定鍵值唯一性。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void SourceKeysMustBeUniqueAfterDatatypeNormalization(OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
            foreach (string scenario in new[] { "toc", "bibliography", "source-styles", "user-source-styles" })
            {
                (string kind, string first, string same, string other, string rule) = Scenario(scenario);
                string duplicate = Index(kind, first + same);
                OdfValidationReport invalid = ValidateBody(duplicate, version, profile);
                Assert.DoesNotContain(invalid.Issues, issue => issue.RuleId == "ODF3101");
                Assert.Contains(invalid.Issues, issue => issue.RuleId == rule && issue.Severity == OdfIssueSeverity.Error);
                OdfValidationReport distinct = ValidateBody(Index(kind, first + other), version, profile);
                Assert.True(distinct.IsValid, distinct.ToJson());
                OdfValidationReport separate = ValidateBody(Index(kind, first, name: "A") + Index(kind, same, name: "B"), version, profile);
                Assert.True(separate.IsValid, separate.ToJson());
            }
    }

    /// <summary>
    /// Foreign wrappers and attributes follow Extended isolation before native index checks.
    /// 外來包裝及屬性先依擴充隔離規則處理，再檢查原生索引。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ForeignContentCannotSupplyRequiredNativeAttributes(OdfVersion version)
    {
        OdfComplianceProfile profile = Profiles(version)[1];
        string missing = Index("table-index", "", " text:use-caption='true' f:caption-sequence-name='Figure'");
        Assert.Contains(ValidateBody(missing, version, profile).Issues, issue => issue.RuleId == "ODF3120");
        string ignored = "<f:opaque>" + Index("table-index", "") + "</f:opaque><text:p>visible</text:p>";
        OdfValidationReport report = ValidateBody(ignored, version, profile);
        Assert.True(report.IsValid, report.ToJson());
        string renamed = Index("table-index", "", " text:use-caption='true'")
            .Replace("<text:", "<t:", StringComparison.Ordinal).Replace("</text:", "</t:", StringComparison.Ordinal)
            .Replace(" text:", " t:", StringComparison.Ordinal);
        Assert.Contains(ValidateBody(renamed, version, profile).Issues, issue => issue.RuleId == "ODF3120");
    }

    /// <summary>
    /// Flat XML and embedded documents share the conditional index checks.
    /// 單一 XML 與內嵌文件共用條件式索引檢查。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void FlatAndEmbeddedIndexesUseTheirOwnSources(OdfVersion version)
    {
        string body = Index("table-index", "", " text:use-caption='true'");
        string flat = Content(version, body).Replace("document-content", "document", StringComparison.Ordinal)
            .Replace(" office:version=", " office:mimetype='application/vnd.oasis.opendocument.text' office:version=", StringComparison.Ordinal);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(flat));
        Assert.Contains(OdfValidator.Validate(input, "index.fodt", Profiles(version)[0]).Issues,
            issue => issue.RuleId == "ODF3120");
        OdfValidationReport embedded = ValidateBody("<text:p>outer</text:p>", OdfVersion.Odf14, Profiles(OdfVersion.Odf14)[0], Content(version, body));
        Assert.Contains(embedded.Issues, issue => issue.RuleId == "ODF3120" && issue.PackagePath == "Object1/content.xml");
    }

    /// <summary>
    /// Replacing default or custom templates produces a single template per key.
    /// 取代預設或自訂範本後，每個鍵值只保留一個範本。
    /// </summary>
    [Fact]
    public void RepeatedBuilderCallsReplaceTemplatesWithoutDuplicateDefinitions()
    {
        using var document = TextDocument.Create();
        var toc = document.AddTableOfContents("Contents", 2);
        var previous = toc.AddEntryTemplate(1, "First");
        previous.AddSpan("old");
        toc.AddEntryTemplate(1, "Last").AddSpan("new");
        previous.AddSpan("detached");
        OdfNode template = Assert.Single(toc.SourceNode!.Children,
            node => node.NamespaceUri == OdfNamespaces.Text && node.LocalName == "table-of-content-entry-template" &&
                node.GetAttribute("outline-level", OdfNamespaces.Text) == "1");
        Assert.Equal("Last", template.GetAttribute("style-name", OdfNamespaces.Text));
        Assert.Equal("new", template.TextContent);
        Assert.Contains(toc.SourceNode.Children, node => node.GetAttribute("outline-level", OdfNamespaces.Text) == "2");

        var bibliography = document.AddBibliography();
        bibliography.AddEntryTemplate("book", "First").AddSpan("old");
        bibliography.AddEntryTemplate(" book ", "Last").AddSpan("new");
        OdfNode book = Assert.Single(bibliography.SourceNode!.Children);
        Assert.Equal("book", book.GetAttribute("bibliography-type", OdfNamespaces.Text));
        Assert.Equal("Last", book.GetAttribute("style-name", OdfNamespaces.Text));
        Assert.Equal("new", book.TextContent);
    }

    /// <summary>
    /// Invalid outline keys are rejected before the source is mutated.
    /// 無效的大綱鍵值會在來源變更前遭到拒絕。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidOutlineLevelPreservesExistingTemplates(int level)
    {
        using var document = TextDocument.Create();
        var toc = document.AddTableOfContents("Contents", 2);
        OdfNode[] original = toc.SourceNode!.Children.ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => toc.AddEntryTemplate(level, "Invalid"));
        Assert.Equal(original, toc.SourceNode.Children.ToArray());
    }

    /// <summary>
    /// The table-index convenience API writes the mandatory source and selects table names.
    /// 表格索引便利 API 寫入必要的來源元素，並使用表格名稱。
    /// </summary>
    [Fact]
    public void TableIndexBuilderProducesACompleteNativeSource()
    {
        using var document = TextDocument.Create();
        document.AddTableIndex();
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;
        using OdfPackage package = OdfPackage.Open(stream);
        OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);
        Assert.True(report.IsValid, report.ToJson());
    }

    private static (string Kind, string First, string Same, string Other, string Rule) Scenario(string name) => name switch
    {
        "toc" => ("table-of-content", TocTemplate("1"), TocTemplate("+01"), TocTemplate("2"), "ODF3121"),
        "bibliography" => ("bibliography", BibliographyTemplate("book"), BibliographyTemplate(" book "), BibliographyTemplate("article"), "ODF3122"),
        "user-source-styles" => ("user-index", SourceStyles("1"), SourceStyles("+01"), SourceStyles("2"), "ODF3123"),
        _ => ("table-of-content", SourceStyles("1"), SourceStyles("+01"), SourceStyles("2"), "ODF3123")
    };

    private static string TocTemplate(string level) => $"<text:table-of-content-entry-template text:outline-level='{level}' text:style-name='Entry'/>";
    private static string BibliographyTemplate(string type) => $"<text:bibliography-entry-template text:bibliography-type='{type}' text:style-name='Entry'/>";
    private static string SourceStyles(string level) => $"<text:index-source-styles text:outline-level='{level}'><text:index-source-style text:style-name='Entry'/></text:index-source-styles>";

    private static string Index(string kind, string children, string attributes = "", string name = "I") =>
        $"<text:{kind} text:name='{name}'><text:{kind}-source{(kind == "user-index" ? " text:index-name='Custom'" : "")}{attributes}>{children}</text:{kind}-source><text:index-body><text:p/></text:index-body></text:{kind}>";

    private static string Content(OdfVersion version, string body) =>
        $"<office:document-content xmlns:office='{OdfNamespaces.Office}' xmlns:text='{OdfNamespaces.Text}' xmlns:t='{OdfNamespaces.Text}' xmlns:f='urn:test:foreign' office:version='{OdfVersionInfo.ToVersionString(version)}'><office:body><office:text>{body}</office:text></office:body></office:document-content>";

    private static OdfValidationReport ValidateBody(string body, OdfVersion version, OdfComplianceProfile profile, string? embedded = null)
    {
        using var stream = new MemoryStream();
        using (OdfPackage writer = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Text, version, true))
        {
            writer.WriteEntry("content.xml", Encoding.UTF8.GetBytes(Content(version, body)), "text/xml");
            if (embedded is not null)
                writer.WriteEntry("Object1/content.xml", Encoding.UTF8.GetBytes(embedded), "text/xml");
            writer.Save();
        }
        stream.Position = 0;
        using OdfPackage package = OdfPackage.Open(stream);
        byte[] original = (byte[])package.ReadEntry("content.xml").Clone();
        OdfValidationReport report = OdfPackageValidator.Validate(package, profile);
        Assert.Equal(original, package.ReadEntry("content.xml"));
        return report;
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
