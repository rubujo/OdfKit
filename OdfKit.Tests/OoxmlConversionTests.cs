using System;
using System.IO;
using System.Linq;
using A = DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using WP = DocumentFormat.OpenXml.Wordprocessing;
using ClosedXML.Excel;
using OdfKit.Chart;
using OdfKit.Conversion;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;
using OdfSpreadsheetDocument = OdfKit.Spreadsheet.SpreadsheetDocument;

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
        using var original = OdfSpreadsheetDocument.Create();
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
        using var original = OdfSpreadsheetDocument.Create();
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
            if (entry.FullName == "word/document.xml")
            { hasWordDocument = true; break; }
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

    /// <summary>
    /// 驗證 ODT → DOCX 轉換可保留頁首與頁尾文字。
    /// </summary>
    [Fact]
    public void OdtToDocx_PreservesHeaderAndFooter()
    {
        using var odtDoc = TextDocument.Create();
        var setup = odtDoc.GetDefaultPageSetup();
        setup.HeaderText = "公司頁首";
        setup.FooterText = "機密頁尾";
        odtDoc.AddParagraph("本文");

        using var docxStream = new MemoryStream();
        OdfToDocxConverter.Convert(odtDoc, docxStream);
        docxStream.Position = 0;

        using var wordDocument = WordprocessingDocument.Open(docxStream, false);
        var mainPart = Assert.IsType<MainDocumentPart>(wordDocument.MainDocumentPart);

        string headerText = string.Concat(mainPart.HeaderParts.Select(part => part.Header?.InnerText ?? string.Empty));
        string footerText = string.Concat(mainPart.FooterParts.Select(part => part.Footer?.InnerText ?? string.Empty));
        WP.Document document = mainPart.Document ?? throw new InvalidDataException("DOCX 文件缺少主要文件。");
        WP.Body body = document.Body ?? throw new InvalidDataException("DOCX 文件缺少本文。");

        Assert.Contains("公司頁首", headerText);
        Assert.Contains("機密頁尾", footerText);
        Assert.NotNull(body.Elements<WP.SectionProperties>().FirstOrDefault());
    }

    /// <summary>
    /// 驗證 ODT → DOCX 轉換可保留段落水平對齊。
    /// </summary>
    [Fact]
    public void OdtToDocx_PreservesParagraphAlignment()
    {
        using var odtDoc = TextDocument.Create();
        var centered = odtDoc.AddParagraph("置中段落");
        centered.HorizontalAlignment = "center";
        var right = odtDoc.AddParagraph("靠右段落");
        right.HorizontalAlignment = "end";
        var justified = odtDoc.AddParagraph("左右對齊段落");
        justified.HorizontalAlignment = "justify";

        using var docxStream = new MemoryStream();
        OdfToDocxConverter.Convert(odtDoc, docxStream);
        docxStream.Position = 0;

        using var wordDocument = WordprocessingDocument.Open(docxStream, false);
        var mainPart = Assert.IsType<MainDocumentPart>(wordDocument.MainDocumentPart);
        WP.Document document = Assert.IsType<WP.Document>(mainPart.Document);
        WP.Body body = Assert.IsType<WP.Body>(document.Body);
        var paragraphs = body.Elements<WP.Paragraph>().ToList();

        Assert.Contains(paragraphs, paragraph =>
            paragraph.InnerText.Contains("置中段落", StringComparison.Ordinal) &&
            paragraph.ParagraphProperties?.Justification?.Val?.Value == WP.JustificationValues.Center);
        Assert.Contains(paragraphs, paragraph =>
            paragraph.InnerText.Contains("靠右段落", StringComparison.Ordinal) &&
            paragraph.ParagraphProperties?.Justification?.Val?.Value == WP.JustificationValues.Right);
        Assert.Contains(paragraphs, paragraph =>
            paragraph.InnerText.Contains("左右對齊段落", StringComparison.Ordinal) &&
            paragraph.ParagraphProperties?.Justification?.Val?.Value == WP.JustificationValues.Both);
    }

    /// <summary>
    /// 驗證 ODT → DOCX 轉換可保留 content.xml 自動樣式中的字元格式。
    /// </summary>
    [Fact]
    public void OdtToDocx_PreservesAutomaticCharacterStyles()
    {
        using var odtDoc = TextDocument.Create();
        var paragraph = odtDoc.AddParagraph();
        paragraph.AddTextRun("一般");
        var styledRun = paragraph.AddTextRun("格式文字");
        styledRun.IsBold = true;
        styledRun.IsItalic = true;
        styledRun.IsUnderline = true;
        styledRun.FontSize = "14pt";

        using var docxStream = new MemoryStream();
        OdfToDocxConverter.Convert(odtDoc, docxStream);
        docxStream.Position = 0;

        using var wordDocument = WordprocessingDocument.Open(docxStream, false);
        var mainPart = Assert.IsType<MainDocumentPart>(wordDocument.MainDocumentPart);
        WP.Document document = Assert.IsType<WP.Document>(mainPart.Document);
        WP.Body body = Assert.IsType<WP.Body>(document.Body);
        var formattedRun = body
            .Descendants<WP.Run>()
            .First(run => run.InnerText == "格式文字");
        var properties = Assert.IsType<WP.RunProperties>(formattedRun.RunProperties);

        Assert.NotNull(properties.GetFirstChild<WP.Bold>());
        Assert.NotNull(properties.GetFirstChild<WP.Italic>());
        Assert.Equal(WP.UnderlineValues.Single, properties.GetFirstChild<WP.Underline>()?.Val?.Value);
        Assert.Equal("28", properties.GetFirstChild<WP.FontSize>()?.Val?.Value);
    }

    /// <summary>
    /// 驗證 DOCX → ODT 反向轉換可保留段落、標題與表格文字。
    /// </summary>
    [Fact]
    public void DocxToOdt_PreservesHeadingsParagraphsAndTableText()
    {
        using var docxStream = new MemoryStream();
        using (var wordDocument = WordprocessingDocument.Create(docxStream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, autoSave: true))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            var body = new WP.Body();
            mainPart.Document = new WP.Document(body);

            body.AppendChild(new WP.Paragraph(
                new WP.ParagraphProperties(new WP.ParagraphStyleId { Val = "Heading1" }),
                new WP.Run(new WP.Text("反向標題"))));
            body.AppendChild(new WP.Paragraph(new WP.Run(new WP.Text("反向段落"))));
            body.AppendChild(new WP.Table(
                new WP.TableRow(
                    new WP.TableCell(new WP.Paragraph(new WP.Run(new WP.Text("A1")))),
                    new WP.TableCell(new WP.Paragraph(new WP.Run(new WP.Text("B1")))))));
        }

        docxStream.Position = 0;
        using TextDocument odtDocument = DocxToOdtConverter.Convert(docxStream);
        string contentXml = SaveTextContentXml(odtDocument);

        Assert.Contains("text:h", contentXml);
        Assert.Contains("text:outline-level=\"1\"", contentXml);
        Assert.Contains("反向標題", contentXml);
        Assert.Contains("反向段落", contentXml);
        Assert.Contains("table:table", contentXml);
        Assert.Contains("A1", contentXml);
        Assert.Contains("B1", contentXml);
    }

    /// <summary>
    /// 驗證 DOCX → ODT 反向轉換可保留頁首與頁尾文字。
    /// </summary>
    [Fact]
    public void DocxToOdt_PreservesHeaderAndFooter()
    {
        using var docxStream = new MemoryStream();
        using (var wordDocument = WordprocessingDocument.Create(docxStream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, autoSave: true))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            var headerPart = mainPart.AddNewPart<HeaderPart>();
            headerPart.Header = new WP.Header(new WP.Paragraph(new WP.Run(new WP.Text("反向頁首"))));
            headerPart.Header.Save();

            var footerPart = mainPart.AddNewPart<FooterPart>();
            footerPart.Footer = new WP.Footer(new WP.Paragraph(new WP.Run(new WP.Text("反向頁尾"))));
            footerPart.Footer.Save();

            var body = new WP.Body(
                new WP.Paragraph(new WP.Run(new WP.Text("本文"))),
                new WP.SectionProperties(
                    new WP.HeaderReference { Type = WP.HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(headerPart) },
                    new WP.FooterReference { Type = WP.HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(footerPart) }));
            mainPart.Document = new WP.Document(body);
        }

        docxStream.Position = 0;
        using TextDocument odtDocument = DocxToOdtConverter.Convert(docxStream);
        string stylesXml = SaveTextStylesXml(odtDocument);

        Assert.Contains("style:header", stylesXml);
        Assert.Contains("反向頁首", stylesXml);
        Assert.Contains("style:footer", stylesXml);
        Assert.Contains("反向頁尾", stylesXml);
    }

    /// <summary>
    /// 驗證 DOCX → ODT 反向轉換可保留內嵌圖片與尺寸。
    /// </summary>
    [Fact]
    public void DocxToOdt_PreservesInlineImage()
    {
        using var docxStream = new MemoryStream();
        using (var wordDocument = WordprocessingDocument.Create(docxStream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, autoSave: true))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            var imagePart = mainPart.AddImagePart(ImagePartType.Png);
            using (var imageStream = new MemoryStream(CreateOnePixelPng()))
            {
                imagePart.FeedData(imageStream);
            }

            string imageRelationshipId = mainPart.GetIdOfPart(imagePart);
            var body = new WP.Body(new WP.Paragraph(
                new WP.Run(new WP.Text("圖片前")),
                new WP.Run(CreateInlineImageDrawing(imageRelationshipId, 720000, 360000))));
            mainPart.Document = new WP.Document(body);
        }

        docxStream.Position = 0;
        using TextDocument odtDocument = DocxToOdtConverter.Convert(docxStream);
        using var odtStream = new MemoryStream();
        odtDocument.SaveToStream(odtStream);
        odtStream.Position = 0;

        using var package = OdfKit.Core.OdfPackage.Open(odtStream, leaveOpen: true);
        Assert.Contains(package.Manifest.Keys, path => path.StartsWith("Pictures/", StringComparison.Ordinal));

        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string contentXml = reader.ReadToEnd();

        Assert.Contains("圖片前", contentXml);
        Assert.Contains("draw:frame", contentXml);
        Assert.Contains("draw:image", contentXml);
        Assert.Contains("xlink:href=\"Pictures/", contentXml);
        Assert.Contains("svg:width=\"2cm\"", contentXml);
        Assert.Contains("svg:height=\"1cm\"", contentXml);
    }

    /// <summary>
    /// 驗證 DOCX → ODT 反向轉換可保留基本字元格式。
    /// </summary>
    [Fact]
    public void DocxToOdt_PreservesRunFormatting()
    {
        using var docxStream = new MemoryStream();
        using (var wordDocument = WordprocessingDocument.Create(docxStream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, autoSave: true))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            var formattedRun = new WP.Run(
                new WP.RunProperties(
                    new WP.Bold(),
                    new WP.Italic(),
                    new WP.Underline { Val = WP.UnderlineValues.Single },
                    new WP.Color { Val = "FF0000" },
                    new WP.FontSize { Val = "24" }),
                new WP.Text("格式文字"));
            mainPart.Document = new WP.Document(new WP.Body(new WP.Paragraph(
                new WP.Run(new WP.Text("一般")),
                formattedRun)));
        }

        docxStream.Position = 0;
        using TextDocument odtDocument = DocxToOdtConverter.Convert(docxStream);
        string contentXml = SaveTextContentXml(odtDocument);

        Assert.Contains("一般", contentXml);
        Assert.Contains("格式文字", contentXml);
        Assert.Contains("text:span", contentXml);
        Assert.Contains("fo:font-weight=\"bold\"", contentXml);
        Assert.Contains("fo:font-style=\"italic\"", contentXml);
        Assert.Contains("style:text-underline-style=\"solid\"", contentXml);
        Assert.Contains("fo:color=\"#FF0000\"", contentXml);
        Assert.Contains("fo:font-size=\"12pt\"", contentXml);
    }

    /// <summary>
    /// 驗證 DOCX → ODT 反向轉換可保留段落水平對齊。
    /// </summary>
    [Fact]
    public void DocxToOdt_PreservesParagraphAlignment()
    {
        using var docxStream = new MemoryStream();
        using (var wordDocument = WordprocessingDocument.Create(docxStream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, autoSave: true))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new WP.Document(new WP.Body(
                CreateAlignedParagraph("置中段落", WP.JustificationValues.Center),
                CreateAlignedParagraph("靠右段落", WP.JustificationValues.Right),
                CreateAlignedParagraph("左右對齊段落", WP.JustificationValues.Both)));
        }

        docxStream.Position = 0;
        using TextDocument odtDocument = DocxToOdtConverter.Convert(docxStream);
        string contentXml = SaveTextContentXml(odtDocument);

        Assert.Contains("置中段落", contentXml);
        Assert.Contains("靠右段落", contentXml);
        Assert.Contains("左右對齊段落", contentXml);
        Assert.Contains("fo:text-align=\"center\"", contentXml);
        Assert.Contains("fo:text-align=\"end\"", contentXml);
        Assert.Contains("fo:text-align=\"justify\"", contentXml);
    }

    /// <summary>
    /// 驗證 DOCX → ODT 反向轉換可保留基本追蹤修訂標記。
    /// </summary>
    [Fact]
    public void DocxToOdt_PreservesTrackedInsertionAndDeletion()
    {
        var changedAt = new DocumentFormat.OpenXml.DateTimeValue(new DateTime(2026, 6, 16, 8, 0, 0, DateTimeKind.Utc));
        using var docxStream = new MemoryStream();
        using (var wordDocument = WordprocessingDocument.Create(docxStream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, autoSave: true))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new WP.Document(new WP.Body(
                new WP.Paragraph(
                    new WP.Run(new WP.Text("保留")),
                    new WP.InsertedRun(new WP.Run(new WP.Text("新增"))) { Author = "Alice", Date = changedAt },
                    new WP.DeletedRun(new WP.Run(new WP.DeletedText("刪除"))) { Author = "Bob", Date = changedAt })));
        }

        docxStream.Position = 0;
        using TextDocument odtDocument = DocxToOdtConverter.Convert(docxStream);
        string contentXml = SaveTextContentXml(odtDocument);

        Assert.Contains("text:tracked-changes", contentXml);
        Assert.Contains("text:insertion", contentXml);
        Assert.Contains("text:deletion", contentXml);
        Assert.Contains("text:change-start", contentXml);
        Assert.Contains("text:change-end", contentXml);
        Assert.Contains("新增", contentXml);
        Assert.Contains("刪除", contentXml);
        Assert.Contains("Alice", contentXml);
        Assert.Contains("Bob", contentXml);
    }

    /// <summary>
    /// 驗證 DOCX 轉換後的追蹤修訂可列舉並接受。
    /// </summary>
    [Fact]
    public void DocxToOdt_TrackedChangesCanBeAcceptedAfterConversion()
    {
        var changedAt = new DocumentFormat.OpenXml.DateTimeValue(new DateTime(2026, 6, 16, 8, 0, 0, DateTimeKind.Utc));
        using var docxStream = new MemoryStream();
        using (var wordDocument = WordprocessingDocument.Create(docxStream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, autoSave: true))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new WP.Document(new WP.Body(
                new WP.Paragraph(
                    new WP.InsertedRun(new WP.Run(new WP.Text("轉換插入"))) { Author = "Alice", Date = changedAt })));
        }

        docxStream.Position = 0;
        using TextDocument odtDocument = DocxToOdtConverter.Convert(docxStream);

        var changes = odtDocument.GetTrackedChanges().ToList();
        Assert.Single(changes);
        Assert.Equal(OdfChangeType.Insertion, changes[0].ChangeType);
        Assert.Equal("Alice", changes[0].Author);
        Assert.Equal("轉換插入", changes[0].Content);

        odtDocument.AcceptAllChanges();

        Assert.Empty(odtDocument.GetTrackedChanges());
        string contentXml = SaveTextContentXml(odtDocument);
        Assert.Contains("轉換插入", contentXml);
        Assert.DoesNotContain("text:change-start", contentXml);
        Assert.DoesNotContain("text:tracked-changes", contentXml);
    }

    /// <summary>
    /// 驗證 XLSX → ODS 反向轉換會保留公式。
    /// </summary>
    [Fact]
    public void XlsxToOdf_PreservesFormulasAsOpenFormula()
    {
        using var xlsxStream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Sheet1");
            worksheet.Cell("A1").Value = 1;
            worksheet.Cell("B1").Value = 2;
            worksheet.Cell("C1").FormulaA1 = "SUM(A1:B1)";
            workbook.SaveAs(xlsxStream);
        }

        xlsxStream.Position = 0;
        using OdfSpreadsheetDocument odsDocument = XlsxToOdfConverter.Convert(xlsxStream);

        Assert.Equal("of:=SUM(A1:B1)", odsDocument.Worksheets[0].Cells["C1"].Formula);
    }

    /// <summary>
    /// 驗證 XLSX → ODS 反向轉換可保留整數範圍資料驗證。
    /// </summary>
    [Fact]
    public void XlsxToOdf_PreservesIntegerBetweenDataValidation()
    {
        using var xlsxStream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Sheet1");
            var validation = worksheet.Cell("A1").CreateDataValidation();
            validation.WholeNumber.Between("1", "100");
            validation.ErrorStyle = XLErrorStyle.Stop;
            validation.ErrorTitle = "無效輸入";
            validation.ErrorMessage = "請輸入 1 至 100 的整數！";
            workbook.SaveAs(xlsxStream);
        }

        xlsxStream.Position = 0;
        using OdfSpreadsheetDocument odsDocument = XlsxToOdfConverter.Convert(xlsxStream);
        string contentXml = SaveSpreadsheetContentXml(odsDocument);

        Assert.Contains("table:content-validations", contentXml);
        Assert.Contains("condition=\"and:oooc:isInteger()and:oooc:isBetween(1,100)\"", contentXml);
        Assert.Contains("table:content-validation-name=\"val_1\"", contentXml);
        Assert.Contains("請輸入 1 至 100 的整數！", contentXml);
    }

    /// <summary>
    /// 驗證 ODS → XLSX 轉換可保留整數範圍資料驗證。
    /// </summary>
    [Fact]
    public void OdfToXlsx_PreservesIntegerBetweenDataValidation()
    {
        using var odsDocument = OdfSpreadsheetDocument.Create();
        odsDocument.AddSheet("Sheet1");
        odsDocument.AddDataValidation("Sheet1", new OdfDataValidation
        {
            ApplyTo = new OdfCellRange(0, 0, 0, 0, "Sheet1"),
            Condition = OdfValidationCondition.IntegerBetween,
            Formula1 = "1",
            Formula2 = "100",
            ErrorTitle = "無效輸入",
            ErrorMessage = "請輸入 1 至 100 的整數！",
            AlertStyle = OdfValidationAlertStyle.Stop
        });

        using var xlsxStream = new MemoryStream();
        OdfToXlsxConverter.Convert(odsDocument, xlsxStream);
        xlsxStream.Position = 0;

        using var workbook = new XLWorkbook(xlsxStream);
        var worksheet = workbook.Worksheet("Sheet1");
        var validations = worksheet.DataValidations.GetAllInRange(worksheet.Cell("A1").AsRange().RangeAddress).ToList();

        var validation = Assert.Single(validations);
        Assert.Equal(XLAllowedValues.WholeNumber, validation.AllowedValues);
        Assert.Equal(XLOperator.Between, validation.Operator);
        Assert.Equal("1", validation.MinValue);
        Assert.Equal("100", validation.MaxValue);
        Assert.Equal(XLErrorStyle.Stop, validation.ErrorStyle);
        Assert.Equal("無效輸入", validation.ErrorTitle);
        Assert.Equal("請輸入 1 至 100 的整數！", validation.ErrorMessage);
    }

    /// <summary>
    /// 驗證 ODS → XLSX 轉換可保留基本儲存格格式。
    /// </summary>
    [Fact]
    public void OdfToXlsx_PreservesBasicCellFormatting()
    {
        using var odsDocument = OdfSpreadsheetDocument.Create();
        var sheet = odsDocument.Worksheets.Add("Sheet1");
        var cell = sheet.Cells["A1"];
        cell.CellValue = "格式";
        odsDocument.StyleEngine.SetLocalStyleProperty(cell.Node, "table-cell", "text-properties", "font-weight", OdfNamespaces.Fo, "bold", "fo");
        odsDocument.StyleEngine.SetLocalStyleProperty(cell.Node, "table-cell", "text-properties", "font-style", OdfNamespaces.Fo, "italic", "fo");
        odsDocument.StyleEngine.SetLocalStyleProperty(cell.Node, "table-cell", "text-properties", "text-underline-style", OdfNamespaces.Style, "solid", "style");
        odsDocument.StyleEngine.SetLocalStyleProperty(cell.Node, "table-cell", "text-properties", "color", OdfNamespaces.Fo, "#FF0000", "fo");
        odsDocument.StyleEngine.SetLocalStyleProperty(cell.Node, "table-cell", "table-cell-properties", "background-color", OdfNamespaces.Fo, "#FFFF00", "fo");
        odsDocument.StyleEngine.SetLocalStyleProperty(cell.Node, "table-cell", "table-cell-properties", "border", OdfNamespaces.Fo, "0.75pt solid #000000", "fo");

        using var xlsxStream = new MemoryStream();
        OdfToXlsxConverter.Convert(odsDocument, xlsxStream);
        xlsxStream.Position = 0;

        using var workbook = new XLWorkbook(xlsxStream);
        var convertedCell = workbook.Worksheet("Sheet1").Cell("A1");

        Assert.True(convertedCell.Style.Font.Bold);
        Assert.True(convertedCell.Style.Font.Italic);
        Assert.Equal(XLFontUnderlineValues.Single, convertedCell.Style.Font.Underline);
        Assert.Equal(XLBorderStyleValues.Thin, convertedCell.Style.Border.TopBorder);
        Assert.Equal(XLBorderStyleValues.Thin, convertedCell.Style.Border.BottomBorder);
        Assert.Equal("格式", convertedCell.GetString());
    }

    /// <summary>
    /// 驗證 ODS → XLSX 轉換可保留色階條件格式。
    /// </summary>
    [Fact]
    public void OdfToXlsx_PreservesColorScaleConditionalFormat()
    {
        using var odsDocument = OdfSpreadsheetDocument.Create();
        var sheet = odsDocument.Worksheets.Add("Sheet1");
        sheet.Cells["A1"].CellValue = 1d;
        sheet.Cells["A2"].CellValue = 2d;
        sheet.AddColorScaleFormat(
            new OdfCellRange(0, 0, 1, 0, "Sheet1"),
            new OdfColor("#FF0000"),
            new OdfColor("#00FF00"));

        using var xlsxStream = new MemoryStream();
        OdfToXlsxConverter.Convert(odsDocument, xlsxStream);
        xlsxStream.Position = 0;

        using var workbook = new XLWorkbook(xlsxStream);
        var worksheet = workbook.Worksheet("Sheet1");
        var conditionalFormat = Assert.Single(worksheet.ConditionalFormats);

        Assert.Equal(XLConditionalFormatType.ColorScale, conditionalFormat.ConditionalFormatType);
        Assert.Equal("A1:A2", conditionalFormat.Range.RangeAddress.ToStringRelative());
    }

    /// <summary>
    /// 驗證 ODS → XLSX 轉換可保留資料橫條條件格式。
    /// </summary>
    [Fact]
    public void OdfToXlsx_PreservesDataBarConditionalFormat()
    {
        using var odsDocument = OdfSpreadsheetDocument.Create();
        var sheet = odsDocument.Worksheets.Add("Sheet1");
        sheet.Cells["B1"].CellValue = 1d;
        sheet.Cells["B2"].CellValue = 2d;
        sheet.AddDataBarFormat(
            new OdfCellRange(0, 1, 1, 1, "Sheet1"),
            new OdfColor("#638EC6"),
            new OdfColor("#FF0000"));

        using var xlsxStream = new MemoryStream();
        OdfToXlsxConverter.Convert(odsDocument, xlsxStream);
        xlsxStream.Position = 0;

        using var workbook = new XLWorkbook(xlsxStream);
        var worksheet = workbook.Worksheet("Sheet1");
        var conditionalFormat = Assert.Single(worksheet.ConditionalFormats);

        Assert.Equal(XLConditionalFormatType.DataBar, conditionalFormat.ConditionalFormatType);
        Assert.Equal("B1:B2", conditionalFormat.Range.RangeAddress.ToStringRelative());
    }

    /// <summary>
    /// 驗證 ODS → XLSX 轉換可保留圖示集條件格式。
    /// </summary>
    [Fact]
    public void OdfToXlsx_PreservesIconSetConditionalFormat()
    {
        using var odsDocument = OdfSpreadsheetDocument.Create();
        var sheet = odsDocument.Worksheets.Add("Sheet1");
        sheet.Cells["C1"].CellValue = 1d;
        sheet.Cells["C2"].CellValue = 2d;
        sheet.AddIconSetFormat(new OdfCellRange(0, 2, 1, 2, "Sheet1"), OdfIconSetType.FiveRating);

        using var xlsxStream = new MemoryStream();
        OdfToXlsxConverter.Convert(odsDocument, xlsxStream);
        xlsxStream.Position = 0;

        using var workbook = new XLWorkbook(xlsxStream);
        var worksheet = workbook.Worksheet("Sheet1");
        var conditionalFormat = Assert.Single(worksheet.ConditionalFormats);

        Assert.Equal(XLConditionalFormatType.IconSet, conditionalFormat.ConditionalFormatType);
        Assert.Equal(XLIconSetStyle.FiveRating, conditionalFormat.IconSetStyle);
        Assert.Equal("C1:C2", conditionalFormat.Range.RangeAddress.ToStringRelative());
    }

    /// <summary>
    /// 驗證 ODS → XLSX 轉換可保留基本嵌入圖表結構。
    /// </summary>
    [Fact]
    public void OdfToXlsx_PreservesEmbeddedChartStructure()
    {
        using var odsDocument = OdfSpreadsheetDocument.Create();
        var sheet = odsDocument.Worksheets.Add("Data");
        sheet.Cells["A1"].CellValue = "月份";
        sheet.Cells["B1"].CellValue = "營收";
        sheet.Cells["A2"].CellValue = "一月";
        sheet.Cells["B2"].CellValue = 120d;
        sheet.Cells["A3"].CellValue = "二月";
        sheet.Cells["B3"].CellValue = 160d;
        odsDocument.AddChart("Data", new OdfCellAddress(4, 0, "Data"), new OdfChartDefinition
        {
            ChartType = OdfChartType.Bar,
            Title = "營收圖表",
            DataRange = new OdfCellRange(0, 0, 2, 1, "Data"),
            HasLegend = true
        });
        Assert.True(odsDocument.Package.HasEntry("Object 1/content.xml"));

        using var xlsxStream = new MemoryStream();
        OdfToXlsxConverter.Convert(odsDocument, xlsxStream);
        xlsxStream.Position = 0;

        using (var spreadsheetDocument = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(xlsxStream, false))
        {
            var validationErrors = new OpenXmlValidator()
                .Validate(spreadsheetDocument, TestContext.Current.CancellationToken)
                .ToList();
            Assert.Empty(validationErrors);
        }
        xlsxStream.Position = 0;

        using var zip = new System.IO.Compression.ZipArchive(xlsxStream, System.IO.Compression.ZipArchiveMode.Read);
        var chartEntry = zip.Entries.FirstOrDefault(entry =>
            entry.FullName.Contains("/charts/chart", StringComparison.OrdinalIgnoreCase) &&
            entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(chartEntry);

        using Stream chartStream = chartEntry.Open();
        using var reader = new StreamReader(chartStream);
        string chartXml = reader.ReadToEnd();

        Assert.Contains("<c:barChart>", chartXml);
        Assert.Contains("營收圖表", chartXml);
        Assert.Contains("Data!B1", chartXml);
        Assert.Contains("Data!A2:A3", chartXml);
        Assert.Contains("Data!B2:B3", chartXml);
    }

    /// <summary>
    /// 驗證 ODS → XLSX 轉換可依圖表類型產生正確的 OOXML series。
    /// </summary>
    [Theory]
    [InlineData(OdfChartType.Line, "<c:lineChart>", "<c:ser>", "<c:barSer>")]
    [InlineData(OdfChartType.Pie, "<c:pieChart>", "<c:ser>", "<c:barSer>")]
    public void OdfToXlsx_PreservesChartTypeSpecificSeries(
        OdfChartType chartType,
        string expectedChartElement,
        string expectedSeriesElement,
        string forbiddenSeriesElement)
    {
        using var odsDocument = OdfSpreadsheetDocument.Create();
        var sheet = odsDocument.Worksheets.Add("Data");
        sheet.Cells["A1"].CellValue = "月份";
        sheet.Cells["B1"].CellValue = "營收";
        sheet.Cells["A2"].CellValue = "一月";
        sheet.Cells["B2"].CellValue = 120d;
        sheet.Cells["A3"].CellValue = "二月";
        sheet.Cells["B3"].CellValue = 160d;
        odsDocument.AddChart("Data", new OdfCellAddress(4, 0, "Data"), new OdfChartDefinition
        {
            ChartType = chartType,
            Title = chartType + " 圖表",
            DataRange = new OdfCellRange(0, 0, 2, 1, "Data")
        });

        using var xlsxStream = new MemoryStream();
        OdfToXlsxConverter.Convert(odsDocument, xlsxStream);
        xlsxStream.Position = 0;

        using var zip = new System.IO.Compression.ZipArchive(xlsxStream, System.IO.Compression.ZipArchiveMode.Read);
        var chartEntry = zip.Entries.First(entry =>
            entry.FullName.Contains("/charts/chart", StringComparison.OrdinalIgnoreCase) &&
            entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
        string chartXml = ReadZipEntry(chartEntry);

        Assert.Contains(expectedChartElement, chartXml);
        Assert.Contains(expectedSeriesElement, chartXml);
        Assert.DoesNotContain(forbiddenSeriesElement, chartXml);
        Assert.Contains("Data!B1", chartXml);
        Assert.Contains("Data!A2:A3", chartXml);
        Assert.Contains("Data!B2:B3", chartXml);
    }

    /// <summary>
    /// 驗證 ODS → XLSX 轉換可保留基本樞紐分析表 OOXML 結構。
    /// </summary>
    [Fact]
    public void OdfToXlsx_PreservesPivotTableStructure()
    {
        using var odsDocument = OdfSpreadsheetDocument.Create();
        var sheet = odsDocument.Worksheets.Add("Sales");
        sheet.Cells["A1"].CellValue = "Category";
        sheet.Cells["B1"].CellValue = "Region";
        sheet.Cells["C1"].CellValue = "Sales";
        sheet.Cells["A2"].CellValue = "Hardware";
        sheet.Cells["B2"].CellValue = "North";
        sheet.Cells["C2"].CellValue = 120d;
        sheet.Cells["A3"].CellValue = "Software";
        sheet.Cells["B3"].CellValue = "South";
        sheet.Cells["C3"].CellValue = 80d;

        new OdfPivotTableBuilder(
            "SalesPivot",
            new OdfCellRange(0, 0, 2, 2, "Sales"),
            new OdfCellAddress(5, 0, "Sales"),
            sheet)
            .AddRowField("Category")
            .AddColumnField("Region")
            .AddDataField("Sales", OdfPivotFunction.Sum)
            .Build();

        using var xlsxStream = new MemoryStream();
        OdfToXlsxConverter.Convert(odsDocument, xlsxStream);
        xlsxStream.Position = 0;

        using var zip = new System.IO.Compression.ZipArchive(xlsxStream, System.IO.Compression.ZipArchiveMode.Read);
        var cacheEntry = zip.GetEntry("xl/pivotCache/pivotCacheDefinition1.xml");
        var pivotEntry = zip.GetEntry("xl/pivotTables/pivotTable1.xml");
        var workbookEntry = zip.GetEntry("xl/workbook.xml");
        var worksheetEntry = zip.GetEntry("xl/worksheets/sheet1.xml");

        Assert.NotNull(cacheEntry);
        Assert.NotNull(pivotEntry);
        Assert.NotNull(workbookEntry);
        Assert.NotNull(worksheetEntry);

        string cacheXml = ReadZipEntry(cacheEntry!);
        string pivotXml = ReadZipEntry(pivotEntry!);
        string workbookXml = ReadZipEntry(workbookEntry!);
        string worksheetXml = ReadZipEntry(worksheetEntry!);

        Assert.Contains("pivotCache", workbookXml);
        Assert.Contains("worksheetSource", cacheXml);
        Assert.Contains("ref=\"Sales!A1:C3\"", cacheXml);
        Assert.Contains("name=\"Category\"", cacheXml);
        Assert.Contains("name=\"Region\"", cacheXml);
        Assert.Contains("name=\"Sales\"", cacheXml);
        Assert.Contains("name=\"SalesPivot\"", pivotXml);
        Assert.Contains("rowFields", pivotXml);
        Assert.Contains("field x=\"0\"", pivotXml);
        Assert.Contains("colFields", pivotXml);
        Assert.Contains("field x=\"1\"", pivotXml);
        Assert.Contains("dataFields", pivotXml);
        Assert.Contains("dataField name=\"Sum of Sales\" fld=\"2\" subtotal=\"sum\"", pivotXml);
        Assert.Contains("pivotTableDefinition", worksheetXml);
    }

    /// <summary>
    /// 驗證 XLSX → ODS 反向轉換可保留基本儲存格格式。
    /// </summary>
    [Fact]
    public void XlsxToOdf_PreservesBasicCellFormatting()
    {
        using var xlsxStream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Sheet1");
            var cell = worksheet.Cell("A1");
            cell.Value = "格式";
            cell.Style.Font.Bold = true;
            cell.Style.Font.Italic = true;
            cell.Style.Font.Underline = XLFontUnderlineValues.Single;
            cell.Style.Font.FontColor = XLColor.FromHtml("#FF0000");
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFFF00");
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            workbook.SaveAs(xlsxStream);
        }

        xlsxStream.Position = 0;
        using OdfSpreadsheetDocument odsDocument = XlsxToOdfConverter.Convert(xlsxStream);
        string contentXml = SaveSpreadsheetContentXml(odsDocument);

        Assert.Contains("style:style", contentXml);
        Assert.Contains("style:family=\"table-cell\"", contentXml);
        Assert.Contains("fo:font-weight=\"bold\"", contentXml);
        Assert.Contains("fo:font-style=\"italic\"", contentXml);
        Assert.Contains("style:text-underline-style=\"solid\"", contentXml);
        Assert.Contains("fo:color=\"#FF0000\"", contentXml);
        Assert.Contains("fo:background-color=\"#FFFF00\"", contentXml);
        Assert.Contains("fo:border-top=\"0.75pt solid #000000\"", contentXml);
    }

    /// <summary>
    /// 驗證 XLSX → ODS 反向轉換可保留色階條件格式。
    /// </summary>
    [Fact]
    public void XlsxToOdf_PreservesColorScaleConditionalFormat()
    {
        using var xlsxStream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Sheet1");
            worksheet.Cell("A1").Value = 1;
            worksheet.Cell("A2").Value = 2;
            worksheet.Range("A1:A2").AddConditionalFormat()
                .ColorScale()
                .LowestValue(XLColor.FromHtml("#FF0000"))
                .HighestValue(XLColor.FromHtml("#00FF00"));
            workbook.SaveAs(xlsxStream);
        }

        xlsxStream.Position = 0;
        using OdfSpreadsheetDocument odsDocument = XlsxToOdfConverter.Convert(xlsxStream);
        string contentXml = SaveSpreadsheetContentXml(odsDocument);

        Assert.Contains("calcext:conditional-formats", contentXml);
        Assert.Contains("calcext:color-scale", contentXml);
        Assert.Contains("calcext:target-range-address=\"Sheet1.A1:Sheet1.A2\"", contentXml);
        Assert.Contains("calcext:color=\"#FF0000\"", contentXml);
        Assert.Contains("calcext:color=\"#00FF00\"", contentXml);
    }

    /// <summary>
    /// 驗證 XLSX → ODS 反向轉換可保留資料橫條條件格式。
    /// </summary>
    [Fact]
    public void XlsxToOdf_PreservesDataBarConditionalFormat()
    {
        using var xlsxStream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Sheet1");
            worksheet.Cell("B1").Value = 1;
            worksheet.Cell("B2").Value = 2;
            worksheet.Range("B1:B2").AddConditionalFormat()
                .DataBar(XLColor.FromHtml("#638EC6"), XLColor.FromHtml("#FF0000"), showBarOnly: false)
                .LowestValue()
                .HighestValue();
            workbook.SaveAs(xlsxStream);
        }

        xlsxStream.Position = 0;
        using OdfSpreadsheetDocument odsDocument = XlsxToOdfConverter.Convert(xlsxStream);
        string contentXml = SaveSpreadsheetContentXml(odsDocument);

        Assert.Contains("calcext:data-bar", contentXml);
        Assert.Contains("calcext:target-range-address=\"Sheet1.B1:Sheet1.B2\"", contentXml);
        Assert.Contains("calcext:positive-color=\"#638EC6\"", contentXml);
        Assert.Contains("calcext:negative-color=\"#FF0000\"", contentXml);
    }

    /// <summary>
    /// 驗證 XLSX → ODS 反向轉換可保留圖示集條件格式。
    /// </summary>
    [Fact]
    public void XlsxToOdf_PreservesIconSetConditionalFormat()
    {
        using var xlsxStream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Sheet1");
            worksheet.Cell("C1").Value = 1;
            worksheet.Cell("C2").Value = 2;
            worksheet.Range("C1:C2").AddConditionalFormat()
                .IconSet(XLIconSetStyle.ThreeTrafficLights1, reverseIconOrder: false, showIconOnly: false);
            workbook.SaveAs(xlsxStream);
        }

        xlsxStream.Position = 0;
        using OdfSpreadsheetDocument odsDocument = XlsxToOdfConverter.Convert(xlsxStream);
        string contentXml = SaveSpreadsheetContentXml(odsDocument);

        Assert.Contains("calcext:icon-set", contentXml);
        Assert.Contains("calcext:target-range-address=\"Sheet1.C1:Sheet1.C2\"", contentXml);
        Assert.Contains("calcext:icon-set-type=\"3TrafficLights1\"", contentXml);
        Assert.Contains("calcext:icon-set-entry", contentXml);
    }

    /// <summary>
    /// 驗證 XLSX → ODS 反向轉換可保留基本圖表結構。
    /// </summary>
    [Fact]
    public void XlsxToOdf_PreservesEmbeddedChartStructure()
    {
        using var sourceOds = OdfSpreadsheetDocument.Create();
        var sheet = sourceOds.Worksheets.Add("Data");
        sheet.Cells["A1"].CellValue = "月份";
        sheet.Cells["B1"].CellValue = "營收";
        sheet.Cells["A2"].CellValue = "一月";
        sheet.Cells["B2"].CellValue = 120d;
        sheet.Cells["A3"].CellValue = "二月";
        sheet.Cells["B3"].CellValue = 160d;
        sourceOds.AddChart("Data", new OdfCellAddress(4, 0, "Data"), new OdfChartDefinition
        {
            ChartType = OdfChartType.Line,
            Title = "反向圖表",
            DataRange = new OdfCellRange(0, 0, 2, 1, "Data")
        });

        using var xlsxStream = new MemoryStream();
        OdfToXlsxConverter.Convert(sourceOds, xlsxStream);
        xlsxStream.Position = 0;

        using OdfSpreadsheetDocument odsDocument = XlsxToOdfConverter.Convert(xlsxStream);
        using var odsStream = new MemoryStream();
        odsDocument.SaveToStream(odsStream);
        odsStream.Position = 0;

        using var package = OdfKit.Core.OdfPackage.Open(odsStream, leaveOpen: true);
        Assert.True(package.HasEntry("Object 1/content.xml"));
        using Stream chartStream = package.GetEntryStream("Object 1/content.xml");
        using var reader = new StreamReader(chartStream);
        string chartXml = reader.ReadToEnd();

        Assert.Contains("chart:class=\"chart:line\"", chartXml);
        Assert.Contains("反向圖表", chartXml);
        Assert.Contains("table:cell-range-address=\"[Data.A1:.B3]\"", chartXml);
    }

    /// <summary>
    /// 驗證 Excel 公式可翻譯為 OpenFormula 公式。
    /// </summary>
    [Fact]
    public void XlsxToOdf_FormulaTranslation_AddsOpenFormulaPrefix()
    {
        Assert.Equal("of:=SUM(A1:B2)", XlsxToOdfConverter.TranslateFormulaToOdf("=SUM(A1:B2)"));
        Assert.Equal("of:=Sheet1.A1", XlsxToOdfConverter.TranslateFormulaToOdf("=Sheet1!A1"));
        Assert.Equal("of:=A1+B1", XlsxToOdfConverter.TranslateFormulaToOdf("A1+B1"));
    }

    /// <summary>
    /// 驗證 50 個常見公式可在 OpenFormula 與 Excel A1 格式之間雙向翻譯。
    /// </summary>
    /// <param name="openFormula">OpenFormula 公式。</param>
    /// <param name="excelFormula">Excel A1 公式。</param>
    [Theory]
    [InlineData("of:=SUM(A1:A10)", "=SUM(A1:A10)")]
    [InlineData("of:=AVERAGE(A1:A10)", "=AVERAGE(A1:A10)")]
    [InlineData("of:=MIN(A1:A10)", "=MIN(A1:A10)")]
    [InlineData("of:=MAX(A1:A10)", "=MAX(A1:A10)")]
    [InlineData("of:=COUNT(A1:A10)", "=COUNT(A1:A10)")]
    [InlineData("of:=COUNTA(A1:A10)", "=COUNTA(A1:A10)")]
    [InlineData("of:=ROUND(A1,2)", "=ROUND(A1,2)")]
    [InlineData("of:=ROUNDUP(A1,0)", "=ROUNDUP(A1,0)")]
    [InlineData("of:=ROUNDDOWN(A1,0)", "=ROUNDDOWN(A1,0)")]
    [InlineData("of:=ABS(A1)", "=ABS(A1)")]
    [InlineData("of:=POWER(A1,2)", "=POWER(A1,2)")]
    [InlineData("of:=SQRT(A1)", "=SQRT(A1)")]
    [InlineData("of:=MOD(A1,2)", "=MOD(A1,2)")]
    [InlineData("of:=INT(A1)", "=INT(A1)")]
    [InlineData("of:=IF(A1>0,A1,0)", "=IF(A1>0,A1,0)")]
    [InlineData("of:=AND(A1>0,B1>0)", "=AND(A1>0,B1>0)")]
    [InlineData("of:=OR(A1>0,B1>0)", "=OR(A1>0,B1>0)")]
    [InlineData("of:=NOT(A1>0)", "=NOT(A1>0)")]
    [InlineData("of:=IFERROR(A1/B1,0)", "=IFERROR(A1/B1,0)")]
    [InlineData("of:=SUMIF(A1:A10,\">0\")", "=SUMIF(A1:A10,\">0\")")]
    [InlineData("of:=COUNTIF(A1:A10,\">0\")", "=COUNTIF(A1:A10,\">0\")")]
    [InlineData("of:=AVERAGEIF(A1:A10,\">0\")", "=AVERAGEIF(A1:A10,\">0\")")]
    [InlineData("of:=SUMIFS(C1:C10,A1:A10,\">0\")", "=SUMIFS(C1:C10,A1:A10,\">0\")")]
    [InlineData("of:=COUNTIFS(A1:A10,\">0\")", "=COUNTIFS(A1:A10,\">0\")")]
    [InlineData("of:=LEFT(A1,3)", "=LEFT(A1,3)")]
    [InlineData("of:=RIGHT(A1,3)", "=RIGHT(A1,3)")]
    [InlineData("of:=MID(A1,2,3)", "=MID(A1,2,3)")]
    [InlineData("of:=LEN(A1)", "=LEN(A1)")]
    [InlineData("of:=TRIM(A1)", "=TRIM(A1)")]
    [InlineData("of:=LOWER(A1)", "=LOWER(A1)")]
    [InlineData("of:=UPPER(A1)", "=UPPER(A1)")]
    [InlineData("of:=CONCATENATE(A1,B1)", "=CONCATENATE(A1,B1)")]
    [InlineData("of:=SUBSTITUTE(A1,\"a\",\"b\")", "=SUBSTITUTE(A1,\"a\",\"b\")")]
    [InlineData("of:=FIND(\"a\",A1)", "=FIND(\"a\",A1)")]
    [InlineData("of:=SEARCH(\"a\",A1)", "=SEARCH(\"a\",A1)")]
    [InlineData("of:=DATE(2026,6,16)", "=DATE(2026,6,16)")]
    [InlineData("of:=TODAY()", "=TODAY()")]
    [InlineData("of:=NOW()", "=NOW()")]
    [InlineData("of:=YEAR(A1)", "=YEAR(A1)")]
    [InlineData("of:=MONTH(A1)", "=MONTH(A1)")]
    [InlineData("of:=DAY(A1)", "=DAY(A1)")]
    [InlineData("of:=WEEKDAY(A1)", "=WEEKDAY(A1)")]
    [InlineData("of:=VLOOKUP(A1,B1:C10,2,FALSE())", "=VLOOKUP(A1,B1:C10,2,FALSE())")]
    [InlineData("of:=HLOOKUP(A1,B1:J2,2,FALSE())", "=HLOOKUP(A1,B1:J2,2,FALSE())")]
    [InlineData("of:=INDEX(A1:C10,2,3)", "=INDEX(A1:C10,2,3)")]
    [InlineData("of:=MATCH(A1,B1:B10,0)", "=MATCH(A1,B1:B10,0)")]
    [InlineData("of:=OFFSET(A1,1,1)", "=OFFSET(A1,1,1)")]
    [InlineData("of:=Sheet1.A1", "=Sheet1!A1")]
    [InlineData("of:=Sheet1.A1:B2", "=Sheet1!A1:B2")]
    [InlineData("of:=Sheet1.A1+Sheet2.B1", "=Sheet1!A1+Sheet2!B1")]
    public void FormulaTranslation_CoversCommonFormulaBenchmark(string openFormula, string excelFormula)
    {
        Assert.Equal(excelFormula, OdfToXlsxConverter.TranslateFormula(openFormula));
        Assert.Equal(openFormula, XlsxToOdfConverter.TranslateFormulaToOdf(excelFormula));
    }

    private static string SaveTextContentXml(TextDocument document)
    {
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using var package = OdfKit.Core.OdfPackage.Open(stream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        return reader.ReadToEnd();
    }

    private static byte[] CreateOnePixelPng()
    {
        return Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
    }

    private static string ReadZipEntry(System.IO.Compression.ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static WP.Drawing CreateInlineImageDrawing(string relationshipId, long cx, long cy)
    {
        return new WP.Drawing(
            new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                new DW.DocProperties { Id = 1, Name = "image.png" },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0, Name = "image.png" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relationshipId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0, Y = 0 },
                                    new A.Extents { Cx = cx, Cy = cy }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            { DistanceFromTop = 0, DistanceFromBottom = 0, DistanceFromLeft = 0, DistanceFromRight = 0 });
    }

    private static WP.Paragraph CreateAlignedParagraph(string text, WP.JustificationValues alignment)
    {
        return new WP.Paragraph(
            new WP.ParagraphProperties(new WP.Justification { Val = alignment }),
            new WP.Run(new WP.Text(text)));
    }

    private static string SaveTextStylesXml(TextDocument document)
    {
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using var package = OdfKit.Core.OdfPackage.Open(stream, leaveOpen: true);
        using Stream stylesStream = package.GetEntryStream("styles.xml");
        using var reader = new StreamReader(stylesStream);
        return reader.ReadToEnd();
    }

    private static string SaveSpreadsheetContentXml(OdfSpreadsheetDocument document)
    {
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using var package = OdfKit.Core.OdfPackage.Open(stream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        return reader.ReadToEnd();
    }
}
