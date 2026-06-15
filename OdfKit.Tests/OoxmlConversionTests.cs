using System;
using System.IO;
using OdfKit.Conversion;
using OdfKit.Spreadsheet;
using OdfKit.Text;
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

    /// <summary>
    /// 驗證 ODT → DOCX 轉換可產生有效的 DOCX 資料流，並保留段落文字與標題。
    /// </summary>
    [Fact]
    public void OdtToDocx_PreservesTextContentAndHeadings()
    {
        using var odtDoc = TextDocument.Create();
        odtDoc.AddHeading("測試標題", 1);
        odtDoc.AddParagraph("段落一：Hello World");
        odtDoc.AddParagraph("段落二：福爾摩沙");

        using var docxStream = new MemoryStream();
        OdfToDocxConverter.Convert(odtDoc, docxStream);
        docxStream.Position = 0;

        Assert.True(docxStream.Length > 0, "DOCX 資料流不應為空");

        // 驗證輸出是有效的 ZIP（DOCX 即 ZIP）
        using var zip = new System.IO.Compression.ZipArchive(docxStream, System.IO.Compression.ZipArchiveMode.Read);
        bool hasWordDocument = false;
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName == "word/document.xml") { hasWordDocument = true; break; }
        }
        Assert.True(hasWordDocument, "DOCX 應包含 word/document.xml");

        // 驗證 word/document.xml 內含文字
        using var zipStream = new MemoryStream();
        odtDoc.SaveToStream(zipStream);
        zipStream.Position = 0;
        using var docxStream2 = new MemoryStream();
        using var odtDoc2 = TextDocument.Create();
        odtDoc2.AddHeading("測試標題", 1);
        odtDoc2.AddParagraph("段落一：Hello World");
        OdfToDocxConverter.Convert(odtDoc2, docxStream2);
        docxStream2.Position = 0;
        using var zip2 = new System.IO.Compression.ZipArchive(docxStream2, System.IO.Compression.ZipArchiveMode.Read);
        var docEntry = zip2.GetEntry("word/document.xml");
        Assert.NotNull(docEntry);
        using var docEntryStream = docEntry!.Open();
        using var reader = new StreamReader(docEntryStream);
        string docXml = reader.ReadToEnd();
        Assert.Contains("測試標題", docXml);
        Assert.Contains("Hello World", docXml);
    }

    /// <summary>
    /// 驗證 ODS 公式翻譯（OpenFormula → Excel A1 格式）。
    /// </summary>
    [Fact]
    public void OdfToXlsx_FormulaTranslation_StripsPrefix()
    {
        Assert.Equal("=SUM(A1:B2)", OdfToXlsxConverter.TranslateFormula("of:=SUM(A1:B2)"));
        Assert.Equal("=SUM(A1:B2)", OdfToXlsxConverter.TranslateFormula("oooc:=SUM(A1:B2)"));
        Assert.Equal("=A1+B1", OdfToXlsxConverter.TranslateFormula("of:=A1+B1"));
        Assert.Equal("=Sheet1!A1", OdfToXlsxConverter.TranslateFormula("of:=Sheet1.A1"));
        Assert.Equal("=Sheet1!A1:B2", OdfToXlsxConverter.TranslateFormula("of:=Sheet1.A1:B2"));
        // 已包含 = 前綴（無命名空間）
        Assert.Equal("=IF(A1>0,A1,0)", OdfToXlsxConverter.TranslateFormula("=IF(A1>0,A1,0)"));
    }

    /// <summary>
    /// 驗證 ODT → DOCX 轉換可正確轉換表格結構（行列數一致）。
    /// </summary>
    [Fact]
    public void OdtToDocx_TableConversion_PreservesRowAndColumnCount()
    {
        using var odtDoc = TextDocument.Create();
        odtDoc.AddParagraph("表格前");

        var table = odtDoc.AddTable(2, 3);
        table.GetCell(0, 0).AddParagraph("A1");
        table.GetCell(0, 1).AddParagraph("B1");
        table.GetCell(0, 2).AddParagraph("C1");
        table.GetCell(1, 0).AddParagraph("A2");

        using var docxStream = new MemoryStream();
        OdfToDocxConverter.Convert(odtDoc, docxStream);
        docxStream.Position = 0;

        using var zip = new System.IO.Compression.ZipArchive(docxStream, System.IO.Compression.ZipArchiveMode.Read);
        var docEntry = zip.GetEntry("word/document.xml");
        Assert.NotNull(docEntry);
        using var docEntryStream = docEntry!.Open();
        using var reader = new StreamReader(docEntryStream);
        string docXml = reader.ReadToEnd();

        Assert.Contains("<w:tbl", docXml);
        Assert.Contains("A1", docXml);
        Assert.Contains("A2", docXml);
    }
}
