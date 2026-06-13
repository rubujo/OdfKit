using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 typed DOM 與 ODFDOM 對標線的基本覆蓋能力。
/// </summary>
public class TypedDomParityTests
{
    /// <summary>
    /// 驗證 factory 會建立 generated 與手寫 typed wrapper，未知元素則回退為通用元素。
    /// </summary>
    [Fact]
    public void NodeFactoryCreatesGeneratedHandWrittenAndFallbackElements()
    {
        OdfNode generated = OdfNodeFactory.CreateElement(
            "animate",
            "urn:oasis:names:tc:opendocument:xmlns:animation:1.0",
            "anim");
        OdfNode handWritten = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        OdfNode fallback = OdfNodeFactory.CreateElement("custom-node", "urn:example:custom", "x");

        Assert.IsType<AnimationAnimateElement>(generated);
        Assert.IsType<TextPElement>(handWritten);
        Assert.IsType<OdfElement>(fallback);
        Assert.Equal("animate", generated.LocalName);
        Assert.Equal("p", handWritten.LocalName);
        Assert.Equal("custom-node", fallback.LocalName);
    }

    /// <summary>
    /// 驗證 generated DOM wrapper 的 class、factory case 與屬性數量沒有意外退化。
    /// </summary>
    [Fact]
    public void GeneratedDomWrapperCoverageDoesNotRegressBelowParityFloor()
    {
        string repoRoot = FindRepositoryRoot();
        string generatedPath = Path.Combine(repoRoot, "OdfKit", "DOM", "Generated", "GeneratedDomWrappers.g.cs");
        string generated = File.ReadAllText(generatedPath);

        int classCount = Regex.Matches(generated, @"public partial class \w+Element").Count;
        int factoryCaseCount = Regex.Matches(generated, "case \".*\": return new .*Element\\(prefix\\);").Count;
        int stringPropertyCount = Regex.Matches(generated, @"public string\? \w+").Count;
        int intPropertyCount = Regex.Matches(generated, @"public int\? \w+").Count;
        int boolPropertyCount = Regex.Matches(generated, @"public bool\? \w+").Count;
        int decimalPropertyCount = Regex.Matches(generated, @"public decimal\? \w+").Count;
        int dateTimePropertyCount = Regex.Matches(generated, @"public DateTime\? \w+").Count;
        int timePropertyCount = Regex.Matches(generated, @"public OdfTime\? \w+").Count;
        int lengthPropertyCount = Regex.Matches(generated, @"public OdfLength\? \w+").Count;
        int borderWidthsPropertyCount = Regex.Matches(generated, @"public OdfBorderWidths\? \w+").Count;
        int durationPropertyCount = Regex.Matches(generated, @"public OdfDuration\? \w+").Count;
        int anglePropertyCount = Regex.Matches(generated, @"public OdfAngle\? \w+").Count;
        int styleNamePropertyCount = Regex.Matches(generated, @"public OdfStyleName\? \w+").Count;
        int styleNameListPropertyCount = Regex.Matches(generated, @"public OdfStyleNameList\? \w+").Count;
        int colorPropertyCount = Regex.Matches(generated, @"public OdfColor\? \w+").Count;
        int iriReferencePropertyCount = Regex.Matches(generated, @"public OdfIriReference\? \w+").Count;
        int percentPropertyCount = Regex.Matches(generated, @"public OdfPercent\? \w+").Count;
        int cellAddressPropertyCount = Regex.Matches(generated, @"public OdfCellAddressReference\? \w+").Count;
        int cellRangeAddressPropertyCount = Regex.Matches(generated, @"public OdfCellRangeAddress\? \w+").Count;
        int cellRangeAddressListPropertyCount = Regex.Matches(generated, @"public OdfCellRangeAddressList\? \w+").Count;
        int vector3DPropertyCount = Regex.Matches(generated, @"public OdfVector3D\? \w+").Count;
        int point3DPropertyCount = Regex.Matches(generated, @"public OdfPoint3D\? \w+").Count;
        int pointListPropertyCount = Regex.Matches(generated, @"public OdfPointList\? \w+").Count;
        int languageCodePropertyCount = Regex.Matches(generated, @"public OdfLanguageCode\? \w+").Count;
        int countryCodePropertyCount = Regex.Matches(generated, @"public OdfCountryCode\? \w+").Count;
        int scriptCodePropertyCount = Regex.Matches(generated, @"public OdfScriptCode\? \w+").Count;
        int languageTagPropertyCount = Regex.Matches(generated, @"public OdfLanguageTag\? \w+").Count;
        int namespacedTokenPropertyCount = Regex.Matches(generated, @"public OdfNamespacedToken\? \w+").Count;
        int characterPropertyCount = Regex.Matches(generated, @"public OdfCharacter\? \w+").Count;
        int textEncodingPropertyCount = Regex.Matches(generated, @"public OdfTextEncoding\? \w+").Count;
        int targetFrameNamePropertyCount = Regex.Matches(generated, @"public OdfTargetFrameName\? \w+").Count;
        int xmlNamePropertyCount = Regex.Matches(generated, @"public OdfXmlName\? \w+").Count;
        int styleFamilyPropertyCount = Regex.Matches(generated, @"public OdfStyleFamily\? \w+").Count;
        int odfVersionPropertyCount = Regex.Matches(generated, @"public OdfVersion\? \w+").Count;
        int mediaTypePropertyCount = Regex.Matches(generated, @"public OdfMediaType\? \w+").Count;
        int propertyCount = stringPropertyCount + intPropertyCount + boolPropertyCount + decimalPropertyCount + dateTimePropertyCount + timePropertyCount + lengthPropertyCount + borderWidthsPropertyCount + durationPropertyCount + anglePropertyCount + styleNamePropertyCount + styleNameListPropertyCount + colorPropertyCount + iriReferencePropertyCount + percentPropertyCount + cellAddressPropertyCount + cellRangeAddressPropertyCount + cellRangeAddressListPropertyCount + vector3DPropertyCount + point3DPropertyCount + pointListPropertyCount + languageCodePropertyCount + countryCodePropertyCount + scriptCodePropertyCount + languageTagPropertyCount + namespacedTokenPropertyCount + characterPropertyCount + textEncodingPropertyCount + targetFrameNamePropertyCount + xmlNamePropertyCount + styleFamilyPropertyCount + odfVersionPropertyCount + mediaTypePropertyCount;

        Assert.True(classCount >= 550, "generated typed element class count regressed: " + classCount);
        Assert.True(factoryCaseCount >= 590, "generated factory case count regressed: " + factoryCaseCount);
        Assert.True(propertyCount >= 100000, "generated attribute property count regressed: " + propertyCount);
        Assert.True(intPropertyCount >= 1000, "generated integer attribute property count regressed: " + intPropertyCount);
        Assert.True(boolPropertyCount >= 10000, "generated boolean attribute property count regressed: " + boolPropertyCount);
        Assert.True(decimalPropertyCount >= 100, "generated decimal attribute property count regressed: " + decimalPropertyCount);
        Assert.True(dateTimePropertyCount >= 100, "generated date/time attribute property count regressed: " + dateTimePropertyCount);
        Assert.True(timePropertyCount >= 6, "generated time attribute property count regressed: " + timePropertyCount);
        Assert.True(lengthPropertyCount >= 10000, "generated length attribute property count regressed: " + lengthPropertyCount);
        Assert.True(borderWidthsPropertyCount >= 723, "generated border widths attribute property count regressed: " + borderWidthsPropertyCount);
        Assert.True(durationPropertyCount >= 1000, "generated duration attribute property count regressed: " + durationPropertyCount);
        Assert.True(anglePropertyCount >= 1000, "generated angle attribute property count regressed: " + anglePropertyCount);
        Assert.True(styleNamePropertyCount >= 1000, "generated style name attribute property count regressed: " + styleNamePropertyCount);
        Assert.True(styleNameListPropertyCount >= 300, "generated style name list attribute property count regressed: " + styleNameListPropertyCount);
        Assert.True(colorPropertyCount >= 1000, "generated color attribute property count regressed: " + colorPropertyCount);
        Assert.True(iriReferencePropertyCount >= 400, "generated IRI reference attribute property count regressed: " + iriReferencePropertyCount);
        Assert.True(percentPropertyCount >= 1000, "generated percent attribute property count regressed: " + percentPropertyCount);
        Assert.True(cellAddressPropertyCount >= 400, "generated cell address attribute property count regressed: " + cellAddressPropertyCount);
        Assert.True(cellRangeAddressPropertyCount >= 400, "generated cell range address attribute property count regressed: " + cellRangeAddressPropertyCount);
        Assert.True(cellRangeAddressListPropertyCount >= 800, "generated cell range address list attribute property count regressed: " + cellRangeAddressListPropertyCount);
        Assert.True(vector3DPropertyCount >= 1000, "generated vector3D attribute property count regressed: " + vector3DPropertyCount);
        Assert.True(point3DPropertyCount >= 90, "generated point3D attribute property count regressed: " + point3DPropertyCount);
        Assert.True(pointListPropertyCount >= 90, "generated point list attribute property count regressed: " + pointListPropertyCount);
        Assert.True(languageCodePropertyCount >= 100, "generated language code attribute property count regressed: " + languageCodePropertyCount);
        Assert.True(countryCodePropertyCount >= 100, "generated country code attribute property count regressed: " + countryCodePropertyCount);
        Assert.True(scriptCodePropertyCount >= 100, "generated script code attribute property count regressed: " + scriptCodePropertyCount);
        Assert.True(languageTagPropertyCount >= 100, "generated language tag attribute property count regressed: " + languageTagPropertyCount);
        Assert.True(namespacedTokenPropertyCount >= 100, "generated namespaced token attribute property count regressed: " + namespacedTokenPropertyCount);
        Assert.True(characterPropertyCount >= 100, "generated character attribute property count regressed: " + characterPropertyCount);
        Assert.True(textEncodingPropertyCount >= 438, "generated text encoding attribute property count regressed: " + textEncodingPropertyCount);
        Assert.True(targetFrameNamePropertyCount >= 205, "generated target frame name attribute property count regressed: " + targetFrameNamePropertyCount);
        Assert.True(xmlNamePropertyCount >= 1000, "generated XML name attribute property count regressed: " + xmlNamePropertyCount);
        Assert.True(styleFamilyPropertyCount >= 50, "generated style family attribute property count regressed: " + styleFamilyPropertyCount);
        Assert.True(odfVersionPropertyCount >= 50, "generated ODF version attribute property count regressed: " + odfVersionPropertyCount);
        Assert.True(mediaTypePropertyCount >= 100, "generated media type attribute property count regressed: " + mediaTypePropertyCount);
    }

    /// <summary>
    /// 驗證 typed DOM coverage report 可輸出 schema-to-wrapper 對照。
    /// </summary>
    [Fact]
    public void TypedDomCoverageReportDeclaresSchemaWrapperMappings()
    {
        OdfTypedDomCoverageReport report = OdfTypedDomCoverage.Build();

        Assert.True(report.SchemaElementCount >= 550, "schema element count too low: " + report.SchemaElementCount);
        Assert.True(report.TypedElementCount >= 550, "typed element count too low: " + report.TypedElementCount);
        Assert.True(report.SchemaAttributeCount >= 100, "schema attribute count too low: " + report.SchemaAttributeCount);
        Assert.Contains(
            report.Elements,
            element => element.NamespaceUri == OdfNamespaces.Text &&
                element.LocalName == "p" &&
                element.HasTypedWrapper &&
                element.WrapperType.Contains("TextPElement", StringComparison.Ordinal));
        Assert.Contains(report.AttributeValueTypeCounts, pair => pair.Key.Length > 0 && pair.Value > 0);
        Assert.True(report.WrapperPropertyTypeCounts["int"] >= 1000);
        Assert.True(report.WrapperPropertyTypeCounts["bool"] >= 10000);
        Assert.True(report.WrapperPropertyTypeCounts["decimal"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["dateTime"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["time"] >= 6);
        Assert.True(report.WrapperPropertyTypeCounts["length"] >= 10000);
        Assert.True(report.WrapperPropertyTypeCounts["borderWidths"] >= 723);
        Assert.True(report.WrapperPropertyTypeCounts["duration"] >= 1000);
        Assert.True(report.WrapperPropertyTypeCounts["angle"] >= 1000);
        Assert.True(report.WrapperPropertyTypeCounts["styleName"] >= 1000);
        Assert.True(report.WrapperPropertyTypeCounts["styleNameList"] >= 300);
        Assert.True(report.WrapperPropertyTypeCounts["color"] >= 1000);
        Assert.True(report.WrapperPropertyTypeCounts["iriReference"] >= 400);
        Assert.True(report.WrapperPropertyTypeCounts["percent"] >= 1000);
        Assert.True(report.WrapperPropertyTypeCounts["cellAddress"] >= 400);
        Assert.True(report.WrapperPropertyTypeCounts["cellRangeAddress"] >= 400);
        Assert.True(report.WrapperPropertyTypeCounts["cellRangeAddressList"] >= 800);
        Assert.True(report.WrapperPropertyTypeCounts["vector3D"] >= 1000);
        Assert.True(report.WrapperPropertyTypeCounts["point3D"] >= 90);
        Assert.True(report.WrapperPropertyTypeCounts["pointList"] >= 90);
        Assert.True(report.WrapperPropertyTypeCounts["languageCode"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["countryCode"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["scriptCode"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["languageTag"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["namespacedToken"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["character"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["textEncoding"] >= 438);
        Assert.True(report.WrapperPropertyTypeCounts["targetFrameName"] >= 205);
        Assert.True(report.WrapperPropertyTypeCounts["xmlName"] >= 1000);
        Assert.True(report.WrapperPropertyTypeCounts["styleFamily"] >= 50);
        Assert.True(report.WrapperPropertyTypeCounts["odfVersion"] >= 50);
        Assert.True(report.WrapperPropertyTypeCounts["mediaType"] >= 100);

        string json = JsonSerializer.Serialize(report.ToJsonModel());
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(report.SchemaElementCount, document.RootElement.GetProperty("summary").GetProperty("schemaElementCount").GetInt32());
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("int").GetInt32() >= 1000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("bool").GetInt32() >= 10000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("time").GetInt32() >= 6);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("length").GetInt32() >= 10000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("borderWidths").GetInt32() >= 723);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("duration").GetInt32() >= 1000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("angle").GetInt32() >= 1000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleName").GetInt32() >= 1000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleNameList").GetInt32() >= 300);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("color").GetInt32() >= 1000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("iriReference").GetInt32() >= 400);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("percent").GetInt32() >= 1000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("cellAddress").GetInt32() >= 400);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("cellRangeAddress").GetInt32() >= 400);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("cellRangeAddressList").GetInt32() >= 800);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("vector3D").GetInt32() >= 1000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("point3D").GetInt32() >= 90);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("pointList").GetInt32() >= 90);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("languageCode").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("countryCode").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("scriptCode").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("languageTag").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("namespacedToken").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("character").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textEncoding").GetInt32() >= 438);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("targetFrameName").GetInt32() >= 205);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("xmlName").GetInt32() >= 1000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleFamily").GetInt32() >= 50);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("odfVersion").GetInt32() >= 50);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("mediaType").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("elements").GetArrayLength() >= report.SchemaElementCount);
    }

    /// <summary>
    /// 驗證 typed DOM 屬性 helper 可用強型別讀寫常用 ODF datatype。
    /// </summary>
    [Fact]
    public void TypedDomAttributeHelpersReadAndWriteCommonDatatypes()
    {
        TableTableCellElement cell = new("table");
        DateTime utc = new(2026, 6, 13, 9, 30, 0, DateTimeKind.Utc);
        DateTime local = new(2026, 6, 13, 17, 30, 0, DateTimeKind.Unspecified);

        cell.NumberColumnsRepeated = 3;
        cell.SetDecimalAttributeValue("value", OdfNamespaces.Office, 12.50m, OdfNamespaces.GetPrefix(OdfNamespaces.Office));
        cell.SetBooleanAttributeValue("boolean-value", OdfNamespaces.Office, true, OdfNamespaces.GetPrefix(OdfNamespaces.Office));
        cell.SetDateTimeAttributeValue("date-value", OdfNamespaces.Office, utc, OdfNamespaces.GetPrefix(OdfNamespaces.Office));
        cell.SetTimeAttributeValue("time-value", OdfNamespaces.Office, new OdfTime(new TimeSpan(12, 30, 45), TimeSpan.Zero), OdfNamespaces.GetPrefix(OdfNamespaces.Office));
        cell.SetLengthAttributeValue("width", OdfNamespaces.Style, OdfLength.FromCentimeters(2.5), OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetBorderWidthsAttributeValue("border-line-width", OdfNamespaces.Style, new OdfBorderWidths(OdfLength.FromPoints(0.5), OdfLength.FromPoints(1), OdfLength.FromPoints(0.5)), OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetDurationAttributeValue("duration", OdfNamespaces.Presentation, new OdfDuration("PT1H30M"), OdfNamespaces.GetPrefix(OdfNamespaces.Presentation));
        cell.SetAngleAttributeValue("rotation-angle", OdfNamespaces.Style, OdfAngle.FromDegrees(45.5m), OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetStyleNameAttributeValue("style-name", OdfNamespaces.Table, new OdfStyleName("CellStyle1"), OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetStyleNameListAttributeValue("style-names", OdfNamespaces.Table, new OdfStyleNameList([new OdfStyleName("CellStyle1"), new OdfStyleName("Accent2")]), OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetColorAttributeValue("fill-color", OdfNamespaces.Draw, OdfColor.FromRgb(255, 204, 0), OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetIriReferenceAttributeValue("href", OdfNamespaces.XLink, new OdfIriReference("../Pictures/logo.svg#main"), OdfNamespaces.GetPrefix(OdfNamespaces.XLink));
        cell.SetPercentAttributeValue("opacity", OdfNamespaces.Draw, new OdfPercent("87.5%"), OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetSignedPercentAttributeValue("shadow-offset", OdfNamespaces.Draw, new OdfPercent("-12.5%"), OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetCellAddressAttributeValue("base-cell-address", OdfNamespaces.Table, new OdfCellAddressReference("'My Sheet'.$A$1"), OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetCellRangeAddressAttributeValue("cell-range-address", OdfNamespaces.Table, new OdfCellRangeAddress("'My Sheet'.$A$1:'My Sheet'.$C$3"), OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetCellRangeAddressListAttributeValue(
            "cell-range-address-list",
            OdfNamespaces.Table,
            new OdfCellRangeAddressList("'My Sheet'.$A$1:'My Sheet'.$C$3 .D4:.E5"),
            OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetVector3DAttributeValue("extrusion-direction", OdfNamespaces.Draw, new OdfVector3D(1m, 0m, -0.5m), OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetPoint3DAttributeValue(
            "extrusion-viewpoint",
            OdfNamespaces.Draw,
            new OdfPoint3D(OdfLength.FromCentimeters(1), OdfLength.FromMillimeters(0), OdfLength.FromInches(-0.5)),
            OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetPointListAttributeValue("points", OdfNamespaces.Draw, new OdfPointList([new OdfPoint2D(0, 0), new OdfPoint2D(10, -20)]), OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetXmlNameAttributeValue("shape-id", OdfNamespaces.Draw, new OdfXmlName("Shape1"), OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetLanguageCodeAttributeValue("language", OdfNamespaces.Fo, new OdfLanguageCode("zh"), OdfNamespaces.GetPrefix(OdfNamespaces.Fo));
        cell.SetCountryCodeAttributeValue("country", OdfNamespaces.Fo, new OdfCountryCode("TW"), OdfNamespaces.GetPrefix(OdfNamespaces.Fo));
        cell.SetScriptCodeAttributeValue("script", OdfNamespaces.Fo, new OdfScriptCode("Hant"), OdfNamespaces.GetPrefix(OdfNamespaces.Fo));
        cell.SetLanguageTagAttributeValue("rfc-language-tag", OdfNamespaces.Table, new OdfLanguageTag("zh-Hant-TW"), OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetNamespacedTokenAttributeValue("type-name", OdfNamespaces.Draw, new OdfNamespacedToken("draw:shape"), OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetCharacterAttributeValue("decimal-replacement", OdfNamespaces.Number, new OdfCharacter("*"), OdfNamespaces.GetPrefix(OdfNamespaces.Number));
        cell.SetTextEncodingAttributeValue("encoding", OdfNamespaces.Text, new OdfTextEncoding("UTF-8"), OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        cell.SetTargetFrameNameAttributeValue("target-frame-name", OdfNamespaces.Office, new OdfTargetFrameName("_blank"), OdfNamespaces.GetPrefix(OdfNamespaces.Office));

        Assert.Equal(3, cell.NumberColumnsRepeated);
        Assert.Equal(12.50m, cell.GetDecimalAttributeValue("value", OdfNamespaces.Office));
        Assert.True(cell.GetBooleanAttributeValue("boolean-value", OdfNamespaces.Office));
        Assert.Equal(utc, cell.GetDateTimeAttributeValue("date-value", OdfNamespaces.Office));
        Assert.Equal("2026-06-13T09:30:00Z", cell.GetAttribute("date-value", OdfNamespaces.Office));
        Assert.Equal(new OdfTime(new TimeSpan(12, 30, 45), TimeSpan.Zero), cell.GetTimeAttributeValue("time-value", OdfNamespaces.Office));
        Assert.Equal("12:30:45Z", cell.GetAttribute("time-value", OdfNamespaces.Office));
        Assert.Equal(OdfLength.FromCentimeters(2.5), cell.GetLengthAttributeValue("width", OdfNamespaces.Style));
        Assert.Equal("2.5cm", cell.GetAttribute("width", OdfNamespaces.Style));
        OdfBorderWidths? borderWidths = cell.GetBorderWidthsAttributeValue("border-line-width", OdfNamespaces.Style);
        Assert.Equal(new OdfBorderWidths("0.5pt 1pt 0.5pt"), borderWidths);
        Assert.Equal(OdfLength.FromPoints(1), borderWidths!.Value.Spacing);
        Assert.Equal(new OdfDuration("PT1H30M"), cell.GetDurationAttributeValue("duration", OdfNamespaces.Presentation));
        Assert.Equal("PT1H30M", cell.GetAttribute("duration", OdfNamespaces.Presentation));
        OdfAngle? angle = cell.GetAngleAttributeValue("rotation-angle", OdfNamespaces.Style);
        Assert.Equal(new OdfAngle("45.5"), angle);
        Assert.True(angle!.Value.TryGetDegrees(out decimal degrees));
        Assert.Equal(45.5m, degrees);
        Assert.Equal(new OdfStyleName("CellStyle1"), cell.GetStyleNameAttributeValue("style-name", OdfNamespaces.Table));
        Assert.Equal("CellStyle1", cell.GetAttribute("style-name", OdfNamespaces.Table));
        OdfStyleNameList? styleNameList = cell.GetStyleNameListAttributeValue("style-names", OdfNamespaces.Table);
        Assert.Equal(2, styleNameList!.Value.StyleNames.Count);
        Assert.Equal("Accent2", styleNameList.Value.StyleNames[1].Value);
        Assert.Equal(new OdfColor("#ffcc00"), cell.GetColorAttributeValue("fill-color", OdfNamespaces.Draw));
        Assert.Equal("#ffcc00", cell.GetAttribute("fill-color", OdfNamespaces.Draw));
        Assert.Equal(new OdfIriReference("../Pictures/logo.svg#main"), cell.GetIriReferenceAttributeValue("href", OdfNamespaces.XLink));
        Assert.Equal("../Pictures/logo.svg#main", cell.GetAttribute("href", OdfNamespaces.XLink));
        Assert.Equal(new OdfPercent("87.5%"), cell.GetPercentAttributeValue("opacity", OdfNamespaces.Draw));
        Assert.Equal(87.5m, cell.GetPercentAttributeValue("opacity", OdfNamespaces.Draw)!.Value.Percent);
        Assert.Equal(new OdfPercent("-12.5%"), cell.GetSignedPercentAttributeValue("shadow-offset", OdfNamespaces.Draw));
        Assert.Equal("-12.5%", cell.GetAttribute("shadow-offset", OdfNamespaces.Draw));
        Assert.Equal(new OdfCellAddressReference("'My Sheet'.$A$1"), cell.GetCellAddressAttributeValue("base-cell-address", OdfNamespaces.Table));
        Assert.Equal(new OdfCellRangeAddress("'My Sheet'.$A$1:'My Sheet'.$C$3"), cell.GetCellRangeAddressAttributeValue("cell-range-address", OdfNamespaces.Table));
        OdfCellRangeAddressList? cellRangeAddressList = cell.GetCellRangeAddressListAttributeValue("cell-range-address-list", OdfNamespaces.Table);
        Assert.Equal(2, cellRangeAddressList!.Value.Ranges.Count);
        Assert.Equal("'My Sheet'.$A$1:'My Sheet'.$C$3", cellRangeAddressList.Value.Ranges[0].Value);
        Assert.Equal(new OdfVector3D("(1 0 -0.5)"), cell.GetVector3DAttributeValue("extrusion-direction", OdfNamespaces.Draw));
        Assert.Equal(-0.5m, cell.GetVector3DAttributeValue("extrusion-direction", OdfNamespaces.Draw)!.Value.Z);
        OdfPoint3D? point = cell.GetPoint3DAttributeValue("extrusion-viewpoint", OdfNamespaces.Draw);
        Assert.Equal(new OdfPoint3D("(1cm 0mm -0.5in)"), point);
        Assert.Equal(OdfUnit.Inches, point!.Value.Z.Unit);
        OdfPointList? pointList = cell.GetPointListAttributeValue("points", OdfNamespaces.Draw);
        Assert.Equal(2, pointList!.Value.Points.Count);
        Assert.Equal(-20, pointList.Value.Points[1].Y);
        Assert.Equal(new OdfXmlName("Shape1"), cell.GetXmlNameAttributeValue("shape-id", OdfNamespaces.Draw));
        Assert.Equal(new OdfLanguageCode("zh"), cell.GetLanguageCodeAttributeValue("language", OdfNamespaces.Fo));
        Assert.Equal(new OdfCountryCode("TW"), cell.GetCountryCodeAttributeValue("country", OdfNamespaces.Fo));
        Assert.Equal(new OdfScriptCode("Hant"), cell.GetScriptCodeAttributeValue("script", OdfNamespaces.Fo));
        Assert.Equal(new OdfLanguageTag("zh-Hant-TW"), cell.GetLanguageTagAttributeValue("rfc-language-tag", OdfNamespaces.Table));
        OdfNamespacedToken? namespacedToken = cell.GetNamespacedTokenAttributeValue("type-name", OdfNamespaces.Draw);
        Assert.Equal(new OdfNamespacedToken("draw:shape"), namespacedToken);
        Assert.Equal("draw", namespacedToken!.Value.Prefix);
        Assert.Equal(new OdfCharacter("*"), cell.GetCharacterAttributeValue("decimal-replacement", OdfNamespaces.Number));
        Assert.Equal(new OdfTextEncoding("UTF-8"), cell.GetTextEncodingAttributeValue("encoding", OdfNamespaces.Text));
        OdfTargetFrameName? targetFrameName = cell.GetTargetFrameNameAttributeValue("target-frame-name", OdfNamespaces.Office);
        Assert.Equal(new OdfTargetFrameName("_blank"), targetFrameName);
        Assert.True(targetFrameName!.Value.IsReservedTarget);

        cell.SetDateTimeAttributeValue("date-value", OdfNamespaces.Office, local, OdfNamespaces.GetPrefix(OdfNamespaces.Office));
        cell.SetAttribute("time-value", OdfNamespaces.Office, "23:59:59.125+02:30");

        Assert.Equal(local, cell.GetDateTimeAttributeValue("date-value", OdfNamespaces.Office));
        Assert.Equal("2026-06-13T17:30:00", cell.GetAttribute("date-value", OdfNamespaces.Office));
        Assert.Equal(new OdfTime(new TimeSpan(0, 23, 59, 59, 125), new TimeSpan(2, 30, 0)), cell.GetTimeAttributeValue("time-value", OdfNamespaces.Office));
        cell.SetAttribute("time-value", OdfNamespaces.Office, "25:00:00");
        Assert.Null(cell.GetTimeAttributeValue("time-value", OdfNamespaces.Office));
        cell.SetAttribute("width", OdfNamespaces.Style, "invalid-length");
        Assert.Null(cell.GetLengthAttributeValue("width", OdfNamespaces.Style));
        cell.SetAttribute("border-line-width", OdfNamespaces.Style, "0.5pt 1pt");
        Assert.Null(cell.GetBorderWidthsAttributeValue("border-line-width", OdfNamespaces.Style));
        cell.SetAttribute("border-line-width", OdfNamespaces.Style, "0pt 1pt 0.5pt");
        Assert.Null(cell.GetBorderWidthsAttributeValue("border-line-width", OdfNamespaces.Style));
        cell.SetAttribute("duration", OdfNamespaces.Presentation, "not-duration");
        Assert.Null(cell.GetDurationAttributeValue("duration", OdfNamespaces.Presentation));
        cell.SetAttribute("rotation-angle", OdfNamespaces.Style, "\u0001");
        Assert.Null(cell.GetAngleAttributeValue("rotation-angle", OdfNamespaces.Style));
        cell.SetAttribute("style-name", OdfNamespaces.Table, "invalid style");
        Assert.Null(cell.GetStyleNameAttributeValue("style-name", OdfNamespaces.Table));
        cell.SetAttribute("style-names", OdfNamespaces.Table, "Valid invalid:name");
        Assert.Null(cell.GetStyleNameListAttributeValue("style-names", OdfNamespaces.Table));
        cell.SetAttribute("style-names", OdfNamespaces.Table, string.Empty);
        Assert.Empty(cell.GetStyleNameListAttributeValue("style-names", OdfNamespaces.Table)!.Value.StyleNames);
        cell.SetAttribute("fill-color", OdfNamespaces.Draw, "#ff0");
        Assert.Null(cell.GetColorAttributeValue("fill-color", OdfNamespaces.Draw));
        cell.SetAttribute("href", OdfNamespaces.XLink, "bad\u0001iri");
        Assert.Null(cell.GetIriReferenceAttributeValue("href", OdfNamespaces.XLink));
        cell.SetAttribute("opacity", OdfNamespaces.Draw, "-1%");
        Assert.Null(cell.GetPercentAttributeValue("opacity", OdfNamespaces.Draw));
        cell.SetAttribute("shadow-offset", OdfNamespaces.Draw, "-101%");
        Assert.Null(cell.GetSignedPercentAttributeValue("shadow-offset", OdfNamespaces.Draw));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cell.SetPercentAttributeValue("opacity", OdfNamespaces.Draw, new OdfPercent("-1%"), OdfNamespaces.GetPrefix(OdfNamespaces.Draw)));
        cell.SetAttribute("base-cell-address", OdfNamespaces.Table, "A1");
        Assert.Null(cell.GetCellAddressAttributeValue("base-cell-address", OdfNamespaces.Table));
        cell.SetAttribute("cell-range-address", OdfNamespaces.Table, ".A1:B2");
        Assert.Null(cell.GetCellRangeAddressAttributeValue("cell-range-address", OdfNamespaces.Table));
        cell.SetAttribute("cell-range-address-list", OdfNamespaces.Table, "'My Sheet'.$A$1:'My Sheet'.$C$3 bad-range");
        Assert.Null(cell.GetCellRangeAddressListAttributeValue("cell-range-address-list", OdfNamespaces.Table));
        cell.SetAttribute("extrusion-direction", OdfNamespaces.Draw, "(1 2)");
        Assert.Null(cell.GetVector3DAttributeValue("extrusion-direction", OdfNamespaces.Draw));
        cell.SetAttribute("extrusion-viewpoint", OdfNamespaces.Draw, "(1px 2px 3px)");
        Assert.Null(cell.GetPoint3DAttributeValue("extrusion-viewpoint", OdfNamespaces.Draw));
        cell.SetAttribute("points", OdfNamespaces.Draw, "0,0 1.5,2");
        Assert.Null(cell.GetPointListAttributeValue("points", OdfNamespaces.Draw));
        cell.SetAttribute("shape-id", OdfNamespaces.Draw, "bad:name");
        Assert.Null(cell.GetXmlNameAttributeValue("shape-id", OdfNamespaces.Draw));
        cell.SetAttribute("language", OdfNamespaces.Fo, "zh-Hant");
        Assert.Null(cell.GetLanguageCodeAttributeValue("language", OdfNamespaces.Fo));
        cell.SetAttribute("country", OdfNamespaces.Fo, "too-long-code");
        Assert.Null(cell.GetCountryCodeAttributeValue("country", OdfNamespaces.Fo));
        cell.SetAttribute("script", OdfNamespaces.Fo, "Hant!");
        Assert.Null(cell.GetScriptCodeAttributeValue("script", OdfNamespaces.Fo));
        cell.SetAttribute("rfc-language-tag", OdfNamespaces.Table, "zh--TW");
        Assert.Null(cell.GetLanguageTagAttributeValue("rfc-language-tag", OdfNamespaces.Table));
        cell.SetAttribute("type-name", OdfNamespaces.Draw, "draw:bad:name");
        Assert.Null(cell.GetNamespacedTokenAttributeValue("type-name", OdfNamespaces.Draw));
        cell.SetAttribute("decimal-replacement", OdfNamespaces.Number, "ab");
        Assert.Null(cell.GetCharacterAttributeValue("decimal-replacement", OdfNamespaces.Number));
        cell.SetAttribute("encoding", OdfNamespaces.Text, "8bit");
        Assert.Null(cell.GetTextEncodingAttributeValue("encoding", OdfNamespaces.Text));
        cell.SetAttribute("target-frame-name", OdfNamespaces.Office, "ReportFrame");
        Assert.False(cell.GetTargetFrameNameAttributeValue("target-frame-name", OdfNamespaces.Office)!.Value.IsReservedTarget);
        cell.SetAttribute("target-frame-name", OdfNamespaces.Office, "\u0001");
        Assert.Null(cell.GetTargetFrameNameAttributeValue("target-frame-name", OdfNamespaces.Office));
        Assert.Equal(7, cell.GetInt32AttributeValue("missing", OdfNamespaces.Table, 7));
        Assert.Null(cell.GetBooleanAttributeValue("missing", OdfNamespaces.Table));

        OdfElement style = new("style", OdfNamespaces.Style, "style");
        style.SetStyleFamilyAttributeValue("family", OdfNamespaces.Style, OdfStyleFamily.TableColumn, OdfNamespaces.GetPrefix(OdfNamespaces.Style));

        Assert.Equal(OdfStyleFamily.TableColumn, style.GetStyleFamilyAttributeValue("family", OdfNamespaces.Style));
        Assert.Equal("table-column", style.GetAttribute("family", OdfNamespaces.Style));
        style.SetAttribute("family", OdfNamespaces.Style, "unknown-family");
        Assert.Null(style.GetStyleFamilyAttributeValue("family", OdfNamespaces.Style));

        OdfElement document = new("document-content", OdfNamespaces.Office, "office");
        document.SetOdfVersionAttributeValue("version", OdfNamespaces.Office, OdfVersion.Odf13, OdfNamespaces.GetPrefix(OdfNamespaces.Office));

        Assert.Equal(OdfVersion.Odf13, document.GetOdfVersionAttributeValue("version", OdfNamespaces.Office));
        Assert.Equal("1.3", document.GetAttribute("version", OdfNamespaces.Office));
        document.SetAttribute("version", OdfNamespaces.Office, "2.0");
        Assert.Null(document.GetOdfVersionAttributeValue("version", OdfNamespaces.Office));

        document.SetMediaTypeAttributeValue(
            "mimetype",
            OdfNamespaces.Office,
            new OdfMediaType("application/vnd.oasis.opendocument.text"),
            OdfNamespaces.GetPrefix(OdfNamespaces.Office));

        Assert.Equal(
            new OdfMediaType("application/vnd.oasis.opendocument.text"),
            document.GetMediaTypeAttributeValue("mimetype", OdfNamespaces.Office));
        document.SetAttribute("mimetype", OdfNamespaces.Office, "not a media type");
        Assert.Null(document.GetMediaTypeAttributeValue("mimetype", OdfNamespaces.Office));
    }

    /// <summary>
    /// 驗證 typed DOM coverage 文件列出 ODFDOM 對標缺口與 coverage guard。
    /// </summary>
    [Fact]
    public void TypedDomCoverageDocumentDeclaresParityGaps()
    {
        string repoRoot = FindRepositoryRoot();
        string document = File.ReadAllText(Path.Combine(repoRoot, "docs", "typed-dom-coverage.md"));

        Assert.Contains("ODFDOM", document, StringComparison.Ordinal);
        Assert.Contains("Coverage guard", document, StringComparison.Ordinal);
        Assert.Contains("Generated typed element classes", document, StringComparison.Ordinal);
        Assert.Contains("schema-to-wrapper coverage report", document, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OdfKit.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("找不到 repository root。");
    }
}
