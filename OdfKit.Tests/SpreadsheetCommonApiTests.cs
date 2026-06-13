using System.IO;
using System.Linq;
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

    private static string ReadEntry(OdfPackage package, string path)
    {
        using Stream stream = package.GetEntryStream(path);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
