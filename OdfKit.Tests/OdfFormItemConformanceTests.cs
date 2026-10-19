using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Database;
using OdfKit.DOM;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests native form list item data, structure and designer persistence.
/// 測試原生表單清單項目的資料、結構及設計器保存。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfFormItemConformanceTests
{
    private static readonly string[] Kinds = ["item", "option"];
    private static readonly string[] Values = ["", " ", " \t\n ", "選項😀<&>"];

    /// <summary>
    /// Native items keep their independent label, value and character data across both reading paths.
    /// 原生項目在兩種讀取路徑中保留各自獨立的標籤、值及字元資料。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void NativeItemsPreserveTheirIndependentData(OdfVersion version)
    {
        XNamespace form = OdfNamespaces.Form;
        foreach (string kind in Kinds)
            foreach (string value in Values)
            {
                XElement item = new(form + kind, new XAttribute(form + "label", "標籤\t\r\n"), value);
                if (kind == "option")
                {
                    item.SetAttributeValue(form + "value", "儲存值\t\r\n");
                    item.SetAttributeValue(form + "selected", "true");
                    item.SetAttributeValue(form + "current-selected", "false");
                }
                XElement document = Document(version, kind, item);
                AssertValid(document, version);
                byte[] bytes = Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
                foreach (bool stream in new[] { false, true })
                {
                    using MemoryStream input = new(bytes);
                    OdfNode dom = stream ? OdfXmlReader.Parse(input) : OdfXmlReader.Parse(bytes);
                    OdfNode node = dom.Descendants().Single(node => node.NamespaceUri == OdfNamespaces.Form && node.LocalName == kind);
                    Assert.Equal(value, node.TextContent);
                    Assert.Equal("標籤\t\r\n", node.GetAttribute("label", OdfNamespaces.Form));
                    if (kind == "option")
                    {
                        Assert.Equal("儲存值\t\r\n", node.GetAttribute("value", OdfNamespaces.Form));
                        Assert.Equal("true", node.GetAttribute("selected", OdfNamespaces.Form));
                        Assert.Equal("false", node.GetAttribute("current-selected", OdfNamespaces.Form));
                    }
                    using MemoryStream output = new();
                    OdfXmlWriter.Write(dom, output);
                    XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()), LoadOptions.PreserveWhitespace);
                    Assert.Equal(value, saved.Descendants(form + kind).Single().Value);
                    foreach (XAttribute attribute in item.Attributes())
                        Assert.Equal(attribute.Value, (string?)saved.Descendants(form + kind).Single().Attribute(attribute.Name));
                    AssertValid(saved, version);
                }
            }
    }

    /// <summary>
    /// Optional attributes stay optional while native child elements, extra attributes and wrong control parents are rejected.
    /// 可選屬性維持可選，原生子元素、額外屬性及錯誤控制項父元素則被拒絕。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void NativeItemsEnforceTheirOwnContentAndParentModels(OdfVersion version)
    {
        XNamespace form = OdfNamespaces.Form;
        foreach (string kind in Kinds)
        {
            AssertValid(Document(version, kind, new XElement(form + kind)), version);
            foreach (int mutation in new[] { 0, 1, 2 })
            {
                XElement item = new(form + kind, "Text");
                if (mutation == 0)
                    item.Add(new XElement(XName.Get("span", OdfNamespaces.Text), "invalid"));
                else if (mutation == 1)
                    item.SetAttributeValue(form + "name", "not-defined-for-items");
                XElement document = Document(version, mutation == 2 ? (kind == "item" ? "option" : "item") : kind, item);
                foreach (OdfComplianceProfile profile in Profiles(version))
                    Assert.Contains(Validate(document, profile).Issues, issue => issue.RuleId == "ODF3101");
            }
        }
    }

    /// <summary>
    /// Selection flags use native boolean lexical values without affecting independent item text.
    /// 選取旗標使用原生布林詞彙值，不影響獨立的項目文字。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void NativeSelectionAttributesUseBooleanDatatypes(OdfVersion version)
    {
        XNamespace form = OdfNamespaces.Form;
        foreach (string attribute in new[] { "selected", "current-selected" })
        {
            foreach (string value in new[] { "true", "false", " true " })
                AssertValid(Document(version, "option", new XElement(form + "option", new XAttribute(form + attribute, value), "Text")), version);
            foreach (string value in new[] { "1", "0", "yes" })
                foreach (OdfComplianceProfile profile in Profiles(version))
                    Assert.Contains(Validate(Document(version, "option", new XElement(form + "option", new XAttribute(form + attribute, value))), profile).Issues,
                        issue => issue.RuleId == "ODF3101");
        }
    }

    /// <summary>
    /// The supported form designer preserves empty, whitespace and Unicode labels in native documents.
    /// 支援的表單設計器在原生文件中保留空標籤、空白及 Unicode。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void DesignerItemsPreserveNativeLabelsAndValues(OdfVersion version)
    {
        using TextDocument document = TextDocument.Create();
        document.TargetVersion = version;
        OdfDatabaseFormDesigner designer = new(document);
        designer.AddComboBox("Combo", string.Empty, Values);
        designer.AddListBox("List", string.Empty, Values);
        using MemoryStream output = new();
        document.SaveToStream(output);
        using OdfPackage package = OdfPackage.Open(new MemoryStream(output.ToArray()));
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            OdfValidationReport report = OdfPackageValidator.Validate(package, profile);
            Assert.True(report.IsValid, report.ToJson());
        }
        OdfNode root = OdfXmlReader.Parse(package.ReadEntry("content.xml"));
        OdfNode[] items = root.Descendants().Where(node => node.NamespaceUri == OdfNamespaces.Form && node.LocalName == "item").ToArray();
        OdfNode[] options = root.Descendants().Where(node => node.NamespaceUri == OdfNamespaces.Form && node.LocalName == "option").ToArray();
        Assert.Equal(Values, items.Select(node => node.TextContent));
        Assert.Equal(Values, items.Select(node => node.GetAttribute("label", OdfNamespaces.Form)));
        Assert.Equal(Values, options.Select(node => node.GetAttribute("label", OdfNamespaces.Form)));
        Assert.Equal(Values, options.Select(node => node.GetAttribute("value", OdfNamespaces.Form)));
    }

    private static XElement Document(OdfVersion version, string kind, XElement item)
    {
        XNamespace office = OdfNamespaces.Office;
        XNamespace form = OdfNamespaces.Form;
        XElement control = new(form + (kind == "item" ? "combobox" : "listbox"), new XAttribute(XNamespace.Xml + "id", "Control1"), item);
        return new XElement(office + "document",
            new XAttribute(XNamespace.Xmlns + "o", office), new XAttribute(XNamespace.Xmlns + "f", form), new XAttribute(XNamespace.Xmlns + "t", OdfNamespaces.Text),
            new XAttribute(office + "version", OdfVersionInfo.ToVersionString(version)),
            new XAttribute(office + "mimetype", "application/vnd.oasis.opendocument.text"),
            new XElement(office + "body", new XElement(office + "text", new XElement(office + "forms", new XElement(form + "form", control)), new XElement(XName.Get("p", OdfNamespaces.Text)))));
    }

    private static OdfValidationReport Validate(XElement document, OdfComplianceProfile profile)
    {
        using MemoryStream input = new(Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting)));
        return OdfValidator.Validate(input, "form-items.fodt", profile);
    }

    private static void AssertValid(XElement document, OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            OdfValidationReport report = Validate(document, profile);
            Assert.True(report.IsValid, report.ToJson());
        }
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
