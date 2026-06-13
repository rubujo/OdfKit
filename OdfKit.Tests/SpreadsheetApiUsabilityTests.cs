using System;
using System.IO;
using System.IO.Compression;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定試算表高階 API 的易用入口。
/// </summary>
public class SpreadsheetApiUsabilityTests
{
    /// <summary>
    /// 驗證可用工作表與儲存格索引建立、保存並重新載入 ODS。
    /// </summary>
    [Fact]
    public void CreateLoadWorksheetsCellsFormulaAndStyle()
    {
        using var workbook = SpreadsheetDocument.Create();
        OdfTableSheet sheet = workbook.Worksheets.Add("Data");

        sheet.Cells["A1"].CellValue = "項目";
        sheet.Cells["B1"].CellValue = 42d;
        sheet.Cells["C1"].Formula = "of:=[.B1]*2";
        sheet.Cells["A1"].StyleName = "HeadingCell";

        using var stream = new MemoryStream();
        workbook.SaveToStream(stream);
        stream.Position = 0;

        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream);
        OdfTableSheet loadedSheet = loaded.Worksheets["Data"];

        Assert.Equal(1, loaded.Worksheets.Count);
        Assert.Equal("Data", loaded.Worksheets[0].Name);
        Assert.Equal("項目", loadedSheet.Cells["A1"].CellValue);
        Assert.Equal(42d, loadedSheet.Cells["B1"].CellValue);
        Assert.Equal("of:=[.B1]*2", loadedSheet.Cells["C1"].Formula);
        Assert.Equal("HeadingCell", loadedSheet.Cells["A1"].StyleName);
    }

    /// <summary>
    /// 驗證可直接保存到路徑再從路徑載入。
    /// </summary>
    [Fact]
    public void SaveAndLoadSpreadsheetByPath()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ods");
        try
        {
            using (var workbook = SpreadsheetDocument.Create())
            {
                workbook.Worksheets.Add("Sheet1").Cells[0, 0].CellValue = true;
                workbook.Save(path);
            }

            using SpreadsheetDocument loaded = SpreadsheetDocument.Load(path);

            Assert.Equal("Sheet1", loaded.Worksheets[0].Name);
            Assert.Equal(true, loaded.Worksheets["Sheet1"].Cells["A1"].CellValue);
            Assert.Equal("application/vnd.oasis.opendocument.spreadsheet", loaded.Package.MimeType);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>
    /// 驗證非 ODS 文件不會被誤載為試算表。
    /// </summary>
    [Fact]
    public void LoadRejectsNonSpreadsheetDocument()
    {
        using var stream = new MemoryStream();
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Text, leaveOpen: true))
        {
            package.Save();
        }

        stream.Position = 0;

        Assert.Throws<InvalidOperationException>(() => SpreadsheetDocument.Load(stream, "text.odt"));
    }

    /// <summary>
    /// 驗證串流寫入器會將列高與最佳列高設定輸出為自動列樣式。
    /// </summary>
    [Fact]
    public void WriteStartRow_Height_WritesAutomaticRowStyle()
    {
        using var stream = new MemoryStream();
        using (var writer = new OdsStreamWriter(stream))
        {
            writer.WriteStartSheet("資料");
            writer.WriteColumn(OdfLength.FromCentimeters(2.5));
            writer.WriteStartRow(height: 18.5, useOptimalHeight: true);
            writer.WriteCell("列高");
            writer.WriteEndRow();
            writer.WriteEndSheet();
        }

        stream.Position = 0;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        string contentXml = ReadZipEntry(zip, "content.xml");
        string stylesXml = ReadZipEntry(zip, "styles.xml");

        Assert.Contains("table:style-name=\"ro_auto_1\"", contentXml);
        Assert.Contains("style:name=\"ro_auto_1\"", stylesXml);
        Assert.Contains("style:family=\"table-row\"", stylesXml);
        Assert.Contains("style:row-height=\"18.5pt\"", stylesXml);
        Assert.Contains("style:use-optimal-row-height=\"true\"", stylesXml);
    }

    private static string ReadZipEntry(ZipArchive zip, string entryName)
    {
        var entry = zip.GetEntry(entryName);
        Assert.NotNull(entry);
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
