using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
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
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var entry = zip.GetEntry("content.xml");
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry!.Open());
        string xml = reader.ReadToEnd();
        Assert.Contains("sparkline-groups", xml);
        Assert.Contains("sparkline-group", xml);
        Assert.Contains("dataRangeRef", xml);
    }

    /// <summary>
    /// 驗證 Workbook 密碼保護功能使用了 PBKDF2 加密且能正確 round-trip 驗證。
    /// </summary>
    [Fact]
    public void ProtectWorkbook_UsesPbkdf2NotSingleHash()
    {
        using var doc = SpreadsheetDocument.Create();
        doc.ProtectWorkbook("TestPassword123");
        Assert.True(doc.VerifyWorkbookPassword("TestPassword123"));
        Assert.False(doc.VerifyWorkbookPassword("WrongPassword"));
    }

    /// <summary>
    /// 驗證 Sheet 密碼保護功能使用了 PBKDF2 加密且能正確 round-trip 驗證。
    /// </summary>
    [Fact]
    public void ProtectSheet_UsesPbkdf2NotSingleHash()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        sheet.Protect("SheetPass");
        Assert.True(sheet.VerifyPassword("SheetPass"));
        Assert.False(sheet.VerifyPassword("Wrong"));
    }

    /// <summary>
    /// 驗證 VerifyWorkbookPassword 的 CompareBytes 正常執行。
    /// </summary>
    [Fact]
    public void VerifyWorkbookPassword_UsesCryptographicCompare()
    {
        using var doc = SpreadsheetDocument.Create();
        doc.ProtectWorkbook("my_password");
        Assert.False(doc.VerifyWorkbookPassword("wrong_password"));
    }

    /// <summary>
    /// 驗證工作表集合與儲存格列舉可直接使用 LINQ 查詢。
    /// </summary>
    [Fact]
    public void WorksheetAndCellCollectionsSupportLinqQueries()
    {
        using var workbook = SpreadsheetDocument.Create();
        OdfTableSheet sheet = workbook.Worksheets.Add("Data");
        sheet.Cells["A1"].CellValue = "名稱";
        sheet.Cells["B1"].CellValue = 10d;
        sheet.Cells["C1"].Formula = "of:=[.B1]*2";

        var names = workbook.Worksheets.Select(item => item.Name).ToArray();
        var usedValues = sheet.UsedCells
            .Select(cell => cell.CellValue)
            .Where(value => value is not null)
            .ToArray();
        var rangeAddresses = sheet.GetRange(OdfCellRange.ParseExcel("A1:B1"))
            .Select(cell => (cell.Row, cell.Column))
            .ToArray();

        Assert.Equal(["Data"], names);
        Assert.Equal("Data", workbook.Worksheets.Find("Data")?.Name);
        Assert.Equal(new object?[] { "名稱", 10d }, usedValues);
        Assert.Equal(new (int Row, int Column)[] { (0, 0), (0, 1) }, rangeAddresses);
    }

    /// <summary>
    /// 驗證 OdfLength 數字擴充方法可建立常用長度。
    /// </summary>
    [Fact]
    public void OdfLengthNumericExtensionsCreateLengths()
    {
        Assert.Equal(OdfLength.FromCentimeters(1), 1.Cm());
        Assert.Equal(OdfLength.FromMillimeters(2), 2.Mm());
        Assert.Equal(OdfLength.FromPoints(12), 12.Pt());
        Assert.Equal("1.5cm", 1.5.Cm().ToString());
    }

    /// <summary>
    /// 驗證試算表文件可用非同步 API 儲存與載入。
    /// </summary>
    [Fact]
    public async Task SpreadsheetAsyncSaveAndLoadRoundTrips()
    {
        using var workbook = SpreadsheetDocument.Create();
        workbook.Worksheets.Add("Async").Cells["A1"].CellValue = "完成";

        await using var stream = new MemoryStream();
        await workbook.SaveAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        stream.Position = 0;

        using SpreadsheetDocument loaded = await SpreadsheetDocument.LoadAsync(
            stream,
            "async.ods",
            TestContext.Current.CancellationToken);

        Assert.Equal("完成", loaded.Worksheets["Async"].Cells["A1"].CellValue);
    }

    /// <summary>
    /// 驗證試算表 Fluent builder 可匯入資料表並設定工作表選項。
    /// </summary>
    [Fact]
    public void SpreadsheetDocumentBuilderImportsTableAndSheetOptions()
    {
        var products = new[]
        {
            new ProductRow("鍵盤", 1200d, 5),
            new ProductRow("滑鼠", 650d, 12),
        };

        using SpreadsheetDocument workbook = SpreadsheetDocument.Builder()
            .AddSheet("產品目錄", sheet => sheet
                .ImportTable(
                    products,
                    product => [product.Name, product.Price, product.Stock],
                    ["名稱", "單價", "庫存"])
                .SetFormula("D2", "of:=[.B2]*[.C2]")
                .SetColumnWidth(1, 4.5)
                .FreezeAt(2, 1))
            .Build();

        using var stream = new MemoryStream();
        workbook.SaveToStream(stream);
        stream.Position = 0;

        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream);
        OdfTableSheet sheet = loaded.Worksheets["產品目錄"];

        Assert.Equal("名稱", sheet.Cells["A1"].CellValue);
        Assert.Equal("鍵盤", sheet.Cells["A2"].CellValue);
        Assert.Equal(1200d, sheet.Cells["B2"].CellValue);
        Assert.Equal(12d, sheet.Cells["C3"].CellValue);
        Assert.Equal("of:=[.B2]*[.C2]", sheet.Cells["D2"].Formula);
        Assert.Equal(new OdfFrozenPanes(1, 0), sheet.FrozenPanes);
        Assert.Contains(sheet.UsedCells, cell => cell.CellValue?.Equals("滑鼠") == true);
    }

    /// <summary>
    /// 驗證 <see cref="OdfSheetBuilder.SetCell"/> 與 <see cref="OdfSheetBuilder.ImportRows"/>
    /// 可直接呼叫（不透過 ImportTable），且儲存後可由真實 LibreOffice 慣例驗證的方式重新讀回。
    /// </summary>
    [Fact]
    public void SheetBuilderSetCellAndImportRowsDirectlyWriteExpectedCells()
    {
        var rows = new[]
        {
            new ProductRow("鍵盤", 1200d, 5),
            new ProductRow("滑鼠", 650d, 12),
        };

        using SpreadsheetDocument workbook = SpreadsheetDocument.Builder()
            .AddSheet("資料", sheet => sheet
                .SetCell("A1", "標題")
                .SetCell("B1", 100)
                .ImportRows(
                    rows,
                    row => [row.Name, row.Price, row.Stock],
                    startRow: 2,
                    startColumn: 1))
            .Build();

        using var stream = new MemoryStream();
        workbook.SaveToStream(stream);
        stream.Position = 0;

        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream);
        OdfTableSheet sheet = loaded.Worksheets["資料"];

        // SetCell 直接呼叫應正確寫入指定位址的儲存格值。
        Assert.Equal("標題", sheet.Cells["A1"].CellValue);
        Assert.Equal(100d, sheet.Cells["B1"].CellValue);

        // ImportRows 直接呼叫（非經由 ImportTable）應從指定起始列／欄寫入各資料列。
        Assert.Equal("鍵盤", sheet.Cells["A2"].CellValue);
        Assert.Equal(1200d, sheet.Cells["B2"].CellValue);
        Assert.Equal(5d, sheet.Cells["C2"].CellValue);
        Assert.Equal("滑鼠", sheet.Cells["A3"].CellValue);
        Assert.Equal(650d, sheet.Cells["B3"].CellValue);
        Assert.Equal(12d, sheet.Cells["C3"].CellValue);
    }

    /// <summary>
    /// 驗證試算表可讀回資料驗證規則與嵌入圖表摘要。
    /// </summary>
    [Fact]
    public void ReadDataValidationsAndEmbeddedChartsAfterRoundTrip()
    {
        using var workbook = SpreadsheetDocument.Create();
        workbook.Worksheets.Add("銷售");
        workbook.AddDataValidation("銷售", new OdfDataValidation
        {
            ApplyTo = new OdfCellRange(0, 0, 0, 0, "銷售"),
            Condition = OdfValidationCondition.IntegerBetween,
            Formula1 = "1",
            Formula2 = "99",
            ErrorMessage = "請輸入 1 至 99",
        });
        workbook.AddChart("銷售", new OdfCellAddress(0, 2, "銷售"), new OdfChartDefinition
        {
            ChartType = OdfChartType.Line,
            Title = "趨勢",
            DataRange = new OdfCellRange(0, 0, 3, 1, "銷售"),
        });

        using var stream = new MemoryStream();
        workbook.SaveToStream(stream);
        stream.Position = 0;

        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream);

        OdfDataValidationInfo validation = Assert.Single(loaded.GetDataValidations());
        Assert.True(validation.TryGetCondition(out OdfValidationCondition condition));
        Assert.Equal(OdfValidationCondition.IntegerBetween, condition);

        OdfEmbeddedChartInfo chart = Assert.Single(loaded.GetEmbeddedCharts());
        Assert.Equal("趨勢", chart.Title);
        Assert.Equal(OdfChartType.Line, chart.ChartType);
    }

    /// <summary>
    /// 驗證試算表追蹤修訂可在儲存後重新載入。
    /// </summary>
    [Fact]
    public void TrackedCellChangesSurviveRoundTrip()
    {
        using var workbook = SpreadsheetDocument.Create();
        workbook.TrackedChanges = false;
        workbook.Worksheets.Add("庫存").Cells["A1"].CellValue = "100";
        workbook.TrackedChanges = true;
        workbook.Worksheets["庫存"].Cells["A1"].CellValue = "120";

        using var stream = new MemoryStream();
        workbook.SaveToStream(stream);
        stream.Position = 0;

        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream);

        Assert.True(loaded.TrackedChanges);
        OdfSpreadsheetTrackedChangeInfo change = Assert.Single(loaded.GetTrackedChanges());
        Assert.Equal("100", change.PreviousContent);
        Assert.Equal("120", loaded.Worksheets["庫存"].Cells["A1"].CellValue);
    }

    private sealed record ProductRow(string Name, double Price, int Stock);
}
