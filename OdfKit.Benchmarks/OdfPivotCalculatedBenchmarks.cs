using BenchmarkDotNet.Attributes;
using OdfKit.Spreadsheet;

namespace OdfKit.Benchmarks;

/// <summary>
/// Measures bounded pivot aggregation with source and calculated data fields.
/// 量測來源欄位與計算欄位的有界樞紐彙總。
/// </summary>
[MemoryDiagnoser]
public class OdfPivotCalculatedBenchmarks
{
    private SpreadsheetDocument? _document;
    private OdfPivotTableBuilder? _standard;
    private OdfPivotTableBuilder? _calculated;
    private OdfPivotTableBuilder? _groupedPercentage;
    private OdfPivotRefreshOptions? _options;

    /// <summary>
    /// Creates ten thousand source records and reusable pivot definitions.
    /// 建立一萬筆來源記錄與可重用的樞紐定義。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = _document.Worksheets.Add("Data");
        sheet.Cells[0, 0].SetValue("Category");
        sheet.Cells[0, 1].SetValue("Revenue");
        sheet.Cells[0, 2].SetValue("Cost");
        for (int row = 1; row <= 10_000; row++)
        {
            sheet.Cells[row, 0].SetValue("C" + (row % 100).ToString(System.Globalization.CultureInfo.InvariantCulture));
            sheet.Cells[row, 1].SetValue((double)(row % 1000));
            sheet.Cells[row, 2].SetValue((double)(row % 400));
        }
        var source = new OdfCellRange(0, 0, 10_000, 2, "Data");
        _standard = new OdfPivotTableBuilder(
                "Standard",
                source,
                new OdfCellAddress(10_010, 0, "Data"),
                sheet)
            .AddRowField("Category")
            .AddDataField("Revenue");
        _calculated = new OdfPivotTableBuilder(
                "Calculated",
                source,
                new OdfCellAddress(10_010, 4, "Data"),
                sheet)
            .AddRowField("Category")
            .AddCalculatedField("Profit", "of:=[.Revenue]-[.Cost]");
        _groupedPercentage = new OdfPivotTableBuilder(
                "GroupedPercentage",
                source,
                new OdfCellAddress(10_010, 8, "Data"),
                sheet)
            .AddRowField("Category")
            .AddColumnField("Cost")
            .GroupField("Cost", new OdfPivotGroupingOptions
            {
                Start = 0,
                End = 400,
                Interval = 50,
            })
            .AddDataField("Revenue")
            .ConfigureValueField("Revenue", new OdfPivotValueOptions
            {
                ShowValuesAs = OdfPivotShowValuesAs.PercentageOfRowTotal,
            })
            .WithGrandTotals(OdfPivotGrandTotal.Both);
        _options = new OdfPivotRefreshOptions();
    }

    /// <summary>
    /// Materializes a source-field aggregate.
    /// 物化來源欄位彙總。
    /// </summary>
    /// <returns>The refresh report. / 刷新報告。</returns>
    [Benchmark(Baseline = true)]
    public OdfPivotRefreshResult StandardAggregate() =>
        _standard!.Refresh(_options, default);

    /// <summary>
    /// Materializes a calculated-field aggregate.
    /// 物化計算欄位彙總。
    /// </summary>
    /// <returns>The refresh report. / 刷新報告。</returns>
    [Benchmark]
    public OdfPivotRefreshResult CalculatedAggregate() =>
        _calculated!.Refresh(_options, default);

    /// <summary>
    /// Materializes grouped percentages and both grand-total axes.
    /// 物化分組百分比與雙軸總計。
    /// </summary>
    /// <returns>The refresh report. / 刷新報告。</returns>
    [Benchmark]
    public OdfPivotRefreshResult GroupedPercentageAndGrandTotals() =>
        _groupedPercentage!.Refresh(_options, default);

    /// <summary>
    /// Releases the benchmark document.
    /// 釋放基準文件。
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document?.Dispose();
    }
}
