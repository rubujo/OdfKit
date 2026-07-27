using System.IO;
using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定圖表實務深度 API 的 round-trip 行為。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Scenario)]
public class ChartAdvancedDepthTests
{
    /// <summary>
    /// 驗證 bubble、stock 與 3D preset 可建立預期圖表類別。
    /// </summary>
    [Fact]
    public void ChartPresetBubbleStockAnd3DCreateExpectedChartClass()
    {
        using ChartDocument bubble = ChartDocument.Builder()
            .WithPreset(OdfChartPreset.Bubble)
            .Build();
        using ChartDocument stock = ChartDocument.Builder()
            .WithPreset(OdfChartPreset.Stock)
            .Build();
        using ChartDocument column3d = ChartDocument.Builder()
            .WithPreset(OdfChartPreset.Column3D)
            .Build();

        Assert.Equal("chart:bubble", bubble.ChartClass);
        Assert.Equal("chart:stock", stock.ChartClass);
        Assert.Equal("chart:bar", column3d.ChartClass);
        Assert.True(column3d.PlotAreaStyle.ThreeDimensional);
        Assert.Equal(OdfDr3dProjection.Perspective, column3d.PlotAreaStyle.Projection);
    }

    /// <summary>
    /// 驗證泡泡圖可用 X、Y 與大小範圍建立並 round-trip。
    /// </summary>
    [Fact]
    public void BubbleChartFromRangesRoundTripsSeriesDomains()
    {
        using ChartDocument chart = ChartDocument.CreateBubble(
            "泡泡圖",
            new OdfBubbleChartSeriesRequest(
                "Data.$A$2:.$A$4",
                "Data.$B$2:.$B$4",
                "Data.$C$2:.$C$4",
                "Data.$B$1",
                "BubbleStyle"));

        OdfBubbleChartSeriesInfo series = Assert.Single(chart.GetBubbleSeries());
        Assert.Equal("Data.$A$2:.$A$4", series.XValuesCellRangeAddress);
        Assert.Equal("Data.$B$2:.$B$4", series.YValuesCellRangeAddress);
        Assert.Equal("Data.$C$2:.$C$4", series.BubbleSizeCellRangeAddress);
        Assert.Equal("Data.$B$1", series.LabelCellAddress);
        Assert.Equal("BubbleStyle", series.StyleName);

        using ChartDocument loaded = RoundTrip(chart);
        OdfBubbleChartSeriesInfo reloaded = Assert.Single(loaded.GetBubbleSeries());
        Assert.Equal("chart:bubble", loaded.ChartClass);
        Assert.Equal(series, reloaded);
    }

    /// <summary>
    /// 驗證股票圖 OHLC、成交量與漲跌標記樣式可 round-trip。
    /// </summary>
    [Fact]
    public void StockChartOhlcRangesRoundTripsMarkers()
    {
        using ChartDocument chart = ChartDocument.CreateStock(
            "股票圖",
            new OdfStockChartSeriesRequest(
                "Stock.$B$2:.$B$6",
                "Stock.$C$2:.$C$6",
                "Stock.$D$2:.$D$6",
                "Stock.$E$2:.$E$6",
                "Stock.$F$2:.$F$6",
                "Stock.$A$1",
                "StockStyle"));
        chart.ApplyStockMarkerStyle(new OdfStockMarkerStyle(
            new OdfChartSurfaceStyle("GainStyle", FillColor: "#00AA66"),
            new OdfChartSurfaceStyle("LossStyle", FillColor: "#CC3333"),
            new OdfChartSurfaceStyle("RangeStyle", StrokeColor: "#333333")));

        using ChartDocument loaded = RoundTrip(chart);
        OdfStockChartSeriesInfo series = Assert.Single(loaded.GetStockSeries());

        Assert.Equal("chart:stock", loaded.ChartClass);
        Assert.Equal("Stock.$B$2:.$B$6", series.OpenCellRangeAddress);
        Assert.Equal("Stock.$C$2:.$C$6", series.HighCellRangeAddress);
        Assert.Equal("Stock.$D$2:.$D$6", series.LowCellRangeAddress);
        Assert.Equal("Stock.$E$2:.$E$6", series.CloseCellRangeAddress);
        Assert.Equal("Stock.$F$2:.$F$6", series.VolumeCellRangeAddress);
        Assert.Equal("GainStyle", loaded.GetStockGainMarkerStyleName());
        Assert.Equal("LossStyle", loaded.GetStockLossMarkerStyleName());
        Assert.Equal("RangeStyle", loaded.GetStockRangeLineStyleName());
    }

    /// <summary>
    /// 驗證 3D 投影、光源與 wall/floor 樣式可 round-trip。
    /// </summary>
    [Fact]
    public void ThreeDChartAppliesProjectionLightsWallAndFloor()
    {
        using ChartDocument chart = ChartDocument.FromTable(
            "Data",
            new OdfCellRange(0, 0, 4, 2, "Data"),
            OdfChartPreset.Bar,
            "3D 長條圖");
        chart.Apply3DOptions(new OdfChart3DOptions
        {
            Projection = OdfDr3dProjection.Parallel,
            AngleOffset = 35,
            LightingMode = true,
            WallStyle = new OdfChartSurfaceStyle("WallStyle", FillColor: "#EEEEEE"),
            FloorStyle = new OdfChartSurfaceStyle("FloorStyle", FillColor: "#DDDDDD"),
            Lights =
            {
                new OdfChartLightRequest("(0 0 1)", "#FFFFFF", Enabled: true, Specular: false),
            },
        });

        using ChartDocument loaded = RoundTrip(chart);
        OdfChartLightInfo light = Assert.Single(loaded.GetLights());

        Assert.True(loaded.PlotAreaStyle.ThreeDimensional);
        Assert.Equal(OdfDr3dProjection.Parallel, loaded.PlotAreaStyle.Projection);
        Assert.Equal(35, loaded.PlotAreaStyle.AngleOffset);
        Assert.True(loaded.PlotAreaStyle.LightingMode);
        Assert.Equal("(0 0 1)", light.Direction);
        Assert.Equal("#FFFFFF", light.DiffuseColor);
        Assert.Equal("WallStyle", loaded.GetWallStyleName());
        Assert.Equal("FloorStyle", loaded.GetFloorStyleName());
    }

    /// <summary>
    /// 驗證折線圖標記樣式與座標軸數字格式可 round-trip。
    /// </summary>
    [Fact]
    public void MarkerStyleAndAxisNumberFormatRoundTrip()
    {
        using ChartDocument chart = ChartDocument.FromTable(
            "Data",
            new OdfCellRange(0, 0, 3, 1, "Data"),
            OdfChartPreset.Line,
            "Marker");
        chart.GetSeriesEditor(0).ApplyMarkerStyle(new OdfChartMarkerStyle("circle", "0.25cm", "#FF0000", "#333333"));
        chart.SetAxisNumberFormat("y", "N2");

        using ChartDocument loaded = RoundTrip(chart);
        OdfChartMarkerStyle marker = loaded.GetSeriesEditor(0).GetMarkerStyle()!;

        Assert.Equal("circle", marker.Symbol);
        Assert.Equal("0.25cm", marker.Size);
        Assert.Equal("#FF0000", marker.FillColor);
        Assert.Equal("N2", loaded.GetAxisNumberFormat("y"));
    }

    /// <summary>
    /// 驗證圖表樣式摘要可讀回 marker 與 data style。
    /// </summary>
    [Fact]
    public void ChartStyleInfoReadsMarkerAndDataStyle()
    {
        using ChartDocument chart = ChartDocument.FromTable(
            "Data",
            new OdfCellRange(0, 0, 3, 1, "Data"),
            OdfChartPreset.Scatter,
            "Styles");
        chart.GetSeriesEditor(0).ApplyMarkerStyle(new OdfChartMarkerStyle("square", "0.3cm", "#00AA66", "#333333"));
        chart.SetAxisNumberFormat("y", "Percent2");

        OdfChartStyleInfo seriesStyle = Assert.Single(chart.GetChartStyles(), style => style.SymbolName == "square");

        Assert.Equal("named-symbol", seriesStyle.SymbolType);
        Assert.Equal("0.3cm", seriesStyle.SymbolSize);
        Assert.Contains(chart.GetChartStyles(), style => style.DataStyleName == "Percent2");
    }

    /// <summary>
    /// 驗證進階圖表設定在 ODS 嵌入圖表中可 round-trip。
    /// </summary>
    [Fact]
    public void EmbeddedChartAdvancedOptionsRoundTripWithStandaloneParity()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[,]
            {
                { "Name", "Value" },
                { "A", 10d },
                { "B", 20d },
            });

        var options = new OdfEmbeddedChartOptions
        {
            Preset = OdfChartPreset.Column3D,
            Title = "Embedded 3D",
            YAxisNumberFormat = "N2",
            ThreeDOptions = new OdfChart3DOptions
            {
                Projection = OdfDr3dProjection.Parallel,
                AngleOffset = 30,
                WallStyle = new OdfChartSurfaceStyle("WallStyle", FillColor: "#EEEEEE"),
                FloorStyle = new OdfChartSurfaceStyle("FloorStyle", FillColor: "#DDDDDD"),
            }
        };
        options.MarkerStyles.Add(new OdfChartMarkerStyle("circle", "0.25cm", "#FF0000", "#333333"));

        _ = document.InsertChartFromRange(
            "Data",
            new OdfCellAddress(0, 3, "Data"),
            new OdfCellRange(0, 0, 2, 1, "Data"),
            options);

        using SpreadsheetDocument loaded = RoundTripSpreadsheet(document);
        OdfChartDocument chart = loaded.GetEmbeddedChartDocument(Assert.Single(loaded.GetEmbeddedCharts()));

        Assert.Equal("Embedded 3D", chart.ChartTitle);
        Assert.True(chart.PlotAreaStyle.ThreeDimensional);
        Assert.Equal(OdfDr3dProjection.Parallel, chart.PlotAreaStyle.Projection);
        Assert.Equal(30, chart.PlotAreaStyle.AngleOffset);
        Assert.Equal("WallStyle", chart.GetWallStyleName());
        Assert.Equal("FloorStyle", chart.GetFloorStyleName());
        Assert.Equal("N2", chart.GetAxisNumberFormat("y"));
        Assert.Equal("circle", chart.GetSeriesEditor(0).GetMarkerStyle()?.Symbol);
    }

    private static ChartDocument RoundTrip(ChartDocument chart)
    {
        var stream = new MemoryStream();
        chart.SaveToStream(stream);
        stream.Position = 0;
        return ChartDocument.Load(stream, "chart.odc");
    }

    private static SpreadsheetDocument RoundTripSpreadsheet(SpreadsheetDocument document)
    {
        var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;
        return SpreadsheetDocument.Load(stream, "chart.ods");
    }
}
