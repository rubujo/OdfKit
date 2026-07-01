using BenchmarkDotNet.Attributes;
using OdfKit.Spreadsheet;

namespace OdfKit.Benchmarks;

/// <summary>
/// <see cref="OdfTableSheet.GetCell(int, int)"/> 逐格填值效能基準（對照 collaboration 大型表格重播發現的
/// OdfTable 全表重掃問題，驗證 Spreadsheet 端的 OdfTableSheetDomAccessEngine 是否有相同模式）。
/// </summary>
[MemoryDiagnoser]
public class OdfTableSheetCellAccessBenchmarks
{
    private const int Rows = 1_000;
    private const int Columns = 20;

    [Benchmark]
    public long FillSheetCellByCell()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Data");
        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Columns; col++)
            {
                sheet.GetCell(row, col).SetValue($"{row}:{col}");
            }
        }

        return (long)Rows * Columns;
    }
}
