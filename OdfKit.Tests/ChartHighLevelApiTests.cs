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
        using var reader = new System.IO.StreamReader(contentStream);
        string xml = reader.ReadToEnd();

        // 驗證 cell-range-address 正確寫入 content.xml
        Assert.Contains("Data.$A$1:.$C$3", xml);
        // 驗證 chart:series
        Assert.Contains("chart:series", xml);
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
}
