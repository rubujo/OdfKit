using System.Xml.Linq;
using OdfKit.Compliance;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests the datatype-specific units of XML Schema length facets.
/// 測試 XML Schema 長度限制依資料型別使用正確單位。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfSchemaLengthFacetConformanceTests
{
    /// <summary>
    /// Length measures characters, binary octets or list items rather than CLR storage units.
    /// 長度計算字元、二進位位元組或清單項目，而非 CLR 儲存單位。
    /// </summary>
    [Theory]
    [InlineData("string", "length", "1", "𠀀", true)]
    [InlineData("string", "length", "1", "😀", true)]
    [InlineData("string", "length", "1", "e\u0301", false)]
    [InlineData("string", "minLength", "2", "😀", false)]
    [InlineData("string", "maxLength", "1", "😀", true)]
    [InlineData("token", "length", "1", "  😀 \t", true)]
    [InlineData("hexBinary", "length", "1", "0A", true)]
    [InlineData("hexBinary", "length", "2", "0A00", true)]
    [InlineData("hexBinary", "minLength", "2", "0A", false)]
    [InlineData("base64Binary", "length", "1", "YQ==", true)]
    [InlineData("base64Binary", "length", "2", "YWI=", true)]
    [InlineData("base64Binary", "length", "3", "YWJj", true)]
    [InlineData("base64Binary", "length", "1", " Y Q = = ", true)]
    [InlineData("base64Binary", "minLength", "2", "YQ==", false)]
    [InlineData("base64Binary", "maxLength", "1", "YWI=", false)]
    [InlineData("NMTOKENS", "length", "2", "one two", true)]
    [InlineData("NMTOKENS", "minLength", "2", "one", false)]
    [InlineData("IDREFS", "length", "2", "one two", true)]
    [InlineData("ENTITIES", "maxLength", "1", "one two", false)]
    [InlineData("QName", "length", "1", "p:name", true)]
    [InlineData("QName", "minLength", "100", "p:name", true)]
    [InlineData("QName", "maxLength", "0", "p:name", true)]
    [InlineData("string", "length", "+1", "a", true)]
    [InlineData("string", "length", "-0", "", true)]
    [InlineData("string", "maxLength", "999999999999999999999999", "a", true)]
    [InlineData("string", "minLength", "999999999999999999999999", "a", false)]
    [InlineData("string", "length", "999999999999999999999999", "a", false)]
    public void FacetUnitsFollowTheDatatype(string type, string facet, string limit, string value, bool expected)
    {
        foreach (string dataType in new[] { type, "xsd:" + type })
        {
            OdfSchemaPatternNode data = new(OdfSchemaPatternNodeKind.Data, "exactlyOne", "", "", "", dataType, "", null, null, [new KeyValuePair<string, string>(facet, limit)], "http://www.w3.org/2001/XMLSchema-datatypes");
            OdfSchemaSet schema = new(OdfVersion.Odf14, new Uri("https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/#rf-length"), "2004-10-28", [], attributes: null, nameClasses: null, patterns: [new OdfSchemaPatternDefinition("start", [new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "exactlyOne", "", "root", "", "", "", null, [data])])]);
            XElement xml = new("root", new XAttribute(XNamespace.Xmlns + "p", "urn:probe"), value);
            Assert.Equal(expected, OdfSchemaPatternValidator.ValidateElement(xml, schema, "start").IsMatch);
            Assert.Equal(value, xml.Value);
        }
    }
}
