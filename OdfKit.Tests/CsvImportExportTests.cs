using System;
using System.IO;
using System.Text;
using OdfKit.Csv;
using OdfKit.Spreadsheet;
using OdfKit.Core;
using OdfKit.DOM;
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

    /// <summary>
    /// 驗證當 ODS 含有極大的 row 或 column 重複次數時， CSV 匯出器不會發生 OOM 且能在合理時間內返回。
    /// </summary>
    [Fact]
    public void ScanCellValues_LargeRepeatCount_DoesNotOOM()
    {
        using var workbook = SpreadsheetDocument.Create();
        var sheet = workbook.Worksheets.Add("Sheet1");
        
        // 建立一個有極大 rows-repeated 的 table-row 元素，並附帶一個 active cell
        var tableNS = OdfNamespaces.Table;
        var rowNode = OdfNodeFactory.CreateElement("table-row", tableNS, "table");
        rowNode.SetAttribute("number-rows-repeated", tableNS, "2000000000", "table");
        
        var cellNode = OdfNodeFactory.CreateElement("table-cell", tableNS, "table");
        cellNode.SetAttribute("value-type", OdfNamespaces.Office, "string", "office");
        
        var pNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        pNode.TextContent = "測試資料";
        cellNode.AppendChild(pNode);
        rowNode.AppendChild(cellNode);
        
        sheet.TableNode.AppendChild(rowNode);

        using var ms = new MemoryStream();
        var startTime = DateTime.UtcNow;
        
        // 執行匯出，驗證是否能在合理時間（e.g. 10秒）內返回，且不發生 OutOfMemoryException
        OdfCsvExporter.ExportToStream(workbook, ms);
        
        var elapsed = DateTime.UtcNow - startTime;
        Assert.True(elapsed.TotalSeconds < 10, $"匯出操作花費了 {elapsed.TotalSeconds} 秒，疑似發生無窮迴圈或處理過大重複次數。");
    }
}
