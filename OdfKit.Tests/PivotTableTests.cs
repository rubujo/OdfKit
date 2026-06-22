using System.IO;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 V-2 樞紐分析表完整性 API 的整合測試。
/// </summary>
public class PivotTableTests
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

    private static OdfNode BuildBasicPivot(OdfTableSheet sheet, string name = "Pivot1")
    {
        var sourceRange = new OdfCellRange(
            new OdfCellAddress(0, 0, "Sheet1"),
            new OdfCellAddress(9, 3, "Sheet1"));
        var targetStart = new OdfCellAddress(12, 0, "Sheet1");

        return new OdfPivotTableBuilder(name, sourceRange, targetStart, sheet)
            .AddRowField("Category")
            .AddColumnField("Region")
            .AddDataField("Sales")
            .Build();
    }

    /// <summary>
    /// 驗證基本欄位（Row/Column/Data/Page）與 XML 節點結構正確。
    /// </summary>
    [Fact]
    public void Build_BasicFields_XmlContainsCorrectOrientation()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        var sourceRange = new OdfCellRange(new OdfCellAddress(0, 0, "Sheet1"), new OdfCellAddress(9, 3, "Sheet1"));

        new OdfPivotTableBuilder("P1", sourceRange, new OdfCellAddress(12, 0, "Sheet1"), sheet)
            .AddRowField("Category")
            .AddColumnField("Region")
            .AddDataField("Sales", OdfPivotFunction.Sum)
            .AddPageField("Year")
            .Build();

        string xml = GetContentXml(doc);

        Assert.Contains("table:data-pilot-table", xml);
        Assert.Contains("table:orientation=\"row\"", xml);
        Assert.Contains("table:orientation=\"column\"", xml);
        Assert.Contains("table:orientation=\"data\"", xml);
        Assert.Contains("table:orientation=\"page\"", xml);
        Assert.Contains("table:function=\"sum\"", xml);
    }

    /// <summary>
    /// 驗證所有 OdfPivotFunction 列舉值正確轉換為 ODF function 屬性字串。
    /// </summary>
    [Theory]
    [InlineData(OdfPivotFunction.Sum, "sum")]
    [InlineData(OdfPivotFunction.Count, "count")]
    [InlineData(OdfPivotFunction.Average, "average")]
    [InlineData(OdfPivotFunction.Max, "max")]
    [InlineData(OdfPivotFunction.Min, "min")]
    public void AddDataField_AllFunctions_WritesCorrectFunctionAttribute(OdfPivotFunction fn, string expected)
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        var src = new OdfCellRange(new OdfCellAddress(0, 0, "Sheet1"), new OdfCellAddress(5, 2, "Sheet1"));

        new OdfPivotTableBuilder("Pivot", src, new OdfCellAddress(8, 0, "Sheet1"), sheet)
            .AddDataField("Amount", fn)
            .Build();

        string xml = GetContentXml(doc);

        Assert.Contains($"table:function=\"{expected}\"", xml);
    }

    /// <summary>
    /// 驗證計算欄位寫入 function="formula" 及 formula 屬性。
    /// </summary>
    [Fact]
    public void AddCalculatedField_WritesFormulaAttribute()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        var src = new OdfCellRange(new OdfCellAddress(0, 0, "Sheet1"), new OdfCellAddress(9, 3, "Sheet1"));

        new OdfPivotTableBuilder("P1", src, new OdfCellAddress(12, 0, "Sheet1"), sheet)
            .AddCalculatedField("Profit", "of:[.Revenue]-[.Cost]")
            .Build();

        string xml = GetContentXml(doc);

        Assert.Contains("table:function=\"formula\"", xml);
        Assert.Contains("table:formula=\"of:[.Revenue]-[.Cost]\"", xml);
    }

    /// <summary>
    /// 驗證排序設定寫入正確的 table:sort-info/table:sort-field 結構。
    /// </summary>
    [Fact]
    public void AddSortInfo_WritesCorrectSortNodes()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        var src = new OdfCellRange(new OdfCellAddress(0, 0, "Sheet1"), new OdfCellAddress(9, 3, "Sheet1"));

        new OdfPivotTableBuilder("P1", src, new OdfCellAddress(12, 0, "Sheet1"), sheet)
            .AddRowField("Category")
            .AddDataField("Sales")
            .AddSortInfo("Sales", ascending: false)
            .Build();

        string xml = GetContentXml(doc);

        Assert.Contains("table:sort-info", xml);
        Assert.Contains("table:sort-field", xml);
        Assert.Contains("table:source-field-name=\"Sales\"", xml);
        Assert.Contains("table:order=\"descending\"", xml);
    }

    /// <summary>
    /// 驗證升冪排序寫入 order="ascending"。
    /// </summary>
    [Fact]
    public void AddSortInfo_Ascending_WritesAscendingOrder()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        var src = new OdfCellRange(new OdfCellAddress(0, 0, "Sheet1"), new OdfCellAddress(9, 1, "Sheet1"));

        new OdfPivotTableBuilder("P1", src, new OdfCellAddress(12, 0, "Sheet1"), sheet)
            .AddRowField("Name")
            .AddSortInfo("Name", ascending: true)
            .Build();

        string xml = GetContentXml(doc);
        Assert.Contains("table:order=\"ascending\"", xml);
    }

    /// <summary>
    /// 驗證篩選條件寫入正確的 table:filter/table:filter-condition 結構。
    /// </summary>
    [Fact]
    public void AddFilter_WritesFilterConditionNode()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        var src = new OdfCellRange(new OdfCellAddress(0, 0, "Sheet1"), new OdfCellAddress(9, 3, "Sheet1"));

        new OdfPivotTableBuilder("P1", src, new OdfCellAddress(12, 0, "Sheet1"), sheet)
            .AddRowField("Region")
            .AddFilter("Region", OdfPivotFilterOperator.Equal, "North")
            .Build();

        string xml = GetContentXml(doc);

        Assert.Contains("table:filter", xml);
        Assert.Contains("table:filter-condition", xml);
        Assert.Contains("table:value=\"North\"", xml);
        Assert.Contains("table:operator=\"=\"", xml);
    }

    /// <summary>
    /// 驗證各篩選運算子正確轉換為 ODF 字串。
    /// </summary>
    [Theory]
    [InlineData(OdfPivotFilterOperator.Equal, "=")]
    [InlineData(OdfPivotFilterOperator.NotEqual, "!=")]
    [InlineData(OdfPivotFilterOperator.GreaterThan, "&gt;")]
    [InlineData(OdfPivotFilterOperator.GreaterThanOrEqual, "&gt;=")]
    [InlineData(OdfPivotFilterOperator.LessThan, "&lt;")]
    [InlineData(OdfPivotFilterOperator.LessThanOrEqual, "&lt;=")]
    public void AddFilter_AllOperators_WritesCorrectOperatorString(OdfPivotFilterOperator op, string expected)
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        var src = new OdfCellRange(new OdfCellAddress(0, 0, "Sheet1"), new OdfCellAddress(9, 1, "Sheet1"));

        new OdfPivotTableBuilder("P1", src, new OdfCellAddress(12, 0, "Sheet1"), sheet)
            .AddRowField("Score")
            .AddFilter("Score", op, "100")
            .Build();

        string xml = GetContentXml(doc);
        Assert.Contains($"table:operator=\"{expected}\"", xml);
    }

    /// <summary>
    /// 驗證 WithColumnHeaders(false) 寫入 has-column-headers="false"。
    /// </summary>
    [Fact]
    public void WithColumnHeaders_False_WritesCorrectAttribute()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        var src = new OdfCellRange(new OdfCellAddress(0, 0, "Sheet1"), new OdfCellAddress(5, 1, "Sheet1"));

        new OdfPivotTableBuilder("P1", src, new OdfCellAddress(8, 0, "Sheet1"), sheet)
            .WithColumnHeaders(false)
            .AddRowField("A")
            .Build();

        string xml = GetContentXml(doc);
        Assert.Contains("table:has-column-headers=\"false\"", xml);
    }

    /// <summary>
    /// 驗證預設情況下 has-column-headers="true" 與 has-row-headers="true"。
    /// </summary>
    [Fact]
    public void Build_DefaultHeaders_WritesTrue()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        BuildBasicPivot(sheet);

        string xml = GetContentXml(doc);
        Assert.Contains("table:has-column-headers=\"true\"", xml);
        Assert.Contains("table:has-row-headers=\"true\"", xml);
    }

    /// <summary>
    /// 驗證 WithRowHeaders(false) 寫入 has-row-headers="false"。
    /// </summary>
    [Fact]
    public void WithRowHeaders_False_WritesCorrectAttribute()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        var src = new OdfCellRange(new OdfCellAddress(0, 0, "Sheet1"), new OdfCellAddress(5, 1, "Sheet1"));

        new OdfPivotTableBuilder("P1", src, new OdfCellAddress(8, 0, "Sheet1"), sheet)
            .WithRowHeaders(false)
            .AddColumnField("A")
            .Build();

        string xml = GetContentXml(doc);
        Assert.Contains("table:has-row-headers=\"false\"", xml);
        // WithRowHeaders(false) 不應影響 has-column-headers，應維持預設 true。
        Assert.Contains("table:has-column-headers=\"true\"", xml);
    }

    /// <summary>
    /// 驗證 WithRowHeaders 與 WithColumnHeaders 可同時設為 false，且彼此互不干擾。
    /// </summary>
    [Fact]
    public void WithRowHeadersAndWithColumnHeaders_BothFalse_WritesBothAttributes()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        var src = new OdfCellRange(new OdfCellAddress(0, 0, "Sheet1"), new OdfCellAddress(5, 1, "Sheet1"));

        new OdfPivotTableBuilder("P1", src, new OdfCellAddress(8, 0, "Sheet1"), sheet)
            .WithRowHeaders(false)
            .WithColumnHeaders(false)
            .AddDataField("A")
            .Build();

        string xml = GetContentXml(doc);
        Assert.Contains("table:has-row-headers=\"false\"", xml);
        Assert.Contains("table:has-column-headers=\"false\"", xml);
    }

    /// <summary>
    /// 驗證多個篩選條件可同時存在於同一個 table:filter 節點中。
    /// </summary>
    [Fact]
    public void AddFilter_MultipleConditions_AllWrittenInSameFilterNode()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        var src = new OdfCellRange(new OdfCellAddress(0, 0, "Sheet1"), new OdfCellAddress(9, 3, "Sheet1"));

        new OdfPivotTableBuilder("P1", src, new OdfCellAddress(12, 0, "Sheet1"), sheet)
            .AddRowField("Region")
            .AddFilter("Year", OdfPivotFilterOperator.GreaterThanOrEqual, "2020")
            .AddFilter("Sales", OdfPivotFilterOperator.GreaterThan, "1000")
            .Build();

        string xml = GetContentXml(doc);

        Assert.Contains("table:value=\"2020\"", xml);
        Assert.Contains("table:value=\"1000\"", xml);
        int filterCount = 0;
        int start = 0;
        while ((start = xml.IndexOf("table:filter-condition", start, System.StringComparison.Ordinal)) >= 0)
        {
            filterCount++;
            start++;
        }
        Assert.Equal(2, filterCount);
    }
}
