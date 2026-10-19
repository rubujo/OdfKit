using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests native index outline keys against their unbounded positive integer datatype.
/// 依無界正整數資料型別測試原生索引大綱鍵值。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfIndexOutlineLevelConformanceTests
{
    /// <summary>
    /// Native keys use XML Schema integer syntax and preserve the full original value.
    /// 原生鍵值使用 XML Schema 整數語法，並保留完整原值。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void NativeLevelsUseUnboundedPositiveIntegers(OdfVersion version)
    {
        foreach (string kind in new[] { "template", "toc-styles", "user-styles" })
        {
            foreach (string level in new[] { "1", "+01", " 0002 ", new string('9', 120) })
            {
                XElement xml = Document(version, kind, [level]);
                foreach (OdfComplianceProfile profile in Profiles(version))
                    AssertValid(Validate(xml, profile));
                OdfNode dom = OdfXmlReader.Parse(Encoding.UTF8.GetBytes(xml.ToString()));
                using MemoryStream output = new();
                OdfXmlWriter.Write(dom, output);
                XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()));
                Assert.Equal(level, saved.Descendants().Attributes(XName.Get("outline-level", OdfNamespaces.Text)).Single().Value);
                foreach (OdfComplianceProfile profile in Profiles(version))
                    AssertValid(Validate(saved, profile));
            }
            foreach (string level in new[] { "0", "+0", "-0", "-1", "1.0", "", "١" })
                foreach (OdfComplianceProfile profile in Profiles(version))
                    Assert.Contains(Validate(Document(version, kind, [level]), profile).Issues, issue => issue.RuleId == "ODF3101");
            XElement missing = Document(version, kind, ["1"]);
            XAttribute nativeLevel = missing.Descendants().Attributes(XName.Get("outline-level", OdfNamespaces.Text)).Single();
            XElement carrier = nativeLevel.Parent!;
            nativeLevel.Remove();
            carrier.SetAttributeValue(XName.Get("outline-level", "urn:test:foreign"), "1");
            foreach (OdfComplianceProfile profile in Profiles(version))
                Assert.Contains(Validate(missing, profile).Issues, issue => issue.RuleId == "ODF3101");
        }
        XElement wrong = Document(version, "template", ["1"]);
        wrong.Descendants(XName.Get("text", OdfNamespaces.Office)).Single().Add(new XElement(XName.Get("p", OdfNamespaces.Text),
            new XElement(XName.Get("span", OdfNamespaces.Text), new XAttribute(XName.Get("outline-level", OdfNamespaces.Text), "1"), "wrong-place")));
        foreach (OdfComplianceProfile profile in Profiles(version))
            Assert.Contains(Validate(wrong, profile).Issues, issue => issue.RuleId == "ODF3101");
    }

    /// <summary>
    /// Same numeric keys collide within a source while distinct source instances remain independent.
    /// 同一來源內相同數值的鍵值會衝突，不同來源執行個體則保持獨立。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void NativeKeyUniquenessUsesFullNumericValues(OdfVersion version)
    {
        string large = new string('9', 120);
        foreach (string kind in new[] { "template", "toc-styles", "user-styles" })
            foreach (var pair in new[] { ("1", " +0001 "), (large, "+000" + large) })
                foreach (OdfComplianceProfile profile in Profiles(version))
                {
                    OdfValidationReport duplicate = Validate(Document(version, kind, [pair.Item1, pair.Item2]), profile);
                    Assert.DoesNotContain(duplicate.Issues, issue => issue.RuleId == "ODF3101");
                    Assert.Contains(duplicate.Issues, issue => issue.RuleId == (kind == "template" ? "ODF3121" : "ODF3123"));
                    XElement separate = Document(version, kind, [pair.Item1]);
                    XElement other = Document(version, kind, [pair.Item2]);
                    XElement index = other.Descendants(XName.Get(kind == "user-styles" ? "user-index" : "table-of-content", OdfNamespaces.Text)).Single();
                    index.SetAttributeValue(XName.Get("name", OdfNamespaces.Text), "Other");
                    separate.Descendants(XName.Get("text", OdfNamespaces.Office)).Single().Add(index);
                    AssertValid(Validate(separate, profile));
                }
    }

    private static XElement Document(OdfVersion version, string kind, string[] levels)
    {
        XNamespace text = OdfNamespaces.Text;
        string indexName = kind == "user-styles" ? "user-index" : "table-of-content";
        XElement source = new(text + (indexName + "-source"));
        if (kind == "user-styles")
            source.SetAttributeValue(text + "index-name", "Custom");
        foreach (string level in levels)
        {
            XElement child = kind == "template"
                ? new XElement(text + "table-of-content-entry-template", new XAttribute(text + "style-name", "Entry"))
                : new XElement(text + "index-source-styles", new XElement(text + "index-source-style", new XAttribute(text + "style-name", "Entry")));
            child.SetAttributeValue(text + "outline-level", level);
            source.Add(child);
        }
        XElement xml = XElement.Parse($"<office:document xmlns:office='{OdfNamespaces.Office}' xmlns:text='{text}' xmlns:style='{OdfNamespaces.Style}' office:version='{OdfVersionInfo.ToVersionString(version)}' office:mimetype='application/vnd.oasis.opendocument.text'><office:styles><style:style style:name='Entry' style:family='paragraph'/></office:styles><office:body><office:text/></office:body></office:document>");
        xml.Descendants(XName.Get("text", OdfNamespaces.Office)).Single().Add(new XElement(text + indexName,
            new XAttribute(text + "name", "I"), source, new XElement(text + "index-body", new XElement(text + "p"))));
        return xml;
    }

    private static OdfValidationReport Validate(XElement xml, OdfComplianceProfile profile)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(xml.ToString()));
        return OdfValidator.Validate(stream, "levels.fodt", profile);
    }

    private static void AssertValid(OdfValidationReport report) => Assert.True(report.IsValid, report.ToJson());

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
