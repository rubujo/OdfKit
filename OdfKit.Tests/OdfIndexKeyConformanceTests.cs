using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests the primary and secondary alphabetical index key relationship.
/// 測試字母索引主要鍵與次要鍵的關係。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfIndexKeyConformanceTests
{
    /// <summary>
    /// A secondary key alone does not represent a conforming single key.
    /// 單獨的次要鍵不是符合規範的單鍵表示法。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void SecondaryKeysRequirePrimaryKeys(OdfVersion version)
    {
        foreach (bool range in new[] { false, true })
            foreach (string value in new[] { "secondary", "", "次要鍵" })
                foreach (OdfComplianceProfile profile in Profiles(version))
                {
                    XElement xml = Document(version, range, null, value);
                    OdfValidationReport report = Validate(xml, profile);
                    Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3101");
                    Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3137");
                    XElement mark = FindMark(xml, range);
                    mark.SetAttributeValue(XName.Get("key1", "urn:test:foreign"), "not-primary");
                    Assert.Contains(Validate(xml, Profiles(version)[1]).Issues, issue => issue.RuleId == "ODF3137");
                }
    }

    /// <summary>
    /// Keys preserve strings and optional absence on both point and range marks.
    /// 單點與範圍標記的鍵值保留字串及可選屬性的省略。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ValidKeysPreserveValuesAndPlacement(OdfVersion version)
    {
        (string? Primary, string? Secondary)[] pairs = [(null, null), ("primary", null), ("", null), ("", ""), (" key ", "次要:鍵😀")];
        foreach (bool range in new[] { false, true })
            foreach (var pair in pairs)
            {
                XElement xml = Document(version, range, pair.Primary, pair.Secondary);
                foreach (OdfComplianceProfile profile in Profiles(version))
                    Assert.True(Validate(xml, profile).IsValid);
                FindMark(xml, range).SetAttributeValue(XName.Get("key2", "urn:test:foreign"), "foreign");
                OdfNode dom = OdfXmlReader.Parse(Encoding.UTF8.GetBytes(xml.ToString()));
                using MemoryStream output = new();
                OdfXmlWriter.Write(dom, output);
                XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()));
                Assert.Equal(pair.Primary, (string?)FindMark(saved, range).Attribute(XName.Get("key1", OdfNamespaces.Text)));
                Assert.Equal(pair.Secondary, (string?)FindMark(saved, range).Attribute(XName.Get("key2", OdfNamespaces.Text)));
                Assert.True(Validate(saved, Profiles(version)[1]).IsValid);
            }
        XElement wrong = Document(version, false, null, null);
        wrong.Descendants(XName.Get("p", OdfNamespaces.Text)).Single().SetAttributeValue(XName.Get("key1", OdfNamespaces.Text), "wrong-place");
        foreach (OdfComplianceProfile profile in Profiles(version))
            Assert.Contains(Validate(wrong, profile).Issues, issue => issue.RuleId == "ODF3101");
        XElement wrongEnd = Document(version, true, null, null);
        wrongEnd.Descendants(XName.Get("alphabetical-index-mark-end", OdfNamespaces.Text)).Single()
            .SetAttributeValue(XName.Get("key2", OdfNamespaces.Text), "wrong-place");
        foreach (OdfComplianceProfile profile in Profiles(version))
            Assert.Contains(Validate(wrongEnd, profile).Issues, issue => issue.RuleId == "ODF3101");
    }

    /// <summary>
    /// The producer rejects invalid combined parameters before changing a paragraph.
    /// 產生器在修改段落前拒絕不合法的參數組合。
    /// </summary>
    [Fact]
    public void ProducerRejectsSecondaryOnlyBeforeMutation()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph("unchanged");
        int before = paragraph.Node.Children.Count;
        foreach (string secondary in new[] { "secondary", "" })
        {
            ArgumentException error = Assert.Throws<ArgumentException>(() => paragraph.AddAlphabeticalIndexMark("term", null, secondary));
            Assert.Equal("key2", error.ParamName);
            Assert.Equal(before, paragraph.Node.Children.Count);
        }
    }

    /// <summary>
    /// Clearing an optional key removes the attribute instead of writing an empty value.
    /// 清除可選鍵值會移除屬性，而非寫入空值。
    /// </summary>
    [Fact]
    public void ClearingKeysRestoresOptionalAbsence()
    {
        using TextDocument document = TextDocument.Create();
        OdfAlphabeticalIndexMark mark = document.AddParagraph("term").AddAlphabeticalIndexMark("term", "primary", "secondary");
        mark.Key1 = "";
        mark.Key2 = "";
        Assert.Equal("", mark.Key1);
        Assert.Equal("", mark.Key2);
        mark.Key2 = null;
        Assert.Null(mark.Node.GetAttribute("key2", OdfNamespaces.Text));
        mark.Key1 = null;
        Assert.Null(mark.Node.GetAttribute("key1", OdfNamespaces.Text));
    }

    /// <summary>
    /// Producer keys retain empty and Unicode values after package round trips.
    /// 產生器的鍵值經封裝往返後保留空字串及 Unicode 值。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ProducerKeysSurviveSaving(OdfVersion version)
    {
        using TextDocument document = TextDocument.Create();
        document.TargetVersion = version;
        document.AddParagraph("term").AddAlphabeticalIndexMark("term", "", " 次要:鍵😀 ");
        using MemoryStream output = new();
        document.SaveToStream(output);
        using TextDocument loaded = TextDocument.Load(new MemoryStream(output.ToArray()));
        OdfDocumentIndexMarkInfo mark = Assert.Single(loaded.GetIndexMarks());
        Assert.Equal("", mark.Key1);
        Assert.Equal(" 次要:鍵😀 ", mark.Key2);
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            OdfValidationReport report = OdfPackageValidator.Validate(loaded.Package, profile);
            Assert.True(report.IsValid, report.ToJson());
        }
    }

    /// <summary>
    /// Existing index generation applies the primary and secondary key before the term.
    /// 既有索引產生功能先採主要鍵及次要鍵，再依詞語排序。
    /// </summary>
    [Fact]
    public void IndexGenerationUsesPrimaryAndSecondaryKeys()
    {
        using TextDocument document = TextDocument.Create();
        (string Term, string Primary, string Secondary)[] marks = [("Zulu", "A", "2"), ("Alpha", "A", "3"), ("Middle", "B", "0"), ("Bravo", "A", "1")];
        foreach (var mark in marks)
            document.AddParagraph(mark.Term).AddAlphabeticalIndexMark(mark.Term, mark.Primary, mark.Secondary);
        OdfAlphabeticalIndex index = document.AddAlphabeticalIndex("Index");
        index.Update();
        Assert.NotNull(index.BodyNode);
        using MemoryStream output = new();
        OdfXmlWriter.Write(index.BodyNode, output);
        XElement body = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()));
        string[] actual = body.Descendants(XName.Get("p", OdfNamespaces.Text)).Select(paragraph => paragraph.Value)
            .Where(value => marks.Any(mark => value.Contains(mark.Term, StringComparison.Ordinal)))
            .Select(value => marks.Single(mark => value.Contains(mark.Term, StringComparison.Ordinal)).Term).ToArray();
        Assert.Equal(["Bravo", "Zulu", "Alpha", "Middle"], actual);
    }

    private static XElement Document(OdfVersion version, bool range, string? primary, string? secondary)
    {
        string content = range ? "<t:alphabetical-index-mark-start t:id='mark'/>term<t:alphabetical-index-mark-end t:id='mark'/>" : "<t:alphabetical-index-mark t:string-value='term'/>";
        XElement xml = XElement.Parse($"<office:document xmlns:office='{OdfNamespaces.Office}' xmlns:t='{OdfNamespaces.Text}' office:version='{OdfVersionInfo.ToVersionString(version)}' office:mimetype='application/vnd.oasis.opendocument.text'><office:body><office:text><t:p>{content}</t:p></office:text></office:body></office:document>");
        XElement mark = FindMark(xml, range);
        mark.SetAttributeValue(XName.Get("key1", OdfNamespaces.Text), primary);
        mark.SetAttributeValue(XName.Get("key2", OdfNamespaces.Text), secondary);
        return xml;
    }

    private static XElement FindMark(XElement xml, bool range) => xml.Descendants(XName.Get(range ? "alphabetical-index-mark-start" : "alphabetical-index-mark", OdfNamespaces.Text)).Single();

    private static OdfValidationReport Validate(XElement xml, OdfComplianceProfile profile)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(xml.ToString()));
        return OdfValidator.Validate(stream, "keys.fodt", profile);
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
