using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests context-specific form numeric, date, and time values.
/// 測試表單數值、日期及時間的情境型別限制。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfFormTypedValueConformanceTests
{
    /// <summary>
    /// Numeric controls accept the XML Schema double lexical domain and reject nonnumeric strings.
    /// 數值控制項接受 XML Schema double 詞彙範圍並拒絕非數值字串。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void NumericValuesUseDoubleDatatype(OdfVersion version)
    {
        foreach (string kind in new[] { "number", "value-range" })
        {
            Exercise("value", [kind], version, [null, "0", "-0", "+1.25", ".5", "1.", "1E100", "-1e-100", "INF", "-INF", "NaN", " \t1.5\r\n"]);
            foreach (string value in new[] { "", "true", "1,234", "1 2", "+INF", "Infinity", "nan", "1e", "0x10" })
                AssertInvalid(Document(version, kind, Leaf(kind, "value", value)), version, kind == "value-range" ? "ODF3141" : "ODF3101");
        }
    }

    /// <summary>
    /// Date controls preserve calendar dates and timezones without accepting date-time values.
    /// 日期控制項保留曆法日期與時區且不接受日期時間混用。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void DateValuesUseDateDatatype(OdfVersion version)
    {
        Exercise("value", ["date"], version, [null, "2024-02-29", "2026-10-16Z", "2026-10-16+14:00", "-0001-01-01", "12026-01-01", " \t2026-10-16\r\n"]);
        foreach (string value in new[] { "", "2023-02-29", "2026-13-01", "0000-01-01", "2026-10-16+14:01", "2026-10-16T00:00:00", "2026-1-1" })
            AssertInvalid(Document(version, "date", Leaf("date", "value", value)), version);
    }

    /// <summary>
    /// Time controls preserve fractional seconds and timezone boundaries while rejecting durations.
    /// 時間控制項保留小數秒與時區邊界並拒絕時間長度。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void TimeValuesUseTimeDatatype(OdfVersion version)
    {
        Exercise("value", ["time"], version, [null, "00:00:00", "23:59:59.123456789", "24:00:00", "12:30:00Z", "12:30:00-14:00", " \t12:30:00\r\n"]);
        foreach (string value in new[] { "", "24:00:01", "12:60:00", "12:00:60", "12:30", "PT12H30M", "12:30:00+14:01", "2026-10-16T12:30:00" })
            AssertInvalid(Document(version, "time", Leaf("time", "value", value)), version);
    }
    /// <summary>
    /// Floating-point lexical checks remain case-sensitive and independent of CLR parser extensions.
    /// 浮點詞彙檢查維持大小寫區分且不受 CLR 解析擴充影響。
    /// </summary>
    [Theory]
    [InlineData("INF", true)]
    [InlineData("-INF", true)]
    [InlineData("NaN", true)]
    [InlineData("0", true)]
    [InlineData("-0", true)]
    [InlineData("+0.0", true)]
    [InlineData(".5", true)]
    [InlineData("1.", true)]
    [InlineData("-.5e+02", true)]
    [InlineData("1E1000", true)]
    [InlineData("1E-1000", true)]
    [InlineData(" \t1.5\r\n", true)]
    [InlineData("Infinity", false)]
    [InlineData("-Infinity", false)]
    [InlineData("+Infinity", false)]
    [InlineData("infinity", false)]
    [InlineData("nan", false)]
    [InlineData("NAN", false)]
    [InlineData("+NaN", false)]
    [InlineData("-NaN", false)]
    [InlineData("+INF", false)]
    [InlineData("inf", false)]
    [InlineData("", false)]
    [InlineData(".", false)]
    [InlineData("+", false)]
    [InlineData("1e", false)]
    [InlineData("1e+", false)]
    [InlineData("1e1.5", false)]
    [InlineData("1e1e1", false)]
    [InlineData("1,234", false)]
    [InlineData("1 2", false)]
    [InlineData("１.５", false)]
    [InlineData("1\u00a0", false)]
    [InlineData("1\u0085", false)]
    public void FloatingPointLexicalSpaceIsStrict(string value, bool expected)
    {
        foreach (string type in new[] { "float", "xsd:float", "double", "xsd:double" })
        {
            OdfSchemaPatternNode data = new(OdfSchemaPatternNodeKind.Data, "exactlyOne", "", "", "", type, "", null, null, null, "http://www.w3.org/2001/XMLSchema-datatypes");
            OdfSchemaSet schema = new(OdfVersion.Odf14, new Uri("https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/"), "2004-10-28", [], attributes: null, nameClasses: null, patterns: [new OdfSchemaPatternDefinition("start", [new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "exactlyOne", "", "root", "", "", "", null, [data])])]);
            XElement xml = new("root", value);
            Assert.Equal(expected, OdfSchemaPatternValidator.ValidateElement(xml, schema, "start").IsMatch);
            Assert.Equal(value, xml.Value);
        }
    }
    /// <summary>
    /// Range values need the prose datatype constraint in addition to their permissive official schema.
    /// 範圍控制項的值除官方寬鬆 schema 外仍須符合正文型別限制。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void RangeValuesDistinguishSchemaAndProseRequirements(OdfVersion version)
    {
        foreach (string value in new[] { "", "arbitrary text", "Infinity", "nan", "1,234" })
        {
            XElement control = Leaf("value-range", "value", value);
            XElement form = new(XName.Get("form", OdfNamespaces.Form), control);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(form, OdfSchemaRegistry.GetSchema(version), "form-form").IsMatch);
            AssertInvalid(Document(version, "value-range", control), version, "ODF3141");
        }
    }

    /// <summary>
    /// Foreign element or attribute names cannot activate native range-value prose rules.
    /// 外部元素或屬性名稱不能啟動原生範圍值正文規則。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void RangeValueRulesUseExpandedXmlNames(OdfVersion version)
    {
        XNamespace form = OdfNamespaces.Form;
        XElement foreignControl = new(XName.Get("value-range", "urn:foreign"), new XAttribute(form + "value", "invalid"));
        XElement foreignAttribute = Leaf("value-range", "value", null);
        foreignAttribute.SetAttributeValue(XName.Get("value", "urn:foreign"), "invalid");
        foreach (XElement control in new[] { foreignControl, foreignAttribute })
            foreach (OdfComplianceProfile profile in Profiles(version))
                Assert.DoesNotContain(Validate(Document(version, "value-range", control), profile).Issues, issue => issue.RuleId == "ODF3141");
    }
    private static void Exercise(string attribute, string[] carriers, OdfVersion version, string?[] values)
    {
        XNamespace form = OdfNamespaces.Form;
        foreach (string kind in carriers)
            foreach (string? value in values)
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
        return OdfValidator.Validate(input, "form-typed-values.fodt", profile);
    }

    private static void AssertValid(XElement document, OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            OdfValidationReport report = Validate(document, profile);
            Assert.True(report.IsValid, report.ToJson());
        }
    }

    private static void AssertInvalid(XElement document, OdfVersion version, string ruleId = "ODF3101")
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
            Assert.Contains(Validate(document, profile).Issues, issue => issue.RuleId == ruleId);
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
