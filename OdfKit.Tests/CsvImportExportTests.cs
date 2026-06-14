using System;
using System.IO;
using System.Text;
using OdfKit.Csv;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 CSV 匯入與匯出 API。
/// </summary>
public class CsvImportExportTests
{
    private static Stream MakeCsvStream(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    /// <summary>
    /// 驗證 ImportFromStream 可正確讀取標題列與資料列。
    /// </summary>
    [Fact]
    public void ImportFromStream_WithHeaders_SetsHeadersAndDataCells()
    {
        const string csv = "名稱,數量,價格\n蘋果,10,25.5\n香蕉,5,12.0";
        using var stream = MakeCsvStream(csv);
        using var workbook = OdfCsvImporter.ImportFromStream(stream);

        Assert.Equal(1, workbook.Worksheets.Count);
        var sheet = workbook.Worksheets[0];
        Assert.Equal("名稱", sheet.Cells[0, 0].CellValue);
        Assert.Equal("數量", sheet.Cells[0, 1].CellValue);
        Assert.Equal("蘋果", sheet.Cells[1, 0].CellValue);
        Assert.Equal("10", sheet.Cells[1, 1].CellValue);
        Assert.Equal("香蕉", sheet.Cells[2, 0].CellValue);
    }

    /// <summary>
    /// 驗證自訂分隔字元（Tab）可正確解析。
    /// </summary>
    [Fact]
    public void ImportFromStream_TabDelimiter_ParsesCorrectly()
    {
        const string tsv = "A\tB\tC\n1\t2\t3";
        using var stream = MakeCsvStream(tsv);
        var options = new OdfCsvOptions { Delimiter = '\t' };
        using var workbook = OdfCsvImporter.ImportFromStream(stream, options);

        Assert.Equal("A", workbook.Worksheets[0].Cells[0, 0].CellValue);
        Assert.Equal("3", workbook.Worksheets[0].Cells[1, 2].CellValue);
    }

    /// <summary>
    /// 驗證 ExportToStream 輸出正確的 CSV 內容。
    /// </summary>
    [Fact]
    public void ExportToStream_RoundTripsCsvData()
    {
        using var workbook = SpreadsheetDocument.Create();
        var sheet = workbook.Worksheets.Add("Test");
        sheet.Cells[0, 0].CellValue = "欄 A";
        sheet.Cells[0, 1].CellValue = "欄 B";
        sheet.Cells[1, 0].CellValue = "值一";
        sheet.Cells[1, 1].CellValue = "值二";

        using var ms = new MemoryStream();
        OdfCsvExporter.ExportToStream(workbook, ms);
        string csv = Encoding.UTF8.GetString(ms.ToArray());

        Assert.Contains("欄 A", csv);
        Assert.Contains("欄 B", csv);
        Assert.Contains("值一", csv);
        Assert.Contains("值二", csv);
    }
}
