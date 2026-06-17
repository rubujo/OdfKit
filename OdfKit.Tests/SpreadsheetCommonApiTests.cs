using System.IO;
using System.Linq;
using System.Xml.Linq;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 ODS 常用操作的高階 API。
/// </summary>
public class SpreadsheetCommonApiTests
{
    /// <summary>
    /// 驗證列、欄與範圍 facade 會寫出對應 ODF 結構。
    /// </summary>
    [Fact]
    public void RowsColumnsRangesFiltersAndValidationAreEasyToUse()
    {
        using var workbook = SpreadsheetDocument.Create();
        OdfTableSheet sheet = workbook.Worksheets.Add("Report");

        sheet.Rows[0].Cells[0].CellValue = "狀態";
        sheet.Rows[1].Cells[0].CellValue = "完成";
        sheet.Rows[2].Cells[0].CellValue = "待辦";
        sheet.Rows[2].Visible = false;
        sheet.Columns[1].Visible = false;
        sheet.Columns[0].SetWidth(OdfLength.FromCentimeters(4.5));
        sheet.Columns[0].AutoFit();
        sheet.Ranges["A1:A3"].NameAs("StatusRange");
        sheet.Ranges["A1:A3"].AddFilter("StatusFilter", (0, "=", "完成"));
        sheet.Ranges["A2:A3"].AddValidationList("StatusValidation", "完成", "待辦");
        sheet.Ranges["B1:C1"].Merge();
        sheet.Ranges["A1:A3"].AddConditionalFormat("cell-content() = \"完成\"", "DoneStyle");
        sheet.AddNamedExpression("TotalRows", "of:=COUNTA([.A1:.A3])", new OdfCellAddress(0, 0, "Report"));
        sheet.FreezePanes(1, 1);

        using var stream = new MemoryStream();
        workbook.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        string contentXml = ReadEntry(package, "content.xml");
        string settingsXml = ReadEntry(package, "settings.xml");

        Assert.Contains("table:name=\"StatusRange\"", contentXml);
        Assert.Contains("table:name=\"StatusFilter\"", contentXml);
        Assert.Contains("table:operator=\"=\"", contentXml);
        Assert.Contains("table:name=\"StatusValidation\"", contentXml);
        Assert.Contains("cell-content-is-in-list(&quot;完成&quot;;&quot;待辦&quot;)", contentXml);
        Assert.Contains("table:content-validation-name=\"StatusValidation\"", contentXml);
        Assert.Contains("table:number-columns-spanned=\"2\"", contentXml);
        Assert.Contains("table:visibility=\"collapse\"", contentXml);
        Assert.Contains("table:frozen-rows=\"1\"", contentXml);
        Assert.Contains("table:frozen-columns=\"1\"", contentXml);
        Assert.Contains("DoneStyle", contentXml);
        Assert.Contains("HorizontalSplitMode", settingsXml);
        Assert.Contains("VerticalSplitMode", settingsXml);

        stream.Position = 0;
        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream, "report.ods");
        OdfTableSheet loadedSheet = loaded.Worksheets["Report"];
        OdfNamedRangeInfo namedRange = Assert.Single(loadedSheet.NamedRanges);
        OdfNamedExpressionInfo namedExpression = Assert.Single(loadedSheet.NamedExpressions);

        Assert.Equal(new OdfFrozenPanes(1, 1), loadedSheet.FrozenPanes);
        Assert.True(loadedSheet.FrozenPanes.IsFrozen);
        Assert.Equal("StatusRange", namedRange.Name);
        Assert.Equal("Report.A1:.A3", namedRange.CellRangeAddress);
        Assert.Equal("TotalRows", namedExpression.Name);
        Assert.Equal("of:=COUNTA([.A1:.A3])", namedExpression.Expression);
        Assert.Equal("Report.A1", namedExpression.BaseCellAddress);
    }

    /// <summary>
    /// 驗證自動列高 (AutoHeight) 的高階 API 以及與固定高度的互斥清除機制。
    /// </summary>
    [Fact]
    public void RowOptimalHeightAndFixedRowHeightMutualExclusionWorks()
    {
        using var workbook = SpreadsheetDocument.Create();
        OdfTableSheet sheet = workbook.Worksheets.Add("OptimalHeightTest");

        OdfSheetRow row = sheet.Rows[2];

        XNamespace tableNs = OdfNamespaces.Table;
        XNamespace styleNs = OdfNamespaces.Style;

        // 1. 驗證預設狀態
        Assert.False(row.OptimalHeight);
        Assert.Null(row.Height);

        // 2. 設定最佳列高，此時應將 use-optimal-row-height 設為 true
        row.OptimalHeight = true;
        Assert.True(row.OptimalHeight);
        Assert.Null(row.Height);

        // 3. 儲存並重新載入驗證
        using (var stream = new MemoryStream())
        {
            workbook.SaveToStream(stream);
            stream.Position = 0;

            using (OdfPackage package = OdfPackage.Open(stream, leaveOpen: true))
            {
                string contentXml = ReadEntry(package, "content.xml");
                var xdoc = XDocument.Parse(contentXml);
                var rowNode = xdoc.Descendants(tableNs + "table-row").ElementAt(2);
                string styleName = rowNode.Attribute(tableNs + "style-name")!.Value;
                var style = xdoc.Descendants(styleNs + "style")
                    .First(s => s.Attribute(styleNs + "name")!.Value == styleName);
                var props = style.Element(styleNs + "table-row-properties");

                // 應包含 use-optimal-row-height = "true" 且不含 row-height
                Assert.Equal("true", props?.Attribute(styleNs + "use-optimal-row-height")?.Value);
                Assert.Null(props?.Attribute(styleNs + "row-height"));
            }

            stream.Position = 0;
            using (SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream, "test.ods"))
            {
                OdfTableSheet loadedSheet = loaded.Worksheets["OptimalHeightTest"];
                Assert.True(loadedSheet.Rows[2].OptimalHeight);
                Assert.Null(loadedSheet.Rows[2].Height);
            }
        }

        // 4. 設定固定高度，此時應清除最佳列高（將 use-optimal-row-height 設為 false，並寫入 row-height）
        row.Height = OdfLength.FromCentimeters(1.5);
        Assert.False(row.OptimalHeight);
        Assert.Equal(OdfLength.FromCentimeters(1.5), row.Height);

        using (var stream = new MemoryStream())
        {
            workbook.SaveToStream(stream);
            stream.Position = 0;

            using (OdfPackage package = OdfPackage.Open(stream, leaveOpen: true))
            {
                string contentXml = ReadEntry(package, "content.xml");
                var xdoc = XDocument.Parse(contentXml);
                var rowNode = xdoc.Descendants(tableNs + "table-row").ElementAt(2);
                string styleName = rowNode.Attribute(tableNs + "style-name")!.Value;
                var style = xdoc.Descendants(styleNs + "style")
                    .First(s => s.Attribute(styleNs + "name")!.Value == styleName);
                var props = style.Element(styleNs + "table-row-properties");

                // 應包含 row-height = "1.5cm" 且 use-optimal-row-height = "false"
                Assert.Equal("1.5cm", props?.Attribute(styleNs + "row-height")?.Value);
                Assert.Equal("false", props?.Attribute(styleNs + "use-optimal-row-height")?.Value);
            }

            stream.Position = 0;
            using (SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream, "test.ods"))
            {
                OdfTableSheet loadedSheet = loaded.Worksheets["OptimalHeightTest"];
                Assert.False(loadedSheet.Rows[2].OptimalHeight);
                Assert.Equal(OdfLength.FromCentimeters(1.5), loadedSheet.Rows[2].Height);
            }
        }

        // 5. 將 Height 設為 null，此時應移除固定列高
        row.Height = null;
        Assert.Null(row.Height);

        using (var stream = new MemoryStream())
        {
            workbook.SaveToStream(stream);
            stream.Position = 0;

            using (OdfPackage package = OdfPackage.Open(stream, leaveOpen: true))
            {
                string contentXml = ReadEntry(package, "content.xml");
                var xdoc = XDocument.Parse(contentXml);
                var rowNode = xdoc.Descendants(tableNs + "table-row").ElementAt(2);
                string styleName = rowNode.Attribute(tableNs + "style-name")!.Value;
                var style = xdoc.Descendants(styleNs + "style")
                    .First(s => s.Attribute(styleNs + "name")!.Value == styleName);
                var props = style.Element(styleNs + "table-row-properties");

                // 應該不再包含 row-height 屬性
                Assert.Null(props?.Attribute(styleNs + "row-height"));
            }
        }
    }

    /// <summary>
    /// 驗證儲存格樣式代理 OdfCellStyleProxy 的屬性讀寫保真度與 local style 懒加載機制。
    /// </summary>
    [Fact]
    public void OdfCellStyleProxyTest()
    {
        using var workbook = SpreadsheetDocument.Create();
        OdfTableSheet sheet = workbook.Worksheets.Add("StyleTest");
        OdfCell cell = sheet.GetCell(1, 1);

        // 設定字型與填滿樣式
        cell.Style.Font.IsBold = true;
        cell.Style.Font.IsItalic = true;
        cell.Style.Font.Size = "13pt";
        cell.Style.Font.Color = "#0000FF";
        cell.Style.Fill.Color = "#00FF00";
        cell.Style.NumberFormat = "N1";

        Assert.True(cell.Style.Font.IsBold);
        Assert.True(cell.Style.Font.IsItalic);
        Assert.Equal("13pt", cell.Style.Font.Size);
        Assert.Equal("#0000FF", cell.Style.Font.Color);
        Assert.Equal("#00FF00", cell.Style.Fill.Color);
        Assert.Equal("N1", cell.Style.NumberFormat);

        // 存檔與重新載入驗證
        using var stream = new MemoryStream();
        workbook.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = SpreadsheetDocument.Load(stream, "style.ods");
        OdfCell loadedCell = loaded.Worksheets["StyleTest"].GetCell(1, 1);

        Assert.True(loadedCell.Style.Font.IsBold);
        Assert.True(loadedCell.Style.Font.IsItalic);
        Assert.Equal("13pt", loadedCell.Style.Font.Size);
        Assert.Equal("#0000FF", loadedCell.Style.Font.Color);
        Assert.Equal("#00FF00", loadedCell.Style.Fill.Color);
        Assert.Equal("N1", loadedCell.Style.NumberFormat);
    }

    /// <summary>
    /// 驗證儲存格範圍受保護狀態設定與 <table:protected-ranges> XML 結構 round-trip 驗證。
    /// </summary>
    [Fact]
    public void OdfCellRangeProtectionTest()
    {
        using var workbook = SpreadsheetDocument.Create();
        OdfTableSheet sheet = workbook.Worksheets.Add("ProtectTest");
        var rangeSelection = sheet.Ranges["B2:D4"];

        Assert.False(rangeSelection.IsProtected);

        // 保護範圍
        rangeSelection.Protect("RangePass5566");
        Assert.True(rangeSelection.IsProtected);
        Assert.True(rangeSelection.VerifyPassword("RangePass5566"));
        Assert.False(rangeSelection.VerifyPassword("WrongPass"));

        // 存檔與載入驗證
        using var stream = new MemoryStream();
        workbook.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = SpreadsheetDocument.Load(stream, "protect.ods");
        var loadedSheet = loaded.Worksheets["ProtectTest"];
        var loadedRange = loadedSheet.Ranges["B2:D4"];

        Assert.True(loadedRange.IsProtected);
        Assert.True(loadedRange.VerifyPassword("RangePass5566"));
        Assert.False(loadedRange.VerifyPassword("WrongPass"));

        // 解除保護
        Assert.False(loadedRange.TryUnprotect("WrongPass"));
        Assert.True(loadedRange.IsProtected);

        Assert.True(loadedRange.TryUnprotect("RangePass5566"));
        Assert.False(loadedRange.IsProtected);
    }

    private static string ReadEntry(OdfPackage package, string path)
    {
        using Stream stream = package.GetEntryStream(path);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
