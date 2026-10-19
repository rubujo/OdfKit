using System.Collections.Concurrent;
using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests the named datatype definitions in each official modern schema.
/// 測試各版現代官方 schema 的具名資料型別定義。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfNamedDatatypeConformanceTests
{
    private static readonly ConcurrentDictionary<(OdfVersion Version, string Type), OdfSchemaSet> Probes = new();
    private static readonly (string Type, string Value, bool Valid)[] Samples =
    [
        ("character", "A", true), ("character", "中", true), ("character", "𠀀", true),
        ("character", "😀", true), ("character", " ", true), ("character", "\t", true),
        ("character", "", false), ("character", "ab", false), ("character", "e\u0301", false),
        ("boolean", "true", true), ("boolean", "false", true), ("boolean", " true ", true),
        ("boolean", "0", false), ("boolean", "1", false), ("boolean", "True", false),
        ("percent", "0%", true), ("percent", "-0%", true), ("percent", "-1%", true),
        ("percent", "-.5%", true), ("percent", "1.%", true), ("percent", "0001%", true),
        ("percent", "12345.678%", true), ("percent", "+1%", false), ("percent", "1e2%", false),
        ("percent", "1 %", false), ("percent", "NaN%", false), ("percent", "1", false),
        ("zeroToHundredPercent", "0%", true), ("zeroToHundredPercent", "00%", true),
        ("zeroToHundredPercent", "01%", true), ("zeroToHundredPercent", ".5%", true),
        ("zeroToHundredPercent", "99.9%", true), ("zeroToHundredPercent", "100.00%", true),
        ("zeroToHundredPercent", "101%", false), ("zeroToHundredPercent", "100.01%", false),
        ("zeroToHundredPercent", "-0%", false), ("zeroToHundredPercent", "001%", false),
        ("zeroToHundredPercent", " 10%", false), ("zeroToHundredPercent", "10% ", false),
        ("signedZeroToHundredPercent", "-100.0%", true), ("signedZeroToHundredPercent", "-.5%", true),
        ("signedZeroToHundredPercent", "-0%", true), ("signedZeroToHundredPercent", "100%", true),
        ("signedZeroToHundredPercent", "-100.1%", false), ("signedZeroToHundredPercent", "101%", false),
        ("signedZeroToHundredPercent", "+1%", false), ("signedZeroToHundredPercent", "-001%", false),
        ("nonNegativePercent", "0%", true), ("nonNegativePercent", ".5%", true),
        ("nonNegativePercent", "500%", true), ("nonNegativePercent", "0001%", true),
        ("nonNegativePercent", "-1%", false), ("nonNegativePercent", "-0%", false),
        ("nonNegativePercent", "+1%", false), ("nonNegativePercent", "INF%", false),
        ("timeOrDateTime", "12:00:00", true), ("timeOrDateTime", "24:00:00Z", true),
        ("timeOrDateTime", "2026-01-01T12:00:00", true),
        ("timeOrDateTime", "10000-01-01T00:00:00Z", true),
        ("timeOrDateTime", "-0001-01-01T00:00:00Z", true),
        ("timeOrDateTime", "2026-01-01", false), ("timeOrDateTime", "P1D", false),
        ("timeOrDateTime", "12:00", false), ("timeOrDateTime", "24:00:00.1", false),
        ("styleName", "P", true), ("styleName", "名稱", true), ("styleName", "_name", true),
        ("styleName", "name-with.dot", true), ("styleName", " Name ", true),
        ("styleName", "", false), ("styleName", "1name", false),
        ("styleName", "a:b", false), ("styleName", "a b", false)
    ];

    /// <summary>
    /// Provides version-specific positive and negative datatype cases.
    /// 提供各版本資料型別的正反例。
    /// </summary>
    public static IEnumerable<object[]> Cases
    {
        get
        {
            foreach (OdfVersion version in new[] { OdfVersion.Odf12, OdfVersion.Odf13, OdfVersion.Odf14 })
                foreach (var sample in Samples)
                    if (sample.Type != "nonNegativePercent" || version == OdfVersion.Odf14)
                        yield return [version, sample.Type, sample.Value, sample.Valid];
        }
    }

    /// <summary>
    /// Named types use their actual schema patterns and preserve the original text.
    /// 具名型別使用其實際 schema 模式，並保留原始文字。
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void NamedTypesRetainTheirDeclaredLexicalSpace(OdfVersion version, string type, string value, bool expected)
    {
        OdfSchemaSet schema = Probes.GetOrAdd((version, type), key =>
        {
            OdfSchemaSet seed = OdfSchemaRegistry.GetSchema(key.Version);
            Assert.NotNull(seed.FindPattern(key.Type));
            OdfSchemaPatternNode reference = new(OdfSchemaPatternNodeKind.Ref, "exactlyOne", "", "", key.Type, "", "");
            OdfSchemaPatternDefinition probe = new("odfkit-type-probe", [new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "exactlyOne", "", "root", "", "", "", null, [reference])]);
            return new OdfSchemaSet(key.Version, seed.SourceUrl, seed.SourceDate, [], attributes: null, nameClasses: null, patterns: seed.Patterns.Values.Append(probe));
        });
        XElement xml = new("root", value);
        Assert.Equal(expected, OdfSchemaPatternValidator.ValidateElement(xml, schema, "odfkit-type-probe").IsMatch);
        Assert.Equal(value, xml.Value);
        XElement carrier = new("root", new XAttribute("value", value));
        OdfNode dom = OdfXmlReader.Parse(Encoding.UTF8.GetBytes(carrier.ToString()));
        using MemoryStream output = new();
        OdfXmlWriter.Write(dom, output);
        XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()));
        string savedValue = saved.Attribute("value")!.Value;
        Assert.Equal(value, savedValue);
        Assert.Equal(expected, OdfSchemaPatternValidator.ValidateElement(new XElement("root", savedValue), schema, "odfkit-type-probe").IsMatch);
    }

    /// <summary>
    /// A supplementary Unicode bullet remains one character through native ODF round trips.
    /// 補充平面 Unicode 清單符號經原生 ODF 往返後仍為單一字元。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void NativeBulletsPreserveSupplementaryCharacters(OdfVersion version)
    {
        foreach (string glyph in new[] { "•", "𠀀", "😀", "ab", "" })
        {
            XElement xml = XElement.Parse($"<office:document xmlns:office='{OdfNamespaces.Office}' xmlns:text='{OdfNamespaces.Text}' xmlns:style='{OdfNamespaces.Style}' office:version='{OdfVersionInfo.ToVersionString(version)}' office:mimetype='application/vnd.oasis.opendocument.text'><office:styles><text:list-style style:name='Bullet'><text:list-level-style-bullet text:level='1'/></text:list-style></office:styles><office:body><office:text><text:p/></office:text></office:body></office:document>");
            xml.Descendants(XName.Get("list-level-style-bullet", OdfNamespaces.Text)).Single().SetAttributeValue(XName.Get("bullet-char", OdfNamespaces.Text), glyph);
            OdfNode dom = OdfXmlReader.Parse(Encoding.UTF8.GetBytes(xml.ToString()));
            using var output = new MemoryStream();
            OdfXmlWriter.Write(dom, output);
            XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()));
            Assert.Equal(glyph, (string?)saved.Descendants(XName.Get("list-level-style-bullet", OdfNamespaces.Text)).Single().Attribute(XName.Get("bullet-char", OdfNamespaces.Text)));
            foreach (OdfComplianceProfile profile in Profiles(version))
            {
                using var input = new MemoryStream(output.ToArray());
                OdfValidationReport report = OdfValidator.Validate(input, "bullet.fodt", profile);
                Assert.Equal(glyph.Length > 0 && glyph != "ab", report.IsValid);
            }
        }
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
