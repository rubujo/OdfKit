using System.IO;
using OdfKit.Chart;
using OdfKit.Core;
using OdfKit.Database;
using OdfKit.DOM;
using OdfKit.Formula;
using OdfKit.Image;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證次要格式（ODC／ODB／ODF／ODI）Wave 2 DEPTH-2 高階 API 的整合場景。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Scenario)]
public class SecondaryFormatApiScenarioTests
{
    /// <summary>
    /// 驗證 ODC 圖表軸與序列進階寫入 API 可於儲存／載入後讀回。
    /// </summary>
    [Fact]
    public void ChartScenario_AxisAdvancedEditingSurvivesRoundTrip()
    {
        using var chartDoc = OdfChartDocument.Create();
        chartDoc.ChartClass = "chart:bar";
        chartDoc.ChartTitle = "季度銷售";
        chartDoc.SetDataRange("Sales", new OdfCellRange(0, 0, 4, 2), firstRowAsHeader: true, firstColumnAsLabel: true);

        chartDoc.SetAxisTitle("y", "銷售額");
        chartDoc.SetAxisMaximum("y", 500);
        chartDoc.SetAxisGrid("y", OdfChartGridKind.Major, true);
        OdfChartStyle axisStyle = chartDoc.CreateChartStyle("AxisGridStyle");
        axisStyle.StrokeColor = "#808080";
        chartDoc.SetAxisStyleName("y", "AxisGridStyle");
        if (chartDoc.SeriesCount > 0)
        {
            OdfChartStyle seriesStyle = chartDoc.CreateChartStyle("SeriesBarStyle");
            seriesStyle.FillColor = "#3366CC";
            seriesStyle.StrokeColor = "#000000";
            OdfChartSeries series = chartDoc.GetSeriesEditor(0);
            series.SeriesClass = "chart:bar";
            series.StyleName = "SeriesBarStyle";
        }

        using var stream = new MemoryStream();
        chartDoc.SaveToStream(stream);
        stream.Position = 0;

        using OdfChartDocument loaded = OdfChartDocument.Load(stream, "chart.odc");
        OdfChartAxisInfo? axisInfo = loaded.FindAxisInfo("y");
        Assert.NotNull(axisInfo);
        Assert.Equal("銷售額", axisInfo!.Title);
        Assert.Equal(500, axisInfo.Maximum);
        Assert.True(axisInfo.HasMajorGrid);
        if (loaded.SeriesCount > 0)
        {
            Assert.Equal("chart:bar", loaded.Series[0].SeriesClass);
            Assert.Equal("SeriesBarStyle", loaded.Series[0].StyleName);
            OdfChartStyleInfo? seriesStyle = loaded.TryGetChartStyle("SeriesBarStyle");
            Assert.NotNull(seriesStyle);
            Assert.Equal("#3366CC", seriesStyle!.FillColor);
        }

        OdfChartStyleInfo? axisStyleInfo = loaded.TryGetChartStyle("AxisGridStyle");
        Assert.NotNull(axisStyleInfo);
        Assert.Equal("#808080", axisStyleInfo!.StrokeColor);
    }

    /// <summary>
    /// 驗證 ODB 連線、資料表、查詢、表單與資料來源設定 API 可於儲存／載入後讀回。
    /// </summary>
    [Fact]
    public void DatabaseScenario_ConnectionTablesQueriesFormsSurviveRoundTrip()
    {
        using var database = OdfDatabaseDocument.Create();
        database.SetConnection("sdbc:embedded:hsqldb");
        database.AddTable("Customers", "SELECT * FROM customers");
        database.AddQuery(
            "ActiveCustomers",
            "SELECT * FROM customers WHERE active = TRUE",
            title: "有效客戶",
            description: "僅列出有效客戶。",
            escapeProcessing: true);
        database.AddDataSourceSetting("DatabaseName", OdfDatabaseDataSourceSettingType.String, "sales_db");
        database.AddForm("CustomerForm", "forms/CustomerForm", "客戶表單", "維護客戶資料。");
        database.AddReport("SalesReport", "reports/SalesReport", "銷售報表", "每月銷售摘要。");

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using OdfDatabaseDocument loaded = OdfDatabaseDocument.Load(stream, "database.odb");
        Assert.Equal("sdbc:embedded:hsqldb", loaded.ConnectionHref);

        OdfDatabaseTableInfo table = Assert.Single(loaded.GetTables());
        Assert.Equal("Customers", table.Name);
        Assert.Equal("SELECT * FROM customers", table.Command);

        OdfDatabaseQueryInfo query = Assert.Single(loaded.GetQueries());
        Assert.Equal("ActiveCustomers", query.Name);
        Assert.Equal("有效客戶", query.Title);
        Assert.True(query.EscapeProcessing);

        OdfDatabaseDataSourceSettingInfo setting = Assert.Single(loaded.GetDataSourceSettings());
        Assert.Equal("DatabaseName", setting.Name);
        Assert.Equal("sales_db", Assert.Single(setting.Values));

        OdfDatabaseFormInfo form = Assert.Single(loaded.GetForms());
        Assert.Equal("CustomerForm", form.Name);
        Assert.Equal("客戶表單", form.Title);

        OdfDatabaseReportInfo report = Assert.Single(loaded.GetReports());
        Assert.Equal("SalesReport", report.Name);
        Assert.Equal("銷售報表", report.Title);
    }

    /// <summary>
    /// 驗證 ODF 公式 token 與 MathML 寫入 API 可於儲存／載入後讀回。
    /// </summary>
    [Fact]
    public void FormulaScenario_MathTokensSurviveRoundTrip()
    {
        using OdfFormulaDocument formula = OdfFormulaDocument.Builder()
            .WithTokens(
                OdfMathToken.Identifier("E"),
                OdfMathToken.Operator("="),
                OdfMathToken.Identifier("m"),
                OdfMathToken.Superscript(
                    OdfMathToken.Identifier("c"),
                    OdfMathToken.Number("2")))
            .Build();

        using var stream = new MemoryStream();
        formula.SaveToStream(stream);
        stream.Position = 0;

        using OdfFormulaDocument loaded = OdfFormulaDocument.Load(stream, "equation.odf");
        Assert.Equal(4, loaded.GetMathTokens().Count);
        Assert.Equal(OdfMathTokenKind.Superscript, loaded.GetMathTokens()[3].Kind);
        Assert.Equal("c", loaded.GetMathTokens()[3].Base?.Text);
        Assert.Equal("2", loaded.GetMathTokens()[3].Script?.Text);
        Assert.Contains("msup", loaded.GetMathML());
        Assert.Equal("math", loaded.MathNode.LocalName);
        Assert.Equal("E", FindMathChild(loaded.MathNode, "mi")?.TextContent);
    }

    /// <summary>
    /// 驗證 ODI 多框架影像與版面 API 可於儲存／載入後讀回。
    /// </summary>
    [Fact]
    public void ImageScenario_MultiFrameLayoutSurvivesRoundTrip()
    {
        byte[] primary = CreatePngBytes();
        byte[] secondary = CreateAlternatePngBytes();

        using var image = OdfImageDocument.Create();
        image.SetImageLayout(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(6),
            OdfLength.FromCentimeters(4),
            "PrimaryFrame",
            "主要照片",
            "主要影像描述。");
        image.SetImage(primary, "Primary.png");
        image.AddImageFrame(
            secondary,
            OdfLength.FromCentimeters(7),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(3),
            "Secondary.png",
            "SecondaryFrame",
            "附圖");

        using var stream = new MemoryStream();
        image.SaveToStream(stream);
        stream.Position = 0;

        using OdfImageDocument loaded = OdfImageDocument.Load(stream, "gallery.odi");
        Assert.Equal(2, loaded.GetImageFrames().Count);

        OdfImageFrameInfo primaryFrame = loaded.GetImageFrames()[0];
        Assert.Equal("PrimaryFrame", primaryFrame.Name);
        Assert.Equal("主要照片", primaryFrame.Title);
        Assert.Equal("image/png", primaryFrame.MediaType);
        Assert.True(primaryFrame.TryGetWidth(out OdfLength width));
        Assert.Equal(OdfLength.FromCentimeters(6), width);

        Assert.True(loaded.UpdateImageFrame(
            "SecondaryFrame",
            OdfLength.FromCentimeters(8),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(4),
            "更新附圖"));

        OdfImageFrameInfo? secondaryFrame = loaded.TryGetImageFrame("SecondaryFrame");
        Assert.NotNull(secondaryFrame);
        Assert.Equal("更新附圖", secondaryFrame!.Title);
        Assert.True(secondaryFrame.TryGetX(out OdfLength x));
        Assert.Equal(OdfLength.FromCentimeters(8), x);
        Assert.Equal(secondary.Length, secondaryFrame.Size);

        Assert.True(loaded.RemoveImageFrame("SecondaryFrame"));
        Assert.Single(loaded.GetImageFrames());
    }

    private static byte[] CreatePngBytes() =>
        System.Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");

    private static byte[] CreateAlternatePngBytes() =>
        System.Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAEklEQVR42mNkYGD4z0ABYBw1KgBvXQV0AAAAAElFTkSuQmCC");

    private static OdfNode? FindMathChild(OdfNode parent, string localName)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == "http://www.w3.org/1998/Math/MathML")
            {
                return child;
            }

            OdfNode? nested = FindMathChild(child, localName);
            if (nested is not null)
                return nested;
        }

        return null;
    }
}
