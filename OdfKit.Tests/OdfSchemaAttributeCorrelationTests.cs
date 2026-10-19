using System.Xml.Linq;
using OdfKit.Compliance;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests RELAX NG attribute and content correlation without ODF-specific element names.
/// 測試 RELAX NG 屬性與內容關聯，不依賴 ODF 專屬元素名稱。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfSchemaAttributeCorrelationTests
{
    /// <summary>
    /// Referenced choices must select the same branch for attributes and children.
    /// 參照的選擇模式必須對屬性及子元素選取同一分支。
    /// </summary>
    [Fact]
    public void ReferencedChoicesKeepAttributesAndChildrenTogether()
    {
        OdfSchemaSet schema = Schema(Ref("options"),
            new OdfSchemaPatternDefinition("options", [Choice(Group(Attribute("mode", "alpha"), Element("a")),
                Group(Attribute("mode", "beta"), Element("b")))]));
        Check(schema, "<root mode='alpha'><a/></root>", true);
        Check(schema, "<root mode='beta'><b/></root>", true);
        Check(schema, "<root mode='alpha'><b/></root>", false);
        Check(schema, "<root mode='beta'><a/></root>", false);
        Check(schema, "<root><a/></root>", false);
    }

    /// <summary>
    /// Attribute-only and content-only alternatives cannot be combined.
    /// 純屬性及純內容的替代分支不可混用。
    /// </summary>
    [Fact]
    public void AttributeOnlyAlternativeCannotBorrowContent()
    {
        OdfSchemaSet schema = Schema(Choice(Attribute("link", "external"), Element("binary")));
        Check(schema, "<root link='external'/>", true);
        Check(schema, "<root><binary/></root>", true);
        Check(schema, "<root link='external'><binary/></root>", false);
        Check(schema, "<root/>", false);
    }

    /// <summary>
    /// An optional attribute-content group is either present together or absent together.
    /// 可選的屬性與內容群組必須一起存在或一起省略。
    /// </summary>
    [Fact]
    public void OptionalGroupRetainsItsRequiredAttribute()
    {
        OdfSchemaSet schema = Schema(Node(OdfSchemaPatternNodeKind.Optional,
            Group(Attribute("mode", "alpha"), Element("a"))));
        Check(schema, "<root/>", true);
        Check(schema, "<root mode='alpha'><a/></root>", true);
        Check(schema, "<root><a/></root>", false);
        Check(schema, "<root mode='alpha'/>", false);
    }

    /// <summary>
    /// Interleaved choices keep independent attributes and accept either attribute order.
    /// 交錯選擇模式保留獨立屬性關聯，並接受不同屬性順序。
    /// </summary>
    [Fact]
    public void InterleavedChoicesPreserveBothDecisions()
    {
        OdfSchemaSet schema = Schema(Node(OdfSchemaPatternNodeKind.Interleave,
            Choice(Group(Attribute("first", "a"), Element("a")), Group(Attribute("first", "b"), Element("b"))),
            Choice(Group(Attribute("second", "c"), Element("c")), Group(Attribute("second", "d"), Element("d")))));
        Check(schema, "<root first='a' second='d'><d/><a/></root>", true);
        Check(schema, "<root second='d' first='a'><a/><d/></root>", true);
        Check(schema, "<root first='a' second='d'><b/><d/></root>", false);
        Check(schema, "<root first='a' second='d'><a/><c/></root>", false);
    }

    /// <summary>
    /// Repetition cannot reuse one consumed attribute for multiple required groups.
    /// 重複模式不可將已消耗的一個屬性重複套用至多個必要群組。
    /// </summary>
    [Theory]
    [InlineData(OdfSchemaPatternNodeKind.ZeroOrMore, true)]
    [InlineData(OdfSchemaPatternNodeKind.OneOrMore, false)]
    public void RepetitionDoesNotReuseAttributes(OdfSchemaPatternNodeKind repetition, bool allowsEmpty)
    {
        OdfSchemaSet schema = Schema(Node(repetition, Group(Attribute("mode", "a"), Element("a"))));
        Check(schema, "<root/>", allowsEmpty);
        Check(schema, "<root mode='a'><a/></root>", true);
        Check(schema, "<root mode='a'><a/><a/></root>", false);
        Check(schema, "<root><a/></root>", false);
    }

    /// <summary>
    /// Resolving one sibling must not change the schema used for another sibling.
    /// 解析一個同層元素時不得改變另一個同層元素使用的結構描述。
    /// </summary>
    [Fact]
    public void SiblingElementsRetainIndependentBranches()
    {
        OdfSchemaPatternNode item = new(OdfSchemaPatternNodeKind.Element, "exactlyOne", "", "item", "", "", "", null,
            [Choice(Group(Attribute("mode", "a"), Element("a")), Group(Attribute("mode", "b"), Element("b")))]);
        OdfSchemaSet schema = Schema(Node(OdfSchemaPatternNodeKind.ZeroOrMore, item));
        Check(schema, "<root><item mode='a'><a/></item><item mode='b'><b/></item></root>", true);
        Check(schema, "<root><item mode='a'><a/></item><item mode='a'><b/></item></root>", false);
    }

    /// <summary>
    /// Wildcard element name classes do not become required child elements.
    /// 萬用元素名稱類別不會被當成必要的子元素。
    /// </summary>
    [Fact]
    public void WildcardNameClassRemainsOutsideTheContentPattern()
    {
        OdfSchemaPatternNode root = new(OdfSchemaPatternNodeKind.Element, "exactlyOne", "", "", "", "", "", null,
            [new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.AnyName, "exactlyOne", "", "", "", "", "",
                [new OdfSchemaNameClass(OdfSchemaNameClassKind.AnyName, "", "", false)]),
                Choice(Group(Attribute("mode", "a"), Element("a")), Group(Attribute("mode", "b"), Element("b")))]);
        OdfSchemaSet schema = new(OdfVersion.Odf14, new Uri("https://example.invalid/wildcard.rng"), "test", [],
            attributes: null, nameClasses: null, patterns: [new OdfSchemaPatternDefinition("start", [root])]);
        Check(schema, "<anything mode='a'><a/></anything>", true);
        Check(schema, "<anything mode='a'><b/></anything>", false);
    }

    /// <summary>
    /// Mixed text remains allowed after consuming its associated attribute.
    /// 消耗關聯屬性後仍允許混合文字。
    /// </summary>
    [Fact]
    public void MixedTextSurvivesAttributeConsumption()
    {
        OdfSchemaSet schema = Schema(Choice(Node(OdfSchemaPatternNodeKind.Mixed, Attribute("mode", "text")),
            Group(Attribute("mode", "element"), Element("a"))));
        Check(schema, "<root mode='text'>content</root>", true);
        Check(schema, "<root mode='text'/>", true);
        Check(schema, "<root mode='element'><a/></root>", true);
        Check(schema, "<root mode='element'>content</root>", false);
        Check(schema, "<root mode='text'><a/></root>", false);
    }

    /// <summary>
    /// Many independent decisions do not lose attribute order independence or child correlation.
    /// 多個獨立選擇仍保留屬性順序無關性及子元素關聯。
    /// </summary>
    [Fact]
    public void ManyIndependentChoicesPreserveTheirOwnValues()
    {
        OdfSchemaPatternNode[] choices = Enumerable.Range(0, 64).Select(index =>
            Choice(Group(Attribute("m" + index, "a"), Element("a" + index)),
                Group(Attribute("m" + index, "b"), Element("b" + index)))).ToArray();
        OdfSchemaSet schema = Schema(Group(choices));
        string attributes = string.Concat(Enumerable.Range(0, 64).Reverse().Select(index => $" m{index}='a'"));
        string children = string.Concat(Enumerable.Range(0, 64).Select(index => $"<a{index}/>"));
        Check(schema, $"<root{attributes}>{children}</root>", true);
        Check(schema, $"<root{attributes}>{children.Replace("<a31/>", "<b31/>", StringComparison.Ordinal)}</root>", false);
    }

    private static void Check(OdfSchemaSet schema, string xml, bool expected) =>
        Assert.Equal(expected, OdfSchemaPatternValidator.ValidateElement(XElement.Parse(xml), schema, "start").IsMatch);

    private static OdfSchemaSet Schema(OdfSchemaPatternNode body, params OdfSchemaPatternDefinition[] definitions) =>
        new(OdfVersion.Odf14, new Uri("https://example.invalid/correlated-attributes.rng"), "test", [],
            attributes: null, nameClasses: null, patterns: new[]
            {
                new OdfSchemaPatternDefinition("start", [new OdfSchemaPatternNode(
                    OdfSchemaPatternNodeKind.Element, "exactlyOne", "", "root", "", "", "", null, [body])])
            }.Concat(definitions));

    private static OdfSchemaPatternNode Attribute(string name, string value) =>
        new(OdfSchemaPatternNodeKind.Attribute, "exactlyOne", "", name, "", "", "", null,
            [new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Value, "exactlyOne", "", "", "", "string", value)]);

    private static OdfSchemaPatternNode Element(string name) =>
        new(OdfSchemaPatternNodeKind.Element, "exactlyOne", "", name, "", "", "");
    private static OdfSchemaPatternNode Ref(string name) => new(OdfSchemaPatternNodeKind.Ref, "exactlyOne", "", "", name, "", "");
    private static OdfSchemaPatternNode Choice(params OdfSchemaPatternNode[] children) => Node(OdfSchemaPatternNodeKind.Choice, children);
    private static OdfSchemaPatternNode Group(params OdfSchemaPatternNode[] children) => Node(OdfSchemaPatternNodeKind.Group, children);
    private static OdfSchemaPatternNode Node(OdfSchemaPatternNodeKind kind, params OdfSchemaPatternNode[] children) =>
        new(kind, "exactlyOne", "", "", "", "", "", null, children);
}
