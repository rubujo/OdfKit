using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OasisOdf13RegressionTests
{
    [Theory]
    [InlineData("$Sheet1.$A$1", "$Sheet1.$A$1:.$B$12", true)]
    [InlineData("$Sheet1.$BY$3", "$Sheet1.$A$3:.$AMJ$3", true)]
    [InlineData("$Query.$C$3", "$Query.$A$1:.$C$3", true)]
    [InlineData("$'Q1 Sales'.$A$1", "$'Q1 Sales'.$A$1:.$B$2", true)]
    [InlineData(".A1", ".A1:.B2", true)]
    [InlineData("not-an-address", ".A1:.B2", false)]
    [InlineData(".A1", "not-a-range", false)]
    [InlineData(".A1junk", ".A1:.B2", false)]
    [InlineData(".A1", ".A1junk", false)]
    public void NamedRangeUsesXmlSchemaAddressPattern(string baseAddress, string range, bool expected)
    {
        XNamespace table = OdfNamespaces.Table;
        var element = new XElement(table + "named-range",
            new XAttribute(table + "name", "Range"),
            new XAttribute(table + "base-cell-address", baseAddress),
            new XAttribute(table + "cell-range-address", range));

        Assert.Equal(expected, OdfSchemaPatternValidator.ValidateElement(
            element, OdfSchemaRegistry.GetSchema(OdfVersion.Odf13), "table-named-range").IsMatch);
    }

    [Theory]
    [InlineData("^[$]$", "^$$", true)]
    [InlineData("^[$]$", "$", false)]
    [InlineData(@"\^\$", "^$", true)]
    [InlineData("[^$]+", "fruit", true)]
    [InlineData("[^$]+", "$", false)]
    [InlineData("abc|def", "xabc", false)]
    [InlineData("abc|def", "defx", false)]
    public void XmlSchemaPatternKeepsLiteralAnchorsAndCharacterClasses(string pattern, string value, bool expected)
    {
        var data = new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Data, "", "", "", "", "string", "",
            [], [], [new KeyValuePair<string, string>("pattern", pattern)], "http://www.w3.org/2001/XMLSchema-datatypes");
        var root = new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "", "urn:regression", "value", "", "", "",
            [], [data]);
        var schema = new OdfSchemaSet(OdfVersion.Odf13,
            new Uri("https://www.w3.org/TR/xmlschema-2/#regexs"), "2004-10-28", [], [], [],
            [new OdfSchemaPatternDefinition("root", [root])]);

        Assert.Equal(expected, OdfSchemaPatternValidator.ValidateElement(
            new XElement(XName.Get("value", "urn:regression"), value), schema, "root").IsMatch);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("maybe", false)]
    public void DropDownAllowsLabelsAndSelectedTextWithoutAllowingInvalidAttributes(string selected, bool expected)
    {
        XNamespace text = OdfNamespaces.Text;
        var element = new XElement(text + "drop-down", new XAttribute(text + "name", "Fruit"),
            new XElement(text + "label", new XAttribute(text + "value", "Apple")),
            new XElement(text + "label", new XAttribute(text + "value", "Raspberry"),
                new XAttribute(text + "current-selected", selected)), "Raspberry");

        Assert.Equal(expected, OdfSchemaPatternValidator.ValidateElement(
            element, OdfSchemaRegistry.GetSchema(OdfVersion.Odf13), "text-drop-down").IsMatch);

        element.Add(new XElement(text + "span", "unexpected"));
        Assert.False(OdfSchemaPatternValidator.ValidateElement(
            element, OdfSchemaRegistry.GetSchema(OdfVersion.Odf13), "text-drop-down").IsMatch);
    }

    [Fact]
    public void DropDownStillRequiresItsName()
    {
        XNamespace text = OdfNamespaces.Text;
        var element = new XElement(text + "drop-down", new XElement(text + "label"), "Apple");
        Assert.False(OdfSchemaPatternValidator.ValidateElement(
            element, OdfSchemaRegistry.GetSchema(OdfVersion.Odf13), "text-drop-down").IsMatch);
    }

    [Theory]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void MasterTextTemplateIsDetectedValidatedAndPreserved(OdfVersion version)
    {
        using var stream = new MemoryStream();
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.TextMasterTemplate, version, true))
        {
            package.Save();
        }
        stream.Position = 0;
        using OdfDocument document = OdfDocument.Load(stream, "master.otm");
        OdfComplianceProfile profile = version == OdfVersion.Odf13
            ? OdfComplianceProfiles.OasisOdf13Extended : OdfComplianceProfiles.OasisOdf14Extended;
        OdfValidationReport report = OdfPackageValidator.Validate(document.Package, profile);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.RuleId + ": " + issue.Message)));
        Assert.IsType<TextMasterDocument>(document);
        Assert.Equal(OdfDocumentKind.TextMasterTemplate, document.DocumentKind);
        Assert.Equal(OdfDocumentKind.Text, document.ContentKind);
        Assert.True(document.IsTemplate);
        Assert.True(document.IsMasterDocument);
        Assert.Equal(".otm", document.Format?.Extension);
        Assert.Equal(OdfDocumentKind.TextMasterTemplate,
            OdfDocumentKindDetector.FromFileName("MASTER.OTM"));

        using var saved = new MemoryStream();
        document.SaveToStream(saved);
        saved.Position = 0;
        using OdfDocument loaded = OdfDocument.Load(saved, "master.otm");
        Assert.Equal(document.Package.MimeType, loaded.Package.MimeType);
        Assert.Equal(OdfDocumentKind.TextMasterTemplate, loaded.DocumentKind);
    }

    [Fact]
    public void MasterTextTemplateIsNotAcceptedAsOdf12()
    {
        using var stream = new MemoryStream();
        using OdfPackage package = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.TextMasterTemplate, OdfVersion.Odf12, true);
        OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf12Extended);
        Assert.Contains(report.Issues, issue => issue.RuleId == "ODF1000");
    }
}
