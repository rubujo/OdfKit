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
            writer.WriteStartRow(height: 18.5, useOptimalHeight: false);
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
        Assert.Contains("style:row-height=\"0.6526cm\"", stylesXml);
    }

    private static string ReadZipEntry(ZipArchive zip, string entryName)
    {
        var entry = zip.GetEntry(entryName);
        Assert.NotNull(entry);
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// 驗證 SplitPanes 寫入 config-item 並可 round-trip。
    /// </summary>
    [Fact]
    public void SplitPanes_WritesConfigItemsToSettingsXml()
    {
        using var workbook = SpreadsheetDocument.Create();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.SplitPanes(splitRow: 3, splitColumn: 2);

        using var ms = new MemoryStream();
        workbook.SaveToStream(ms);
        ms.Position = 0;

        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(ms);
        using var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
        var entry = zip.GetEntry("settings.xml");
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry!.Open());
        string xml = reader.ReadToEnd();
        Assert.Contains("HorizontalSplitMode", xml);
        Assert.Contains("VerticalSplitMode", xml);
    }

    /// <summary>
    /// 驗證 AddSparklineGroup 寫入 calcext:sparkline-groups 並可 round-trip。
    /// </summary>
    [Fact]
    public void AddSparklineGroup_WritesCalcExtSparklineGroupXml()
    {
        using var workbook = SpreadsheetDocument.Create();
        var sheet = workbook.Worksheets.Add("Data");
        sheet.Cells["A1"].CellValue = 10d;
        sheet.Cells["A2"].CellValue = 20d;
        sheet.Cells["A3"].CellValue = 15d;

        var dataRange = OdfCellRange.ParseExcel("A1:A3");
        var hostCell = OdfCellAddress.ParseExcel("B1");
        sheet.AddSparklineGroup(dataRange, hostCell, SparklineType.Line);

        using var ms = new MemoryStream();
        workbook.SaveToStream(ms);
        ms.Position = 0;

        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(ms);
        using var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
        var entry = zip.GetEntry("content.xml");
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry!.Open());
        string xml = reader.ReadToEnd();
        Assert.Contains("sparkline-groups", xml);
        Assert.Contains("sparkline-group", xml);
        Assert.Contains("dataRangeRef", xml);
    }
}
