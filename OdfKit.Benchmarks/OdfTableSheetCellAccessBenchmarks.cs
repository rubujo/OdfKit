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

    /// <summary>
    /// 「先以 <see cref="OdfTableSheet.InsertRows(int, int)"/> 批次插入以 <c>number-rows-repeated</c>
    /// 壓縮表示的大量空白列，再對整張表隨機（非循序）存取儲存格」的混合情境。此模式會讓列／儲存格存取
    /// 快取一旦遇到壓縮節點，之後每次呼叫都回退為全表重掃引擎路徑；此基準量化該回退路徑的實際成本。
    /// </summary>
    [Benchmark]
    public long RandomAccessAfterBulkInsertRepeatedRows()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Data");

        for (int row = 0; row < 20; row++)
        {
            sheet.GetCell(row, 0).SetValue($"seed:{row}");
        }

        sheet.InsertRows(10, 5_000);

        long touched = 0;
        int totalRows = 20 + 5_000;
        for (int i = 0; i < 2_000; i++)
        {
            int row = (i * 97) % totalRows;
            int col = (i * 31) % Columns;
            sheet.GetCell(row, col).SetValue($"{row}:{col}:{i}");
            touched++;
        }

        return touched;
    }
}
