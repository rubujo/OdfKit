using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests XML Schema date and dateTime lexical spaces independently of CLR date ranges.
/// 測試 XML Schema 日期及日期時間的詞法空間，不受 CLR 日期範圍限制。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfSchemaDateConformanceTests
{
    /// <summary>
    /// Dates require a complete calendar date and preserve XML Schema year and timezone rules.
    /// 日期要求完整曆日，並保留 XML Schema 的年份及時區規則。
    /// </summary>
    [Theory]
    [InlineData("2026-01-01", true)]
    [InlineData("2026-01-01Z", true)]
    [InlineData("0001-01-01+14:00", true)]
    [InlineData("9999-12-31-14:00", true)]
    [InlineData("10000-02-29", true)]
    [InlineData("-0001-02-29", true)]
    [InlineData("-0005-02-29", true)]
    [InlineData("2000-02-29", true)]
    [InlineData("\t2026-01-01 \r\n", true)]
    [InlineData("12:00:00", false)]
    [InlineData("2026", false)]
    [InlineData("2026-01", false)]
    [InlineData("--01-01", false)]
    [InlineData("2026-01-01T00:00:00", false)]
    [InlineData("0000-01-01", false)]
    [InlineData("-0000-01-01", false)]
    [InlineData("01000-01-01", false)]
    [InlineData("+2026-01-01", false)]
    [InlineData("1900-02-29", false)]
    [InlineData("10001-02-29", false)]
    [InlineData("-0004-02-29", false)]
    [InlineData("2026-04-31", false)]
    [InlineData("2026-00-01", false)]
    [InlineData("2026-01-00", false)]
    [InlineData("2026-01-01+14:01", false)]
    [InlineData("2026-01-01+15:00", false)]
    [InlineData("2026-01-01.5", false)]
    [InlineData("２０２６-01-01", false)]
    [InlineData("2026-01-01\u00a0", false)]
    public void DateLexicalValuesRespectTheSpecification(string value, bool expected)
    {
        foreach (string type in new[] { "date", "xsd:date" })
            Check(type, value, expected);
    }

    /// <summary>
    /// Date-time values require a calendar date and a valid time, including midnight rollover.
    /// 日期時間要求曆日及合法時間，包含午夜換日表示。
    /// </summary>
    [Theory]
    [InlineData("2026-01-01T00:00:00Z", true)]
    [InlineData("10000-02-29T24:00:00.000+14:00", true)]
    [InlineData("-0001-02-29T12:00:00Z", true)]
    [InlineData("9999-12-31T24:00:00Z", true)]
    [InlineData("0001-01-01T00:00:00+14:00", true)]
    [InlineData("2026-01-01T12:00:00.12345678901234567890", true)]
    [InlineData("2026-01-01", false)]
    [InlineData("12:00:00", false)]
    [InlineData("2026-01-01 12:00:00", false)]
    [InlineData("2026-01-01t12:00:00", false)]
    [InlineData("0000-01-01T00:00:00", false)]
    [InlineData("2026-02-29T00:00:00", false)]
    [InlineData("2026-01-01T24:00:00.001", false)]
    [InlineData("2026-01-01T00:00:00+14:01", false)]
    [InlineData("2026-01-01T00:00:60", false)]
    [InlineData("2026-01-01T１２:00:00", false)]
    public void DateTimeLexicalValuesRespectTheSpecification(string value, bool expected)
    {
        foreach (string type in new[] { "dateTime", "xsd:dateTime" })
            Check(type, value, expected);
    }

    /// <summary>
    /// Year parsing does not impose a machine integer bound or truncate the original value.
    /// 年份解析不套用機器整數上限，也不截斷原值。
    /// </summary>
    [Fact]
    public void LargeYearsKeepTheirOriginalLexicalValues()
    {
        string year = new string('9', 120);
        Check("date", year + "-12-31", true);
        Check("date", "-" + year + "-12-31", true);
        Check("dateTime", year + "-12-31T24:00:00Z", true);
    }

    /// <summary>
    /// Native spreadsheet date values reject partial dates and accept legal extended years.
    /// 原生試算表日期值拒絕不完整日期，並接受合法的延伸年份。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void NativeDateValuesUseTheXmlSchemaLexicalSpace(OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            foreach (string value in new[] { "2026-01-01", "10000-01-01T00:00:00Z", "-0001-01-01T00:00:00Z" })
                Assert.True(ValidateNative(version, profile, value).IsValid);
            foreach (string value in new[] { "12:00:00", "2026", "2026-01", "P1D", "PT1H" })
                Assert.Contains(ValidateNative(version, profile, value).Issues, issue => issue.RuleId == "ODF3101");
        }
    }

    private static void Check(string type, string value, bool expected)
    {
        OdfSchemaPatternNode data = new(OdfSchemaPatternNodeKind.Data, "exactlyOne", "", "", "", type, "", null, null, null, "http://www.w3.org/2001/XMLSchema-datatypes");
        OdfSchemaSet schema = new(OdfVersion.Odf14, new Uri("https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/"), "2004-10-28", [], attributes: null, nameClasses: null, patterns: [new OdfSchemaPatternDefinition("start", [new OdfSchemaPatternNode(OdfSchemaPatternNodeKind.Element, "exactlyOne", "", "root", "", "", "", null, [data])])]);
        XElement xml = new("root", value);
        Assert.Equal(expected, OdfSchemaPatternValidator.ValidateElement(xml, schema, "start").IsMatch);
        Assert.Equal(value, xml.Value);
    }
    private static OdfValidationReport ValidateNative(OdfVersion version, OdfComplianceProfile profile, string value)
    {
        XElement xml = XElement.Parse($"<office:document xmlns:office='{OdfNamespaces.Office}' xmlns:table='{OdfNamespaces.Table}' xmlns:text='{OdfNamespaces.Text}' office:version='{OdfVersionInfo.ToVersionString(version)}' office:mimetype='application/vnd.oasis.opendocument.spreadsheet'><office:body><office:spreadsheet><table:table table:name='Sheet'><table:table-column/><table:table-row><table:table-cell office:value-type='date'><text:p>Date</text:p></table:table-cell></table:table-row></table:table></office:spreadsheet></office:body></office:document>");
        xml.Descendants(XName.Get("table-cell", OdfNamespaces.Table)).Single().SetAttributeValue(XName.Get("date-value", OdfNamespaces.Office), value);
        OdfNode dom = OdfXmlReader.Parse(Encoding.UTF8.GetBytes(xml.ToString()));
        using var output = new MemoryStream();
        OdfXmlWriter.Write(dom, output);
        XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()));
        Assert.Equal(value, (string?)saved.Descendants(XName.Get("table-cell", OdfNamespaces.Table)).Single().Attribute(XName.Get("date-value", OdfNamespaces.Office)));
        using var input = new MemoryStream(output.ToArray());
        return OdfValidator.Validate(input, "dates.fods", profile);
    }
    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
