using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests the native carriers, lexical values and nullable representation of selection attributes.
/// 測試選取屬性的原生承載元素、詞彙值及可空表示。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfFormSelectionAttributeConformanceTests
{
    private static readonly string[] Attributes = ["selected", "current-selected"];
    private static readonly string[] Kinds = ["option", "radio"];

    /// <summary>
    /// Both native carriers preserve explicit flags and leave omitted default-false flags absent.
    /// 兩種原生承載元素保留明示旗標，省略而預設為 false 的旗標則維持缺省。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void NativeCarriersPreserveExplicitAndAbsentFlags(OdfVersion version)
    {
        XNamespace form = OdfNamespaces.Form;
        foreach (string kind in Kinds)
            foreach (string attribute in Attributes)
                foreach (string? value in new string?[] { null, "true", "false", " true " })
                {
                    XElement control = Control(kind, attribute, value);
                    XElement document = Document(version, kind, control);
                    AssertValid(document, version);
                    byte[] bytes = Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
                    foreach (bool stream in new[] { false, true })
                    {
                        using MemoryStream input = new(bytes);
                        OdfNode root = stream ? OdfXmlReader.Parse(input) : OdfXmlReader.Parse(bytes);
                        OdfNode node = root.Descendants().Single(node => node.NamespaceUri == OdfNamespaces.Form && node.LocalName == kind);
                        Assert.Equal(value, node.GetAttribute(attribute, OdfNamespaces.Form));
                        using MemoryStream output = new();
                        OdfXmlWriter.Write(root, output);
                        XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()), LoadOptions.PreserveWhitespace);
                        Assert.Equal(value, (string?)saved.Descendants(form + kind).Single().Attribute(form + attribute));
                        AssertValid(saved, version);
                    }
                }
    }

    /// <summary>
    /// Selection flags reject non-ODF Boolean spellings and native controls outside their declared carrier set.
    /// 選取旗標拒絕非 ODF 布林拼法及不在宣告承載清單內的原生控制項。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void SelectionFlagsEnforceNativeDatatypesAndCarriers(OdfVersion version)
    {
        foreach (string attribute in Attributes)
        {
            foreach (string kind in Kinds)
                foreach (string value in new[] { "0", "1", "True", "yes" })
                    AssertInvalid(Document(version, kind, Control(kind, attribute, value)), version);
            AssertInvalid(Document(version, "text", Control("text", attribute, "true")), version);
        }
    }

    /// <summary>
    /// Typed wrappers preserve absence separately from an explicit false value and serialize canonical Boolean values.
    /// 型別化包裝將缺省與明示 false 分開保存，並序列化標準布林值。
    /// </summary>
    [Fact]
    public void TypedSelectionPropertiesPreserveNullableRawValues()
    {
        FormOptionElement option = new();
        FormRadioElement radio = new();
        Assert.Null(option.Selected);
        Assert.Null(option.CurrentSelected);
        Assert.Null(radio.Selected);
        Assert.Null(radio.CurrentSelected);
        option.Selected = true;
        option.CurrentSelected = false;
        radio.Selected = false;
        radio.CurrentSelected = true;
        Assert.Equal("true", option.GetAttribute("selected", OdfNamespaces.Form));
        Assert.Equal("false", option.GetAttribute("current-selected", OdfNamespaces.Form));
        Assert.Equal("false", radio.GetAttribute("selected", OdfNamespaces.Form));
        Assert.Equal("true", radio.GetAttribute("current-selected", OdfNamespaces.Form));
        option.Selected = null;
        option.CurrentSelected = null;
        radio.Selected = null;
        radio.CurrentSelected = null;
        Assert.Null(option.GetAttribute("selected", OdfNamespaces.Form));
        Assert.Null(option.GetAttribute("current-selected", OdfNamespaces.Form));
        Assert.Null(radio.GetAttribute("selected", OdfNamespaces.Form));
        Assert.Null(radio.GetAttribute("current-selected", OdfNamespaces.Form));
    }

    private static XElement Control(string kind, string attribute, string? value)
    {
        XNamespace form = OdfNamespaces.Form;
        XElement control = new(form + kind);
        if (kind != "option")
            control.SetAttributeValue(XNamespace.Xml + "id", "Control1");
        if (kind == "radio")
            control.SetAttributeValue(form + "name", "Group");
        control.SetAttributeValue(form + attribute, value);
        return control;
    }

    private static XElement Document(OdfVersion version, string kind, XElement control)
    {
        XNamespace office = OdfNamespaces.Office;
        XNamespace form = OdfNamespaces.Form;
        if (kind == "option")
            control = new XElement(form + "listbox", new XAttribute(XNamespace.Xml + "id", "List1"), control);
        return new XElement(office + "document",
            new XAttribute(XNamespace.Xmlns + "o", office), new XAttribute(XNamespace.Xmlns + "f", form), new XAttribute(XNamespace.Xmlns + "t", OdfNamespaces.Text),
            new XAttribute(office + "version", OdfVersionInfo.ToVersionString(version)),
            new XAttribute(office + "mimetype", "application/vnd.oasis.opendocument.text"),
            new XElement(office + "body", new XElement(office + "text", new XElement(office + "forms", new XElement(form + "form", control)), new XElement(XName.Get("p", OdfNamespaces.Text)))));
    }

    private static OdfValidationReport Validate(XElement document, OdfComplianceProfile profile)
    {
        using MemoryStream input = new(Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting)));
        return OdfValidator.Validate(input, "selection.fodt", profile);
    }

    private static void AssertValid(XElement document, OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            OdfValidationReport report = Validate(document, profile);
            Assert.True(report.IsValid, report.ToJson());
        }
    }

    private static void AssertInvalid(XElement document, OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
            Assert.Contains(Validate(document, profile).Issues, issue => issue.RuleId == "ODF3101");
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
