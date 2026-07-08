using System.IO;
using ClosedXML.Excel;
using MiniExcelLibs;
using OdfKit.Spreadsheet;

namespace OdfKit.Benchmarks;

/// <summary>
/// Shared write implementations reused by both the BenchmarkDotNet-driven
/// <see cref="CompetitiveStreamWriteBenchmarks"/> class and the standalone manual timing runner.
/// 供 BenchmarkDotNet 驅動的 <see cref="CompetitiveStreamWriteBenchmarks"/> 類別與獨立手動計時執行器共用的寫入實作。
/// </summary>
/// <remarks>
/// This is a cross-format reference comparison, not a same-format contest: OdfKit writes ODF
/// spreadsheets (.ods) while MiniExcel and ClosedXML write OOXML spreadsheets (.xlsx). Both container
/// formats are ZIP + XML, but the schemas differ, so absolute byte-for-byte output size is not
/// directly comparable; see docs/performance-comparison.md for the full methodology discussion.
/// 這是跨格式參考對比，而非同格式對決：OdfKit 寫入 ODF 試算表（.ods），MiniExcel 與 ClosedXML 寫入
/// OOXML 試算表（.xlsx）。兩者容器皆為 ZIP + XML，但 schema 不同，因此輸出檔案的絕對位元組大小不能直接
/// 對等比較；完整方法論討論請見 docs/performance-comparison.md。
/// </remarks>
internal static class CompetitiveStreamWriters
{
    /// <summary>
    /// Writes the competitive benchmark dataset using <see cref="OdsStreamWriter"/> (streaming ODS writer).
    /// 使用 <see cref="OdsStreamWriter"/>（串流式 ODS 寫入器）寫入跨套件對比基準資料集。
    /// </summary>
    /// <param name="output">The destination stream. / 目標資料流。</param>
    internal static void WriteOdsStreamWriter(Stream output)
    {
        using var writer = new OdsStreamWriter(output);
        writer.WriteStartSheet("Data");
        foreach (CompetitiveBenchmarkRow row in CompetitiveBenchmarkData.GenerateRows())
        {
            writer.WriteStartRow();
            writer.WriteCell((double)row.Id);
            writer.WriteCell(row.Name);
            writer.WriteCell(row.Amount);
            writer.WriteCell((double)row.Quantity);
            writer.WriteCell(row.OrderDate);
            writer.WriteCell(row.IsActive);
            writer.WriteCell(row.Score);
            writer.WriteCell(row.Category);
            writer.WriteCell((double)row.SequenceNumber);
            writer.WriteCell(row.Notes);
            writer.WriteEndRow();
        }

        writer.WriteEndSheet();
    }

    /// <summary>
    /// Writes the competitive benchmark dataset using MiniExcel's streaming <c>SaveAs</c> API.
    /// 使用 MiniExcel 的串流式 <c>SaveAs</c> API 寫入跨套件對比基準資料集。
    /// </summary>
    /// <param name="output">The destination stream. / 目標資料流。</param>
    internal static void WriteMiniExcel(Stream output) =>
        // printHeader: false，使三個情境皆恰好輸出 1,000,000 個資料列，避免因表頭列而產生列數落差。
        output.SaveAs(CompetitiveBenchmarkData.GenerateRows(), printHeader: false, excelType: ExcelType.XLSX);

    /// <summary>
    /// Writes the competitive benchmark dataset using ClosedXML's in-memory DOM writer, used here as
    /// the non-streaming (whole-workbook-in-memory) control group.
    /// 使用 ClosedXML 的記憶體內 DOM 寫入器寫入跨套件對比基準資料集，於此作為非串流（整份活頁簿常駐記憶體）的對照組。
    /// </summary>
    /// <param name="output">The destination stream. / 目標資料流。</param>
    internal static void WriteClosedXml(Stream output)
    {
        using var workbook = new XLWorkbook();
        IXLWorksheet sheet = workbook.AddWorksheet("Data");
        int rowIndex = 1;
        foreach (CompetitiveBenchmarkRow row in CompetitiveBenchmarkData.GenerateRows())
        {
            sheet.Cell(rowIndex, 1).Value = row.Id;
            sheet.Cell(rowIndex, 2).Value = row.Name;
            sheet.Cell(rowIndex, 3).Value = row.Amount;
            sheet.Cell(rowIndex, 4).Value = row.Quantity;
            sheet.Cell(rowIndex, 5).Value = row.OrderDate;
            sheet.Cell(rowIndex, 6).Value = row.IsActive;
            sheet.Cell(rowIndex, 7).Value = row.Score;
            sheet.Cell(rowIndex, 8).Value = row.Category;
            sheet.Cell(rowIndex, 9).Value = row.SequenceNumber;
            sheet.Cell(rowIndex, 10).Value = row.Notes;
            rowIndex++;
        }

        workbook.SaveAs(output);
    }
}
