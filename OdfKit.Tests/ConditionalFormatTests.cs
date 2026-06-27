using System.IO;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 J-1 色階/資料橫條/圖示集條件格式 API 的整合測試。
/// </summary>
public class ConditionalFormatTests
{
    private static string GetContentXml(SpreadsheetDocument doc)
    {
        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;
        using var pkg = OdfPackage.Open(ms, leaveOpen: true);
        using var stream = pkg.GetEntryStream("content.xml");
        return new StreamReader(stream).ReadToEnd();
    }

    /// <summary>
    /// 驗證兩色色階寫入正確的 calcext:color-scale 結構（min/max 兩個 entry）。
    /// </summary>
    [Fact]
    public void AddColorScaleFormat_TwoColor_XmlContainsMinMaxEntries()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        var range = new OdfCellRange(0, 0, 9, 0);

        sheet.AddColorScaleFormat(range,
            minColor: new OdfColor("#FF0000"),
            maxColor: new OdfColor("#00FF00"));

        string xml = GetContentXml(doc);

        Assert.Contains("calcext:color-scale", xml);
        Assert.Contains("calcext:type=\"minimum\"", xml);
        Assert.Contains("calcext:color=\"#FF0000\"", xml);
        Assert.Contains("calcext:type=\"maximum\"", xml);
        Assert.Contains("calcext:color=\"#00FF00\"", xml);
        Assert.DoesNotContain("calcext:type=\"percentile\"", xml);
    }

    /// <summary>
    /// 驗證三色色階寫入正確的 calcext:color-scale 結構（min/percentile/max 三個 entry）。
    /// </summary>
    [Fact]
    public void AddColorScaleFormat_ThreeColor_XmlContainsMidEntry()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        var range = new OdfCellRange(0, 0, 9, 0);

        sheet.AddColorScaleFormat(range,
            minColor: new OdfColor("#FF0000"),
            maxColor: new OdfColor("#00FF00"),
            midColor: new OdfColor("#FFFF00"));

        string xml = GetContentXml(doc);

        Assert.Contains("calcext:type=\"percentile\"", xml);
        Assert.Contains("calcext:value=\"50\"", xml);
        Assert.Contains("calcext:color=\"#FFFF00\"", xml);
    }

    /// <summary>
    /// 驗證資料橫條條件格式寫入正確的 calcext:data-bar 元素及正值色彩。
    /// </summary>
    [Fact]
    public void AddDataBarFormat_XmlContainsDataBarWithPositiveColor()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        var range = new OdfCellRange(0, 1, 9, 1);

        sheet.AddDataBarFormat(range,
            positiveColor: new OdfColor("#638EC6"),
            negativeColor: new OdfColor("#FF0000"));

        string xml = GetContentXml(doc);

        Assert.Contains("calcext:data-bar", xml);
        Assert.Contains("calcext:positive-color=\"#638EC6\"", xml);
        Assert.Contains("calcext:negative-color=\"#FF0000\"", xml);
    }

    /// <summary>
    /// 驗證資料橫條在未指定負值色彩時，仍會寫入 negative-color 屬性並套用預設色彩
    /// （真實 LibreOffice 一律會寫出此屬性，省略會導致長條比例計算例外）。
    /// </summary>
    [Fact]
    public void AddDataBarFormat_NoNegativeColor_WritesDefaultNegativeColor()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        sheet.AddDataBarFormat(new OdfCellRange(0, 0, 4, 0), new OdfColor("#4472C4"));

        string xml = GetContentXml(doc);

        Assert.Contains("calcext:data-bar", xml);
        Assert.Contains("calcext:negative-color=\"#ff0000\"", xml);
    }

    /// <summary>
    /// 驗證計畫名 AddDataBar API 會建立可讀回的資料橫條條件格式。
    /// </summary>
    [Fact]
    public void AddDataBar_CreatesReadableDataBarFormat()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");

        sheet.AddDataBar(new OdfCellRange(0, 1, 9, 1), new OdfColor("#638EC6"));

        var format = Assert.Single(sheet.ConditionalFormats);
        Assert.Equal(OdfConditionalFormatKind.DataBar, format.Kind);
        Assert.Equal("#638EC6", format.PositiveColor?.Value);
    }

    /// <summary>
    /// 驗證三箭頭圖示集寫入正確的 calcext:icon-set 元素與 icon-set-type。
    /// </summary>
    [Fact]
    public void AddIconSetFormat_ThreeArrows_XmlContainsCorrectType()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        var range = new OdfCellRange(0, 2, 9, 2);

        sheet.AddIconSetFormat(range, OdfIconSetType.ThreeArrows);

        string xml = GetContentXml(doc);

        Assert.Contains("calcext:icon-set", xml);
        Assert.Contains("calcext:icon-set-type=\"3Arrows\"", xml);
        Assert.Contains("calcext:formatting-entry", xml);
    }

    /// <summary>
    /// 驗證 FiveRating 圖示集寫入 5 個 entry 且 icon-set-type 正確。
    /// </summary>
    [Fact]
    public void AddIconSetFormat_FiveRating_HasFiveEntries()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        sheet.AddIconSetFormat(new OdfCellRange(0, 0, 9, 0), OdfIconSetType.FiveRating);

        string xml = GetContentXml(doc);

        Assert.Contains("calcext:icon-set-type=\"5Rating\"", xml);
        int count = 0;
        int start = 0;
        while ((start = xml.IndexOf("calcext:formatting-entry", start, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            start++;
        }
        Assert.Equal(5, count);
    }

    /// <summary>
    /// 驗證計畫名 AddIconSet API 會建立可讀回的圖示集條件格式。
    /// </summary>
    [Fact]
    public void AddIconSet_CreatesReadableIconSetFormat()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");

        sheet.AddIconSet(new OdfCellRange(0, 2, 9, 2), OdfIconSetType.ThreeTrafficLights);

        var format = Assert.Single(sheet.ConditionalFormats);
        Assert.Equal(OdfConditionalFormatKind.IconSet, format.Kind);
        Assert.Equal(OdfIconSetType.ThreeTrafficLights, format.IconSetType);
        Assert.Equal("3TrafficLights1", format.IconSetTypeName);
    }

    /// <summary>
    /// 驗證三種條件格式可在同一個工作表中共存（使用同一個 calcext:conditional-formats 節點）。
    /// </summary>
    [Fact]
    public void AllThreeFormats_CanCoexistInSameSheet()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");

        sheet.AddColorScaleFormat(new OdfCellRange(0, 0, 9, 0), new OdfColor("#FF0000"), new OdfColor("#00FF00"));
        sheet.AddDataBarFormat(new OdfCellRange(0, 1, 9, 1), new OdfColor("#4472C4"));
        sheet.AddIconSetFormat(new OdfCellRange(0, 2, 9, 2), OdfIconSetType.ThreeTrafficLights);

        string xml = GetContentXml(doc);

        Assert.Contains("calcext:color-scale", xml);
        Assert.Contains("calcext:data-bar", xml);
        Assert.Contains("calcext:icon-set", xml);
        Assert.Contains("calcext:icon-set-type=\"3TrafficLights1\"", xml);
    }

    /// <summary>
    /// 驗證兩色色階寫入後可透過 <see cref="OdfTableSheet.ConditionalFormats"/> 讀回。
    /// </summary>
    [Fact]
    public void GetConditionalFormats_ColorScaleTwoColor_RoundTrips()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        var range = new OdfCellRange(0, 0, 9, 0);

        sheet.AddColorScaleFormat(range,
            minColor: new OdfColor("#FF0000"),
            maxColor: new OdfColor("#00FF00"));

        Assert.Single(sheet.ConditionalFormats);
        var format = sheet.ConditionalFormats[0];
        Assert.Equal(OdfConditionalFormatKind.ColorScale, format.Kind);
        Assert.Equal("#FF0000", format.MinColor?.Value);
        Assert.Equal("#00FF00", format.MaxColor?.Value);
        Assert.Null(format.MidColor);
        Assert.True(format.TryGetTargetRange(out var parsed));
        Assert.Equal(0, parsed.StartAddress.Row);
        Assert.Equal(9, parsed.EndAddress.Row);
    }

    /// <summary>
    /// 驗證三色色階、資料橫條與圖示集可透過讀取 API 列舉。
    /// </summary>
    [Fact]
    public void GetConditionalFormats_AllThreeKinds_EnumeratesCorrectly()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");

        sheet.AddColorScaleFormat(
            new OdfCellRange(0, 0, 9, 0),
            new OdfColor("#FF0000"),
            new OdfColor("#00FF00"),
            new OdfColor("#FFFF00"));
        sheet.AddDataBarFormat(
            new OdfCellRange(0, 1, 9, 1),
            new OdfColor("#4472C4"),
            new OdfColor("#FF0000"));
        sheet.AddIconSetFormat(new OdfCellRange(0, 2, 9, 2), OdfIconSetType.FiveRating);

        Assert.Equal(3, sheet.ConditionalFormats.Count);

        var colorScale = sheet.ConditionalFormats[0];
        Assert.Equal(OdfConditionalFormatKind.ColorScale, colorScale.Kind);
        Assert.Equal("#FFFF00", colorScale.MidColor?.Value);

        var dataBar = sheet.ConditionalFormats[1];
        Assert.Equal(OdfConditionalFormatKind.DataBar, dataBar.Kind);
        Assert.Equal("#4472C4", dataBar.PositiveColor?.Value);
        Assert.Equal("#FF0000", dataBar.NegativeColor?.Value);

        var iconSet = sheet.ConditionalFormats[2];
        Assert.Equal(OdfConditionalFormatKind.IconSet, iconSet.Kind);
        Assert.Equal(OdfIconSetType.FiveRating, iconSet.IconSetType);
        Assert.Equal("5Rating", iconSet.IconSetTypeName);
    }

    /// <summary>
    /// 驗證單一條件格式寫入後可透過讀取 API 讀回。
    /// </summary>
    [Fact]
    public void GetConditionalFormats_SimpleCondition_RoundTrips()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        sheet.AddConditionalFormat(new OdfCellRange(1, 1, 5, 3), "cell-content()>=100", "GoodStyle");

        Assert.Single(sheet.ConditionalFormats);
        var format = sheet.ConditionalFormats[0];
        Assert.Equal(OdfConditionalFormatKind.Condition, format.Kind);
        Assert.Equal("cell-content()>=100", format.ConditionValue);
        Assert.Equal("GoodStyle", format.StyleName);
    }

    /// <summary>
    /// 驗證走勢圖群組寫入後可透過 <see cref="OdfTableSheet.SparklineGroups"/> 讀回。
    /// </summary>
    [Fact]
    public void GetSparklineGroups_AfterAdd_RoundTrips()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Data");
        sheet.Cells["A1"].CellValue = 10d;
        sheet.Cells["A2"].CellValue = 20d;
        sheet.Cells["A3"].CellValue = 15d;

        var dataRange = OdfCellRange.ParseExcel("A1:A3");
        var hostCell = OdfCellAddress.ParseExcel("B1");
        sheet.AddSparklineGroup(dataRange, hostCell, SparklineType.Line);

        Assert.Single(sheet.SparklineGroups);
        var group = sheet.SparklineGroups[0];
        Assert.Equal(SparklineType.Line, group.Type);
        Assert.Single(group.Sparklines);

        var sparkline = group.Sparklines[0];
        Assert.True(sparkline.TryGetDataRange(out var parsedRange));
        Assert.Equal(0, parsedRange.StartAddress.Row);
        Assert.Equal(2, parsedRange.EndAddress.Row);
        Assert.True(sparkline.TryGetHostCell(out var parsedHost));
        Assert.Equal(0, parsedHost.Row);
        Assert.Equal(1, parsedHost.Column);
    }

    /// <summary>
    /// 驗證載入既有 ODS 後可讀取 calcext 條件格式（save/load round-trip）。
    /// </summary>
    [Fact]
    public void GetConditionalFormats_AfterSaveLoad_PreservesRules()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        sheet.AddDataBarFormat(new OdfCellRange(0, 0, 4, 0), new OdfColor("#638EC6"));

        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;

        using var loaded = SpreadsheetDocument.Load(ms);
        var loadedSheet = loaded.Worksheets[0];
        Assert.Single(loadedSheet.ConditionalFormats);
        Assert.Equal(OdfConditionalFormatKind.DataBar, loadedSheet.ConditionalFormats[0].Kind);
        Assert.Equal("#638EC6", loadedSheet.ConditionalFormats[0].PositiveColor?.Value);
    }
}
