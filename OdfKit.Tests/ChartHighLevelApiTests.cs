using System;
using System.IO;
using System.Linq;
using OdfKit.Chart;
using OdfKit.Core;
using OdfKit.DOM;
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
        Assert.Equal("[LocalTable.A1:.B5]", cellRange);

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
}
