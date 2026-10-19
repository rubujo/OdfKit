using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests the uniqueness of native user-defined metadata names.
/// 測試原生使用者自訂中繼資料名稱的唯一性。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfMetadataNameConformanceTests
{
    /// <summary>
    /// Equal string names are rejected, including empty names and aliased namespaces.
    /// 拒絕相同字串名稱，包含空名稱及使用別名前綴的命名空間。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void DuplicateNamesAreRejected(OdfVersion version)
    {
        foreach (string name in new[] { "same", "", "  name  ", "名稱:一" })
            foreach (OdfComplianceProfile profile in Profiles(version))
            {
                XElement xml = Document(version, name, name);
                xml.SetAttributeValue(XNamespace.Xmlns + "m", OdfNamespaces.Meta);
                OdfValidationReport report = Validate(xml, profile);
                Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3101");
                Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3134");
            }
    }

    /// <summary>
    /// Name comparison preserves case, whitespace and namespace identity.
    /// 名稱比較保留大小寫、空白及命名空間識別。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void DistinctNamesRemainValid(OdfVersion version)
    {
        foreach (string[] names in new[] { new[] { "name", "Name" }, new[] { "name", " name " }, new[] { "", " " }, new[] { "名稱:一", "名稱:二" } })
            foreach (OdfComplianceProfile profile in Profiles(version))
                AssertValid(Validate(Document(version, names), profile));
        XElement foreign = Document(version, "one", "two");
        XNamespace other = "urn:test:foreign";
        foreach (XElement property in foreign.Descendants(XName.Get("user-defined", OdfNamespaces.Meta)))
            property.SetAttributeValue(other + "name", "same");
        foreign.Element(XName.Get("meta", OdfNamespaces.Office))!.Add(new XElement(other + "user-defined", new XAttribute(XName.Get("name", OdfNamespaces.Meta), "one")));
        AssertValid(Validate(foreign, Profiles(version)[1]));
    }

    /// <summary>
    /// SDK updates replace an existing name and preserve other distinct names after saving.
    /// SDK 更新會替換既有同名項目，並在儲存後保留其他相異名稱。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ProducerUpdatesKeepNamesUnique(OdfVersion version)
    {
        using TextDocument document = TextDocument.Create();
        document.TargetVersion = version;
        document.SetCustomProperty("name", "first");
        document.SetCustomProperty("name", "replacement");
        document.SetCustomProperty("Name", "distinct");
        document.SetCustomProperty("name:part", "colon");
        document.SetCustomProperty("name_part", "underscore");
        using var saved = new MemoryStream();
        document.SaveToStream(saved);
        using TextDocument loaded = TextDocument.Load(new MemoryStream(saved.ToArray()));
        Assert.Equal("replacement", loaded.FindCustomProperty<string>("name"));
        Assert.Equal("distinct", loaded.FindCustomProperty<string>("Name"));
        Assert.Equal("colon", loaded.FindCustomProperty<string>("name:part"));
        Assert.Equal("underscore", loaded.FindCustomProperty<string>("name_part"));
        foreach (OdfComplianceProfile profile in Profiles(version))
            AssertValid(OdfPackageValidator.Validate(loaded.Package, profile));
    }

    private static XElement Document(OdfVersion version, params string[] names)
    {
        XNamespace office = OdfNamespaces.Office;
        XNamespace meta = OdfNamespaces.Meta;
        XElement xml = XElement.Parse($"<office:document xmlns:office='{office}' xmlns:meta='{meta}' xmlns:text='{OdfNamespaces.Text}' office:version='{OdfVersionInfo.ToVersionString(version)}' office:mimetype='application/vnd.oasis.opendocument.text'><office:meta/><office:body><office:text><text:p/></office:text></office:body></office:document>");
        foreach (string name in names)
            xml.Element(office + "meta")!.Add(new XElement(meta + "user-defined", new XAttribute(meta + "name", name), "value"));
        return xml;
    }
    private static OdfValidationReport Validate(XElement xml, OdfComplianceProfile profile)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml.ToString()));
        return OdfValidator.Validate(stream, "metadata.fodt", profile);
    }
    private static void AssertValid(OdfValidationReport report) => Assert.True(report.IsValid, report.ToJson());
    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
