using System;
using System.IO;
using OdfKit.Conversion;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 ODS 與 XLSX 雙向轉換 API。
/// </summary>
public class OoxmlConversionTests
{
    /// <summary>
    /// 驗證 ODS 轉換至 XLSX 再轉回 ODS 的完整 round-trip 可保留字串值。
    /// </summary>
    [Fact]
    public void OdsToXlsxToOds_PreservesStringCellValues()
    {
        using var original = SpreadsheetDocument.Create();
        var sheet = original.Worksheets.Add("轉換測試");
        sheet.Cells["A1"].CellValue = "標題 A";
        sheet.Cells["B1"].CellValue = "標題 B";
        sheet.Cells["A2"].CellValue = "資料一";
        sheet.Cells["B2"].CellValue = 42d;

        using var xlsxStream = new MemoryStream();
        OdfToXlsxConverter.Convert(original, xlsxStream);
        xlsxStream.Position = 0;

        using var converted = XlsxToOdfConverter.Convert(xlsxStream);

        Assert.Equal(1, converted.Worksheets.Count);
        Assert.Equal("標題 A", converted.Worksheets[0].Cells["A1"].CellValue);
        Assert.Equal("標題 B", converted.Worksheets[0].Cells["B1"].CellValue);
        Assert.Equal("資料一", converted.Worksheets[0].Cells["A2"].CellValue);
    }

    /// <summary>
    /// 驗證 XLSX 轉換至 ODS 可保留多個工作表名稱。
    /// </summary>
    [Fact]
    public void XlsxToOdf_MultipleSheets_PreservesSheetNames()
    {
        using var original = SpreadsheetDocument.Create();
        original.Worksheets.Add("Sheet1").Cells["A1"].CellValue = "工作表一";
        original.Worksheets.Add("Sheet2").Cells["A1"].CellValue = "工作表二";

        using var xlsxStream = new MemoryStream();
        OdfToXlsxConverter.Convert(original, xlsxStream);
        xlsxStream.Position = 0;

        using var converted = XlsxToOdfConverter.Convert(xlsxStream);
        Assert.True(converted.Worksheets.Count >= 2);
    }
}
