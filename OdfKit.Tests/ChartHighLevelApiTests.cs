using System;
using System.IO;
using System.Linq;
using OdfKit.Chart;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Extensions.Imaging;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定圖表文件高階 API 的整合測試。
/// </summary>
public class ChartHighLevelApiTests
{
    /// <summary>
    /// 驗證建立圖表與取得圖表定義的正確性。
    /// </summary>
    [Fact]
    public void CreateAndGetChartDefinitionTest()
    {
        var definition = new OdfChartDefinition
        {
            ChartType = OdfChartType.Line,
            Title = "銷售趨勢圖",
            DataRange = new OdfCellRange(0, 0, 4, 1, "LocalTable"),
            HasLegend = true
        };

        using var chartDoc = ChartDocument.Create(definition);

        // 驗證基本屬性
        Assert.Equal("chart:line", chartDoc.ChartClass);
        Assert.Equal("銷售趨勢圖", chartDoc.ChartTitle);
        Assert.Equal("end", chartDoc.LegendPosition);

        // 驗證 XML 屬性
        string? cellRange = chartDoc.ChartNode.GetAttribute("cell-range-address", OdfNamespaces.Table);
        Assert.Equal("LocalTable.A1:.B5", cellRange);

        // 驗證 GetChartDefinition
        var readDef = chartDoc.GetChartDefinition();
        Assert.Equal(OdfChartType.Line, readDef.ChartType);
        Assert.Equal("銷售趨勢圖", readDef.Title);
        Assert.True(readDef.HasLegend);
        Assert.Equal("LocalTable", readDef.DataRange.StartAddress.SheetName);
        Assert.Equal(0, readDef.DataRange.StartAddress.Row);
        Assert.Equal(0, readDef.DataRange.StartAddress.Column);
        Assert.Equal(4, readDef.DataRange.EndAddress.Row);
        Assert.Equal(1, readDef.DataRange.EndAddress.Column);
    }

    /// <summary>
    /// 驗證更新本地資料表格（UpdateData）時，產生的 XML 儲存格結構與型別標記正確。
    /// </summary>
    [Fact]
    public void UpdateDataWritesCorrectXmlStructure()
    {
        var definition = new OdfChartDefinition
        {
            ChartType = OdfChartType.Bar,
            Title = "業績統計",
            DataRange = new OdfCellRange(0, 0, 2, 2, "LocalTable"),
            HasLegend = false
        };

        using var chartDoc = ChartDocument.Create(definition);

        var date = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var data = new object?[][]
        {
            new object?[] { "季度", "銷量", "是否達標" },
            new object?[] { "Q1", 1250.5, true },
            new object?[] { "Q2", null, date }
        };

        chartDoc.UpdateData(data);

        // 驗證 XML 內容中是否包含正確 the table、row 與 cell
        using var stream = new MemoryStream();
        chartDoc.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string xml = reader.ReadToEnd();

        // 驗證包含 table-column、table-row、table-cell 等
        Assert.Contains("<table:table table:name=\"LocalTable\">", xml);
        Assert.Contains("<table:table-column />", xml);

        // 驗證儲存格內容
        Assert.Contains("office:value-type=\"string\"", xml);
        Assert.Contains("<text:p>季度</text:p>", xml);

        Assert.Contains("office:value-type=\"float\"", xml);
        Assert.Contains("office:value=\"1250.5\"", xml);
        Assert.Contains("<text:p>1250.5</text:p>", xml);

        Assert.Contains("office:value-type=\"boolean\"", xml);
        Assert.Contains("office:boolean-value=\"true\"", xml);
        Assert.Contains("<text:p>TRUE</text:p>", xml);

        Assert.Contains("office:value-type=\"date\"", xml);
        Assert.Contains("office:date-value=\"2026-06-15T12:00:00Z\"", xml);
        Assert.Contains("<text:p>2026-06-15T12:00:00Z</text:p>", xml);
    }

    /// <summary>
    /// 驗證圖表本地資料快取只在需要時掃描 DOM，並會在更新資料後失效。
    /// </summary>
    [Fact]
    public void LocalDataCacheLoadsOnDemandAndInvalidatesAfterUpdate()
    {
        using var chartDoc = ChartDocument.Create(new OdfChartDefinition
        {
            ChartType = OdfChartType.Bar,
            Title = "快取驗證",
            DataRange = new OdfCellRange(0, 0, 1, 1, "LocalTable")
        });

        chartDoc.UpdateData(new object?[][]
        {
            new object?[] { "季度", "銷量" },
            new object?[] { "Q1", 10d }
        });

        Assert.Equal(0, chartDoc.LocalDataCacheBuildCount);

        OdfChartDataCache first = chartDoc.GetLocalDataCache();

        Assert.Equal(1, chartDoc.LocalDataCacheBuildCount);
        Assert.Equal(2, first.Rows.Count);
        Assert.Equal("季度", first.Rows[0][0]);
        Assert.Equal("銷量", first.Rows[0][1]);
        Assert.Equal("Q1", first.Rows[1][0]);
        Assert.Equal(10d, first.Rows[1][1]);

        OdfChartDataCache second = chartDoc.GetLocalDataCache();

        Assert.Same(first, second);
        Assert.Equal(1, chartDoc.LocalDataCacheBuildCount);

        chartDoc.UpdateData(new object?[][]
        {
            new object?[] { "季度", "銷量" },
            new object?[] { "Q2", 25d }
        });

        Assert.Equal(1, chartDoc.LocalDataCacheBuildCount);

        OdfChartDataCache rebuilt = chartDoc.GetLocalDataCache();

        Assert.NotSame(first, rebuilt);
        Assert.Equal(2, chartDoc.LocalDataCacheBuildCount);
        Assert.Equal("Q2", rebuilt.Rows[1][0]);
        Assert.Equal(25d, rebuilt.Rows[1][1]);
    }

    // ── V-1: SetDataRange / GetDataRange / InsertChart ──────────────────────

    /// <summary>
    /// 驗證 SetDataRange 正確設定 chart:chart 的 table:cell-range-address 屬性。
    /// </summary>
    [Fact]
    public void SetDataRange_SetsChartCellRangeAttribute()
    {
        using var chartDoc = OdfChartDocument.Create();
        var range = new OdfCellRange(0, 0, 4, 1);

        chartDoc.SetDataRange("Sheet1", range);

        string? attr = chartDoc.ChartNode.GetAttribute("cell-range-address", OdfNamespaces.Table);
        Assert.Equal("Sheet1.$A$1:.$B$5", attr);
    }

    /// <summary>
    /// 驗證 GetDataRange 能正確還原 SetDataRange 所設定的範圍。
    /// </summary>
    [Fact]
    public void GetDataRange_ReturnsCorrectRange()
    {
        using var chartDoc = OdfChartDocument.Create();
        var originalRange = new OdfCellRange(0, 0, 4, 2);

        chartDoc.SetDataRange("Sales", originalRange);

        var (sheetName, range) = chartDoc.GetDataRange();

        Assert.Equal("Sales", sheetName);
        Assert.True(range.HasValue);
        var r = range!.Value;
        Assert.Equal(0, r.StartAddress.Row);
        Assert.Equal(0, r.StartAddress.Column);
        Assert.Equal(4, r.EndAddress.Row);
        Assert.Equal(2, r.EndAddress.Column);
    }

    /// <summary>
    /// 驗證 SetDataRange 建立正確的 chart:data-source 與 chart:series 節點。
    /// </summary>
    [Fact]
    public void SetDataRange_CreatesDataSourceAndSeriesNodes()
    {
        using var chartDoc = OdfChartDocument.Create();
        // A1:C5：A 欄為標籤，B/C 欄為資料序列，第 1 列為標頭
        var range = new OdfCellRange(0, 0, 4, 2);

        chartDoc.SetDataRange("Sheet1", range, firstRowAsHeader: true, firstColumnAsLabel: true);

        // 應有 2 個 series（B 欄、C 欄）
        var series = chartDoc.Series;
        Assert.Equal(2, series.Count);

        // 第一個 series 的資料範圍
        Assert.Equal("Sheet1.$B$2:.$B$5", series[0].ValuesCellRangeAddress);
        Assert.Equal("Sheet1.$B$1", series[0].LabelCellAddress);

        // 第二個 series
        Assert.Equal("Sheet1.$C$2:.$C$5", series[1].ValuesCellRangeAddress);
        Assert.Equal("Sheet1.$C$1", series[1].LabelCellAddress);

        // X 軸分類範圍
        string? catRange = chartDoc.CategoriesCellRangeAddress;
        Assert.Equal("Sheet1.$A$2:.$A$5", catRange);
    }

    /// <summary>
    /// 驗證 OdfTableSheet.InsertChart() 在 ODS 套件中建立嵌入圖表物件，
    /// 並可透過 GetDataRange() 驗證資料繫結已正確持久化。
    /// </summary>
    [Fact]
    public void InsertChart_EmbeddedChartDocumentPersistedInOds()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Data");

        // 填入 3×4 資料：A1=標題列，B-D 欄 2~4 列
        sheet.Cells["A1"].CellValue = "Name";
        sheet.Cells["B1"].CellValue = "Q1";
        sheet.Cells["C1"].CellValue = "Q2";
        sheet.Cells["A2"].CellValue = "Alpha";
        sheet.Cells["B2"].CellValue = 100d;
        sheet.Cells["C2"].CellValue = 200d;
        sheet.Cells["A3"].CellValue = "Beta";
        sheet.Cells["B3"].CellValue = 150d;
        sheet.Cells["C3"].CellValue = 250d;

        var dataRange = new OdfCellRange(0, 0, 2, 2);
        // InsertChart 回傳 OdfChartDocument，不可 Dispose（父文件管理生命週期）
        OdfChartDocument chartDoc = sheet.InsertChart(dataRange, OdfChartType.Bar);

        // 驗證資料繫結
        var (sheetName, range) = chartDoc.GetDataRange();
        Assert.Equal("Data", sheetName);
        Assert.True(range.HasValue);
        var r = range!.Value;
        Assert.Equal(0, r.StartAddress.Row);
        Assert.Equal(0, r.StartAddress.Column);
        Assert.Equal(2, r.EndAddress.Row);
        Assert.Equal(2, r.EndAddress.Column);

        // 儲存後重新開啟，驗證嵌入物件的 content.xml 存在
        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;

        using var pkg = OdfPackage.Open(ms, leaveOpen: true);
        Assert.True(pkg.HasEntry("Object 1/content.xml"));
        Assert.True(pkg.HasEntry("Object 1/mimetype"));

        using var contentStream = pkg.GetEntryStream("Object 1/content.xml");
        using var reader = new StreamReader(contentStream);
        string xml = reader.ReadToEnd();

        // 驗證 cell-range-address 正確寫入 content.xml
        Assert.Contains("Data.$A$1:.$C$3", xml);
        // 驗證 chart:series
        Assert.Contains("chart:series", xml);
    }

    /// <summary>
    /// 驗證 <see cref="OdfChartDocument.GetChartDefinition"/> 可讀回已設定的圖表屬性。
    /// </summary>
    [Fact]
    public void GetChartDefinition_RoundTripsAfterSetDataRange()
    {
        using var chartDoc = OdfChartDocument.Create();
        chartDoc.ChartClass = "chart:bar";
        chartDoc.ChartTitle = "季度銷售";
        chartDoc.SetLegend("end");
        chartDoc.SetDataRange("Sales", new OdfCellRange(0, 0, 5, 2));

        OdfChartDefinition definition = chartDoc.GetChartDefinition();
        Assert.Equal(OdfChartType.Bar, definition.ChartType);
        Assert.Equal("季度銷售", definition.Title);
        Assert.True(definition.HasLegend);
        Assert.Equal("Sales", definition.DataRange.StartAddress.SheetName);
        Assert.Equal(5, definition.DataRange.EndAddress.Row);
        Assert.Equal(2, definition.DataRange.EndAddress.Column);
    }

    /// <summary>
    /// 驗證嵌入 ODS 圖表可透過 <see cref="OdfChartDocument.GetChartDefinition"/> 讀回摘要。
    /// </summary>
    [Fact]
    public void GetChartDefinition_RoundTripsForEmbeddedChart()
    {
        using var doc = SpreadsheetDocument.Create();
        OdfTableSheet sheet = doc.AddSheet("Data");
        sheet.GetCell(0, 0).SetValue("標題");
        sheet.GetCell(0, 1).SetValue("數值");
        sheet.GetCell(1, 0).SetValue("A");
        sheet.GetCell(1, 1).SetValue(10d);

        OdfChartDocument chartDoc = sheet.InsertChart(new OdfCellRange(0, 0, 1, 1), OdfChartType.Line);
        chartDoc.ChartTitle = "嵌入折線圖";

        OdfChartDefinition definition = chartDoc.GetChartDefinition();
        Assert.Equal(OdfChartType.Line, definition.ChartType);
        Assert.Equal("嵌入折線圖", definition.Title);
        Assert.Equal("Data", definition.DataRange.StartAddress.SheetName);
    }

    /// <summary>
    /// 驗證座標軸進階屬性與序列個別屬性寫入。
    /// </summary>
    [Fact]
    public void AxisAndSeriesAdvancedEditing_WritesAttributes()
    {
        using var chartDoc = OdfChartDocument.Create();
        chartDoc.SetDataRange("Sales", new OdfCellRange(0, 0, 4, 2), firstRowAsHeader: true, firstColumnAsLabel: true);

        chartDoc.SetAxisTitle("y", "銷售額");
        chartDoc.SetAxisLogarithmic("y", true);
        chartDoc.SetAxisMinimum("y", 0);
        chartDoc.SetAxisMaximum("y", 1000);
        chartDoc.SetAxisReverseDirection("y", true);
        chartDoc.SetAxisDisplayLabels("y", true);
        chartDoc.SetAxisGrid("y", OdfChartGridKind.Major, true);
        chartDoc.SetAxisStyleName("y", "AxisStyle1");

        Assert.Equal(2, chartDoc.SeriesCount);
        OdfChartSeries series = chartDoc.GetSeriesEditor(0);
        series.SeriesClass = "chart:line";
        series.StyleName = "SeriesStyle1";
        series.AttachedAxis = "primary-y";

        using var stream = new MemoryStream();
        chartDoc.SaveToStream(stream);
        stream.Position = 0;
        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string xml = reader.ReadToEnd();

        Assert.Contains("chart:logarithmic=\"true\"", xml);
        Assert.Contains("chart:minimum=\"0\"", xml);
        Assert.Contains("chart:maximum=\"1000\"", xml);
        Assert.Contains("chart:reverse-direction=\"true\"", xml);
        Assert.Contains("chart:display-label=\"true\"", xml);
        Assert.Contains("chart:grid chart:class=\"major\"", xml);
        Assert.Contains("chart:style-name=\"AxisStyle1\"", xml);
        Assert.Contains("chart:class=\"chart:line\"", xml);
        Assert.Contains("chart:style-name=\"SeriesStyle1\"", xml);
        Assert.Contains("chart:attached-axis=\"primary-y\"", xml);

        OdfChartAxisInfo? axisInfo = chartDoc.GetAxisInfo("y");
        Assert.NotNull(axisInfo);
        Assert.Equal("銷售額", axisInfo!.Title);
        Assert.True(axisInfo.Logarithmic);
        Assert.True(axisInfo.ReverseDirection);
        Assert.Equal(0, axisInfo.Minimum);
        Assert.Equal(1000, axisInfo.Maximum);
        Assert.True(axisInfo.DisplayLabels);
        Assert.True(axisInfo.HasMajorGrid);
        Assert.Equal("AxisStyle1", axisInfo.StyleName);

        OdfChartSeriesInfo seriesInfo = chartDoc.Series[0];
        Assert.Equal("chart:line", seriesInfo.SeriesClass);
        Assert.Equal("SeriesStyle1", seriesInfo.StyleName);
        Assert.Equal("primary-y", seriesInfo.AttachedAxis);
    }

    /// <summary>
    /// 驗證圖表自動樣式可建立、指派至序列並於儲存／載入後讀回。
    /// </summary>
    [Fact]
    public void ChartStyle_CreateAssignAndRoundTrip()
    {
        using var chartDoc = OdfChartDocument.Create();
        chartDoc.SetDataRange("Sales", new OdfCellRange(0, 0, 4, 2), firstRowAsHeader: true, firstColumnAsLabel: true);

        OdfChartStyle seriesStyle = chartDoc.CreateChartStyle("SeriesRed");
        seriesStyle.FillColor = "#FF0000";
        seriesStyle.StrokeColor = "#000000";
        seriesStyle.StrokeWidth = "0.05cm";

        chartDoc.GetSeriesEditor(0).StyleName = "SeriesRed";

        using var stream = new MemoryStream();
        chartDoc.SaveToStream(stream);
        stream.Position = 0;

        using OdfChartDocument loaded = OdfChartDocument.Load(stream);
        OdfChartStyleInfo? styleInfo = loaded.TryGetChartStyle("SeriesRed");
        Assert.NotNull(styleInfo);
        Assert.Equal("#FF0000", styleInfo!.FillColor);
        Assert.Equal("#000000", styleInfo.StrokeColor);
        Assert.Equal("0.05cm", styleInfo.StrokeWidth);
        Assert.Equal("SeriesRed", loaded.Series[0].StyleName);
        Assert.Single(loaded.GetChartStyles());
    }

    /// <summary>
    /// 驗證 <see cref="OdfChartDocument.RemoveChartStyle"/> 可移除圖表自動樣式。
    /// </summary>
    [Fact]
    public void RemoveChartStyle_RemovesAutomaticStyle()
    {
        using var chartDoc = OdfChartDocument.Create();
        chartDoc.SetDataRange("Sales", new OdfCellRange(0, 0, 4, 2), firstRowAsHeader: true, firstColumnAsLabel: true);

        OdfChartStyle style = chartDoc.CreateChartStyle("TempStyle");
        style.FillColor = "#ABCDEF";
        chartDoc.GetSeriesEditor(0).StyleName = "TempStyle";

        Assert.True(chartDoc.RemoveChartStyle("TempStyle"));
        Assert.Empty(chartDoc.GetChartStyles());
        Assert.Null(chartDoc.TryGetChartStyle("TempStyle"));
        Assert.False(chartDoc.RemoveChartStyle("TempStyle"));
    }

    /// <summary>
    /// 驗證 SetDataRange 與 GetDataRange 對帶空格工作表名稱的往返一致性。
    /// </summary>
    [Fact]
    public void SetDataRange_SheetNameWithSpaces_RoundTrip()
    {
        using var chartDoc = OdfChartDocument.Create();
        var range = new OdfCellRange(0, 0, 9, 3);

        chartDoc.SetDataRange("My Sheet", range);

        var (sheetName, result) = chartDoc.GetDataRange();
        Assert.Equal("My Sheet", sheetName);
        Assert.True(result.HasValue);
        var r = result!.Value;
        Assert.Equal(0, r.StartAddress.Row);
        Assert.Equal(9, r.EndAddress.Row);
        Assert.Equal(3, r.EndAddress.Column);
    }

    /// <summary>
    /// 驗證 <see cref="OdfChartDocument.SetSeriesDataLabels"/> 與 <see cref="OdfChartDocument.GetSeriesDataLabels"/> 的往返一致性。
    /// </summary>
    [Fact]
    public void SeriesDataLabels_RoundTripsAfterSetAndSave()
    {
        using var chartDoc = OdfChartDocument.Create();
        chartDoc.SetDataRange("Sales", new OdfCellRange(0, 0, 4, 2), firstRowAsHeader: true, firstColumnAsLabel: true);

        var labels = new OdfChartDataLabelInfo(showValue: true, showPercentage: true, showCategoryName: true, showLegendKey: false);
        chartDoc.SetSeriesDataLabels(0, labels);

        Assert.Null(chartDoc.GetSeriesDataLabels(1));

        using var stream = new MemoryStream();
        chartDoc.SaveToStream(stream);
        stream.Position = 0;

        using OdfChartDocument loaded = OdfChartDocument.Load(stream);
        OdfChartDataLabelInfo? readLabels = loaded.GetSeriesDataLabels(0);
        Assert.NotNull(readLabels);
        Assert.True(readLabels!.ShowValue);
        Assert.True(readLabels.ShowPercentage);
        Assert.True(readLabels.ShowCategoryName);
        Assert.False(readLabels.ShowLegendKey);

        Assert.True(chartDoc.SeriesCount > 1);
        chartDoc.SetSeriesDataLabels(0, null);
        Assert.Null(chartDoc.GetSeriesDataLabels(0));
    }

    /// <summary>
    /// 驗證 <see cref="OdfChartDocument.SetWallStyleName"/> 與 <see cref="OdfChartDocument.SetFloorStyleName"/> 的往返一致性。
    /// </summary>
    [Fact]
    public void WallAndFloorStyleName_RoundTripsAfterSetAndSave()
    {
        using var chartDoc = OdfChartDocument.Create();
        chartDoc.SetDataRange("Sales", new OdfCellRange(0, 0, 4, 2));

        chartDoc.SetWallStyleName("WallStyle1");
        chartDoc.SetFloorStyleName("FloorStyle1");

        Assert.Equal("WallStyle1", chartDoc.GetWallStyleName());
        Assert.Equal("FloorStyle1", chartDoc.GetFloorStyleName());

        using var stream = new MemoryStream();
        chartDoc.SaveToStream(stream);
        stream.Position = 0;

        using OdfChartDocument loaded = OdfChartDocument.Load(stream);
        Assert.Equal("WallStyle1", loaded.GetWallStyleName());
        Assert.Equal("FloorStyle1", loaded.GetFloorStyleName());

        loaded.SetWallStyleName(null);
        Assert.Null(loaded.GetWallStyleName());
    }

    /// <summary>
    /// 驗證軸標籤格式相關樣式屬性（C-3）可往返。
    /// </summary>
    [Fact]
    public void AxisLabelPositionStyle_RoundTripsAfterSaveAndLoad()
    {
        using var chartDoc = OdfChartDocument.Create();
        chartDoc.SetDataRange("Sales", new OdfCellRange(0, 0, 4, 2));
        chartDoc.SetAxisStyleName("y", "YAxisStyle");
        OdfChartStyle style = chartDoc.CreateChartStyle("YAxisStyle");
        style.LabelPosition = "outside-end";
        style.LabelPositionNegative = "outside-start";
        style.AxisLabelPosition = "near-axis";

        using var stream = new MemoryStream();
        chartDoc.SaveToStream(stream);
        stream.Position = 0;

        using OdfChartDocument loaded = OdfChartDocument.Load(stream);
        OdfChartStyleInfo? info = loaded.TryGetChartStyle("YAxisStyle");
        Assert.NotNull(info);

        OdfChartStyle reopened = loaded.CreateChartStyle("YAxisStyle");
        Assert.Equal("outside-end", reopened.LabelPosition);
        Assert.Equal("outside-start", reopened.LabelPositionNegative);
        Assert.Equal("near-axis", reopened.AxisLabelPosition);
    }

    /// <summary>
    /// 驗證圖例對齊與圖例樣式（C-4）可往返。
    /// </summary>
    [Fact]
    public void LegendAlignmentAndStyle_RoundTripsAfterSaveAndLoad()
    {
        using var chartDoc = OdfChartDocument.Create();
        chartDoc.SetDataRange("Sales", new OdfCellRange(0, 0, 4, 2));
        chartDoc.SetLegend("end");
        chartDoc.LegendAlignment = "center";
        chartDoc.LegendStyle.FillColor = "#EEEEEE";

        using var stream = new MemoryStream();
        chartDoc.SaveToStream(stream);
        stream.Position = 0;

        using OdfChartDocument loaded = OdfChartDocument.Load(stream);
        Assert.Equal("center", loaded.LegendAlignment);
        Assert.Equal("#EEEEEE", loaded.LegendStyle.FillColor);
    }

    /// <summary>
    /// 驗證序列誤差棒（<c>chart:error-indicator</c>）、趨勢線（<c>chart:regression-curve</c>）
    /// 與平均值線（<c>chart:mean-value</c>）可設定、移除並於儲存／載入後保留，且元素順序符合
    /// OASIS ODF 1.4 schema 規定（domain、mean-value、regression-curve、error-indicator、
    /// data-point、data-label）。
    /// </summary>
    [Fact]
    public void SeriesErrorIndicatorRegressionCurveAndMeanValue_RoundTripAfterSaveAndLoad()
    {
        using var chartDoc = OdfChartDocument.Create();
        chartDoc.SetDataRange("Sales", new OdfCellRange(0, 0, 4, 2), firstRowAsHeader: true, firstColumnAsLabel: true);

        OdfChartSeries series = chartDoc.GetSeriesEditor(0);
        Assert.Null(series.GetErrorIndicator());
        Assert.Null(series.GetRegressionCurve());
        Assert.Null(series.GetMeanValue());

        series.SetMeanValue(new OdfChartMeanValueInfo("MeanStyle1"));
        series.SetRegressionCurve(new OdfChartRegressionCurveInfo("RegressionStyle1"));
        series.SetErrorIndicator(new OdfChartErrorIndicatorInfo("y", "ErrorStyle1"));
        series.AddDataPoint(1, "PointStyleA");

        using var stream = new MemoryStream();
        chartDoc.SaveToStream(stream);
        stream.Position = 0;
        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string xml = reader.ReadToEnd();

        int meanIndex = xml.IndexOf("chart:mean-value", StringComparison.Ordinal);
        int regressionIndex = xml.IndexOf("chart:regression-curve", StringComparison.Ordinal);
        int errorIndex = xml.IndexOf("chart:error-indicator", StringComparison.Ordinal);
        int dataPointIndex = xml.IndexOf("chart:data-point", StringComparison.Ordinal);
        Assert.True(meanIndex >= 0 && meanIndex < regressionIndex, "chart:mean-value 應位於 chart:regression-curve 之前。");
        Assert.True(regressionIndex < errorIndex, "chart:regression-curve 應位於 chart:error-indicator 之前。");
        Assert.True(errorIndex < dataPointIndex, "chart:error-indicator 應位於 chart:data-point 之前。");

        stream.Position = 0;
        using OdfChartDocument loaded = OdfChartDocument.Load(stream);
        OdfChartSeries loadedSeries = loaded.GetSeriesEditor(0);

        OdfChartMeanValueInfo? meanValue = loadedSeries.GetMeanValue();
        Assert.NotNull(meanValue);
        Assert.Equal("MeanStyle1", meanValue!.StyleName);

        OdfChartRegressionCurveInfo? regressionCurve = loadedSeries.GetRegressionCurve();
        Assert.NotNull(regressionCurve);
        Assert.Equal("RegressionStyle1", regressionCurve!.StyleName);

        OdfChartErrorIndicatorInfo? errorIndicator = loadedSeries.GetErrorIndicator();
        Assert.NotNull(errorIndicator);
        Assert.Equal("y", errorIndicator!.Dimension);
        Assert.Equal("ErrorStyle1", errorIndicator.StyleName);

        loadedSeries.SetErrorIndicator(null);
        loadedSeries.SetRegressionCurve(null);
        loadedSeries.SetMeanValue(null);
        Assert.Null(loadedSeries.GetErrorIndicator());
        Assert.Null(loadedSeries.GetRegressionCurve());
        Assert.Null(loadedSeries.GetMeanValue());
    }

    /// <summary>
    /// 驗證資料點樣式覆蓋（C-5）可新增、列舉並往返。
    /// </summary>
    [Fact]
    public void SeriesDataPoints_RoundTripsAfterSaveAndLoad()
    {
        using var chartDoc = OdfChartDocument.Create();
        chartDoc.SetDataRange("Sales", new OdfCellRange(0, 0, 4, 2), firstRowAsHeader: true, firstColumnAsLabel: true);

        OdfChartSeries series = chartDoc.GetSeriesEditor(0);
        series.AddDataPoint(2, "PointStyleA");
        series.AddDataPoint(1, "PointStyleB");

        using var stream = new MemoryStream();
        chartDoc.SaveToStream(stream);
        stream.Position = 0;

        using OdfChartDocument loaded = OdfChartDocument.Load(stream);
        var points = loaded.GetSeriesEditor(0).GetDataPoints();
        Assert.Equal(2, points.Count);
        Assert.Equal(2, points[0].Repeated);
        Assert.Equal("PointStyleA", points[0].StyleName);
        Assert.Equal(1, points[1].Repeated);
        Assert.Equal("PointStyleB", points[1].StyleName);

        loaded.GetSeriesEditor(0).ClearDataPoints();
        Assert.Empty(loaded.GetSeriesEditor(0).GetDataPoints());
    }

    /// <summary>
    /// 驗證 3D 光源與投影／照明樣式設定（C-6）可往返。
    /// </summary>
    [Fact]
    public void Lights_And_3DProjectionStyle_RoundTripsAfterSaveAndLoad()
    {
        using var chartDoc = OdfChartDocument.Create();
        chartDoc.SetDataRange("Sales", new OdfCellRange(0, 0, 4, 2));
        chartDoc.AddLight("(0 0 1)", "#FFFFFF", enabled: true, specular: false);
        chartDoc.PlotAreaStyle.Projection = OdfDr3dProjection.Perspective;
        chartDoc.PlotAreaStyle.LightingMode = true;

        using var stream = new MemoryStream();
        chartDoc.SaveToStream(stream);
        stream.Position = 0;

        using OdfChartDocument loaded = OdfChartDocument.Load(stream);
        OdfChartLightInfo light = Assert.Single(loaded.GetLights());
        Assert.Equal("(0 0 1)", light.Direction);
        Assert.Equal("#FFFFFF", light.DiffuseColor);
        Assert.True(light.Enabled);
        Assert.False(light.Specular);

        Assert.Equal(OdfDr3dProjection.Perspective, loaded.PlotAreaStyle.Projection);
        Assert.True(loaded.PlotAreaStyle.LightingMode);

        loaded.ClearLights();
        Assert.Empty(loaded.GetLights());
    }

    /// <summary>
    /// 驗證股票圖漲跌標記與範圍線樣式名稱（C-7）可往返。
    /// </summary>
    [Fact]
    public void StockMarkerStyleNames_RoundTripAfterSaveAndLoad()
    {
        using var chartDoc = OdfChartDocument.Create();
        chartDoc.SetDataRange("Stock", new OdfCellRange(0, 0, 4, 3));
        chartDoc.SetStockGainMarkerStyleName("GainStyle");
        chartDoc.SetStockLossMarkerStyleName("LossStyle");
        chartDoc.SetStockRangeLineStyleName("RangeStyle");

        using var stream = new MemoryStream();
        chartDoc.SaveToStream(stream);
        stream.Position = 0;

        using OdfChartDocument loaded = OdfChartDocument.Load(stream);
        Assert.Equal("GainStyle", loaded.GetStockGainMarkerStyleName());
        Assert.Equal("LossStyle", loaded.GetStockLossMarkerStyleName());
        Assert.Equal("RangeStyle", loaded.GetStockRangeLineStyleName());
    }

    /// <summary>
    /// 驗證對包含圖表的工作表呼交 RenderChartsToFallbackImages 時，能成功透過 ScottPlot 產生 PNG 影像，
    /// 寫入 OdfPackage 中，並正確更新 XML 以加入 draw:image 節點且不損壞既有的 draw:object 節點。
    /// </summary>
    [Fact]
    public void RenderChartsToFallbackImages_GeneratesPngAndUpdatesXml()
    {
        // 1. 建立試算表並填入資料
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("DataSheet");

        sheet.Cells["A1"].CellValue = "季度";
        sheet.Cells["B1"].CellValue = "銷售額";
        sheet.Cells["A2"].CellValue = "第一季";
        sheet.Cells["B2"].CellValue = 120.5d;
        sheet.Cells["A3"].CellValue = "第二季";
        sheet.Cells["B3"].CellValue = 250d;
        sheet.Cells["A4"].CellValue = "第三季";
        sheet.Cells["B4"].CellValue = 180.2d;
        sheet.Cells["A5"].CellValue = "第四季";
        sheet.Cells["B5"].CellValue = 300.9d;

        // 2. 插入一個長條圖
        var dataRange = new OdfCellRange(0, 0, 4, 1);
        var chartDoc = sheet.InsertChart(dataRange, OdfChartType.Bar);
        chartDoc.ChartTitle = "季度銷售統計圖";

        // 3. 執行圖表渲染 fallback
        doc.RenderChartsToFallbackImages();

        // 4. 驗證 PNG 影像是否已寫入 package
        string fallbackPath = "Pictures/chart-fallback-Object 1.png";
        Assert.True(doc.Package.HasEntry(fallbackPath), "應該在 package 中生成 fallback PNG 影像。");

        using (var pngStream = doc.Package.GetEntryStream(fallbackPath))
        {
            Assert.True(pngStream.Length > 0, "產生的 PNG 影像檔案大小應該大於 0。");
        }

        // 5. 驗證 Sheet XML 結構中 draw:frame 下同時存在 draw:object 與 draw:image
        var frames = sheet.TableNode.Descendants()
            .Where(c => c.NodeType == OdfNodeType.Element &&
                        c.LocalName == "frame" &&
                        c.NamespaceUri == OdfNamespaces.Draw)
            .ToList();

        Assert.Single(frames);
        var frame = frames[0];

        // 驗證原本的 draw:object 還在
        var objNode = frame.Children.Find(c =>
            c.NodeType == OdfNodeType.Element &&
            c.LocalName == "object" &&
            c.NamespaceUri == OdfNamespaces.Draw);
        Assert.NotNull(objNode);
        Assert.Equal("./Object 1", objNode.GetAttribute("href", OdfNamespaces.XLink));

        // 驗證新產生的 draw:image
        var imgNode = frame.Children.Find(c =>
            c.NodeType == OdfNodeType.Element &&
            c.LocalName == "image" &&
            c.NamespaceUri == OdfNamespaces.Draw);
        Assert.NotNull(imgNode);
        Assert.Equal(fallbackPath, imgNode.GetAttribute("href", OdfNamespaces.XLink));
    }

    /// <summary>
    /// 驗證統一圖例模型與既有圖例屬性可雙向同步。
    /// </summary>
    [Fact]
    public void LegendModel_SynchronizesWithLegacyLegendProperties()
    {
        using var chartDoc = OdfChartDocument.Create();
        chartDoc.Legend.IsVisible = true;
        chartDoc.Legend.Position = "end";
        chartDoc.Legend.Alignment = "center";
        chartDoc.Legend.StyleName = "LegendStyleA";

        Assert.Equal("end", chartDoc.LegendPosition);
        Assert.Equal("center", chartDoc.LegendAlignment);
        Assert.Equal("LegendStyleA", chartDoc.Legend.StyleName);

        chartDoc.LegendPosition = null;
        Assert.False(chartDoc.Legend.IsVisible);
    }

    /// <summary>
    /// 驗證圖表 Fluent builder 可鏈式設定圖表、圖例、座標軸與序列設定。
    /// </summary>
    [Fact]
    public void ChartBuilder_ConfiguresChartLegendAxisAndSeries()
    {
        using ChartDocument chart = ChartDocument.Builder()
            .WithType(OdfChartType.Bar)
            .WithTitle("年度營收")
            .WithDataRange("Sheet1", new OdfCellRange(0, 0, 4, 2), firstRowAsHeader: true)
            .WithLegend(position: "end")
            .WithAxis("y", axis => axis
                .WithTitle("營收（萬元）")
                .WithMinimum(0)
                .WithLogarithmic(false))
            .ConfigureSeries(0, series => series
                .WithStyle(style => style.FillColor = "#4472C4")
                .WithErrorIndicator(new OdfChartErrorIndicatorInfo("y", "ErrorStyle1")))
            .Build();

        Assert.Equal("chart:bar", chart.ChartClass);
        Assert.Equal("年度營收", chart.ChartTitle);
        Assert.Equal("end", chart.Legend.Position);
        Assert.Equal("營收（萬元）", chart.GetAxisTitle("y"));

        OdfChartAxisInfo? axisInfo = chart.GetAxisInfo("y");
        Assert.NotNull(axisInfo);
        Assert.Equal(0d, axisInfo!.Minimum);
        Assert.False(axisInfo.Logarithmic);

        OdfChartSeries seriesEditor = chart.GetSeriesEditor(0);
        Assert.Equal("#4472C4", seriesEditor.Style.FillColor);
        OdfChartErrorIndicatorInfo? indicator = seriesEditor.GetErrorIndicator();
        Assert.NotNull(indicator);
        Assert.Equal("y", indicator!.Dimension);
        Assert.Equal("ErrorStyle1", indicator.StyleName);
    }
}
