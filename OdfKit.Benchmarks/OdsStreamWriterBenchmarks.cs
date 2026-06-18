using BenchmarkDotNet.Attributes;
using OdfKit.Spreadsheet;

namespace OdfKit.Benchmarks;

/// <summary>
/// <see cref="OdsStreamWriter"/> 大量列寫入效能基準。
/// </summary>
[MemoryDiagnoser]
public class OdsStreamWriterBenchmarks
{
    private const int RowCount = 10_000;
    private MemoryStream _outputStream = null!;

    [GlobalSetup]
    public void Setup() => _outputStream = new MemoryStream();

    [IterationSetup]
    public void IterationSetup()
    {
        _outputStream.SetLength(0);
        _outputStream.Position = 0;
    }

    [Benchmark]
    public long WriteRows()
    {
        using var writer = new OdsStreamWriter(_outputStream);
        writer.WriteStartSheet("Data");
        for (int row = 0; row < RowCount; row++)
        {
            writer.WriteStartRow();
            writer.WriteCell(row);
            writer.WriteCell($"值 {row}");
            writer.WriteEndRow();
        }

        writer.WriteEndSheet();
        writer.Dispose();
        return _outputStream.Length;
    }
}
