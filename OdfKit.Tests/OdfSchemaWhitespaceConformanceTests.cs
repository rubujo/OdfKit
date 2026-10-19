using System.Xml.Linq;
using OdfKit.Compliance;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests XML Schema whitespace normalization and RELAX NG XML whitespace tokenization.
/// 測試 XML Schema 空白正規化及 RELAX NG 的 XML 空白分詞。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfSchemaWhitespaceConformanceTests
{
    /// <summary>
    /// Derived and atomic datatypes normalize XML whitespace before lexical validation.
    /// 衍生及原子資料型別在詞法驗證前正規化 XML 空白。
    /// </summary>
    /// <param name="type">The datatype name. / 資料型別名稱。</param>
    /// <param name="value">The lexical input. / 詞法輸入。</param>
    [Theory]
    [InlineData("NCName", " \tA\r\n")]
    [InlineData("ID", " \tA\r\n")]
    [InlineData("IDREF", " \tA\r\n")]
    [InlineData("ENTITY", " \tA\r\n")]
    [InlineData("Name", " q:A ")]
    [InlineData("NMTOKEN", " 12:Name ")]
    [InlineData("language", " en-US ")]
    [InlineData("time", " 13:20:00Z ")]
    [InlineData("date", " 2026-10-16 ")]
    [InlineData("duration", " PT1H ")]
    [InlineData("hexBinary", " 0A ")]
    [InlineData("double", " 1.0 ")]
    [InlineData("decimal", " 1.0 ")]
    [InlineData("boolean", " true ")]
    public void DatatypesApplyTheirWhitespaceRules(string type, string value)
    {
        foreach (string name in new[] { type, "xsd:" + type })
            Check(Data(name), value, true);
    }

    /// <summary>
    /// Whitespace normalization precedes length and pattern facets without collapsing strings.
    /// 空白正規化先於長度及模式 facet，且不摺疊一般字串。
    /// </summary>
    [Fact]
    public void FacetsObserveTheNormalizedLexicalValue()
    {
        Check(Data("NCName", ("length", "1")), " \tA\n", true);
        Check(Data("token", ("length", "3")), " A\t  B ", true);
        Check(Data("token", ("pattern", "A B")), " A\t B ", true);
        Check(Data("string", ("length", "3")), " A ", true);
        Check(Data("string", ("length", "1")), " A ", false);
        Check(Data("normalizedString", ("length", "6")), " a\t b ", true);
        Check(Data("normalizedString", ("pattern", " a  b ")), " a\t b ", true);
    }

    /// <summary>
    /// Typed literal equality preserves, replaces, or collapses only the appropriate XML whitespace.
    /// 型別常值的相等判斷依規則保留、替換或摺疊 XML 空白。
    /// </summary>
    [Fact]
    public void LiteralEqualityRespectsWhitespaceAndNonbreakingSpaces()
    {
        Check(Value("token", "A B"), " \tA \tB\n ", true);
        Check(Value("", "A B"), " A\tB ", true);
        Check(Value("NCName", "A"), " A ", true);
        Check(Value("normalizedString", " a  b "), " a\t b ", true);
        Check(Value("normalizedString", " a b "), "a b", false);
        Check(Value("string", " A "), "A", false);
        Check(Value("token", "A"), "A\u00a0", false);
        Check(Value("", "A B"), "A\u2003B", false);
        Check(Data("token", ("length", "2")), "A\u00a0", true);
        Check(Data("token", ("length", "1")), "A\u00a0", false);
    }

    /// <summary>
    /// Only XML whitespace separates list tokens or participates in primitive normalization.
    /// 只有 XML 空白可分隔清單 token 或參與原始型別正規化。
    /// </summary>
    [Fact]
    public void NonXmlWhitespaceCannotBecomeATokenSeparator()
    {
        foreach (string type in new[] { "IDREFS", "NMTOKENS" })
        {
            Check(Data(type), " A\tB\r\n", true);
            Check(Data(type), "A\u00a0B", false);
            Check(Data(type), "A\u2003B", false);
        }
        OdfSchemaPatternNode list = new(OdfSchemaPatternNodeKind.List, "exactlyOne", "", "", "", "", "", null,
            [Value("", "A"), Value("", "B")]);
        Check(list, "A\tB", true);
        Check(list, "A\u00a0B", false);
        Check(Data("base64Binary"), "AQID\u00a0", false);
        Check(Data("NCName"), "\u00a0A\u00a0", false);
        Check(Data("integer"), "12\u00a0", false);
    }

    /// <summary>
    /// Datatype exclusions compare against the normalized value.
    /// 資料型別的排除模式使用正規化後的值比對。
    /// </summary>
    [Fact]
    public void ExclusionsUseTheSameWhitespaceNormalization()
    {
        OdfSchemaPatternNode except = new(OdfSchemaPatternNodeKind.Except, "exactlyOne", "", "", "", "", "", null,
            [Value("NCName", "A")]);
        OdfSchemaPatternNode data = new(OdfSchemaPatternNodeKind.Data, "exactlyOne", "", "", "", "NCName", "", null,
            [except]);
        Check(data, " A ", false);
        Check(data, " B ", true);
    }

    private static OdfSchemaPatternNode Data(string type, params (string Name, string Value)[] facets) =>
        new(OdfSchemaPatternNodeKind.Data, "exactlyOne", "", "", "", type, "", null, null,
            facets.Select(facet => new KeyValuePair<string, string>(facet.Name, facet.Value)),
            "http://www.w3.org/2001/XMLSchema-datatypes");

    private static OdfSchemaPatternNode Value(string type, string value) =>
        new(OdfSchemaPatternNodeKind.Value, "exactlyOne", "", "", "", type, value);

    private static void Check(OdfSchemaPatternNode body, string value, bool expected)
    {
        OdfSchemaSet schema = new(OdfVersion.Odf14, new Uri("https://example.invalid/whitespace.rng"), "test", [],
            attributes: null, nameClasses: null, patterns: [new OdfSchemaPatternDefinition("start",
                [new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "exactlyOne", "", "root", "", "", "", null, [body])])]);
        XElement element = new("root", value);
        string original = element.ToString(SaveOptions.DisableFormatting);
        Assert.Equal(expected, OdfSchemaPatternValidator.ValidateElement(element, schema, "start").IsMatch);
        Assert.Equal(original, element.ToString(SaveOptions.DisableFormatting));
    }
}
