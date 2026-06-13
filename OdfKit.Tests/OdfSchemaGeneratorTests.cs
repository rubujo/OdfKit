using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using OdfKit.Tools.OdfSchemaGenerator;
using Xunit;

namespace OdfKit.Tests
{
    public class OdfSchemaGeneratorTests
    {
        [Fact]
        public void ReaderExtractsQualifiedNamesDeterministically()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(CreateRelaxNgFixture()));

            SchemaMetadata metadata = new RelaxNgSchemaMetadataReader().Read(stream, "fixture.rng");

            Assert.Equal("fixture.rng", metadata.Source);
            Assert.Equal(
                new[]
                {
                    "urn:oasis:names:tc:opendocument:xmlns:office:1.0|document-content",
                    "urn:oasis:names:tc:opendocument:xmlns:office:1.0|spreadsheet",
                    "urn:oasis:names:tc:opendocument:xmlns:table:1.0|table",
                    "urn:oasis:names:tc:opendocument:xmlns:text:1.0|p"
                },
                metadata.Elements.Select(item => item.NamespaceUri + "|" + item.LocalName).ToArray());

            Assert.Equal(
                new[]
                {
                    "urn:oasis:names:tc:opendocument:xmlns:office:1.0|version",
                    "urn:oasis:names:tc:opendocument:xmlns:text:1.0|style-name"
                },
                metadata.Attributes.Select(item => item.NamespaceUri + "|" + item.LocalName).ToArray());

            Assert.Equal(
                new[]
                {
                    "bounded-data:data",
                    "duplicate:anyName,except,interleave,name,nsName",
                    "paragraph:",
                    "root:choice,group,optional,zeroOrMore",
                    "sheet:",
                    "start:",
                    "table:",
                    "wildcard-element:anyName,empty,except,nsName"
                },
                metadata.Patterns.Select(item => item.Name + ":" + string.Join(",", item.PatternKinds)).ToArray());
            Assert.Equal(
                new[]
                {
                    "bounded-data:",
                    "duplicate:",
                    "paragraph:style-attributes",
                    "root:paragraph,sheet",
                    "sheet:table",
                    "start:root",
                    "table:",
                    "wildcard-element:"
                },
                metadata.Patterns.Select(item => item.Name + ":" + string.Join(",", item.References)).ToArray());

            SchemaPatternMetadata root = metadata.Patterns.Single(pattern => pattern.Name == "root");
            Assert.Equal(
                new[]
                {
                    "urn:oasis:names:tc:opendocument:xmlns:office:1.0|document-content|exactlyOne",
                    "urn:oasis:names:tc:opendocument:xmlns:text:1.0|p|zeroOrMore"
                },
                root.ChildElements.Select(item => item.NamespaceUri + "|" + item.LocalName + "|" + item.Occurrence).ToArray());
            Assert.Equal(
                new[]
                {
                    "urn:oasis:names:tc:opendocument:xmlns:office:1.0|version|optional"
                },
                root.Attributes.Select(item => item.NamespaceUri + "|" + item.LocalName + "|" + item.Occurrence).ToArray());

            SchemaPatternMetadata duplicate = metadata.Patterns.Single(pattern => pattern.Name == "duplicate");
            Assert.Equal(
                new[]
                {
                    "anyName|||False",
                    "name|urn:oasis:names:tc:opendocument:xmlns:text:1.0|span|True",
                    "nsName|urn:oasis:names:tc:opendocument:xmlns:draw:1.0||True"
                },
                duplicate.NameClasses.Select(item => item.Kind + "|" + item.NamespaceUri + "|" + item.LocalName + "|" + item.IsExcept).ToArray());

            SchemaPatternNodeMetadata rootElement = root.PatternTree[0];
            Assert.Equal("element", rootElement.Kind);
            Assert.Equal("urn:oasis:names:tc:opendocument:xmlns:office:1.0", rootElement.NamespaceUri);
            Assert.Equal("document-content", rootElement.LocalName);
            Assert.Collection(
                rootElement.Children,
                optional =>
                {
                    Assert.Equal("optional", optional.Kind);
                    Assert.Equal("optional", optional.Occurrence);
                    SchemaPatternNodeMetadata attribute = Assert.Single(optional.Children);
                    Assert.Equal("attribute", attribute.Kind);
                    Assert.Equal("optional", attribute.Occurrence);
                    Assert.Equal("version", attribute.LocalName);
                },
                zeroOrMore =>
                {
                    Assert.Equal("zeroOrMore", zeroOrMore.Kind);
                    SchemaPatternNodeMetadata paragraph = Assert.Single(zeroOrMore.Children);
                    Assert.Equal("element", paragraph.Kind);
                    Assert.Equal("zeroOrMore", paragraph.Occurrence);
                    Assert.Equal("p", paragraph.LocalName);
                });

            SchemaPatternNodeMetadata rootGroup = root.PatternTree[1];
            Assert.Equal("group", rootGroup.Kind);
            SchemaPatternNodeMetadata rootChoice = Assert.Single(rootGroup.Children);
            Assert.Equal("choice", rootChoice.Kind);
            Assert.Equal(
                new[] { "sheet", "paragraph", "paragraph" },
                rootChoice.Children.Select(item => item.ReferenceName).ToArray());

            SchemaPatternNodeMetadata duplicateAnyName = duplicate.PatternTree[0].Children[1];
            Assert.Equal("anyName", duplicateAnyName.Kind);
            Assert.Collection(
                duplicateAnyName.NameClasses,
                nameClass => Assert.Equal("anyName", nameClass.Kind));
            SchemaPatternNodeMetadata duplicateExcept = Assert.Single(duplicateAnyName.Children);
            Assert.Equal("except", duplicateExcept.Kind);
            Assert.All(duplicateExcept.Children, child => Assert.True(child.NameClasses.Single().IsExcept));

            SchemaPatternMetadata wildcardElement = metadata.Patterns.Single(pattern => pattern.Name == "wildcard-element");
            SchemaPatternNodeMetadata wildcardElementRoot = Assert.Single(wildcardElement.PatternTree);
            Assert.Equal("element", wildcardElementRoot.Kind);
            SchemaPatternNodeMetadata wildcardNameClass = wildcardElementRoot.Children[0];
            Assert.Equal("anyName", wildcardNameClass.Kind);
            Assert.Equal("except", Assert.Single(wildcardNameClass.Children).Kind);
            Assert.Equal("empty", wildcardElementRoot.Children[1].Kind);

            SchemaPatternMetadata boundedData = metadata.Patterns.Single(pattern => pattern.Name == "bounded-data");
            SchemaPatternNodeMetadata data = Assert.Single(boundedData.PatternTree);
            Assert.Equal("data", data.Kind);
            Assert.Equal("integer", data.DataType);
            Assert.Equal(
                new[] { "maxInclusive|10", "minInclusive|1" },
                data.DataParameters.Select(item => item.Name + "|" + item.Value).ToArray());

            SchemaPatternMetadata start = metadata.Patterns.Single(pattern => pattern.Name == "start");
            SchemaPatternNodeMetadata startRoot = Assert.Single(start.PatternTree);
            Assert.Equal("ref", startRoot.Kind);
            Assert.Equal("root", startRoot.ReferenceName);
        }

        [Fact]
        public void JsonWriterUsesStableOrderingAndEscaping()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(CreateRelaxNgFixture()));
            SchemaMetadata metadata = new RelaxNgSchemaMetadataReader().Read(stream, "fixture.rng");
            using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

            new SchemaMetadataJsonWriter().Write(metadata, writer);

            string json = writer.ToString();
            Assert.Contains("\"source\": \"fixture.rng\"", json);
            Assert.Contains("\"sourceDate\": \"generated\"", json);
            Assert.Contains("\"patterns\": [", json);
            Assert.Contains("\"name\": \"root\"", json);
            Assert.Contains("\"name\": \"start\"", json);
            Assert.Contains("\"references\": [\"paragraph\", \"sheet\"]", json);
            Assert.Contains("\"references\": [\"root\"]", json);
            Assert.Contains("\"patternKinds\": [\"choice\", \"group\", \"optional\", \"zeroOrMore\"]", json);
            Assert.Contains("\"childElements\": [", json);
            Assert.Contains("\"nameClasses\": [", json);
            Assert.Contains("\"patternTree\": [", json);
            Assert.Contains("\"referenceName\": \"sheet\"", json);
            Assert.Contains("\"dataParameters\": [{ \"name\": \"maxInclusive\", \"value\": \"10\" }, { \"name\": \"minInclusive\", \"value\": \"1\" }]", json);
            Assert.Contains("\"isExcept\": true", json);
            Assert.Contains("\"occurrence\": \"zeroOrMore\"", json);
            Assert.True(json.IndexOf("document-content", System.StringComparison.Ordinal) < json.IndexOf("table", System.StringComparison.Ordinal));
            Assert.True(json.IndexOf("office:1.0", System.StringComparison.Ordinal) < json.IndexOf("text:1.0", System.StringComparison.Ordinal));
        }

        [Fact]
        public void CSharpWriterEmitsDeterministicRuntimeSeed()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(CreateRelaxNgFixture()));
            SchemaMetadata metadata = new RelaxNgSchemaMetadataReader().Read(stream, "https://example.invalid/schema.rng");
            using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

            new SchemaMetadataCSharpWriter().Write(metadata, writer, "FixtureSchemaMetadata");

            string code = writer.ToString();
            Assert.Contains("internal static class FixtureSchemaMetadata", code);
            Assert.Contains("public static OdfSchemaSet Create(OdfSchemaSet? baseSchema)", code);
            Assert.Contains("var generated = new OdfSchemaSet(OdfVersion.Odf14", code);
            Assert.Contains("baseSchema.MergeWith(generated)", code);
            Assert.Contains("new OdfElementDefinition(new OdfQualifiedName(\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\", \"document-content\"), OdfSchemaElementRole.DocumentRoot", code);
            Assert.Contains("new OdfElementDefinition(new OdfQualifiedName(\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\", \"spreadsheet\"), OdfSchemaElementRole.BodyContent, OdfVersionRange.AllKnown, OdfDocumentKind.Spreadsheet)", code);
            Assert.Contains("new OdfAttributeDefinition(new OdfQualifiedName(\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\", \"version\"), \"odf-version\", OdfVersionRange.AllKnown, isRequiredOnDocumentRoot: true)", code);
            Assert.Contains("new OdfSchemaNameClass(OdfSchemaNameClassKind.AnyName, \"\", \"\", false)", code);
            Assert.Contains("new OdfSchemaNameClass(OdfSchemaNameClassKind.NamespaceName, \"urn:oasis:names:tc:opendocument:xmlns:draw:1.0\", \"\", true)", code);
            Assert.Contains("new OdfSchemaNameClass(OdfSchemaNameClassKind.Name, \"urn:oasis:names:tc:opendocument:xmlns:text:1.0\", \"span\", true)", code);
            Assert.Contains("var patterns = new List<OdfSchemaPatternDefinition>", code);
            Assert.Contains("new OdfSchemaPatternDefinition(\"root\", new[]", code);
            Assert.Contains("new OdfSchemaPatternDefinition(\"start\", new[]", code);
            Assert.Contains("new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, \"exactlyOne\", \"urn:oasis:names:tc:opendocument:xmlns:office:1.0\", \"document-content\"", code);
            Assert.Contains("new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Choice", code);
            Assert.Contains("new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Ref, \"exactlyOne\", \"\", \"\", \"sheet\"", code);
            Assert.Contains("new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.AnyName", code);
            Assert.Contains("new KeyValuePair<string, string>(\"maxInclusive\", \"10\")", code);
            Assert.Contains("new KeyValuePair<string, string>(\"minInclusive\", \"1\")", code);
            Assert.Contains("), \"generated\", elements, attributes, nameClasses, patterns);", code);
            Assert.True(code.IndexOf("\"document-content\"", System.StringComparison.Ordinal) < code.IndexOf("\"table\"", System.StringComparison.Ordinal));
        }

        [Fact]
        public void CSharpWriterTreatsRelaxNgDivAsTransparentGroup()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(CreateGrammar(
                "<define name=\"wrapped\"><div><element name=\"text:p\"><empty /></element></div></define>")));
            SchemaMetadata metadata = new RelaxNgSchemaMetadataReader().Read(stream, "https://example.invalid/schema.rng");
            using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

            new SchemaMetadataCSharpWriter().Write(metadata, writer, "FixtureSchemaMetadata");

            string code = writer.ToString();
            Assert.Contains("new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Group", code);
            Assert.Contains("new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, \"exactlyOne\", \"urn:oasis:names:tc:opendocument:xmlns:text:1.0\", \"p\"", code);
            Assert.DoesNotContain("OdfSchemaPatternNodeKind.Other", code);
        }

        [Fact]
        public void DomWrapperWriterEmitsTypedAttributePropertiesFromDatatypeNodes()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(CreateGrammar(
                "<define name=\"positive-integer\"><data type=\"positiveInteger\" /></define>" +
                "<define name=\"IDREF\"><data type=\"IDREF\" /></define>" +
                "<define name=\"NCName\"><data type=\"NCName\" /></define>" +
                "<define name=\"boolean\"><choice><value>true</value><value>false</value></choice></define>" +
                "<define name=\"styleNameRef\"><data type=\"NCName\" /></define>" +
                "<define name=\"styleNameRefs\"><list><zeroOrMore><data type=\"NCName\" /></zeroOrMore></list></define>" +
                "<define name=\"color\"><data type=\"string\"><param name=\"pattern\">#[0-9a-fA-F]{6}</param></data></define>" +
                "<define name=\"anyIRI\"><data type=\"anyURI\" /></define>" +
                "<define name=\"zeroToHundredPercent\"><data type=\"string\"><param name=\"pattern\">([0-9]?[0-9](\\.[0-9]*)?|100(\\.0*)?|\\.[0-9]+)%</param></data></define>" +
                "<define name=\"signedZeroToHundredPercent\"><data type=\"string\"><param name=\"pattern\">-?([0-9]?[0-9](\\.[0-9]*)?|100(\\.0*)?|\\.[0-9]+)%</param></data></define>" +
                "<define name=\"cellAddress\"><data type=\"string\"><param name=\"pattern\">($?([^\\. ']+|'([^']|'')+'))?\\.$?[A-Z]+$?[0-9]+</param></data></define>" +
                "<define name=\"cellRangeAddress\"><data type=\"string\"><param name=\"pattern\">($?([^\\. ']+|'([^']|'')+'))?\\.$?[A-Z]+$?[0-9]+(:($?([^\\. ']+|'([^']|'')+'))?\\.$?[A-Z]+$?[0-9]+)?</param></data></define>" +
                "<define name=\"cellRangeAddressList\"><data type=\"string\" /></define>" +
                "<define name=\"vector3D\"><data type=\"string\"><param name=\"pattern\">\\([ ]*-?([0-9]+(\\.[0-9]*)?|\\.[0-9]+)([ ]+-?([0-9]+(\\.[0-9]*)?|\\.[0-9]+)){2}[ ]*\\)</param></data></define>" +
                "<define name=\"point3D\"><data type=\"string\"><param name=\"pattern\">\\([ ]*-?([0-9]+(\\.[0-9]*)?|\\.[0-9]+)((cm)|(mm)|(in)|(pt)|(pc))([ ]+-?([0-9]+(\\.[0-9]*)?|\\.[0-9]+)((cm)|(mm)|(in)|(pt)|(pc))){2}[ ]*\\)</param></data></define>" +
                "<define name=\"points\"><data type=\"string\"><param name=\"pattern\">-?[0-9]+,-?[0-9]+([ ]+-?[0-9]+,-?[0-9]+)*</param></data></define>" +
                "<define name=\"languageCode\"><data type=\"token\"><param name=\"pattern\">[A-Za-z]{1,8}</param></data></define>" +
                "<define name=\"countryCode\"><data type=\"token\"><param name=\"pattern\">[A-Za-z0-9]{1,8}</param></data></define>" +
                "<define name=\"scriptCode\"><data type=\"token\"><param name=\"pattern\">[A-Za-z0-9]{1,8}</param></data></define>" +
                "<define name=\"language\"><data type=\"language\" /></define>" +
                "<define name=\"namespacedToken\"><data type=\"QName\"><param name=\"pattern\">[^:]+:[^:]+</param></data></define>" +
                "<define name=\"character\"><data type=\"string\"><param name=\"length\">1</param></data></define>" +
                "<define name=\"textEncoding\"><data type=\"string\"><param name=\"pattern\">[A-Za-z][A-Za-z0-9._\\-]*</param></data></define>" +
                "<define name=\"targetFrameName\"><choice><value>_self</value><value>_blank</value><ref name=\"string\" /></choice></define>" +
                "<define name=\"lineStyle\"><choice><value>none</value><value>solid</value><value>dotted</value></choice></define>" +
                "<define name=\"lineType\"><choice><value>none</value><value>single</value><value>double</value></choice></define>" +
                "<define name=\"borderWidths\"><list><ref name=\"positiveLength\" /><ref name=\"positiveLength\" /><ref name=\"positiveLength\" /></list></define>" +
                "<define name=\"length\"><data type=\"string\"><param name=\"pattern\">[0-9]+cm</param></data></define>" +
                "<define name=\"percent\"><data type=\"string\"><param name=\"pattern\">[0-9]+%</param></data></define>" +
                "<define name=\"angle\"><data type=\"string\" /></define>" +
                "<define name=\"typed-attributes\"><element name=\"table:calculation-settings\">" +
                "<attribute name=\"table:number-columns-spanned\"><ref name=\"positive-integer\" /></attribute>" +
                "<attribute name=\"table:enabled\"><ref name=\"boolean\" /></attribute>" +
                "<attribute name=\"table:style-name\"><ref name=\"styleNameRef\" /></attribute>" +
                "<attribute name=\"table:style-names\"><ref name=\"styleNameRefs\" /></attribute>" +
                "<attribute name=\"draw:fill-color\"><ref name=\"color\" /></attribute>" +
                "<attribute name=\"xlink:href\"><ref name=\"anyIRI\" /></attribute>" +
                "<attribute name=\"draw:opacity\"><ref name=\"zeroToHundredPercent\" /></attribute>" +
                "<attribute name=\"draw:shadow-offset\"><ref name=\"signedZeroToHundredPercent\" /></attribute>" +
                "<attribute name=\"table:base-cell-address\"><ref name=\"cellAddress\" /></attribute>" +
                "<attribute name=\"table:cell-range-address\"><ref name=\"cellRangeAddress\" /></attribute>" +
                "<attribute name=\"table:cell-range-address-list\"><ref name=\"cellRangeAddressList\" /></attribute>" +
                "<attribute name=\"draw:extrusion-direction\"><ref name=\"vector3D\" /></attribute>" +
                "<attribute name=\"draw:extrusion-viewpoint\"><ref name=\"point3D\" /></attribute>" +
                "<attribute name=\"draw:points\"><ref name=\"points\" /></attribute>" +
                "<attribute name=\"fo:language\"><ref name=\"languageCode\" /></attribute>" +
                "<attribute name=\"fo:country\"><ref name=\"countryCode\" /></attribute>" +
                "<attribute name=\"fo:script\"><ref name=\"scriptCode\" /></attribute>" +
                "<attribute name=\"table:rfc-language-tag\"><ref name=\"language\" /></attribute>" +
                "<attribute name=\"draw:type-name\"><ref name=\"namespacedToken\" /></attribute>" +
                "<attribute name=\"number:decimal-replacement\"><ref name=\"character\" /></attribute>" +
                "<attribute name=\"text:encoding\"><ref name=\"textEncoding\" /></attribute>" +
                "<attribute name=\"office:target-frame-name\"><ref name=\"targetFrameName\" /></attribute>" +
                "<attribute name=\"style:text-underline-style\"><ref name=\"lineStyle\" /></attribute>" +
                "<attribute name=\"style:text-underline-type\"><ref name=\"lineType\" /></attribute>" +
                "<attribute name=\"style:border-line-width\"><ref name=\"borderWidths\" /></attribute>" +
                "<attribute name=\"draw:shape-id\"><ref name=\"IDREF\" /></attribute>" +
                "<attribute name=\"draw:name-token\"><ref name=\"NCName\" /></attribute>" +
                "<attribute name=\"table:width\"><ref name=\"length\" /></attribute>" +
                "<attribute name=\"table:scale\"><ref name=\"percent\" /></attribute>" +
                "<attribute name=\"office:boolean-value\"><data type=\"boolean\" /></attribute>" +
                "<attribute name=\"office:value\"><data type=\"decimal\" /></attribute>" +
                "<attribute name=\"office:date-value\"><data type=\"dateTime\" /></attribute>" +
                "<attribute name=\"office:time-value\"><data type=\"time\" /></attribute>" +
                "<attribute name=\"presentation:duration\"><data type=\"duration\" /></attribute>" +
                "<attribute name=\"draw:angle\"><ref name=\"angle\" /></attribute>" +
                "<attribute name=\"office:version\"><value>1.4</value></attribute>" +
                "<attribute name=\"office:mimetype\"><data type=\"string\" /></attribute>" +
                "<attribute name=\"style:family\"><value>paragraph</value></attribute>" +
                "<attribute name=\"table:name\"><data type=\"string\" /></attribute>" +
                "</element></define>")));
            SchemaMetadata metadata = new RelaxNgSchemaMetadataReader().Read(stream, "https://example.invalid/schema.rng");
            using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

            new DomWrappersCSharpWriter().Write(metadata, writer);

            string code = writer.ToString();
            Assert.Contains("public partial class TableCalculationSettingsElement : OdfElement", code);
            Assert.Contains("public int? NumberColumnsSpanned", code);
            Assert.Contains("get => GetNullableInt32AttributeValue(\"number-columns-spanned\", \"urn:oasis:names:tc:opendocument:xmlns:table:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetInt32AttributeValue(\"number-columns-spanned\", \"urn:oasis:names:tc:opendocument:xmlns:table:1.0\", value.Value, \"table\", GetDocumentVersion());", code);
            Assert.Contains("public OdfLength? Width", code);
            Assert.Contains("get => GetLengthAttributeValue(\"width\", \"urn:oasis:names:tc:opendocument:xmlns:table:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetLengthAttributeValue(\"width\", \"urn:oasis:names:tc:opendocument:xmlns:table:1.0\", value.Value, \"table\", GetDocumentVersion());", code);
            Assert.Contains("public OdfLength? Scale", code);
            Assert.Contains("get => GetLengthAttributeValue(\"scale\", \"urn:oasis:names:tc:opendocument:xmlns:table:1.0\", GetDocumentVersion());", code);
            Assert.Contains("public bool? BooleanValue", code);
            Assert.Contains("get => GetBooleanAttributeValue(\"boolean-value\", \"urn:oasis:names:tc:opendocument:xmlns:office:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetBooleanAttributeValue(\"boolean-value\", \"urn:oasis:names:tc:opendocument:xmlns:office:1.0\", value.Value, \"office\", GetDocumentVersion());", code);
            Assert.Contains("public bool? Enabled", code);
            Assert.Contains("get => GetBooleanAttributeValue(\"enabled\", \"urn:oasis:names:tc:opendocument:xmlns:table:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetBooleanAttributeValue(\"enabled\", \"urn:oasis:names:tc:opendocument:xmlns:table:1.0\", value.Value, \"table\", GetDocumentVersion());", code);
            Assert.Contains("public OdfStyleName? StyleName", code);
            Assert.Contains("get => GetStyleNameAttributeValue(\"style-name\", \"urn:oasis:names:tc:opendocument:xmlns:table:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetStyleNameAttributeValue(\"style-name\", \"urn:oasis:names:tc:opendocument:xmlns:table:1.0\", value.Value, \"table\", GetDocumentVersion());", code);
            Assert.Contains("public OdfStyleNameList? StyleNames", code);
            Assert.Contains("get => GetStyleNameListAttributeValue(\"style-names\", \"urn:oasis:names:tc:opendocument:xmlns:table:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetStyleNameListAttributeValue(\"style-names\", \"urn:oasis:names:tc:opendocument:xmlns:table:1.0\", value.Value, \"table\", GetDocumentVersion());", code);
            Assert.Contains("public OdfColor? FillColor", code);
            Assert.Contains("get => GetColorAttributeValue(\"fill-color\", \"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetColorAttributeValue(\"fill-color\", \"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\", value.Value, \"draw\", GetDocumentVersion());", code);
            Assert.Contains("public OdfIriReference? Href", code);
            Assert.Contains("get => GetIriReferenceAttributeValue(\"href\", \"http://www.w3.org/1999/xlink\", GetDocumentVersion());", code);
            Assert.Contains("SetIriReferenceAttributeValue(\"href\", \"http://www.w3.org/1999/xlink\", value.Value, \"xlink\", GetDocumentVersion());", code);
            Assert.Contains("public OdfPercent? Opacity", code);
            Assert.Contains("get => GetPercentAttributeValue(\"opacity\", \"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetPercentAttributeValue(\"opacity\", \"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\", value.Value, \"draw\", GetDocumentVersion());", code);
            Assert.Contains("public OdfPercent? ShadowOffset", code);
            Assert.Contains("get => GetSignedPercentAttributeValue(\"shadow-offset\", \"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetSignedPercentAttributeValue(\"shadow-offset\", \"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\", value.Value, \"draw\", GetDocumentVersion());", code);
            Assert.Contains("public OdfCellAddressReference? BaseCellAddress", code);
            Assert.Contains("get => GetCellAddressAttributeValue(\"base-cell-address\", \"urn:oasis:names:tc:opendocument:xmlns:table:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetCellAddressAttributeValue(\"base-cell-address\", \"urn:oasis:names:tc:opendocument:xmlns:table:1.0\", value.Value, \"table\", GetDocumentVersion());", code);
            Assert.Contains("public OdfCellRangeAddress? CellRangeAddress", code);
            Assert.Contains("get => GetCellRangeAddressAttributeValue(\"cell-range-address\", \"urn:oasis:names:tc:opendocument:xmlns:table:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetCellRangeAddressAttributeValue(\"cell-range-address\", \"urn:oasis:names:tc:opendocument:xmlns:table:1.0\", value.Value, \"table\", GetDocumentVersion());", code);
            Assert.Contains("public OdfCellRangeAddressList? CellRangeAddressList", code);
            Assert.Contains("get => GetCellRangeAddressListAttributeValue(\"cell-range-address-list\", \"urn:oasis:names:tc:opendocument:xmlns:table:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetCellRangeAddressListAttributeValue(\"cell-range-address-list\", \"urn:oasis:names:tc:opendocument:xmlns:table:1.0\", value.Value, \"table\", GetDocumentVersion());", code);
            Assert.Contains("public OdfVector3D? ExtrusionDirection", code);
            Assert.Contains("get => GetVector3DAttributeValue(\"extrusion-direction\", \"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetVector3DAttributeValue(\"extrusion-direction\", \"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\", value.Value, \"draw\", GetDocumentVersion());", code);
            Assert.Contains("public OdfPoint3D? ExtrusionViewpoint", code);
            Assert.Contains("get => GetPoint3DAttributeValue(\"extrusion-viewpoint\", \"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetPoint3DAttributeValue(\"extrusion-viewpoint\", \"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\", value.Value, \"draw\", GetDocumentVersion());", code);
            Assert.Contains("public OdfPointList? Points", code);
            Assert.Contains("get => GetPointListAttributeValue(\"points\", \"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetPointListAttributeValue(\"points\", \"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\", value.Value, \"draw\", GetDocumentVersion());", code);
            Assert.Contains("public OdfLanguageCode? Language", code);
            Assert.Contains("get => GetLanguageCodeAttributeValue(\"language\", \"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\", GetDocumentVersion());", code);
            Assert.Contains("public OdfCountryCode? Country", code);
            Assert.Contains("get => GetCountryCodeAttributeValue(\"country\", \"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\", GetDocumentVersion());", code);
            Assert.Contains("public OdfScriptCode? Script", code);
            Assert.Contains("get => GetScriptCodeAttributeValue(\"script\", \"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\", GetDocumentVersion());", code);
            Assert.Contains("public OdfLanguageTag? RfcLanguageTag", code);
            Assert.Contains("get => GetLanguageTagAttributeValue(\"rfc-language-tag\", \"urn:oasis:names:tc:opendocument:xmlns:table:1.0\", GetDocumentVersion());", code);
            Assert.Contains("public OdfNamespacedToken? TypeName", code);
            Assert.Contains("get => GetNamespacedTokenAttributeValue(\"type-name\", \"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetNamespacedTokenAttributeValue(\"type-name\", \"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\", value.Value, \"draw\", GetDocumentVersion());", code);
            Assert.Contains("public OdfCharacter? DecimalReplacement", code);
            Assert.Contains("get => GetCharacterAttributeValue(\"decimal-replacement\", \"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\", GetDocumentVersion());", code);
            Assert.Contains("public OdfTextEncoding? Encoding", code);
            Assert.Contains("get => GetTextEncodingAttributeValue(\"encoding\", \"urn:oasis:names:tc:opendocument:xmlns:text:1.0\", GetDocumentVersion());", code);
            Assert.Contains("public OdfTargetFrameName? TargetFrameName", code);
            Assert.Contains("get => GetTargetFrameNameAttributeValue(\"target-frame-name\", \"urn:oasis:names:tc:opendocument:xmlns:office:1.0\", GetDocumentVersion());", code);
            Assert.Contains("public OdfLineStyle? TextUnderlineStyle", code);
            Assert.Contains("get => GetLineStyleAttributeValue(\"text-underline-style\", \"urn:oasis:names:tc:opendocument:xmlns:style:1.0\", GetDocumentVersion());", code);
            Assert.Contains("public OdfLineType? TextUnderlineType", code);
            Assert.Contains("get => GetLineTypeAttributeValue(\"text-underline-type\", \"urn:oasis:names:tc:opendocument:xmlns:style:1.0\", GetDocumentVersion());", code);
            Assert.Contains("public OdfBorderWidths? BorderLineWidth", code);
            Assert.Contains("get => GetBorderWidthsAttributeValue(\"border-line-width\", \"urn:oasis:names:tc:opendocument:xmlns:style:1.0\", GetDocumentVersion());", code);
            Assert.Contains("public OdfXmlName? ShapeId", code);
            Assert.Contains("get => GetXmlNameAttributeValue(\"shape-id\", \"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetXmlNameAttributeValue(\"shape-id\", \"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\", value.Value, \"draw\", GetDocumentVersion());", code);
            Assert.Contains("public OdfXmlName? NameToken", code);
            Assert.Contains("get => GetXmlNameAttributeValue(\"name-token\", \"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\", GetDocumentVersion());", code);
            Assert.Contains("public decimal? Value", code);
            Assert.Contains("get => GetDecimalAttributeValue(\"value\", \"urn:oasis:names:tc:opendocument:xmlns:office:1.0\", GetDocumentVersion());", code);
            Assert.Contains("public DateTime? DateValue", code);
            Assert.Contains("get => GetDateTimeAttributeValue(\"date-value\", \"urn:oasis:names:tc:opendocument:xmlns:office:1.0\", GetDocumentVersion());", code);
            Assert.Contains("public OdfTime? TimeValue", code);
            Assert.Contains("get => GetTimeAttributeValue(\"time-value\", \"urn:oasis:names:tc:opendocument:xmlns:office:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetTimeAttributeValue(\"time-value\", \"urn:oasis:names:tc:opendocument:xmlns:office:1.0\", value.Value, \"office\", GetDocumentVersion());", code);
            Assert.Contains("public OdfDuration? Duration", code);
            Assert.Contains("get => GetDurationAttributeValue(\"duration\", \"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetDurationAttributeValue(\"duration\", \"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\", value.Value, \"presentation\", GetDocumentVersion());", code);
            Assert.Contains("public OdfAngle? Angle", code);
            Assert.Contains("get => GetAngleAttributeValue(\"angle\", \"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetAngleAttributeValue(\"angle\", \"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\", value.Value, \"draw\", GetDocumentVersion());", code);
            Assert.Contains("public OdfStyleFamily? Family", code);
            Assert.Contains("get => GetStyleFamilyAttributeValue(\"family\", \"urn:oasis:names:tc:opendocument:xmlns:style:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetStyleFamilyAttributeValue(\"family\", \"urn:oasis:names:tc:opendocument:xmlns:style:1.0\", value.Value, \"style\", GetDocumentVersion());", code);
            Assert.Contains("public OdfVersion? Version", code);
            Assert.Contains("get => GetOdfVersionAttributeValue(\"version\", \"urn:oasis:names:tc:opendocument:xmlns:office:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetOdfVersionAttributeValue(\"version\", \"urn:oasis:names:tc:opendocument:xmlns:office:1.0\", value.Value, \"office\", GetDocumentVersion());", code);
            Assert.Contains("public OdfMediaType? Mimetype", code);
            Assert.Contains("get => GetMediaTypeAttributeValue(\"mimetype\", \"urn:oasis:names:tc:opendocument:xmlns:office:1.0\", GetDocumentVersion());", code);
            Assert.Contains("SetMediaTypeAttributeValue(\"mimetype\", \"urn:oasis:names:tc:opendocument:xmlns:office:1.0\", value.Value, \"office\", GetDocumentVersion());", code);
            Assert.Contains("public string? Name", code);
            Assert.Contains("get => GetAttributeValue(\"name\", \"urn:oasis:names:tc:opendocument:xmlns:table:1.0\", GetDocumentVersion());", code);
        }

        [Fact]
        public void ReaderAndCSharpWriterTreatParentRefAsRuntimeReference()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(CreateGrammar(
                "<define name=\"paragraph\"><element name=\"text:p\"><empty /></element></define>" +
                "<define name=\"wrapper\"><element name=\"office:document-content\"><parentRef name=\"paragraph\" /></element></define>")));
            SchemaMetadata metadata = new RelaxNgSchemaMetadataReader().Read(stream, "https://example.invalid/schema.rng");
            SchemaPatternMetadata wrapper = metadata.Patterns.Single(pattern => pattern.Name == "wrapper");
            using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

            new SchemaMetadataCSharpWriter().Write(metadata, writer, "FixtureSchemaMetadata");

            Assert.Contains("paragraph", wrapper.References);
            SchemaPatternNodeMetadata parentRef = wrapper.PatternTree[0].Children.Single();
            Assert.Equal("parentRef", parentRef.Kind);
            Assert.Equal("paragraph", parentRef.ReferenceName);
            string code = writer.ToString();
            Assert.Contains("new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Ref, \"exactlyOne\", \"\", \"\", \"paragraph\"", code);
            Assert.DoesNotContain("OdfSchemaPatternNodeKind.Other, \"exactlyOne\", \"\", \"\", \"paragraph\"", code);
        }

        [Fact]
        public void ReaderPreservesRelaxNgDefineCombineSemantics()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(CreateGrammar(
                "<define name=\"combined\" combine=\"interleave\"><element name=\"text:p\"><empty /></element></define>" +
                "<define name=\"combined\" combine=\"interleave\"><element name=\"table:table\"><empty /></element></define>" +
                "<define name=\"choice-combined\" combine=\"choice\"><element name=\"text:p\"><empty /></element></define>" +
                "<define name=\"choice-combined\" combine=\"choice\"><element name=\"table:table\"><empty /></element></define>")));
            SchemaMetadata metadata = new RelaxNgSchemaMetadataReader().Read(stream, "https://example.invalid/schema.rng");
            SchemaPatternMetadata combined = metadata.Patterns.Single(pattern => pattern.Name == "combined");
            SchemaPatternMetadata choiceCombined = metadata.Patterns.Single(pattern => pattern.Name == "choice-combined");
            using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

            new SchemaMetadataCSharpWriter().Write(metadata, writer, "FixtureSchemaMetadata");

            SchemaPatternNodeMetadata root = Assert.Single(combined.PatternTree);
            Assert.Equal("interleave", root.Kind);
            Assert.Equal(new[] { "p", "table" }, root.Children.Select(child => child.LocalName).ToArray());
            Assert.Equal(2, choiceCombined.PatternTree.Count);
            string code = writer.ToString();
            Assert.Contains("new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Interleave", code);
            Assert.Contains("new OdfSchemaPatternDefinition(\"choice-combined\", new[]", code);
        }

        [Fact]
        public void ReaderInheritsRelaxNgNamespaceContext()
        {
            const string officeNamespace = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
            string schema =
                "<grammar xmlns=\"http://relaxng.org/ns/structure/1.0\" ns=\"" + officeNamespace + "\">" +
                "<define name=\"root\">" +
                "<element name=\"document-content\">" +
                "<attribute name=\"version\" />" +
                "<element name=\"body\" />" +
                "<element><name>text</name><empty /></element>" +
                "<anyName><except><nsName /></except></anyName>" +
                "</element>" +
                "</define>" +
                "</grammar>";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(schema));

            SchemaMetadata metadata = new RelaxNgSchemaMetadataReader().Read(stream, "https://example.invalid/schema.rng");

            Assert.Contains(metadata.Elements, item =>
                item.NamespaceUri == officeNamespace &&
                item.LocalName == "document-content");
            Assert.Contains(metadata.Elements, item =>
                item.NamespaceUri == officeNamespace &&
                item.LocalName == "body");
            Assert.Contains(metadata.Attributes, item =>
                item.NamespaceUri == officeNamespace &&
                item.LocalName == "version");

            SchemaPatternMetadata root = metadata.Patterns.Single(pattern => pattern.Name == "root");
            SchemaPatternNodeMetadata rootElement = Assert.Single(root.PatternTree);
            Assert.Equal(officeNamespace, rootElement.NamespaceUri);
            Assert.Equal(officeNamespace, rootElement.Children[0].NamespaceUri);
            Assert.Equal(officeNamespace, rootElement.Children[1].NamespaceUri);
            SchemaPatternNodeMetadata dynamicElementName = rootElement.Children[2].Children[0];
            Assert.Equal("name", dynamicElementName.Kind);
            Assert.Equal(officeNamespace, dynamicElementName.NameClasses.Single().NamespaceUri);
            SchemaPatternNodeMetadata excludedNamespace = rootElement.Children[3].Children.Single().Children.Single();
            Assert.Equal("nsName", excludedNamespace.Kind);
            Assert.Equal(officeNamespace, excludedNamespace.NameClasses.Single().NamespaceUri);
        }

        [Fact]
        public void ReaderAndWritersPreserveRelaxNgDatatypeLibraryContext()
        {
            const string xmlSchemaDatatypeLibrary = "http://www.w3.org/2001/XMLSchema-datatypes";
            string schema =
                "<grammar xmlns=\"http://relaxng.org/ns/structure/1.0\" datatypeLibrary=\"" + xmlSchemaDatatypeLibrary + "\">" +
                "<define name=\"typed\">" +
                "<choice>" +
                "<data type=\"integer\"><param name=\"minInclusive\">1</param></data>" +
                "<value type=\"token\">approved</value>" +
                "</choice>" +
                "</define>" +
                "</grammar>";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(schema));
            SchemaMetadata metadata = new RelaxNgSchemaMetadataReader().Read(stream, "https://example.invalid/schema.rng");
            SchemaPatternMetadata typed = metadata.Patterns.Single(pattern => pattern.Name == "typed");
            SchemaPatternNodeMetadata choice = Assert.Single(typed.PatternTree);
            SchemaPatternNodeMetadata data = choice.Children[0];
            SchemaPatternNodeMetadata value = choice.Children[1];
            using var jsonWriter = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
            using var csharpWriter = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

            new SchemaMetadataJsonWriter().Write(metadata, jsonWriter);
            new SchemaMetadataCSharpWriter().Write(metadata, csharpWriter, "FixtureSchemaMetadata");

            Assert.Equal(xmlSchemaDatatypeLibrary, data.DataTypeLibrary);
            Assert.Equal(xmlSchemaDatatypeLibrary, value.DataTypeLibrary);
            Assert.Contains("\"dataTypeLibrary\": \"" + xmlSchemaDatatypeLibrary + "\"", jsonWriter.ToString());
            Assert.Contains("dataTypeLibrary: \"" + xmlSchemaDatatypeLibrary + "\"", csharpWriter.ToString());
        }

        [Fact]
        public void CSharpProviderWriterEmitsRuntimeProviderHook()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(CreateRelaxNgFixture()));
            SchemaMetadata metadata = new RelaxNgSchemaMetadataReader().Read(stream, "https://example.invalid/schema.rng");
            using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

            new SchemaMetadataCSharpWriter().WriteProvider(metadata, writer, "FixtureSchemaMetadata");

            string code = writer.ToString();
            Assert.Contains("internal static partial class OdfGeneratedSchemaProvider", code);
            Assert.Contains("static partial void TryCreateOdf14Core(OdfSchemaSet baseSchema, ref OdfSchemaSet? generated)", code);
            Assert.Contains("generated = FixtureSchemaMetadata.Create(baseSchema);", code);
            Assert.True(code.IndexOf("internal static class FixtureSchemaMetadata", System.StringComparison.Ordinal) <
                code.IndexOf("internal static partial class OdfGeneratedSchemaProvider", System.StringComparison.Ordinal));
        }

        [Fact]
        public void CliWritesProviderArtifactToOutputPath()
        {
            string directory = Path.Combine(Path.GetTempPath(), "OdfKitSchemaGeneratorCliTests", Path.GetRandomFileName());
            Directory.CreateDirectory(directory);
            try
            {
                string schemaPath = Path.Combine(directory, "schema.rng");
                string outputPath = Path.Combine(directory, "Generated", "Odf14Schema.g.cs");
                File.WriteAllText(schemaPath, CreateRelaxNgFixture(), Encoding.UTF8);
                using var stdout = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
                using var stderr = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

                int exitCode = OdfSchemaGeneratorCli.Run(
                    new[]
                    {
                        "--format",
                        "csharp-provider",
                        "--class-name",
                        "Odf14OfficialSchemaMetadata",
                        "--source-url",
                        "https://docs.oasis-open.org/office/OpenDocument/v1.4/os/schemas/OpenDocument-v1.4-schema.rng",
                        "--source-date",
                        "2025-10-06",
                        "--output",
                        outputPath,
                        schemaPath
                    },
                    stdout,
                    stderr);

                Assert.Equal(0, exitCode);
                Assert.Equal(string.Empty, stderr.ToString());
                Assert.Equal(string.Empty, stdout.ToString());
                string code = File.ReadAllText(outputPath, Encoding.UTF8);
                Assert.Contains("internal static class Odf14OfficialSchemaMetadata", code);
                Assert.Contains("generated = Odf14OfficialSchemaMetadata.Create(baseSchema);", code);
                Assert.Contains("new Uri(\"https://docs.oasis-open.org/office/OpenDocument/v1.4/os/schemas/OpenDocument-v1.4-schema.rng\"), \"2025-10-06\"", code);
                Assert.Contains("new OdfSchemaPatternDefinition(\"start\", new[]", code);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void CliRejectsInvalidClassName()
        {
            using var stdout = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
            using var stderr = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

            int exitCode = OdfSchemaGeneratorCli.Run(
                new[] { "--format", "csharp-provider", "--class-name", "not-a-class", "schema.rng" },
                stdout,
                stderr);

            Assert.Equal(2, exitCode);
            Assert.Contains("Class name must be a valid C# identifier.", stderr.ToString());
        }

        [Fact]
        public void CliRejectsInvalidSourceDate()
        {
            using var stdout = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
            using var stderr = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

            int exitCode = OdfSchemaGeneratorCli.Run(
                new[] { "--source-date", "2025/10/06", "schema.rng" },
                stdout,
                stderr);

            Assert.Equal(2, exitCode);
            Assert.Contains("Source date must use yyyy-MM-dd format.", stderr.ToString());
        }

        [Fact]
        public void OasisOdf14GenerationManifestDefinesProviderArtifact()
        {
            string repoRoot = FindRepositoryRoot();
            string manifestPath = Path.Combine(repoRoot, "tools", "OdfSchemaGenerator", "oasis-odf14-schema.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath, Encoding.UTF8));
            JsonElement root = document.RootElement;

            Assert.Equal("1.4", root.GetProperty("version").GetString());
            Assert.Equal("csharp-provider", root.GetProperty("format").GetString());
            Assert.Equal("Odf14OfficialSchemaMetadata", root.GetProperty("className").GetString());
            Assert.Equal("2025-10-06", root.GetProperty("sourceDate").GetString());
            Assert.Equal(
                "https://docs.oasis-open.org/office/OpenDocument/v1.4/os/schemas/OpenDocument-v1.4-schema.rng",
                root.GetProperty("sourceUrl").GetString());
            Assert.Equal(
                "tools/OdfSchemaGenerator/schemas/OpenDocument-v1.4-schema.rng",
                root.GetProperty("schemaPath").GetString());
            Assert.Equal(
                "OdfKit/Compliance/Generated/Odf14OfficialSchemaProvider.g.cs",
                root.GetProperty("outputPath").GetString());
        }

        [Fact]
        public void OasisOdf14GenerationScriptUsesManifestAndGeneratorProject()
        {
            string repoRoot = FindRepositoryRoot();
            string scriptPath = Path.Combine(repoRoot, "eng", "Generate-OdfSchemaProvider.ps1");
            string script = File.ReadAllText(scriptPath, Encoding.UTF8);

            Assert.Contains("tools/OdfSchemaGenerator/oasis-odf14-schema.json", script);
            Assert.Contains("tools/OdfSchemaGenerator/OdfSchemaGenerator.csproj", script);
            Assert.Contains("function Get-RequiredManifestValue", script);
            Assert.Contains("Schema generation manifest is missing required property", script);
            Assert.Contains("$arguments = @(", script);
            Assert.Contains("& dotnet @arguments", script);
            Assert.Contains("--source-url", script);
            Assert.Contains("--source-date", script);
            Assert.Contains("--class-name", script);
            Assert.Contains("--output", script);
        }

        [Fact]
        public void ReaderFollowsRelativeIncludesAndExternalRefsOnce()
        {
            string directory = Path.Combine(Path.GetTempPath(), "OdfKitSchemaGeneratorTests", Path.GetRandomFileName());
            Directory.CreateDirectory(directory);
            try
            {
                string rootPath = Path.Combine(directory, "root.rng");
                string childPath = Path.Combine(directory, "child.rng");
                string loopPath = Path.Combine(directory, "loop.rng");

                File.WriteAllText(
                    rootPath,
                    CreateGrammar(
                        "<include href=\"child.rng\" />" +
                        "<externalRef href=\"loop.rng\" />" +
                        "<externalRef href=\"https://example.invalid/remote.rng\" />" +
                        "<include href=\"missing.rng\" />" +
                        "<define name=\"root\"><element name=\"office:document-content\"><ref name=\"paragraph\" /></element></define>"),
                    Encoding.UTF8);
                File.WriteAllText(
                    childPath,
                    CreateGrammar("<define name=\"paragraph\"><element name=\"text:p\"><attribute name=\"text:style-name\" /></element></define>"),
                    Encoding.UTF8);
                File.WriteAllText(
                    loopPath,
                    CreateGrammar("<include href=\"root.rng\" /><define name=\"sheet\"><element name=\"table:table\" /></define>"),
                    Encoding.UTF8);

                SchemaMetadata metadata = new RelaxNgSchemaMetadataReader().ReadFile(rootPath);

                Assert.Equal(Path.GetFullPath(rootPath), metadata.Source);
                Assert.Equal(
                    new[]
                    {
                        "urn:oasis:names:tc:opendocument:xmlns:office:1.0|document-content",
                        "urn:oasis:names:tc:opendocument:xmlns:table:1.0|table",
                        "urn:oasis:names:tc:opendocument:xmlns:text:1.0|p"
                    },
                    metadata.Elements.Select(item => item.NamespaceUri + "|" + item.LocalName).ToArray());
                Assert.Single(metadata.Attributes);
                Assert.Contains(metadata.Patterns, pattern => pattern.Name == "root" && pattern.References.Contains("paragraph"));
                Assert.Contains(metadata.Patterns, pattern => pattern.Name == "root" && pattern.ChildElements.Any(child => child.LocalName == "document-content"));
                Assert.Contains(metadata.Patterns, pattern => pattern.Name == "sheet");
                Assert.Contains(metadata.MissingReferences, path => path.EndsWith("missing.rng", System.StringComparison.Ordinal));
                Assert.Contains("https://example.invalid/remote.rng", metadata.ExternalReferences);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void ReaderRejectsReferencesOutsideSchemaRoot()
        {
            string directory = Path.Combine(Path.GetTempPath(), "OdfKitSchemaGeneratorTests", Path.GetRandomFileName());
            string schemaDirectory = Path.Combine(directory, "schemas");
            Directory.CreateDirectory(schemaDirectory);
            try
            {
                string rootPath = Path.Combine(schemaDirectory, "root.rng");
                string outsidePath = Path.Combine(directory, "outside.rng");

                File.WriteAllText(
                    rootPath,
                    CreateGrammar(
                        "<include href=\"../outside.rng\" />" +
                        "<define name=\"root\"><element name=\"office:document-content\" /></define>"),
                    Encoding.UTF8);
                File.WriteAllText(
                    outsidePath,
                    CreateGrammar("<define name=\"escaped\"><element name=\"text:p\" /></define>"),
                    Encoding.UTF8);

                SchemaMetadata metadata = new RelaxNgSchemaMetadataReader().ReadFile(rootPath);
                using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

                new SchemaMetadataJsonWriter().Write(metadata, writer);

                Assert.DoesNotContain(metadata.Elements, item => item.LocalName == "p");
                Assert.DoesNotContain(metadata.Patterns, pattern => pattern.Name == "escaped");
                string rejected = Assert.Single(metadata.RejectedReferences);
                Assert.Equal(Path.GetFullPath(outsidePath), rejected);
                Assert.Contains("\"rejectedReferences\": [", writer.ToString());
                Assert.Contains(JsonEncodedPath(rejected), writer.ToString());
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static string CreateRelaxNgFixture()
        {
            return CreateGrammar(
                "<start><ref name=\"root\" /></start>" +
                "<define name=\"root\"><element name=\"office:document-content\"><optional><attribute name=\"office:version\" /></optional><zeroOrMore><element name=\"text:p\" /></zeroOrMore></element></define>" +
                "<define name=\"root\"><group><choice><ref name=\"sheet\" /><ref name=\"paragraph\" /><ref name=\"paragraph\" /></choice></group></define>" +
                "<define name=\"sheet\"><element name=\"office:spreadsheet\"><ref name=\"table\" /></element></define>" +
                "<define name=\"paragraph\"><element name=\"text:p\"><attribute name=\"text:style-name\" /></element></define>" +
                "<define name=\"paragraph\"><ref name=\"style-attributes\" /></define>" +
                "<define name=\"duplicate\"><interleave><element name=\"text:p\" /><anyName><except><nsName ns=\"urn:oasis:names:tc:opendocument:xmlns:draw:1.0\" /><name>text:span</name></except></anyName></interleave></define>" +
                "<define name=\"bounded-data\"><data type=\"integer\"><param name=\"minInclusive\">1</param><param name=\"maxInclusive\">10</param></data></define>" +
                "<define name=\"wildcard-element\"><element><anyName><except><nsName ns=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" /></except></anyName><empty /></element></define>" +
                "<define name=\"table\"><element name=\"table:table\" /></define>");
        }

        private static string CreateGrammar(string body)
        {
            return "<grammar xmlns=\"http://relaxng.org/ns/structure/1.0\"" +
                " xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"" +
                " xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"" +
                " xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\"" +
                " xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\"" +
                " xmlns:xlink=\"http://www.w3.org/1999/xlink\"" +
                " xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\"" +
                " xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\">" +
                body +
                "</grammar>";
        }

        private static string FindRepositoryRoot()
        {
            string? directory = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(directory))
            {
                if (File.Exists(Path.Combine(directory, "implementation_plan.md")))
                {
                    return directory;
                }

                directory = Directory.GetParent(directory)?.FullName;
            }

            throw new DirectoryNotFoundException("Repository root could not be located.");
        }

        private static string JsonEncodedPath(string path)
        {
            return path.Replace("\\", "\\\\");
        }
    }
}
