using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests form and control names as native string attributes.
/// 測試表單與控制項名稱的原生字串屬性。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfFormNameConformanceTests
{
    private static readonly string[] NameCarriers = ["button", "checkbox", "column", "combobox", "date", "file", "fixed-text", "form", "formatted-text", "frame", "generic-control", "grid", "hidden", "image", "image-frame", "listbox", "number", "password", "radio", "text", "textarea", "time", "value-range"];
    private static readonly string?[] Values = [null, "", " \t\r\n ", "文字😀<&>", "a:b", "1 control", "same name"];

    /// <summary>
    /// All declared forms and controls retain arbitrary names as strings.
    /// 所有宣告的表單與控制項均以字串保留任意名稱。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void NamesPreserveAllDeclaredCarrierContexts(OdfVersion version) => Exercise("name", NameCarriers, version);

    /// <summary>
    /// Names do not create global XML identities and cannot move onto list item elements.
    /// 名稱不形成全域 XML 識別碼且不能移至清單項目元素。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void NamesKeepStringScopeAndNativeCarrierRestrictions(OdfVersion version)
    {
        XNamespace form = OdfNamespaces.Form;
        XElement first = Leaf("text", "name", "same name");
        XElement second = Leaf("text", "name", "same name");
        second.SetAttributeValue(XNamespace.Xml + "id", "Control2");
        XElement document = Document(version, "text", first);
        document.Descendants(form + "form").Single().Add(second);
        AssertValid(document, version);
        AssertInvalid(Document(version, "item", Leaf("item", "name", "not-declared")), version);
        AssertInvalid(Document(version, "option", Leaf("option", "name", "not-declared")), version);
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
                    OdfElement node = Assert.IsAssignableFrom<OdfElement>(root.Descendants().Last(node => node.NamespaceUri == OdfNamespaces.Form && node.LocalName == kind));
                    Assert.Equal(value, node.GetAttributeValue(attribute, OdfNamespaces.Form, version));
                    if (value is null)
                        node.RemoveAttribute(attribute, OdfNamespaces.Form);
                    else
                        node.SetAttributeValue(attribute, OdfNamespaces.Form, value, "f", version);
                    Assert.Equal(value, node.GetAttributeValue(attribute, OdfNamespaces.Form, version));
                    using MemoryStream output = new();
                    OdfXmlWriter.Write(root, output);
                    XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()), LoadOptions.PreserveWhitespace);
                    Assert.Equal(value, (string?)saved.Descendants(form + kind).Last().Attribute(form + attribute));
                    AssertValid(saved, version);
                }
            }
    }

    private static XElement Leaf(string kind, string attribute, string? value)
    {
        XNamespace form = OdfNamespaces.Form;
        XElement control = new(form + kind);
        if (kind is not ("column" or "item" or "option" or "form"))
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
        return OdfValidator.Validate(input, "form-names.fodt", profile);
    }

    private static void AssertValid(XElement document, OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            OdfValidationReport report = Validate(document, profile);
            Assert.True(report.IsValid, document.ToString(SaveOptions.DisableFormatting) + report.ToJson());
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
