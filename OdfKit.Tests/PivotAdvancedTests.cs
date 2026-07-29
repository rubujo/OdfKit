using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定進階樞紐分析表的物化、持久化、資源界線與跨版本相容性。
/// </summary>
public sealed class PivotAdvancedTests
{
    /// <summary>
    /// 驗證跨工作表來源可依月份分組、計算列百分比並物化雙軸總計。
    /// </summary>
    [Fact]
    public void RefreshGroupsDatesTransformsValuesAndWritesGrandTotalsAcrossSheets()
    {
        using SpreadsheetDocument document = CreateSalesDocument();
        OdfTableSheet output = document.Worksheets["Output"];

        OdfPivotRefreshResult result = CreateAdvancedBuilder(document)
            .Refresh(null, TestContext.Current.CancellationToken);

        Assert.Equal(4, result.SourceRows);
        Assert.Equal(4, result.GroupCount);
        Assert.Equal("2026-01", output.Cells[1, 0].CellValue);
        Assert.Equal(1d / 3d, Assert.IsType<double>(output.Cells[1, 1].CellValue), 12);
        Assert.Equal(2d / 3d, Assert.IsType<double>(output.Cells[1, 2].CellValue), 12);
        Assert.Equal(30d, output.Cells[1, 3].CellValue);
        Assert.Equal("2026-02", output.Cells[2, 0].CellValue);
        Assert.Equal(0.75d, output.Cells[2, 1].CellValue);
        Assert.Equal(0.25d, output.Cells[2, 2].CellValue);
        Assert.Equal(40d, output.Cells[2, 3].CellValue);
        Assert.Equal("Grand Total", output.Cells[3, 0].CellValue);
        Assert.Equal(40d, output.Cells[3, 1].CellValue);
        Assert.Equal(30d, output.Cells[3, 2].CellValue);
        Assert.Equal(70d, output.Cells[3, 3].CellValue);
    }

    /// <summary>
    /// 驗證進階設定可由強型別摘要讀回，且重新載入後仍可安全刷新。
    /// </summary>
    [Fact]
    public void AdvancedSettingsRoundTripAndPersistedPivotRefreshes()
    {
        using var stream = new MemoryStream();
        using (SpreadsheetDocument document = CreateSalesDocument())
        {
            OdfPivotTableBuilder builder = CreateAdvancedBuilder(document);
            builder.Refresh(null, TestContext.Current.CancellationToken);
            builder.Build();
            document.SaveToStream(stream);
        }

        stream.Position = 0;
        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream);
        OdfPivotTableInfo pivot = Assert.Single(loaded.GetPivotTables());
        Assert.Equal(OdfPivotGrandTotal.Both, pivot.GrandTotals);
        Assert.False(pivot.ShowFilterButton);
        Assert.True(pivot.DrillDownOnDoubleClick);
        OdfPivotTableFieldInfo dateField = Assert.Single(
            pivot.Fields,
            field => field.SourceFieldName == "Date");
        Assert.Equal(OdfPivotLayout.OutlineSubtotalsTop, dateField.Layout);
        Assert.Equal(OdfPivotDateGroup.Months, dateField.Grouping!.DateGroup);
        OdfPivotTableFieldInfo salesField = Assert.Single(
            pivot.Fields,
            field => field.SourceFieldName == "Sales");
        Assert.Equal(OdfPivotShowValuesAs.PercentageOfRowTotal, salesField.ValueOptions!.ShowValuesAs);

        loaded.Worksheets["Data"].Cells[1, 2].SetValue(20d);
        OdfPivotRefreshResult result = loaded.RefreshPivotTable(
            "SalesPivot",
            new OdfPivotRefreshOptions { MaximumSourceCells = 100 },
            TestContext.Current.CancellationToken);

        Assert.Equal(4, result.SourceRows);
        Assert.Equal(0.5d, loaded.Worksheets["Output"].Cells[1, 1].CellValue);
        Assert.Equal(40d, loaded.Worksheets["Output"].Cells[1, 3].CellValue);
        Assert.Equal(80d, loaded.Worksheets["Output"].Cells[3, 3].CellValue);
    }

    /// <summary>
    /// 驗證資源上限會在任何目標儲存格寫入前終止刷新。
    /// </summary>
    [Fact]
    public void RefreshOverBudgetDoesNotPartiallyOverwriteTarget()
    {
        using SpreadsheetDocument document = CreateSalesDocument();
        OdfTableSheet output = document.Worksheets["Output"];
        output.Cells[0, 0].SetValue("sentinel");

        Assert.Throws<InvalidOperationException>(
            () => CreateAdvancedBuilder(document).Refresh(
                new OdfPivotRefreshOptions { MaximumOutputCells = 4 },
                TestContext.Current.CancellationToken));

        Assert.Equal("sentinel", output.Cells[0, 0].CellValue);
        Assert.Null(output.Cells[0, 1].CellValue);
        Assert.Null(output.Cells[1, 0].CellValue);
    }

    /// <summary>
    /// 驗證分組上限與無效浮點數在建構階段即被拒絕。
    /// </summary>
    [Fact]
    public void GroupingRejectsUnboundedOrNonFiniteConfiguration()
    {
        using SpreadsheetDocument document = CreateSalesDocument();
        OdfTableSheet output = document.Worksheets["Output"];
        var source = new OdfCellRange(0, 0, 4, 2, "Data");
        var target = new OdfCellAddress(0, 0, "Output");

        Assert.ThrowsAny<ArgumentException>(
            () => new OdfPivotTableBuilder("P1", source, target, output)
                .GroupField("Sales", new OdfPivotGroupingOptions
                {
                    Start = 0,
                    End = 2_000_000,
                    Interval = 1,
                }));
        Assert.ThrowsAny<ArgumentException>(
            () => new OdfPivotTableBuilder("P2", source, target, output)
                .GroupField("Sales", new OdfPivotGroupingOptions
                {
                    Start = 0,
                    End = 100,
                    Interval = double.NaN,
                }));
    }

    /// <summary>
    /// 驗證其餘 Show Values As 模式使用已彙總的有界矩陣計算，不需重新掃描來源。
    /// </summary>
    [Theory]
    [InlineData(OdfPivotShowValuesAs.PercentageOfColumnTotal, 1, 1, 0.25d)]
    [InlineData(OdfPivotShowValuesAs.PercentageOfGrandTotal, 1, 1, 0.10d)]
    [InlineData(OdfPivotShowValuesAs.RunningTotal, 2, 1, 40d)]
    [InlineData(OdfPivotShowValuesAs.DifferenceFrom, 2, 1, 20d)]
    [InlineData(OdfPivotShowValuesAs.PercentageDifferenceFrom, 2, 1, 2d)]
    [InlineData(OdfPivotShowValuesAs.Index, 1, 1, 0.625d)]
    public void ShowValuesAsModesProduceExpectedValues(
        OdfPivotShowValuesAs mode,
        int resultRow,
        int resultColumn,
        double expected)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet data = document.Worksheets.Add("Data");
        OdfTableSheet output = document.Worksheets.Add("Output");
        data.Cells[0, 0].SetValue("Region");
        data.Cells[0, 1].SetValue("Quarter");
        data.Cells[0, 2].SetValue("Sales");
        object?[,] rows =
        {
            { "North", "Q1", 10d },
            { "North", "Q2", 30d },
            { "South", "Q1", 30d },
            { "South", "Q2", 30d },
        };
        for (int row = 0; row < rows.GetLength(0); row++)
        {
            for (int column = 0; column < rows.GetLength(1); column++)
                data.Cells[row + 1, column].CellValue = rows[row, column];
        }

        new OdfPivotTableBuilder(
                "ValueModes",
                new OdfCellRange(0, 0, 4, 2, "Data"),
                new OdfCellAddress(0, 0, "Output"),
                output)
            .AddRowField("Region")
            .AddColumnField("Quarter")
            .AddDataField("Sales")
            .ConfigureValueField("Sales", new OdfPivotValueOptions
            {
                ShowValuesAs = mode,
                BaseFieldName = mode is OdfPivotShowValuesAs.DifferenceFrom or
                    OdfPivotShowValuesAs.PercentageDifferenceFrom
                    ? "Region"
                    : null,
                BaseMemberName = mode is OdfPivotShowValuesAs.DifferenceFrom or
                    OdfPivotShowValuesAs.PercentageDifferenceFrom
                    ? "North"
                    : null,
            })
            .Refresh(null, TestContext.Current.CancellationToken);

        Assert.Equal(expected, Assert.IsType<double>(output.Cells[resultRow, resultColumn].CellValue), 12);
    }

    /// <summary>
    /// 驗證 ODF 1.0～1.4 官方 schema 均接受 DataPilot 的分組、版面與欄位參照。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf10)]
    [InlineData(OdfVersion.Odf11)]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void AdvancedPivotPassesOfficialSchemaForAllSupportedVersions(OdfVersion version)
    {
        using SpreadsheetDocument document = CreateSalesDocument();
        document.TargetVersion = version;
        CreateAdvancedBuilder(document).Build();

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        string contentXml = ReadContentXml(stream);
        XNamespace table = OdfNamespaces.Table;
        XElement pivot = XDocument.Parse(contentXml)
            .Descendants(table + "data-pilot-table")
            .Single();
        OdfSchemaPatternValidationResult result = OdfSchemaPatternValidator.ValidateElement(
            pivot,
            OdfSchemaRegistry.GetSchema(version),
            "table-data-pilot-table");

        Assert.True(
            result.IsMatch,
            string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)) +
            Environment.NewLine +
            contentXml);
    }

    private static SpreadsheetDocument CreateSalesDocument()
    {
        SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet data = document.Worksheets.Add("Data");
        OdfTableSheet output = document.Worksheets.Add("Output");
        output.Cells[0, 0].SetValue("Output");
        data.Cells[0, 0].SetValue("Date");
        data.Cells[0, 1].SetValue("Region");
        data.Cells[0, 2].SetValue("Sales");
        data.Cells[1, 0].SetValue(new DateTime(2026, 1, 5));
        data.Cells[1, 1].SetValue("North");
        data.Cells[1, 2].SetValue(10d);
        data.Cells[2, 0].SetValue(new DateTime(2026, 1, 20));
        data.Cells[2, 1].SetValue("South");
        data.Cells[2, 2].SetValue(20d);
        data.Cells[3, 0].SetValue(new DateTime(2026, 2, 3));
        data.Cells[3, 1].SetValue("North");
        data.Cells[3, 2].SetValue(30d);
        data.Cells[4, 0].SetValue(new DateTime(2026, 2, 14));
        data.Cells[4, 1].SetValue("South");
        data.Cells[4, 2].SetValue(10d);
        return document;
    }

    private static OdfPivotTableBuilder CreateAdvancedBuilder(SpreadsheetDocument document) =>
        new OdfPivotTableBuilder(
            "SalesPivot",
            new OdfCellRange(0, 0, 4, 2, "Data"),
            new OdfCellAddress(0, 0, "Output"),
            document.Worksheets["Output"])
            .AddRowField("Date")
            .GroupField("Date", new OdfPivotGroupingOptions { DateGroup = OdfPivotDateGroup.Months })
            .AddColumnField("Region")
            .AddDataField("Sales")
            .ConfigureValueField("Sales", new OdfPivotValueOptions
            {
                ShowValuesAs = OdfPivotShowValuesAs.PercentageOfRowTotal,
            })
            .WithGrandTotals(OdfPivotGrandTotal.Both)
            .WithLayout(OdfPivotLayout.OutlineSubtotalsTop)
            .WithFilterButton(false)
            .WithDrillDown(true);

    private static string ReadContentXml(MemoryStream stream)
    {
        stream.Position = 0;
        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream content = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(content);
        string result = reader.ReadToEnd();
        stream.Position = 0;
        return result;
    }
}
