using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests color, variable name and value type definitions against the official schemas.
/// 依官方 schema 測試色彩、變數名稱及值型別定義。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfBasicNamedDatatypeConformanceTests
{
    /// <summary>
    /// Named datatypes keep their own grammar and preserve raw attribute values.
    /// 具名資料型別維持各自文法，並保留原始屬性值。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void BasicNamedTypesRespectTheirDefinitions(OdfVersion version)
    {
        (string Type, string Value, bool Valid)[] samples =
        [
            ("color", "#000000", true), ("color", "#ffffff", true), ("color", "#aB12Cd", true),
            ("color", "#fff", false), ("color", "#12345678", false), ("color", "red", false),
            ("color", "#gg0000", false), ("color", " #000000", false), ("color", "#000000 ", false),
            ("variableName", "", true), ("variableName", "1name", true), ("variableName", "a:b", true),
            ("variableName", " name with spaces ", true), ("variableName", "變數😀", true),
            ("variableName", "\tname\n", true),
            ("valueType", "float", true), ("valueType", "time", true), ("valueType", "date", true),
            ("valueType", "percentage", true), ("valueType", "currency", true),
            ("valueType", "boolean", true), ("valueType", "string", true), ("valueType", " float ", true),
            ("valueType", "", false), ("valueType", "float string", false), ("valueType", "Float", false),
            ("valueType", "double", false), ("valueType", "error", false), ("valueType", "error.", false)
        ];
        OdfSchemaSet seed = OdfSchemaRegistry.GetSchema(version);
        foreach (string type in new[] { "color", "variableName", "valueType" })
        {
            Assert.NotNull(seed.FindPattern(type));
            OdfSchemaPatternNode reference = new(OdfSchemaPatternNodeKind.Ref, "exactlyOne", "", "", type, "", "");
            OdfSchemaPatternDefinition probe = new("odfkit-basic-type-probe", [new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "exactlyOne", "", "root", "", "", "", null, [reference])]);
            OdfSchemaSet schema = new(version, seed.SourceUrl, seed.SourceDate, [], attributes: null, nameClasses: null, patterns: seed.Patterns.Values.Append(probe));
            foreach (var sample in samples.Where(sample => sample.Type == type))
            {
                Assert.Equal(sample.Valid, OdfSchemaPatternValidator.ValidateElement(new XElement("root", sample.Value), schema, "odfkit-basic-type-probe").IsMatch);
                XElement carrier = new("root", new XAttribute("value", sample.Value));
                OdfNode dom = OdfXmlReader.Parse(Encoding.UTF8.GetBytes(carrier.ToString()));
                using MemoryStream output = new();
                OdfXmlWriter.Write(dom, output);
                XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()));
                Assert.Equal(sample.Value, (string?)saved.Attribute("value"));
            }
        }
    }

    /// <summary>
    /// Color objects preserve hexadecimal spellings and produce all three byte channels.
    /// 色彩物件保留十六進位拼寫，並產生三個位元組通道。
    /// </summary>
    [Fact]
    public void ColorModelsUseSixHexadecimalDigits()
    {
        foreach (string value in new[] { "#000000", "#ffffff", "#aB12Cd" })
        {
            Assert.True(OdfColor.TryParse(value, out OdfColor color));
            Assert.Equal(value, color.Value);
        }
        foreach (string value in new[] { "", "red", "#fff", "#12345678", "#gg0000", " #000000" })
        {
            Assert.False(OdfColor.TryParse(value, out _));
            Assert.Throws<ArgumentException>(() => new OdfColor(value));
        }
        foreach (byte red in new byte[] { 0, 127, 255 })
            foreach (byte green in new byte[] { 0, 127, 255 })
                foreach (byte blue in new byte[] { 0, 127, 255 })
                    Assert.Equal($"#{red:x2}{green:x2}{blue:x2}", OdfColor.FromRgb(red, green, blue).Value);
    }
}
