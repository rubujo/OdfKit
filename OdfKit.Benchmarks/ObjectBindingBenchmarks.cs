using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using OdfKit.Spreadsheet;

namespace OdfKit.Benchmarks;

/// <summary>
/// 物件繫結（ImportRecords）效能基準（量測用，尚未納入 eng/Benchmark-Regression.ps1 回歸硬閘門）。
/// </summary>
[MemoryDiagnoser]
public class ObjectBindingBenchmarks
{
    private const int RecordCount = 50_000;
    private List<BenchmarkRecord> _records = [];

    [GlobalSetup]
    public void Setup()
    {
        _records = new List<BenchmarkRecord>(RecordCount);
        for (int i = 0; i < RecordCount; i++)
        {
            _records.Add(new BenchmarkRecord { Name = $"Item-{i:D7}", Value = i * 1.5d });
        }
    }

    [Benchmark]
    public int ImportRecords()
    {
        using SpreadsheetDocument workbook = SpreadsheetDocument.Create();
        workbook.Worksheets.Add("Data");
        OdfObjectBindingReport report = workbook.ImportRecords("Data", "A1", _records);
        return report.RowCount;
    }

    private sealed class BenchmarkRecord
    {
        public string Name { get; set; } = string.Empty;

        public double Value { get; set; }
    }
}
