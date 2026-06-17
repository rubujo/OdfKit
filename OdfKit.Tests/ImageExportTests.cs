using System;
using System.IO;
using OdfKit.DOM;
using OdfKit.Export;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 ODS 工作表轉換至 PNG 與 JPEG 影像匯出 API。
/// </summary>
public class ImageExportTests
{
    /// <summary>
    /// 驗證 ExportToPng 輸出非空的 PNG 位元組流。
    /// </summary>
    [Fact]
    public void ExportToPng_WithCellData_ProducesNonEmptyPngStream()
    {
        using var workbook = SpreadsheetDocument.Create();
        var sheet = workbook.Worksheets.Add("圖表");
        sheet.Cells["A1"].CellValue = "名稱";
        sheet.Cells["B1"].CellValue = "數值";
        sheet.Cells["A2"].CellValue = "項目 A";
        sheet.Cells["B2"].CellValue = 100d;

        using var ms = new MemoryStream();
        OdfImageExporter.ExportToPng(sheet, ms);

        Assert.True(ms.Length > 100, "PNG 輸出不得為空。");
        ms.Position = 0;
        byte[] sig = new byte[4];
        ms.Read(sig, 0, 4);
        Assert.Equal(0x89, sig[0]);
        Assert.Equal(0x50, sig[1]);
        Assert.Equal(0x4E, sig[2]);
        Assert.Equal(0x47, sig[3]);
    }

    /// <summary>
    /// 驗證 ExportToJpeg 輸出非空且符合 JFIF 格式的位元組流。
    /// </summary>
    [Fact]
    public void ExportToJpeg_WithCellData_ProducesNonEmptyJpegStream()
    {
        using var workbook = SpreadsheetDocument.Create();
        var sheet = workbook.Worksheets.Add("圖表");
        sheet.Cells["A1"].CellValue = "JPEG 測試";

        using var ms = new MemoryStream();
        OdfImageExporter.ExportToJpeg(sheet, ms, quality: 85);

        Assert.True(ms.Length > 100, "JPEG 輸出不得為空。");
        ms.Position = 0;
        Assert.Equal(0xFF, ms.ReadByte());
        Assert.Equal(0xD8, ms.ReadByte());
    }

    /// <summary>
    /// 驗證 quality 超出範圍時拋出 ArgumentOutOfRangeException。
    /// </summary>
    [Fact]
    public void ExportToJpeg_InvalidQuality_ThrowsArgumentOutOfRangeException()
    {
        using var workbook = SpreadsheetDocument.Create();
        var sheet = workbook.Worksheets.Add("Sheet1");
        using var ms = new MemoryStream();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OdfImageExporter.ExportToJpeg(sheet, ms, quality: 0));
    }

    /// <summary>
    /// 驗證匯出影像時不會改變工作表的 DOM 結構（匯出前後 TableNode 的 XML 內容完全相同）。
    /// </summary>
    [Fact]
    public void Export_DoesNotMutateDocumentStructure()
    {
        using var workbook = SpreadsheetDocument.Create();
        var sheet = workbook.Worksheets.Add("測試工作表");

        sheet.Cells["A1"].CellValue = "名稱";
        sheet.Cells["B1"].CellValue = "數值";
        sheet.Cells["A2"].CellValue = "項目 A";
        sheet.Cells["B2"].CellValue = 100d;

        using var msBefore = new MemoryStream();
        OdfXmlWriter.Write(sheet.TableNode, msBefore);
        byte[] bytesBefore = msBefore.ToArray();

        using var ms = new MemoryStream();
        OdfImageExporter.ExportToPng(sheet, ms);

        using var msAfter = new MemoryStream();
        OdfXmlWriter.Write(sheet.TableNode, msAfter);
        byte[] bytesAfter = msAfter.ToArray();

        Assert.Equal(bytesBefore, bytesAfter);
    }
}
