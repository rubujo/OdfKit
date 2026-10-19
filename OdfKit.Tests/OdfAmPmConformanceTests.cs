using System;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests native ODF AM/PM formatting obligations.
/// 測試 ODF 上午／下午格式的原生語意。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfAmPmConformanceTests
{
    /// <summary>
    /// Checks twelve-hour boundaries in both date and time styles.
    /// 檢查日期及時間樣式的十二小時制邊界。
    /// </summary>
    [Theory]
    [InlineData(0, 12, "AM")]
    [InlineData(1, 1, "AM")]
    [InlineData(11, 11, "AM")]
    [InlineData(12, 12, "PM")]
    [InlineData(13, 1, "PM")]
    [InlineData(23, 11, "PM")]
    public void NativeMarkerUsesTwelveHourClock(int hour, int expectedHour, string marker)
    {
        foreach (string rootName in new[] { "time-style", "date-style" })
            foreach (bool markerFirst in new[] { false, true })
                foreach (bool longHours in new[] { false, true })
                {
                    var root = Style(rootName, longHours, markerFirst, OdfNamespaces.Number);
                    string hours = expectedHour.ToString(longHours ? "D2" : "D", CultureInfo.InvariantCulture);
                    string expected = markerFirst ? marker + hours : hours + marker;
                    Assert.Equal(expected, OdfNumberFormatEngine.Format(new DateTime(2026, 1, 1, hour, 0, 0), root));
                }
    }

    /// <summary>
    /// Foreign marker names must not change the native clock.
    /// 外來命名空間的同名標記不得改變原生時制。
    /// </summary>
    [Theory]
    [InlineData(0, "00")]
    [InlineData(13, "13")]
    [InlineData(23, "23")]
    public void ForeignMarkerKeepsTwentyFourHourClock(int hour, string expected)
    {
        foreach (string rootName in new[] { "time-style", "date-style" })
        {
            var root = Style(rootName, true, false, "urn:example:foreign");
            Assert.Equal(expected, OdfNumberFormatEngine.Format(new DateTime(2026, 1, 1, hour, 0, 0), root));
        }
    }

    /// <summary>
    /// Date styles preserve ordered date and time components at date boundaries.
    /// 日期樣式在日期邊界仍保留依序排列的日期與時間欄位。
    /// </summary>
    [Fact]
    public void DateStyleIncludesTimeAtDateTimeBoundaries()
    {
        var root = new OdfNode(OdfNodeType.Element, "date-style", OdfNamespaces.Number, "n");
        foreach (string name in new[] { "year", "month", "day", "hours", "minutes", "seconds", "am-pm" })
        {
            var child = new OdfNode(OdfNodeType.Element, name, OdfNamespaces.Number, "alternate");
            child.SetAttribute("style", OdfNamespaces.Number, "long", "n");
            root.AppendChild(child);
        }
        Assert.Equal("00010101120000AM", OdfNumberFormatEngine.Format(DateTime.MinValue, root));
        Assert.Equal("99991231115959PM", OdfNumberFormatEngine.Format(DateTime.MaxValue, root));
    }

    private static OdfNode Style(string rootName, bool longHours, bool markerFirst, string markerNamespace)
    {
        var root = new OdfNode(OdfNodeType.Element, rootName, OdfNamespaces.Number, "n");
        var hours = new OdfNode(OdfNodeType.Element, "hours", OdfNamespaces.Number, "other");
        hours.SetAttribute("style", OdfNamespaces.Number, longHours ? "long" : "short", "n");
        var marker = new OdfNode(OdfNodeType.Element, "am-pm", markerNamespace, "n");
        root.AppendChild(markerFirst ? marker : hours);
        root.AppendChild(markerFirst ? hours : marker);
        return root;
    }
}
