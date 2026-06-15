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
        return new System.IO.StreamReader(stream).ReadToEnd();
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
        Assert.Contains("calcext:type=\"min\"", xml);
        Assert.Contains("calcext:color=\"#FF0000\"", xml);
        Assert.Contains("calcext:type=\"max\"", xml);
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
    /// 驗證資料橫條在未指定負值色彩時不寫入 negative-color 屬性。
    /// </summary>
    [Fact]
    public void AddDataBarFormat_NoNegativeColor_DoesNotWriteNegativeAttr()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        sheet.AddDataBarFormat(new OdfCellRange(0, 0, 4, 0), new OdfColor("#4472C4"));

        string xml = GetContentXml(doc);

        Assert.Contains("calcext:data-bar", xml);
        Assert.DoesNotContain("negative-color", xml);
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
        Assert.Contains("calcext:icon-set-entry", xml);
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
        while ((start = xml.IndexOf("calcext:icon-set-entry", start, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            start++;
        }
        Assert.Equal(5, count);
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
}
