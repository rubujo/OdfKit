using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Tools.OdfSchemaGenerator;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests RELAX NG syntax normalization independently of document datatype whitespace.
/// 測試 RELAX NG 語法正規化，並與文件資料型別的空白規則分開處理。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfSchemaNameNormalizationConformanceTests
{
    private static readonly string[] ExpectedPatternNames = ["root", "shared", "start"];
    private static readonly string[] ExpectedRootReferences = ["shared"];

    /// <summary>
    /// Qualified names represent an absent namespace without conflating it with a nonempty namespace.
    /// 限定名稱可表示沒有命名空間，並與非空命名空間保持區別。
    /// </summary>
    [Fact]
    public void QualifiedNamesSupportUnqualifiedAttributes()
    {
        OdfQualifiedName bare = new(string.Empty, "count");
        OdfQualifiedName same = new(string.Empty, "count");
        Assert.Equal(bare, same);
        Assert.Equal(bare.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(bare, new OdfQualifiedName("urn:test", "count"));
        Assert.Equal("{}count", bare.ToString());
        Assert.Throws<ArgumentException>(() => new OdfQualifiedName(null!, "count"));
        Assert.Throws<ArgumentException>(() => new OdfQualifiedName(" \t", "count"));
        Assert.Throws<ArgumentException>(() => new OdfQualifiedName(string.Empty, string.Empty));
    }

    /// <summary>
    /// Attribute shorthand names use no namespace while explicit name classes inherit their context.
    /// 屬性簡寫名稱不使用命名空間，明確名稱類別則繼承其脈絡。
    /// </summary>
    [Fact]
    public void GeneratorDistinguishesAttributeNamespaceContexts()
    {
        const string grammar = """
            <grammar xmlns="http://relaxng.org/ns/structure/1.0" ns="urn:inherited" xmlns:p="urn:prefix">
              <start><element name="root"><ref name="attributes"/></element></start>
              <define name="attributes"><group>
                <attribute name="bare"><text/></attribute>
                <attribute name="qualified" ns="urn:explicit"><text/></attribute>
                <attribute name="empty" ns=""><text/></attribute>
                <attribute name="p:prefixed" ns="urn:ignored"><text/></attribute>
                <attribute><name>inherited</name><text/></attribute>
                <attribute><name ns="">empty-name</name><text/></attribute>
              </group></define>
            </grammar>
            """;
        using MemoryStream input = new(Encoding.UTF8.GetBytes(grammar));
        SchemaMetadata metadata = RelaxNgSchemaMetadataReader.Read(input, "namespace-context.rng");
        SchemaPatternNodeMetadata[] nodes = Flatten(metadata.Patterns.Single(pattern => pattern.Name == "attributes").PatternTree).ToArray();
        Assert.Contains(nodes, node => node.Kind == "attribute" && node.LocalName == "bare" && node.NamespaceUri == string.Empty);
        Assert.Contains(nodes, node => node.Kind == "attribute" && node.LocalName == "qualified" && node.NamespaceUri == "urn:explicit");
        Assert.Contains(nodes, node => node.Kind == "attribute" && node.LocalName == "empty" && node.NamespaceUri == string.Empty);
        Assert.Contains(nodes, node => node.Kind == "attribute" && node.LocalName == "prefixed" && node.NamespaceUri == "urn:prefix");
        Assert.Contains(nodes, node => node.Kind == "name" && node.NameClasses.Any(name => name.LocalName == "inherited" && name.NamespaceUri == "urn:inherited"));
        Assert.Contains(nodes, node => node.Kind == "name" && node.NameClasses.Any(name => name.LocalName == "empty-name" && name.NamespaceUri == string.Empty));
        Assert.Contains(metadata.Attributes, name => name.LocalName == "bare" && name.NamespaceUri == string.Empty);
        Assert.Contains(metadata.Attributes, name => name.LocalName == "inherited" && name.NamespaceUri == "urn:inherited");
    }

    /// <summary>
    /// Schema names, types and combine methods discard only surrounding XML whitespace.
    /// Schema 名稱、型別及合併方式只移除周圍的 XML 空白。
    /// </summary>
    [Fact]
    public void GeneratorNormalizesSchemaTokens()
    {
        const string grammar = """
            <grammar xmlns="http://relaxng.org/ns/structure/1.0" ns="urn:test"
                     datatypeLibrary="http://www.w3.org/2001/XMLSchema-datatypes">
              <start><ref name=" root "/></start>
              <define name=" root "><element name=" item "><ref name=" shared "/></element></define>
              <define name=" shared " combine=" interleave "><attribute name=" first "><data type=" integer "/></attribute></define>
              <define name="shared" combine="interleave"><attribute name="second"><data type="string"/></attribute></define>
            </grammar>
            """;
        using MemoryStream input = new(Encoding.UTF8.GetBytes(grammar));
        SchemaMetadata metadata = RelaxNgSchemaMetadataReader.Read(input, "normalization.rng");
        Assert.Equal(ExpectedPatternNames, metadata.Patterns.Select(pattern => pattern.Name).Order(StringComparer.Ordinal));
        Assert.Contains(metadata.Elements, element => element.LocalName == "item");
        Assert.Contains(metadata.Attributes, attribute => attribute.LocalName == "first");
        SchemaPatternMetadata shared = Assert.Single(metadata.Patterns, pattern => pattern.Name == "shared");
        Assert.Equal("interleave", Assert.Single(shared.PatternTree).Kind);
        Assert.Contains(Flatten(shared.PatternTree), node => node.Kind == "data" && node.DataType == "integer");
        Assert.Contains(Flatten(metadata.Patterns.Single(pattern => pattern.Name == "root").PatternTree), node => node.Kind == "ref" && node.ReferenceName == "shared");
        Assert.Equal(ExpectedRootReferences, metadata.Patterns.Single(pattern => pattern.Name == "root").References);
        Assert.Contains(Flatten(metadata.Patterns.Single(pattern => pattern.Name == "start").PatternTree), node => node.ReferenceName == "root");
    }

    /// <summary>
    /// String literals and pattern parameters retain significant whitespace.
    /// 字串常值及模式參數保留有意義的空白。
    /// </summary>
    [Fact]
    public void GeneratorPreservesLiteralAndParameterText()
    {
        const string grammar = """
            <grammar xmlns="http://relaxng.org/ns/structure/1.0" ns="urn:test"
                     datatypeLibrary="http://www.w3.org/2001/XMLSchema-datatypes">
              <start><element name="root"><choice>
                <value type="string"> A </value>
                <value type="string"> </value>
                <data type="string"><param name=" pattern "> B </param></data>
              </choice></element></start>
            </grammar>
            """;
        using MemoryStream input = new(Encoding.UTF8.GetBytes(grammar));
        SchemaMetadata metadata = RelaxNgSchemaMetadataReader.Read(input, "literal.rng");
        SchemaPatternNodeMetadata[] nodes = Flatten(metadata.Patterns.Single(pattern => pattern.Name == "start").PatternTree).ToArray();
        Assert.Contains(nodes, node => node.Kind == "value" && node.Value == " A ");
        Assert.Contains(nodes, node => node.Kind == "value" && node.Value == " ");
        Assert.Contains(nodes, node => node.Kind == "data" && node.DataParameters.Any(parameter => parameter.Name == "pattern" && parameter.Value == " B "));
    }

    /// <summary>
    /// Legacy official definition names resolve after RELAX NG syntax normalization.
    /// 舊版官方定義名稱經 RELAX NG 語法正規化後可正確解析。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf10)]
    [InlineData(OdfVersion.Odf11)]
    public void LegacyOfficialDefinitionNamesResolve(OdfVersion version)
    {
        OdfSchemaSet schema = OdfSchemaRegistry.GetSchema(version);
        Assert.NotNull(schema.FindPattern("style-graphic-properties-elements"));
        Assert.NotNull(schema.FindPattern("style-drawing-page-properties-attlist"));
        Assert.DoesNotContain(schema.Patterns.Keys, name => name != name.Trim(' ', '\t', '\r', '\n'));
    }

    /// <summary>
    /// Official legacy schemas retain statistics attributes declared without a namespace.
    /// 官方舊版 schema 保留未指定命名空間的統計屬性。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf10)]
    [InlineData(OdfVersion.Odf11)]
    public void LegacyUnqualifiedStatisticsAttributesRemainDefined(OdfVersion version)
    {
        OdfSchemaSet schema = OdfSchemaRegistry.GetSchema(version);
        foreach (string name in new[] { "frame-count", "sentence-count", "syllable-count", "non-whitespace-character-count" })
        {
            Assert.NotNull(schema.FindAttribute(string.Empty, name));
            XElement statistics = new(XName.Get("document-statistic", OdfNamespaces.Meta), new XAttribute(name, "1"));
            Assert.True(OdfSchemaPatternValidator.ValidateElement(statistics, schema, "office-meta-data").IsMatch);
            statistics.SetAttributeValue(name, "-1");
            Assert.False(OdfSchemaPatternValidator.ValidateElement(statistics, schema, "office-meta-data").IsMatch);
        }
    }

    /// <summary>
    /// Native legacy transition filters accept valid calculation modes and reject invalid ones.
    /// 原生舊版轉場濾鏡接受合法計算模式，並拒絕不合法模式。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf10)]
    [InlineData(OdfVersion.Odf11)]
    public void LegacyTransitionFilterModesUseResolvedDefinitions(OdfVersion version)
    {
        OdfSchemaSet schema = OdfSchemaRegistry.GetSchema(version);
        foreach (string? mode in new string?[] { null, "discrete", "linear", "paced", "spline" })
        {
            XElement native = new(XName.Get("transitionFilter", OdfNamespaces.Anim), new XAttribute(XName.Get("type", OdfNamespaces.Smil), "barWipe"));
            if (mode is not null)
                native.SetAttributeValue(XName.Get("calcMode", OdfNamespaces.Smil), mode);
            Assert.True(OdfSchemaPatternValidator.ValidateElement(native, schema, "animation-element").IsMatch);
            native.SetAttributeValue(XName.Get("calcMode", OdfNamespaces.Smil), "invalid");
            Assert.False(OdfSchemaPatternValidator.ValidateElement(native, schema, "animation-element").IsMatch);
        }
    }

    /// <summary>
    /// Character content predicates follow references but exclude attribute and nested element content.
    /// 字元內容判定沿參照解析，但排除屬性及巢狀元素的內容。
    /// </summary>
    [Fact]
    public void GeneratorCharacterContentRespectsElementBoundaries()
    {
        const string grammar = """
            <grammar xmlns="http://relaxng.org/ns/structure/1.0" ns="urn:test">
              <start><ref name="root"/></start>
              <define name="root"><element name="root"><attribute name="id"><text/></attribute><ref name="loop"/></element></define>
              <define name="loop"><choice><ref name="loop"/><ref name="children"/></choice></define>
              <define name="children"><zeroOrMore><ref name="leaf"/></zeroOrMore></define>
              <define name="leaf"><element><choice><name>a</name><name>b</name></choice><text/></element></define>
              <define name="mixed"><element name="mixed"><mixed><element name="item"><empty/></element></mixed></element></define>
              <define name="constant"><element name="constant"><value>literal</value></element></define>
            </grammar>
            """;
        using MemoryStream input = new(Encoding.UTF8.GetBytes(grammar));
        SchemaMetadata metadata = RelaxNgSchemaMetadataReader.Read(input, "content.rng");
        metadata.Version = "1.4";
        using StringWriter output = new();
        SchemaMetadataCSharpWriter.Write(metadata, output);
        string predicate = output.ToString().Split("internal static bool AllowsCharacterData", StringSplitOptions.None).Last();
        Assert.Contains("localName is \"a\" or \"b\" or \"constant\" or \"mixed\"", predicate);
        Assert.DoesNotContain("\"root\"", predicate);
        Assert.DoesNotContain("\"item\"", predicate);
        Assert.DoesNotContain("\"id\"", predicate);
    }

    private static IEnumerable<SchemaPatternNodeMetadata> Flatten(IEnumerable<SchemaPatternNodeMetadata> nodes)
    {
        foreach (SchemaPatternNodeMetadata node in nodes)
        {
            yield return node;
            foreach (SchemaPatternNodeMetadata child in Flatten(node.Children))
                yield return child;
        }
    }
}
