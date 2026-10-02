using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using OdfKit.Conversion;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;
using WP = DocumentFormat.OpenXml.Wordprocessing;

namespace OdfKit.Tests;

/// <summary>
/// 使用真實 LibreOffice 驗證「真實文件」缺陷修正：空白字元、清單編號格式、頁碼欄位、
/// XLSX／DOCX 轉換的結構，以及 OdfKit 載入 LibreOffice 儲存的文件。
/// 這些測試原本是一次性的人工探針，改為可重複執行的互通測試；沒有 LibreOffice 時略過。
/// </summary>
public partial class LibreOfficeInteropTests
{
    /// <summary>
    /// 驗證 OdfKit 寫出的連續空格、定位字元與換行在 LibreOffice 中完整保留，
    /// 且 <c>AddListWithStyle</c> 的編號格式、前綴與後綴（<c>a)</c>、<c>(I)</c>）確實被套用。
    /// </summary>
    [Fact]
    public void LibreOfficeKeepsWhitespaceAndListNumberingOfOdfKitText()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過空白與清單編號互通性測試。");
        }

        using var workspace = new InteropWorkspace();
        string odtPath = Path.Combine(workspace.Root, "whitespace-list.odt");
        using (TextDocument document = TextDocument.Create())
        {
            document.AddParagraph("a    b\tc\nd  ");
            document.AddParagraph("  lead");
            OdfList list = document.AddListWithStyle(
                "InteropList",
                new[]
                {
                    new OdfListLevelStyle { Level = 1, Type = OdfListLevelType.Number, NumFormat = "a", NumSuffix = ")" },
                    new OdfListLevelStyle { Level = 2, Type = OdfListLevelType.Number, NumFormat = "I", NumPrefix = "(", NumSuffix = ")" },
                });
            list.AddItem("first", 1);
            list.AddItem("second", 1);
            list.AddItem("nested", 2);
            document.Save(odtPath);
        }

        string txt = workspace.ConvertToText(sofficePath!, odtPath);
        Assert.Contains("a    b\tc\nd  ", txt);
        Assert.Contains("\r\n  lead\r\n", txt);
        Assert.Contains("a) first", txt);
        Assert.Contains("b) second", txt);
        Assert.Contains("(I) nested", txt);
    }

    /// <summary>
    /// 驗證頁尾的頁碼與總頁數欄位：<c>AddPageCountField</c> 寫出 <c>text:page-count</c>，
    /// LibreOffice 匯出 DOCX 時還原為 <c>PAGE</c> 與 <c>NUMPAGES</c>。
    /// 修正前總頁數寫成 <c>select-page="last"</c>，LibreOffice 把它當成一般頁碼。
    /// </summary>
    [Fact]
    public void LibreOfficeRecognizesPageNumberAndPageCountFields()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過頁碼欄位互通性測試。");
        }

        using var workspace = new InteropWorkspace();
        string odtPath = Path.Combine(workspace.Root, "page-fields.odt");
        using (TextDocument document = TextDocument.Create())
        {
            document.AddParagraph("第一頁");
            OdfPageSetup setup = document.GetDefaultPageSetup();
            setup.Footer.AddPageNumberField();
            setup.Footer.AddPageCountField();
            document.Save(odtPath);
        }

        string docxPath = workspace.Convert(sofficePath!, odtPath, "docx");
        string footerXml = ReadOoxmlPartMatching(docxPath, new Regex(@"^word/footer\d*\.xml$"));
        string[] instructions = Regex.Matches(footerXml, "<w:instrText[^>]*>([^<]*)</w:instrText>")
            .Select(match => match.Groups[1].Value.Trim())
            .ToArray();
        Assert.Equal(new[] { "PAGE", "NUMPAGES" }, instructions);
    }

    /// <summary>
    /// 驗證 OdfKit 把 XLSX 轉成 ODS 的結果由 LibreOffice 再匯出為 XLSX 後與原始內容一致：
    /// 日期不依機器時區位移、合併儲存格保留、前導空白保留、位於遠端位址的儲存格仍在原位置。
    /// </summary>
    [Fact]
    public void LibreOfficeReadsOdfKitConvertedXlsxWithDatesMergesAndSparseCells()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過 XLSX 轉換互通性測試。");
        }

        using var workspace = new InteropWorkspace();
        string xlsxPath = Path.Combine(workspace.Root, "source.xlsx");
        using (var workbook = new XLWorkbook())
        {
            IXLWorksheet worksheet = workbook.AddWorksheet("資料");
            worksheet.Cell("A1").Value = new DateTime(2017, 9, 23);
            worksheet.Cell("A2").Value = new DateTime(2024, 2, 29, 13, 45, 30);
            worksheet.Cell("A3").Value = "標題";
            worksheet.Range("A3:C3").Merge();
            worksheet.Cell("A5").Value = "  前導空白與  連續空白  ";
            worksheet.Cell(3000, 60).Value = "遠端";
            workbook.SaveAs(xlsxPath);
        }

        string odsPath = Path.Combine(workspace.Root, "converted.ods");
        using (var input = File.OpenRead(xlsxPath))
        using (OdfKit.Spreadsheet.SpreadsheetDocument converted = XlsxToOdfConverter.Convert(input))
        {
            converted.Save(odsPath);
        }

        string roundTripPath = workspace.Convert(sofficePath!, odsPath, "xlsx");
        using var roundTrip = new XLWorkbook(roundTripPath);
        IXLWorksheet sheet = roundTrip.Worksheet("資料");

        Assert.Equal(new DateTime(2017, 9, 23), sheet.Cell("A1").GetDateTime());
        Assert.Equal(new DateTime(2024, 2, 29, 13, 45, 30), sheet.Cell("A2").GetDateTime());
        Assert.Contains("A3:C3", sheet.MergedRanges.Select(range => range.RangeAddress.ToString()).ToArray());
        Assert.Equal("  前導空白與  連續空白  ", sheet.Cell("A5").GetString());
        Assert.Equal("遠端", sheet.Cell(3000, 60).GetString());
    }

    /// <summary>
    /// 驗證 OdfKit 把 DOCX 轉成 ODT 的結果由 LibreOffice 開啟後：清單顯示專案符號、分頁符號使文件成為兩頁、
    /// 註腳內文存在、頁尾還原為 <c>PAGE</c> 與 <c>NUMPAGES</c> 欄位、表格儲存格內的清單保留。
    /// </summary>
    [Fact]
    public void LibreOfficeOpensOdfKitConvertedDocxWithListsNotesBreaksAndFooter()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過 DOCX 轉換互通性測試。");
        }

        using var workspace = new InteropWorkspace();
        string docxPath = Path.Combine(workspace.Root, "source.docx");
        CreateStructuredDocx(docxPath);

        string odtPath = Path.Combine(workspace.Root, "converted.odt");
        using (var input = File.OpenRead(docxPath))
        using (TextDocument converted = DocxToOdtConverter.Convert(input))
        {
            converted.Save(odtPath);
        }

        string txt = workspace.ConvertToText(sofficePath!, odtPath);
        Assert.Contains("• 項目一", txt);
        Assert.Contains("• 項目二", txt);
        Assert.Contains("1. 巢狀項目", txt);
        Assert.Contains("含註腳的句子1結尾", txt);
        Assert.Contains("儲存格清單甲", txt);

        string pdfPath = workspace.Convert(sofficePath!, odtPath, "pdf");
        string pdf = Encoding.Latin1.GetString(File.ReadAllBytes(pdfPath));
        Assert.Equal(2, Regex.Matches(pdf, @"/Type\s*/Page(?![a-z])").Count);

        string roundTripDocx = workspace.Convert(sofficePath!, odtPath, "docx");
        string footnotes = ReadOoxmlPartMatching(roundTripDocx, new Regex(@"^word/footnotes\.xml$"));
        Assert.Contains("這是註腳內文", footnotes);
        string footer = ReadOoxmlPartMatching(roundTripDocx, new Regex(@"^word/footer\d*\.xml$"));
        string[] instructions = Regex.Matches(footer, "<w:instrText[^>]*>([^<]*)</w:instrText>")
            .Select(match => match.Groups[1].Value.Trim())
            .ToArray();
        Assert.Equal(new[] { "PAGE", "NUMPAGES" }, instructions);
    }

    /// <summary>
    /// 驗證 OdfKit 把多章節 DOCX 轉成 ODT 後由 LibreOffice 開啟：直向與橫向章節各自的頁面大小、各自的頁首，
    /// 以及 <c>HYPERLINK</c> 欄位與超連結內的分頁符號都保留。修正前只採用最後一個章節的設定。
    /// </summary>
    [Fact]
    public void LibreOfficeOpensOdfKitConvertedDocxWithSectionsAndHyperlinkFields()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過 DOCX 多章節互通性測試。");
        }

        using var workspace = new InteropWorkspace();
        string docxPath = Path.Combine(workspace.Root, "sections.docx");
        using (WordprocessingDocument document = WordprocessingDocument.Create(docxPath, WordprocessingDocumentType.Document))
        {
            MainDocumentPart main = document.AddMainDocumentPart();
            HeaderPart first = main.AddNewPart<HeaderPart>();
            first.Header = new WP.Header(new WP.Paragraph(new WP.Run(new WP.Text("縱向頁首"))));
            first.Header.Save();
            HeaderPart second = main.AddNewPart<HeaderPart>();
            second.Header = new WP.Header(new WP.Paragraph(new WP.Run(new WP.Text("橫向頁首"))));
            second.Header.Save();

            static WP.Run Field(WP.FieldCharValues type) => new(new WP.FieldChar { FieldCharType = type });

            main.Document = new WP.Document(new WP.Body(
                new WP.Paragraph(
                    new WP.ParagraphProperties(new WP.SectionProperties(
                        new WP.HeaderReference { Type = WP.HeaderFooterValues.Default, Id = main.GetIdOfPart(first) },
                        new WP.PageSize { Width = 11906U, Height = 16838U })),
                    new WP.Run(new WP.Text("縱向內文")),
                    Field(WP.FieldCharValues.Begin),
                    new WP.Run(new WP.FieldCode(" HYPERLINK \"https://example.org/field\" ") { Space = SpaceProcessingModeValues.Preserve }),
                    Field(WP.FieldCharValues.Separate),
                    new WP.Run(new WP.Text("欄位連結")),
                    Field(WP.FieldCharValues.End)),
                new WP.Paragraph(new WP.Run(new WP.Text("橫向內文"))),
                new WP.SectionProperties(
                    new WP.HeaderReference { Type = WP.HeaderFooterValues.Default, Id = main.GetIdOfPart(second) },
                    new WP.PageSize { Width = 16838U, Height = 11906U, Orient = WP.PageOrientationValues.Landscape })));
            main.Document.Save();
        }

        string odtPath = Path.Combine(workspace.Root, "converted.odt");
        using (var input = File.OpenRead(docxPath))
        using (TextDocument converted = DocxToOdtConverter.Convert(input))
        {
            converted.Save(odtPath);
        }

        string pdfPath = workspace.Convert(sofficePath!, odtPath, "pdf");
        string pdf = Encoding.Latin1.GetString(File.ReadAllBytes(pdfPath));
        Assert.Equal(2, Regex.Matches(pdf, @"/Type\s*/Page(?![a-z])").Count);
        string[] mediaBoxes = Regex.Matches(pdf, @"/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]")
            .Select(match => double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) < double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)
                ? "portrait"
                : "landscape")
            .ToArray();
        Assert.Contains("portrait", mediaBoxes);
        Assert.Contains("landscape", mediaBoxes);

        string roundTripDocx = workspace.Convert(sofficePath!, odtPath, "docx");
        string headers = string.Concat(Regex.Matches(ReadAllOoxmlParts(roundTripDocx, new Regex(@"^word/header\d*\.xml$")), "<w:t[^>]*>([^<]*)</w:t>")
            .Select(match => match.Groups[1].Value));
        Assert.Contains("縱向頁首", headers);
        Assert.Contains("橫向頁首", headers);
        string rels = ReadAllOoxmlParts(roundTripDocx, new Regex(@"^word/_rels/document\.xml\.rels$"));
        Assert.Contains("https://example.org/field", rels);
    }

    /// <summary>
    /// 驗證 LibreOffice 儲存的 ODS（儲存格帶有宣告在根元素的 <c>calcext:</c> 前綴屬性、表格超過 8 KB）
    /// 由 OdfKit DOM 載入後資料完整，修改後再儲存仍完整。修正前延遲具現化的外殼只宣告七個前綴，
    /// 解析失敗後整張工作表被搶救成空表，儲存就把資料永久清掉。
    /// </summary>
    [Fact]
    public void OdfKitLoadsLibreOfficeSpreadsheetCompletelyAndKeepsItOnSave()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過 LibreOffice 試算表載入互通性測試。");
        }

        using var workspace = new InteropWorkspace();
        string xlsxPath = Path.Combine(workspace.Root, "large.xlsx");
        using (var workbook = new XLWorkbook())
        {
            IXLWorksheet worksheet = workbook.AddWorksheet("S");
            for (int row = 1; row <= 60; row++)
            {
                for (int column = 1; column <= 6; column++)
                {
                    worksheet.Cell(row, column).Value = $"R{row}C{column} 資料內容";
                }
            }

            workbook.SaveAs(xlsxPath);
        }

        string odsPath = workspace.Convert(sofficePath!, xlsxPath, "ods");
        Assert.True(new FileInfo(odsPath).Length > 0);

        using (OdfKit.Spreadsheet.SpreadsheetDocument loaded = OdfKit.Spreadsheet.SpreadsheetDocument.Load(odsPath))
        {
            OdfTableSheet sheet = loaded.GetSheets().Single();
            // LibreOffice 在每列尾端另寫一個帶樣式的重複空白儲存格，因此只計算有內容的儲存格。
            Assert.Equal(360, sheet.GetUsedCells().Count(cell => !string.IsNullOrEmpty(cell.DisplayText)));
            Assert.Equal("R60C6 資料內容", sheet.GetCell(59, 5).CellValue);
            sheet.GetCell(0, 6).CellValue = "新增";

            string savedPath = Path.Combine(workspace.Root, "saved.ods");
            loaded.Save(savedPath);

            using OdfKit.Spreadsheet.SpreadsheetDocument reloaded = OdfKit.Spreadsheet.SpreadsheetDocument.Load(savedPath);
            OdfTableSheet reloadedSheet = reloaded.GetSheets().Single();
            Assert.Equal(361, reloadedSheet.GetUsedCells().Count(cell => !string.IsNullOrEmpty(cell.DisplayText)));
            Assert.Equal("R1C1 資料內容", reloadedSheet.GetCell(0, 0).CellValue);
            Assert.Equal("新增", reloadedSheet.GetCell(0, 6).CellValue);
        }
    }

    /// <summary>
    /// 驗證 LibreOffice 儲存的 ODT 經 OdfKit 載入再儲存後，兩個 <c>text:span</c> 之間的單一空格仍在。
    /// 修正前快速解析器丟棄文字節點開頭的空白，「粗體 與 斜體」變成「粗體與 斜體」。
    /// </summary>
    [Fact]
    public void OdfKitKeepsSpaceBetweenSpansOfLibreOfficeWrittenOdt()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過 LibreOffice 文字文件空白互通性測試。");
        }

        using var workspace = new InteropWorkspace();
        string docxPath = Path.Combine(workspace.Root, "spans.docx");
        using (WordprocessingDocument document = WordprocessingDocument.Create(docxPath, WordprocessingDocumentType.Document))
        {
            MainDocumentPart main = document.AddMainDocumentPart();
            main.Document = new WP.Document(new WP.Body(
                new WP.Paragraph(
                    new WP.Run(new WP.Text("前 ") { Space = SpaceProcessingModeValues.Preserve }),
                    new WP.Run(new WP.RunProperties(new WP.Bold()), new WP.Text("粗體")),
                    new WP.Run(new WP.Text(" 與 ") { Space = SpaceProcessingModeValues.Preserve }),
                    new WP.Run(new WP.RunProperties(new WP.Italic()), new WP.Text("斜體")))));
            main.Document.Save();
        }

        string odtPath = workspace.Convert(sofficePath!, docxPath, "odt");
        string originalXml = ReadContentXmlFromFile(odtPath);
        Assert.Contains("</text:span> 與 <text:span", originalXml);

        using TextDocument loaded = TextDocument.Load(odtPath);
        Assert.Equal("前 粗體 與 斜體", loaded.Body.Paragraphs.First().TextContent);
        Assert.Contains("</text:span> 與 <text:span", ReadContentXml(loaded));
    }

    private static string ReadContentXmlFromFile(string odfPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(odfPath);
        using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string ReadAllOoxmlParts(string ooxmlPath, Regex namePattern)
    {
        using ZipArchive archive = ZipFile.OpenRead(ooxmlPath);
        var builder = new StringBuilder();
        foreach (ZipArchiveEntry entry in archive.Entries.Where(item => namePattern.IsMatch(item.FullName)))
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            builder.Append(reader.ReadToEnd());
        }

        return builder.ToString();
    }

    private static string ReadOoxmlPartMatching(string ooxmlPath, Regex namePattern)
    {
        using ZipArchive archive = ZipFile.OpenRead(ooxmlPath);
        ZipArchiveEntry entry = archive.Entries.First(item => namePattern.IsMatch(item.FullName));
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void CreateStructuredDocx(string path)
    {
        using WordprocessingDocument document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        MainDocumentPart main = document.AddMainDocumentPart();
        var body = new WP.Body();
        main.Document = new WP.Document(body);

        body.Append(new WP.Paragraph(
            new WP.Run(new WP.Text("含註腳的句子")),
            new WP.Run(new WP.FootnoteReference { Id = 2 }),
            new WP.Run(new WP.Text("結尾"))));
        body.Append(StructuredListParagraph("項目一", level: 0));
        body.Append(StructuredListParagraph("項目二", level: 0));
        body.Append(StructuredListParagraph("巢狀項目", level: 1));
        body.Append(new WP.Paragraph(new WP.Run(new WP.Break { Type = WP.BreakValues.Page })));
        body.Append(new WP.Paragraph(new WP.Run(new WP.Text("第二頁的段落"))));
        body.Append(new WP.Table(new WP.TableRow(
            new WP.TableCell(StructuredListParagraph("儲存格清單甲", level: 0), StructuredListParagraph("儲存格清單乙", level: 0)),
            new WP.TableCell(new WP.Paragraph(new WP.Run(new WP.Text("旁邊")))))));

        NumberingDefinitionsPart numbering = main.AddNewPart<NumberingDefinitionsPart>();
        numbering.Numbering = new WP.Numbering(
            new WP.AbstractNum(
                new WP.Level(
                    new WP.NumberingFormat { Val = WP.NumberFormatValues.Bullet },
                    new WP.LevelText { Val = "•" })
                { LevelIndex = 0 },
                new WP.Level(
                    new WP.NumberingFormat { Val = WP.NumberFormatValues.Decimal },
                    new WP.LevelText { Val = "%2." })
                { LevelIndex = 1 })
            { AbstractNumberId = 0 },
            new WP.NumberingInstance(new WP.AbstractNumId { Val = 0 }) { NumberID = 1 });
        numbering.Numbering.Save();

        FootnotesPart footnotes = main.AddNewPart<FootnotesPart>();
        footnotes.Footnotes = new WP.Footnotes(
            new WP.Footnote(new WP.Paragraph(new WP.Run(new WP.FootnoteReferenceMark()), new WP.Run(new WP.Text(" 這是註腳內文")))) { Id = 2 });
        footnotes.Footnotes.Save();

        FooterPart footer = main.AddNewPart<FooterPart>();
        footer.Footer = new WP.Footer(new WP.Paragraph(
            new WP.Run(new WP.Text("第 ") { Space = SpaceProcessingModeValues.Preserve }),
            new WP.Run(new WP.FieldChar { FieldCharType = WP.FieldCharValues.Begin }),
            new WP.Run(new WP.FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve }),
            new WP.Run(new WP.FieldChar { FieldCharType = WP.FieldCharValues.Separate }),
            new WP.Run(new WP.Text("1")),
            new WP.Run(new WP.FieldChar { FieldCharType = WP.FieldCharValues.End }),
            new WP.Run(new WP.Text(" 頁，共 ") { Space = SpaceProcessingModeValues.Preserve }),
            new WP.SimpleField(new WP.Run(new WP.Text("9"))) { Instruction = " NUMPAGES " }));
        footer.Footer.Save();

        body.Append(new WP.SectionProperties(
            new WP.FooterReference { Type = WP.HeaderFooterValues.Default, Id = main.GetIdOfPart(footer) }));
        main.Document.Save();
    }

    private static WP.Paragraph StructuredListParagraph(string text, int level) =>
        new(
            new WP.ParagraphProperties(
                new WP.NumberingProperties(
                    new WP.NumberingLevelReference { Val = level },
                    new WP.NumberingId { Val = 1 })),
            new WP.Run(new WP.Text(text)));

    /// <summary>
    /// 互通測試用的暫存工作區：包含獨立的 LibreOffice 使用者設定目錄與輸出目錄，釋放時清除。
    /// </summary>
    private sealed class InteropWorkspace : IDisposable
    {
        private readonly string _profile;
        private readonly string _output;

        internal InteropWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficeRealDocuments_" + Guid.NewGuid().ToString("N"));
            _profile = Path.Combine(Root, "profile");
            _output = Path.Combine(Root, "out");
            Directory.CreateDirectory(_profile);
            Directory.CreateDirectory(_output);
        }

        internal string Root { get; }

        /// <summary>以 LibreOffice 轉換成指定格式並傳回輸出檔路徑。</summary>
        internal string Convert(string sofficePath, string inputPath, string targetFormat)
        {
            RunSoffice(sofficePath, _profile, _output, targetFormat, inputPath);
            string extension = targetFormat.Split(':')[0];
            string outputPath = Path.Combine(_output, Path.GetFileNameWithoutExtension(inputPath) + "." + extension);
            Assert.True(File.Exists(outputPath), $"LibreOffice 應輸出 {Path.GetFileName(outputPath)}。");
            return outputPath;
        }

        /// <summary>以 LibreOffice 匯出 UTF-8 純文字並讀回。</summary>
        internal string ConvertToText(string sofficePath, string inputPath)
        {
            RunSoffice(sofficePath, _profile, _output, "txt:Text (encoded):UTF8", inputPath);
            string outputPath = Path.Combine(_output, Path.GetFileNameWithoutExtension(inputPath) + ".txt");
            Assert.True(File.Exists(outputPath), "LibreOffice 應輸出純文字轉換結果。");
            return File.ReadAllText(outputPath, new UTF8Encoding(false));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // 暫存檔清理失敗不應讓測試失敗。
            }
            catch (UnauthorizedAccessException)
            {
                // 同上。
            }
        }
    }
}
