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
/// Tests radio selection consistency and documented form designer defaults.
/// 測試選項按鈕的選取一致性及表單設計器宣告的預設狀態。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfRadioSelectionConformanceTests
{
    /// <summary>
    /// A named radio group cannot contain multiple default or current selections.
    /// 具名選項按鈕群組不能同時有多個預設或目前選取項目。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12, "selected")]
    [InlineData(OdfVersion.Odf13, "selected")]
    [InlineData(OdfVersion.Odf14, "selected")]
    [InlineData(OdfVersion.Odf12, "current-selected")]
    [InlineData(OdfVersion.Odf13, "current-selected")]
    [InlineData(OdfVersion.Odf14, "current-selected")]
    public void SameNameRadioSelectionsMustBeExclusive(OdfVersion version, string attribute)
    {
        foreach (string name in new[] { "Group", string.Empty })
        {
            XElement first = Radio("R1", name, attribute, "true");
            XElement second = Radio("R2", name, attribute, " true ");
            XElement form = Form(first, second);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(form, OdfSchemaRegistry.GetSchema(version), "form-form").IsMatch);
            foreach (OdfComplianceProfile profile in Profiles(version))
                Assert.Contains(Validate(Document(version, form), profile).Issues,
                    issue => issue.RuleId == (attribute == "selected" ? "ODF3139" : "ODF3140"));
        }
    }

    /// <summary>
    /// Default and current selection sets are independent, and group names remain local to their form.
    /// 預設與目前選取集合彼此獨立，群組名稱也維持在所屬表單範圍內。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void SelectionStatesAndFormOwnersRemainIndependent(OdfVersion version)
    {
        AssertValid(Document(version, Form(Radio("R1", "G", "selected", "true"), Radio("R2", "G", "current-selected", "true"))), version);
        AssertValid(Document(version, Form(Radio("R1", "G", "selected", "true")), Form(Radio("R2", "G", "selected", "true"))), version);
        AssertValid(Document(version, Form(Radio("R1", "G", "current-selected", "true")), Form(Radio("R2", "G", "current-selected", "true"))), version);
        AssertValid(Document(version, Form(Radio("R1", "G", "selected", "true"), Radio("R2", " G ", "selected", "true"))), version);
        AssertValid(Document(version, Form(Radio("R1", "G", "selected", "false"), Radio("R2", "G", "selected", "false"))), version);
    }

    /// <summary>
    /// Foreign radio names and selection attributes cannot add native selection group members.
    /// 外來的 radio 名稱或選取屬性不能加入原生選取群組。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ForeignNamesDoNotCreateNativeSelectionConflicts(OdfVersion version)
    {
        XNamespace form = OdfNamespaces.Form;
        XNamespace foreign = "urn:test:foreign";
        foreach (string attribute in new[] { "selected", "current-selected" })
        {
            XElement real = Radio("R1", "G", attribute, "true");
            XElement other = Radio("R2", "G", attribute, "false");
            other.SetAttributeValue(foreign + attribute, "true");
            XElement fake = new(foreign + "radio", new XAttribute(form + "name", "G"), new XAttribute(form + attribute, "true"));
            foreach (OdfComplianceProfile profile in Profiles(version))
                Assert.DoesNotContain(Validate(Document(version, Form(real, other, fake)), profile).Issues,
                    issue => issue.RuleId is "ODF3139" or "ODF3140");
        }
    }

    /// <summary>
    /// Designer parameters documented as default states write the corresponding default attributes.
    /// 設計器中宣告為預設狀態的參數會寫入對應的預設屬性。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12, "checkbox")]
    [InlineData(OdfVersion.Odf13, "checkbox")]
    [InlineData(OdfVersion.Odf14, "checkbox")]
    [InlineData(OdfVersion.Odf12, "radio")]
    [InlineData(OdfVersion.Odf13, "radio")]
    [InlineData(OdfVersion.Odf14, "radio")]
    public void DesignerParametersSetDocumentedDefaults(OdfVersion version, string kind)
    {
        using TextDocument document = TextDocument.Create();
        document.TargetVersion = version;
        OdfDatabaseFormDesigner designer = new(document);
        foreach (bool selected in new[] { true, false })
        {
            OdfNode node = kind == "radio"
                ? designer.AddRadioButton("Group", "Choice", selected)
                : designer.AddCheckBox(selected ? "Check1" : "Check2", "Choice", selected);
            if (kind == "radio")
            {
                Assert.Equal(selected ? "true" : "false", node.GetAttribute("selected", OdfNamespaces.Form));
                Assert.Equal(selected ? "true" : "false", node.GetAttribute("current-selected", OdfNamespaces.Form));
            }
            else
            {
                Assert.Equal(selected ? "checked" : "unchecked", node.GetAttribute("state", OdfNamespaces.Form));
                Assert.Equal(selected ? "checked" : "unchecked", node.GetAttribute("current-state", OdfNamespaces.Form));
            }
        }
        using MemoryStream output = new();
        document.SaveToStream(output);
        using OdfPackage package = OdfPackage.Open(new MemoryStream(output.ToArray()));
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            OdfValidationReport report = OdfPackageValidator.Validate(package, profile);
            Assert.True(report.IsValid, report.ToJson());
        }
    }

    private static XElement Radio(string id, string name, string attribute, string value)
    {
        XNamespace form = OdfNamespaces.Form;
        return new XElement(form + "radio", new XAttribute(XNamespace.Xml + "id", id),
            new XAttribute(form + "name", name), new XAttribute(form + attribute, value));
    }

    private static XElement Form(params XElement[] controls) => new(XName.Get("form", OdfNamespaces.Form), controls);

    private static XElement Document(OdfVersion version, params XElement[] forms)
    {
        XNamespace office = OdfNamespaces.Office;
        return new XElement(office + "document",
            new XAttribute(XNamespace.Xmlns + "o", office), new XAttribute(XNamespace.Xmlns + "f", OdfNamespaces.Form), new XAttribute(XNamespace.Xmlns + "t", OdfNamespaces.Text),
            new XAttribute(office + "version", OdfVersionInfo.ToVersionString(version)),
            new XAttribute(office + "mimetype", "application/vnd.oasis.opendocument.text"),
            new XElement(office + "body", new XElement(office + "text", new XElement(office + "forms", forms), new XElement(XName.Get("p", OdfNamespaces.Text)))));
    }

    private static OdfValidationReport Validate(XElement document, OdfComplianceProfile profile)
    {
        using MemoryStream input = new(Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting)));
        return OdfValidator.Validate(input, "radio.fodt", profile);
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
