using System;
using System.IO;
using System.Linq;
using OdfKit.Chart;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 針對嵌入式圖表與影像版面樣式實作的整合測試。
/// </summary>
public class EmbeddedChartIntegrationTests
{
    private static readonly byte[] DummyImageBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

    /// <summary>
    /// 驗證 OdfChartStyle 包含 plot-area、series、grid 的框線、背景色與 3D 角度等設定。
    /// </summary>
    [Fact]
    public void TestChartStyleProperties()
    {
        var definition = new OdfChartDefinition
        {
            ChartType = OdfChartType.Bar,
            Title = "測試圖表",
            DataRange = new OdfCellRange(0, 0, 3, 2, "LocalTable"),
            HasLegend = true
        };

        using var chartDoc = OdfChartDocument.Create();
        chartDoc.ChartClass = "chart:bar";

        // 1. 測試 PlotAreaStyle 的背景色與 3D 角度
        OdfChartStyle plotAreaStyle = chartDoc.PlotAreaStyle;
        plotAreaStyle.FillColor = "#FF0000";
        plotAreaStyle.StrokeColor = "#00FF00";
        plotAreaStyle.StrokeWidth = "0.1cm";
        plotAreaStyle.Fill = "solid";
        plotAreaStyle.Stroke = "solid";
        plotAreaStyle.ThreeDimensional = true;
        plotAreaStyle.AngleOffset = 45;

        Assert.Equal("#FF0000", plotAreaStyle.FillColor);
        Assert.Equal("#00FF00", plotAreaStyle.StrokeColor);
        Assert.Equal("0.1cm", plotAreaStyle.StrokeWidth);
        Assert.Equal("solid", plotAreaStyle.Fill);
        Assert.Equal("solid", plotAreaStyle.Stroke);
        Assert.True(plotAreaStyle.ThreeDimensional);
        Assert.Equal(45, plotAreaStyle.AngleOffset);

        // 2. 測試 GridStyle 的框線設定
        OdfChartStyle gridStyle = chartDoc.GridStyle;
        gridStyle.StrokeColor = "#0000FF";
        gridStyle.StrokeWidth = "0.02cm";
        gridStyle.Stroke = "solid";

        Assert.Equal("#0000FF", gridStyle.StrokeColor);
        Assert.Equal("0.02cm", gridStyle.StrokeWidth);
        Assert.Equal("solid", gridStyle.Stroke);

        // 3. 測試 SeriesStyle 的樣式設定
        OdfNode plotAreaNode = chartDoc.ChartNode.Children.First(c => c.LocalName == "plot-area");
        OdfNode seriesNode = OdfNodeFactory.CreateElement("series", OdfNamespaces.Chart, "chart");
        plotAreaNode.AppendChild(seriesNode);

        var series = chartDoc.GetSeriesEditor(0);
        OdfChartStyle seriesStyle = series.Style;
        seriesStyle.FillColor = "#FFFF00";
        seriesStyle.Fill = "solid";

        Assert.Equal("#FFFF00", seriesStyle.FillColor);
        Assert.Equal("solid", seriesStyle.Fill);

        // 驗證 ToInfo 摘要
        OdfChartStyleInfo info = plotAreaStyle.ToInfo();
        Assert.Equal("#FF0000", info.FillColor);
        Assert.Equal("#00FF00", info.StrokeColor);
        Assert.Equal("0.1cm", info.StrokeWidth);
        Assert.Equal("solid", info.Fill);
        Assert.Equal("solid", info.Stroke);
        Assert.True(info.ThreeDimensional);
        Assert.Equal(45, info.AngleOffset);
    }

    /// <summary>
    /// 驗證 OdfImageLayout 封裝外框線、影像間距、環繞、裁剪與透明度等屬性。
    /// </summary>
    [Fact]
    public void TestImageLayoutProperties()
    {
        using var doc = TextDocument.Create();
        OdfParagraph paragraph = doc.AddParagraph();
        OdfImage image = doc.AddImageFrame(paragraph, DummyImageBytes, OdfLength.Parse("5cm"), OdfLength.Parse("5cm"), "MyImage");

        OdfImageLayout layout = image.Layout;

        var docField = typeof(OdfImageLayout).GetField("_document", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(docField);
        var docVal = docField.GetValue(layout);
        Assert.NotNull(docVal); // Check if _document is null

        layout.Border = "1pt solid #000000";

        string? styleName = image.FrameNode.GetAttribute("style-name", OdfNamespaces.Draw);
        Assert.NotNull(styleName); // Check if style-name is set
        layout.Margin = "0.5cm";
        layout.MarginTop = "0.1cm";
        layout.MarginBottom = "0.2cm";
        layout.MarginLeft = "0.3cm";
        layout.MarginRight = "0.4cm";
        layout.Wrap = "parallel";
        layout.Crop = "rect(0cm, 1cm, 0cm, 1cm)";
        layout.Opacity = "80%";

        Assert.Equal("1pt solid #000000", layout.Border);
        Assert.Equal("0.5cm", layout.Margin);
        Assert.Equal("0.1cm", layout.MarginTop);
        Assert.Equal("0.2cm", layout.MarginBottom);
        Assert.Equal("0.3cm", layout.MarginLeft);
        Assert.Equal("0.4cm", layout.MarginRight);
        Assert.Equal("parallel", layout.Wrap);
        Assert.Equal("rect(0cm, 1cm, 0cm, 1cm)", layout.Crop);
        Assert.Equal("80%", layout.Opacity);
    }

    /// <summary>
    /// 驗證在 TextDocument 中新增 AddChart 與 AddImageFrame API 能自動寫入 XML 與建立物件。
    /// </summary>
    [Fact]
    public void TestTextDocumentAddChartAndImageFrame()
    {
        using var doc = TextDocument.Create();
        OdfParagraph paragraph = doc.AddParagraph();

        // 1. 測試 AddImageFrame
        OdfImage image = doc.AddImageFrame(paragraph, DummyImageBytes, OdfLength.Parse("10cm"), OdfLength.Parse("8cm"), "EmbedImage");
        Assert.NotNull(image);
        Assert.Equal("EmbedImage", image.Name);
        Assert.Equal("10cm", image.Width);
        Assert.Equal("8cm", image.Height);

        // 2. 測試 AddChart
        var chartDef = new OdfChartDefinition
        {
            ChartType = OdfChartType.Pie,
            Title = "圓餅圖",
            DataRange = new OdfCellRange(0, 0, 3, 1, "LocalTable"),
            HasLegend = true
        };

        OdfNode chartNode = doc.AddChart(paragraph, chartDef, OdfLength.Parse("12cm"), OdfLength.Parse("7cm"));
        Assert.NotNull(chartNode);

        // 驗證 subpackage 檔案建立
        Assert.True(doc.Package.HasEntry("Object 1/mimetype"));
        Assert.True(doc.Package.HasEntry("Object 1/content.xml"));
        Assert.True(doc.Package.HasEntry("Object 1/styles.xml"));
    }

    /// <summary>
    /// 驗證在 SpreadsheetDocument 中新增 AddChart 與 AddImageFrame API 能自動寫入 XML。
    /// </summary>
    [Fact]
    public void TestSpreadsheetDocumentAddChartAndImageFrame()
    {
        using var doc = SpreadsheetDocument.Create();
        doc.AddSheet("Sheet1");
        var anchor = new OdfCellAddress(1, 1, "Sheet1");

        // 1. 測試 AddImageFrame
        OdfImage image = doc.AddImageFrame("Sheet1", anchor, DummyImageBytes, OdfLength.Parse("8cm"), OdfLength.Parse("6cm"), "SpreadsheetImage");
        Assert.NotNull(image);
        Assert.Equal("SpreadsheetImage", image.Name);
        Assert.Equal("8cm", image.Width);
        Assert.Equal("6cm", image.Height);

        // 2. 測試 AddChart
        var chartDef = new OdfChartDefinition
        {
            ChartType = OdfChartType.Bar,
            Title = "試算表圖表",
            DataRange = new OdfCellRange(0, 0, 3, 2, "Sheet1"),
            HasLegend = false
        };

        OdfNode chartNode = doc.AddChart("Sheet1", anchor, chartDef, OdfLength.Parse("14cm"), OdfLength.Parse("8cm"));
        Assert.NotNull(chartNode);

        // 驗證 subpackage 檔案建立
        Assert.True(doc.Package.HasEntry("Object 1/content.xml"));
    }
}
