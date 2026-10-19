using System.Xml.Linq;
using OdfKit.Compliance;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests XML Schema time boundaries and ASCII lexical digits.
/// 測試 XML Schema 時間邊界與 ASCII 詞法數字。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfSchemaTimeConformanceTests
{
    /// <summary>
    /// Midnight at hour 24 requires zero minutes, seconds and fractional seconds.
    /// 二十四時的午夜表示必須使用零分、零秒及零小數秒。
    /// </summary>
    [Theory]
    [InlineData("24:00:00", true)]
    [InlineData("24:00:00.000Z", true)]
    [InlineData("24:00:00+14:00", true)]
    [InlineData("24:00:00-14:00", true)]
    [InlineData("00:00:00", true)]
    [InlineData("23:59:59.9999999", true)]
    [InlineData("24:00:00.0001", false)]
    [InlineData("24:00:01", false)]
    [InlineData("24:01:00", false)]
    [InlineData("25:00:00", false)]
    [InlineData("00:00:00+14:01", false)]
    [InlineData("00:00:00-15:00", false)]
    [InlineData("00:00:60", false)]
    [InlineData("٢٤:٠٠:٠٠", false)]
    [InlineData("２４:００:００", false)]
    public void TimeLexicalValuesRespectTheSpecification(string value, bool expected)
    {
        foreach (string type in new[] { "time", "xsd:time" })
        {
            OdfSchemaPatternNode data = new(OdfSchemaPatternNodeKind.Data, "exactlyOne", "", "", "", type, "", null, null,
                null, "http://www.w3.org/2001/XMLSchema-datatypes");
            OdfSchemaSet schema = new(OdfVersion.Odf14, new Uri("https://www.w3.org/TR/xmlschema-2/#time"), "2004-10-28", [],
                attributes: null, nameClasses: null, patterns: [new OdfSchemaPatternDefinition("start",
                    [new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "exactlyOne", "", "root", "", "", "", null, [data])])]);
            XElement element = new("root", value);
            Assert.Equal(expected, OdfSchemaPatternValidator.ValidateElement(element, schema, "start").IsMatch);
            Assert.Equal(value, element.Value);
        }
    }
}
