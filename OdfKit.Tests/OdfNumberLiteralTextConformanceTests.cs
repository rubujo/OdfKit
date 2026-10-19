using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests fixed literal text in native ODF data styles.
/// 測試原生 ODF 資料樣式中的固定文字常值。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfNumberLiteralTextConformanceTests
{
    private static readonly string[] StyleKinds = ["number-style", "currency-style", "percentage-style", "date-style", "time-style", "boolean-style", "text-style"];
    private static readonly string[] Values = ["", " ", "\t\n", "固定😀<&>"];

    /// <summary>
    /// Styles preserve fixed text, including an explicitly empty literal in styles that permit literal-only content.
    /// 樣式保留固定文字；允許只有常值內容的樣式也保留明確指定的空常值。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void StylesPreserveTheirFixedTextOutput(OdfVersion version)
    {
        foreach (string kind in StyleKinds)
            foreach (string value in Values)
            {
                XElement style = Style(kind, new XElement(XName.Get("text", OdfNamespaces.Number), value));
                string expected = value;
                if (kind is "date-style" or "time-style")
                {
                    style.Add(new XElement(XName.Get(kind == "date-style" ? "year" : "hours", OdfNamespaces.Number),
                        new XAttribute(XName.Get("style", OdfNamespaces.Number), "long")));
                    expected += kind == "date-style" ? "2026" : "13";
                }
                Assert.True(OdfSchemaPatternValidator.ValidateElement(style, OdfSchemaRegistry.GetSchema(version), "number-" + kind).IsMatch, version + " " + kind);
                OdfNode root = ParseContainer(style);
                OdfNode node = Assert.Single(root.Children);
                Assert.Equal(value, node.Children.Single(child => child.NamespaceUri == OdfNamespaces.Number && child.LocalName == "text").TextContent);
                object input = kind switch
                {
                    "date-style" or "time-style" => new DateTime(2026, 10, 16, 13, 2, 3),
                    "boolean-style" => true,
                    "text-style" => "Input",
                    _ => 12.0
                };
                Assert.Equal(expected, OdfNumberFormatEngine.Format(input, "N1", root));
                if (input is double number)
                    Assert.Equal(expected, OdfNumberFormatEngine.Format(number, node));
                else if (input is DateTime date)
                    Assert.Equal(expected, OdfNumberFormatEngine.Format(date, node));
                else if (input is bool)
                    Assert.Equal(expected, OdfNumberFormatEngine.Format(1.0, node));
                using MemoryStream output = new();
                OdfXmlWriter.Write(root, output);
                XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()), LoadOptions.PreserveWhitespace);
                Assert.Equal(value, saved.Descendants(XName.Get("text", OdfNamespaces.Number)).Single().Value);
            }
    }

    /// <summary>
    /// A foreign root with a native style name does not select native boolean or percentage semantics.
    /// 使用原生樣式名稱的外來根元素不會選用原生布林或百分比語意。
    /// </summary>
    [Fact]
    public void ForeignStyleNamesDoNotSelectNativeSemantics()
    {
        OdfNode boolean = new(OdfNodeType.Element, "boolean-style", "urn:test:foreign", "n");
        boolean.AppendChild(new OdfNode(OdfNodeType.Element, "boolean", OdfNamespaces.Number, "n"));
        Assert.Equal("1", OdfNumberFormatEngine.Format(1.0, boolean));
        OdfNode percentage = new(OdfNodeType.Element, "percentage-style", "urn:test:foreign", "n");
        OdfNode number = new(OdfNodeType.Element, "number", OdfNamespaces.Number, "n");
        number.SetAttribute("decimal-places", OdfNamespaces.Number, "0", "n");
        percentage.AppendChild(number);
        Assert.Equal("12", OdfNumberFormatEngine.Format(12.0, percentage));
    }

    /// <summary>
    /// Boolean and string placeholders preserve the literal text around their values.
    /// 布林值及字串預留位置保留其值前後的常值文字。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void BooleanAndTextValuesRetainSurroundingLiterals(OdfVersion version)
    {
        XNamespace number = OdfNamespaces.Number;
        foreach (string kind in new[] { "boolean-style", "text-style" })
        {
            string placeholder = kind == "boolean-style" ? "boolean" : "text-content";
            XElement style = Style(kind, new XElement(number + "text", " ["), new XElement(number + placeholder), new XElement(number + "text", "] "));
            Assert.True(OdfSchemaPatternValidator.ValidateElement(style, OdfSchemaRegistry.GetSchema(version), "number-" + kind).IsMatch);
            OdfNode root = ParseContainer(style);
            if (kind == "boolean-style")
            {
                Assert.Equal(" [TRUE] ", OdfNumberFormatEngine.Format(true, "N1", root));
                Assert.Equal(" [FALSE] ", OdfNumberFormatEngine.Format(false, "N1", root));
                Assert.Equal(" [TRUE] ", OdfNumberFormatEngine.Format(1.0, Assert.Single(root.Children)));
                Assert.Equal(" [FALSE] ", OdfNumberFormatEngine.Format(0.0, Assert.Single(root.Children)));
            }
            else
            {
                Assert.Equal(" [Input] ", OdfNumberFormatEngine.Format("Input", "N1", root));
                Assert.Equal(" [] ", OdfNumberFormatEngine.Format(string.Empty, "N1", root));
            }
            OdfNode foreign = new(OdfNodeType.Element, "text", "urn:test:foreign", "n");
            foreign.TextContent = "must not appear";
            Assert.Single(root.Children).AppendChild(foreign);
            Assert.Equal(kind == "boolean-style" ? " [TRUE] " : " [Input] ",
                OdfNumberFormatEngine.Format(kind == "boolean-style" ? (object)true : "Input", "N1", root));
        }
    }

    /// <summary>
    /// Literal text has no native attributes or child elements and cannot appear under arbitrary parents.
    /// 常值文字沒有原生屬性或子元素，也不能出現在任意父元素內。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void LiteralTextUsesItsNativeContentModel(OdfVersion version)
    {
        OdfSchemaSet schema = OdfSchemaRegistry.GetSchema(version);
        XNamespace number = OdfNamespaces.Number;
        foreach (int mutation in new[] { 0, 1 })
        {
            XElement text = new(number + "text", "Text");
            if (mutation == 0)
                text.SetAttributeValue(number + "style", "invalid");
            else
                text.Add(new XElement(number + "number"));
            Assert.False(OdfSchemaPatternValidator.ValidateElement(Style("number-style", text), schema, "number-number-style").IsMatch);
        }
        XElement paragraph = new(XName.Get("p", OdfNamespaces.Text), new XElement(number + "text", "Text"));
        Assert.False(OdfSchemaPatternValidator.ValidateElement(paragraph, schema, "text-p").IsMatch);
    }

    /// <summary>
    /// ODF 1.4 additionally permits fixed text in a named list number format.
    /// ODF 1.4 另允許在具名清單編號格式中使用固定文字。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ListNumberFormatRetainsItsVersionSpecificLiteralText(OdfVersion version)
    {
        foreach (string value in Values)
        {
            XElement style = Style("num-list-format", new XElement(XName.Get("text", OdfNamespaces.Number), value));
            bool match = OdfSchemaPatternValidator.ValidateElement(style, OdfSchemaRegistry.GetSchema(version), "number-num-list-format").IsMatch;
            Assert.Equal(version == OdfVersion.Odf14, match);
            if (!match)
                continue;
            OdfNode root = ParseContainer(style);
            Assert.Equal(value, Assert.Single(root.Children).Children.Single().TextContent);
            using MemoryStream output = new();
            OdfXmlWriter.Write(root, output);
            XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()), LoadOptions.PreserveWhitespace);
            Assert.Equal(value, saved.Descendants(XName.Get("text", OdfNamespaces.Number)).Single().Value);
        }
    }

    private static XElement Style(string kind, params XElement[] content) => new(XName.Get(kind, OdfNamespaces.Number),
        new XAttribute(XName.Get("name", OdfNamespaces.Style), "N1"), content);

    private static OdfNode ParseContainer(XElement style)
    {
        XElement container = new(XName.Get("styles", OdfNamespaces.Office), style);
        return OdfXmlReader.Parse(Encoding.UTF8.GetBytes(container.ToString(SaveOptions.DisableFormatting)));
    }
}
