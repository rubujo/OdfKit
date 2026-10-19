using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests geometric datatype lexical forms beyond permissive schema primitives.
/// 測試幾何資料型別在寬鬆 schema 基礎型別之外的詞法限制。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfGeometricLexicalConformanceTests
{
    /// <summary>
    /// Consecutive spaces separate points without creating empty coordinate pairs.
    /// 連續空格分隔座標點，不產生空的座標對。
    /// </summary>
    [Fact]
    public void PointListsAcceptConsecutiveSpaces()
    {
        foreach (string value in new[] { "0,0  1,1", "-1,2   3,-4  5,6" })
        {
            Assert.True(OdfPointList.TryParse(value, out OdfPointList points));
            Assert.Equal(value, points.Value);
            Assert.Equal(value, new OdfPointList(value).Value);
        }
    }

    /// <summary>
    /// Angles require a numeric value and an optional adjacent angle unit.
    /// 角度需要數值，並可緊接角度單位。
    /// </summary>
    [Fact]
    public void AngleModelsCheckNumericLexicalForms()
    {
        foreach (string value in new[] { "0", "-90", "+45.5", ".5", "1e2", "90deg", "-1.5grad", "3.14rad" })
        {
            Assert.True(OdfAngle.TryParse(value, out OdfAngle angle));
            Assert.Equal(value, angle.Value);
        }
        foreach (string value in new[] { "banana", "NaN", "INF", "1 deg", "1deggrad", "1,2", ".", "1e", "90DEG", "1.", "1.deg", "\u00A090" })
        {
            Assert.False(OdfAngle.TryParse(value, out _));
            Assert.Throws<ArgumentException>(() => new OdfAngle(value));
        }
    }

    /// <summary>
    /// Unitless angles default to degrees and other supported units convert without changing source values.
    /// 無單位角度預設為度，其他支援單位轉換後仍保留來源值。
    /// </summary>
    [Fact]
    public void AngleUnitsConvertToDegreesWithoutChangingLexicalValues()
    {
        foreach ((string value, decimal expected) in new[] { ("90", 90m), ("1e2", 100m), ("90deg", 90m), ("100grad", 90m), ("-100grad", -90m), ("3.1415926535897932384626433833rad", 180m) })
        {
            OdfAngle angle = new(value);
            Assert.True(angle.TryGetDegrees(out decimal degrees));
            Assert.Equal(expected, degrees);
            Assert.Equal(value, angle.Value);
        }
        string large = new string('9', 120) + "rad";
        OdfAngle unbounded = new(large);
        Assert.False(unbounded.TryGetDegrees(out _));
        Assert.Equal(large, unbounded.Value);
        OdfAngle overflowing = new("79228162514264337593543950335rad");
        Assert.False(overflowing.TryGetDegrees(out decimal overflowResult));
        Assert.Equal(0m, overflowResult);
        Assert.False(default(OdfAngle).TryGetDegrees(out _));
        Assert.Equal("90deg", new OdfAngle(" \t90deg\r\n").Value);
    }

    /// <summary>
    /// Native gradient angles must satisfy their prose datatype definition.
    /// 原生漸層角度必須符合正文資料型別定義。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void NativeAnglesRejectNonNumericValues(OdfVersion version)
    {
        XElement xml = XElement.Parse($"<office:document xmlns:office='{OdfNamespaces.Office}' xmlns:draw='{OdfNamespaces.Draw}' xmlns:text='{OdfNamespaces.Text}' office:version='{OdfVersionInfo.ToVersionString(version)}' office:mimetype='application/vnd.oasis.opendocument.text'><office:styles><draw:gradient draw:name='G' draw:style='linear' draw:start-color='#000000' draw:end-color='#ffffff' draw:angle='90deg'/></office:styles><office:body><office:text><text:p/></office:text></office:body></office:document>");
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            Assert.True(Validate(xml, profile).IsValid);
            foreach (string invalid in new[] { "banana", "NaN", "1 deg" })
            {
                XElement bad = new(xml);
                bad.Descendants(XName.Get("gradient", OdfNamespaces.Draw)).Single().SetAttributeValue(XName.Get("angle", OdfNamespaces.Draw), invalid);
                Assert.False(Validate(bad, profile).IsValid);
            }
        }
    }

    /// <summary>
    /// Every official angle attribute, including angle lists, receives semantic validation.
    /// 每個官方角度屬性及角度清單都接受語意驗證。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void AllNativeAngleAttributesValidateTheirAngleTokens(OdfVersion version)
    {
        XName[] names = OdfSchemaRegistry.GetSchema(version).Patterns.Values
            .SelectMany(pattern => Flatten(pattern.Roots))
            .Where(node => node.Kind == OdfSchemaPatternNodeKind.Attribute &&
                Flatten(node.Children).Any(child => child.Kind == OdfSchemaPatternNodeKind.Ref && child.ReferenceName == "angle"))
            .Select(node => XName.Get(node.LocalName, node.NamespaceUri)).Distinct().ToArray();
        Assert.Equal(13, names.Length);
        foreach (XName name in names)
        {
            (XElement xml, XElement carrier) = AngleDocument(version, name);
            string valid = name.LocalName == "extrusion-rotation-angle" ? "0deg 0rad" :
                name.LocalName == "extrusion-skew" ? "1 0grad" : "0deg";
            carrier.SetAttributeValue(name, valid);
            foreach (OdfComplianceProfile profile in Profiles(version))
            {
                OdfValidationReport report = Validate(xml, profile);
                Assert.True(report.IsValid, name + ": " + report.ToJson());
                string[] invalidValues = name.LocalName == "extrusion-rotation-angle" ? ["banana 0rad", "0deg banana"] :
                    name.LocalName == "extrusion-skew" ? ["1 banana"] : ["banana", "NaN", "1 deg"];
                foreach (string invalid in invalidValues)
                {
                    XElement bad = new(xml);
                    bad.Descendants(carrier.Name).Single().SetAttributeValue(name, invalid);
                    OdfValidationReport rejected = Validate(bad, profile);
                    Assert.DoesNotContain(rejected.Issues, issue => issue.RuleId == "ODF3101");
                    Assert.Contains(rejected.Issues, issue => issue.RuleId == "ODF3136");
                }
            }
            carrier.SetAttributeValue(XName.Get(name.LocalName, "urn:test:foreign"), "banana");
            Assert.True(Validate(xml, Profiles(version)[1]).IsValid);
            OdfNode dom = OdfXmlReader.Parse(Encoding.UTF8.GetBytes(xml.ToString()));
            using MemoryStream output = new();
            OdfXmlWriter.Write(dom, output);
            XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()));
            Assert.Equal(valid, (string?)saved.Descendants(carrier.Name).Single().Attribute(name));
            Assert.True(Validate(saved, Profiles(version)[1]).IsValid);
        }
    }

    private static IEnumerable<OdfSchemaPatternNode> Flatten(IEnumerable<OdfSchemaPatternNode> nodes)
    {
        foreach (OdfSchemaPatternNode node in nodes)
        {
            yield return node;
            foreach (OdfSchemaPatternNode child in Flatten(node.Children))
                yield return child;
        }
    }

    private static (XElement Document, XElement Carrier) AngleDocument(OdfVersion version, XName name)
    {
        string styles = "";
        string body = "<text:p/>";
        XName target;
        if (name.NamespaceName == OdfNamespaces.Draw && name.LocalName is "start-angle" or "end-angle")
        {
            body = "<text:p><draw:ellipse svg:width='1cm' svg:height='1cm' draw:kind='arc' draw:start-angle='0' draw:end-angle='90'/></text:p>";
            target = XName.Get("ellipse", OdfNamespaces.Draw);
        }
        else if (name.NamespaceName == OdfNamespaces.Draw && name.LocalName == "angle")
        {
            styles = "<draw:gradient draw:name='G' draw:style='linear'/>";
            target = XName.Get("gradient", OdfNamespaces.Draw);
        }
        else if (name.NamespaceName == OdfNamespaces.Draw && name.LocalName == "rotation")
        {
            styles = "<draw:hatch draw:name='H' draw:style='single'/>";
            target = XName.Get("hatch", OdfNamespaces.Draw);
        }
        else if (name.NamespaceName == OdfNamespaces.Dr3d && name.LocalName == "shadow-slant")
        {
            body = "<text:p><dr3d:scene svg:width='1cm' svg:height='1cm'/></text:p>";
            target = XName.Get("scene", OdfNamespaces.Dr3d);
        }
        else if (name.NamespaceName == OdfNamespaces.Draw && name.LocalName is "text-rotate-angle" or "extrusion-rotation-angle" or "extrusion-skew")
        {
            body = "<text:p><draw:custom-shape svg:width='1cm' svg:height='1cm'><draw:enhanced-geometry/></draw:custom-shape></text:p>";
            target = XName.Get("enhanced-geometry", OdfNamespaces.Draw);
        }
        else
        {
            string family = name.NamespaceName == OdfNamespaces.Chart ? "chart" :
                name.NamespaceName == OdfNamespaces.Style && name.LocalName == "rotation-angle" ? "table-cell" :
                name.NamespaceName == OdfNamespaces.Style && name.LocalName == "text-rotation-angle" ? "text" : "graphic";
            target = XName.Get(family + "-properties", OdfNamespaces.Style);
            styles = $"<style:style style:name='S' style:family='{family}'><style:{target.LocalName}/></style:style>";
        }
        XElement xml = XElement.Parse($"<office:document xmlns:office='{OdfNamespaces.Office}' xmlns:draw='{OdfNamespaces.Draw}' xmlns:text='{OdfNamespaces.Text}' xmlns:style='{OdfNamespaces.Style}' xmlns:svg='{OdfNamespaces.Svg}' xmlns:dr3d='{OdfNamespaces.Dr3d}' xmlns:chart='{OdfNamespaces.Chart}' office:version='{OdfVersionInfo.ToVersionString(version)}' office:mimetype='application/vnd.oasis.opendocument.text'><office:styles>{styles}</office:styles><office:body><office:text>{body}</office:text></office:body></office:document>");
        return (xml, xml.Descendants(target).Single());
    }

    private static OdfValidationReport Validate(XElement xml, OdfComplianceProfile profile)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(xml.ToString()));
        return OdfValidator.Validate(stream, "angles.fodt", profile);
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
