using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests all native carriers of form labels and string-valued reference values.
/// 測試表單標籤及字串參照值的全部原生承載元素。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfFormStringAttributeConformanceTests
{
    private static readonly string[] LabelCarriers = ["button", "checkbox", "column", "fixed-text", "frame", "item", "option", "radio"];
    private static readonly string[] ValueCarriers = ["checkbox", "combobox", "file", "formatted-text", "hidden", "image", "option", "password", "radio", "text", "textarea"];
    private static readonly string?[] Values = [null, "", " \t\r\n ", "文字😀<&>", "a:b", "true", "1.5"];

    /// <summary>
    /// Labels retain string values on all eight declared carrier elements.
    /// 標籤在宣告的八種承載元素上保留字串值。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void LabelsPreserveStringsOnAllNativeCarriers(OdfVersion version) => Exercise("label", LabelCarriers, version);

    /// <summary>
    /// The string-valued form:value contexts retain arbitrary strings on all eleven declared carriers.
    /// form:value 的字串情境在宣告的十一種承載元素上保留任意字串。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void StringValuesPreserveAllDeclaredCarrierContexts(OdfVersion version) => Exercise("value", ValueCarriers, version);

    /// <summary>
    /// Labels cannot move to unrelated controls, and numeric values retain their own datatype.
    /// 標籤不能移至無關控制項，數值情境也維持自身型別。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void OtherContextsKeepTheirOwnAttributeModels(OdfVersion version)
    {
        AssertInvalid(Document(version, "text", Leaf("text", "label", "not-declared")), version);
        AssertValid(Document(version, "number", Leaf("number", "value", "12")), version);
        AssertInvalid(Document(version, "number", Leaf("number", "value", "not-a-number")), version);
    }

    private static void Exercise(string attribute, string[] carriers, OdfVersion version)
    {
        XNamespace form = OdfNamespaces.Form;
        foreach (string kind in carriers)
            foreach (string? value in Values)
            {
                XElement document = Document(version, kind, Leaf(kind, attribute, value));
                AssertValid(document, version);
                byte[] bytes = Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
                foreach (bool stream in new[] { false, true })
                {
                    using MemoryStream input = new(bytes);
                    OdfNode root = stream ? OdfXmlReader.Parse(input) : OdfXmlReader.Parse(bytes);
                    OdfElement node = Assert.IsAssignableFrom<OdfElement>(root.Descendants().Single(node => node.NamespaceUri == OdfNamespaces.Form && node.LocalName == kind));
                    Assert.Equal(value, node.GetAttributeValue(attribute, OdfNamespaces.Form, version));
                    if (value is null)
                        node.RemoveAttribute(attribute, OdfNamespaces.Form);
                    else
                        node.SetAttributeValue(attribute, OdfNamespaces.Form, value, "f", version);
                    Assert.Equal(value, node.GetAttributeValue(attribute, OdfNamespaces.Form, version));
                    using MemoryStream output = new();
                    OdfXmlWriter.Write(root, output);
                    XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()), LoadOptions.PreserveWhitespace);
                    Assert.Equal(value, (string?)saved.Descendants(form + kind).Single().Attribute(form + attribute));
                    AssertValid(saved, version);
                }
            }
    }

    private static XElement Leaf(string kind, string attribute, string? value)
    {
        XNamespace form = OdfNamespaces.Form;
        XElement control = new(form + kind);
        if (kind is not ("column" or "item" or "option"))
            control.SetAttributeValue(XNamespace.Xml + "id", "Control1");
        control.SetAttributeValue(form + "name", kind is "item" or "option" ? null : "Control name");
        if (kind == "column")
            control.Add(new XElement(form + "text", new XAttribute(XNamespace.Xml + "id", "ColumnControl1")));
        control.SetAttributeValue(form + attribute, value);
        return control;
    }

    private static XElement Document(OdfVersion version, string kind, XElement control)
    {
        XNamespace office = OdfNamespaces.Office;
        XNamespace form = OdfNamespaces.Form;
        if (kind is "item" or "option")
            control = new XElement(form + (kind == "item" ? "combobox" : "listbox"), new XAttribute(XNamespace.Xml + "id", "Parent1"), control);
        else if (kind == "column")
            control = new XElement(form + "grid", new XAttribute(XNamespace.Xml + "id", "Grid1"), control);
        return new XElement(office + "document",
            new XAttribute(XNamespace.Xmlns + "o", office), new XAttribute(XNamespace.Xmlns + "f", form), new XAttribute(XNamespace.Xmlns + "t", OdfNamespaces.Text),
            new XAttribute(office + "version", OdfVersionInfo.ToVersionString(version)),
            new XAttribute(office + "mimetype", "application/vnd.oasis.opendocument.text"),
            new XElement(office + "body", new XElement(office + "text", new XElement(office + "forms", new XElement(form + "form", control)), new XElement(XName.Get("p", OdfNamespaces.Text)))));
    }

    private static OdfValidationReport Validate(XElement document, OdfComplianceProfile profile)
    {
        using MemoryStream input = new(Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting)));
        return OdfValidator.Validate(input, "form-strings.fodt", profile);
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
