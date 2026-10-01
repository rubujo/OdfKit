using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using OdfKit.Compliance;
using OdfKit.Conversion;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;
using WP = DocumentFormat.OpenXml.Wordprocessing;
using OdfSpreadsheetDocument = OdfKit.Spreadsheet.SpreadsheetDocument;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定以真實性驗證（把 Office 風格的 XLSX／DOCX 轉成 ODF 並對照產生器已知的真值）所發現的轉換缺陷：
/// 試算表日期的時區、稀疏儲存格的展開、合併儲存格，以及 DOCX 轉換遺失的定位字元、換行、超連結、
/// 內容控制項、表格合併與巢狀表格。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Regression)]
public sealed class RealWorldConverterFidelityTests
{
    private static readonly XNamespace s_text = OdfNamespaces.Text;
    private static readonly XNamespace s_table = OdfNamespaces.Table;
    private static readonly XNamespace s_office = OdfNamespaces.Office;
    private static readonly XNamespace s_xlink = OdfNamespaces.XLink;

    // ---------- XLSX → ODS ----------

    /// <summary>
    /// 驗證 XLSX 的日期（不含時區的序號）轉成 ODS 時保持原本的日期與時間，不會依執行機器的時區位移，
    /// 也不會被標成 UTC（<c>Z</c>）。
    /// </summary>
    [Fact]
    public void XlsxDateCellsConvertWithoutTimeZoneShift()
    {
        using var xlsx = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            IXLWorksheet worksheet = workbook.AddWorksheet("D");
            worksheet.Cell("A1").Value = new DateTime(2017, 9, 23);
            worksheet.Cell("A2").Value = new DateTime(2024, 2, 29, 13, 45, 30);
            workbook.SaveAs(xlsx);
        }

        xlsx.Position = 0;
        using OdfSpreadsheetDocument converted = XlsxToOdfConverter.Convert(xlsx);
        XElement content = XElement.Parse(SaveContentXml(converted));
        string?[] values = content.Descendants(s_table + "table-cell")
            .Select(cell => (string?)cell.Attribute(s_office + "date-value"))
            .Where(value => value is not null)
            .ToArray();

        Assert.Equal(new[] { "2017-09-23T00:00:00", "2024-02-29T13:45:30" }, values);
    }

    /// <summary>
    /// 驗證只有少數儲存格卻位於遠端位址的 XLSX，不會被展開成整個已使用範圍的空白儲存格。
    /// </summary>
    [Fact]
    public void XlsxSparseSheetDoesNotMaterializeBlankCells()
    {
        using var xlsx = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            IXLWorksheet worksheet = workbook.AddWorksheet("S");
            worksheet.Cell("A1").Value = "起點";
            worksheet.Cell(3000, 60).Value = "遠端";
            workbook.SaveAs(xlsx);
        }

        xlsx.Position = 0;
        using OdfSpreadsheetDocument converted = XlsxToOdfConverter.Convert(xlsx);
        XElement content = XElement.Parse(SaveContentXml(converted));

        int cellCount = content.Descendants(s_table + "table-cell").Count();
        Assert.True(cellCount < 3200, $"轉換後有 {cellCount} 個儲存格元素；整個已使用範圍展開約需 18 萬個。");
        Assert.Equal("遠端", converted.Worksheets[0].GetCell(2999, 59).CellValue);
    }

    /// <summary>
    /// 驗證 XLSX 的合併儲存格會轉成 <c>number-columns-spanned</c>／<c>number-rows-spanned</c> 與
    /// <c>table:covered-table-cell</c>。
    /// </summary>
    [Fact]
    public void XlsxMergedRangesConvertToSpannedCells()
    {
        using var xlsx = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            IXLWorksheet worksheet = workbook.AddWorksheet("M");
            worksheet.Cell("A1").Value = "標題";
            worksheet.Range("A1:C1").Merge();
            worksheet.Cell("A3").Value = "垂直";
            worksheet.Range("A3:A4").Merge();
            worksheet.Cell("B6").Value = "單格";
            workbook.SaveAs(xlsx);
        }

        xlsx.Position = 0;
        using OdfSpreadsheetDocument converted = XlsxToOdfConverter.Convert(xlsx);
        XElement content = XElement.Parse(SaveContentXml(converted));

        XElement[] spanned = content.Descendants(s_table + "table-cell")
            .Where(cell => cell.Attribute(s_table + "number-columns-spanned") is not null)
            .ToArray();
        Assert.Equal(2, spanned.Length);
        Assert.Equal("3", (string?)spanned[0].Attribute(s_table + "number-columns-spanned"));
        Assert.Equal("1", (string?)spanned[0].Attribute(s_table + "number-rows-spanned"));
        Assert.Equal("1", (string?)spanned[1].Attribute(s_table + "number-columns-spanned"));
        Assert.Equal("2", (string?)spanned[1].Attribute(s_table + "number-rows-spanned"));
        Assert.Equal(3, content.Descendants(s_table + "covered-table-cell").Count());
    }

    // ---------- DOCX → ODT ----------

    /// <summary>
    /// 驗證 DOCX 的定位字元、換行與連續空格轉換後不會遺失。
    /// </summary>
    [Fact]
    public void DocxTabBreakAndSpacesSurviveConversion()
    {
        using MemoryStream docx = CreateDocx(body =>
        {
            body.Append(new WP.Paragraph(
                new WP.Run(new WP.Text("前綴  ") { Space = SpaceProcessingModeValues.Preserve }),
                new WP.Run(new WP.TabChar()),
                new WP.Run(new WP.Text("定位後")),
                new WP.Run(new WP.Break()),
                new WP.Run(new WP.Text("換行後   ") { Space = SpaceProcessingModeValues.Preserve })));
        });

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        Assert.Equal("前綴  \t定位後\n換行後   ", odt.Body.Paragraphs.First().TextContent);
        XElement paragraph = ParseContentParagraph(SaveContentXml(odt), 0);
        Assert.Contains("tab", DescribeChildren(paragraph));
        Assert.Contains("line-break", DescribeChildren(paragraph));
    }

    /// <summary>
    /// 驗證段落中的超連結（原本整個被丟棄）轉成帶有 <c>xlink:href</c> 與 <c>xlink:type</c> 的 <c>text:a</c>，
    /// 且連結文字保留在原本的位置。
    /// </summary>
    [Fact]
    public void DocxHyperlinkConvertsToAnchorAndKeepsText()
    {
        using MemoryStream docx = CreateDocx(
            body =>
            {
                body.Append(new WP.Paragraph(
                    new WP.Run(new WP.Text("連結：") { Space = SpaceProcessingModeValues.Preserve }),
                    new WP.Hyperlink(new WP.Run(new WP.Text("範例網站"))) { Id = "rIdLink" },
                    new WP.Run(new WP.Text("。"))));
            },
            main => main.AddHyperlinkRelationship(new Uri("https://example.org/page"), true, "rIdLink"));

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        Assert.Equal("連結：範例網站。", odt.Body.Paragraphs.First().TextContent);

        XElement anchor = XElement.Parse(SaveContentXml(odt)).Descendants(s_text + "a").Single();
        Assert.Equal("https://example.org/page", (string?)anchor.Attribute(s_xlink + "href"));
        Assert.Equal("simple", (string?)anchor.Attribute(s_xlink + "type"));
        Assert.Equal("範例網站", anchor.Value);
    }

    /// <summary>
    /// 驗證不安全協定的超連結目標不會寫入 ODT，但連結文字仍保留。
    /// </summary>
    [Fact]
    public void DocxHyperlinkWithUnsafeSchemeKeepsTextWithoutAnchor()
    {
        using MemoryStream docx = CreateDocx(
            body =>
            {
                body.Append(new WP.Paragraph(
                    new WP.Hyperlink(new WP.Run(new WP.Text("點我"))) { Id = "rIdBad" }));
            },
            main => main.AddHyperlinkRelationship(new Uri("javascript:alert(1)"), true, "rIdBad"));

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        Assert.Equal("點我", odt.Body.Paragraphs.First().TextContent);
        Assert.Empty(XElement.Parse(SaveContentXml(odt)).Descendants(s_text + "a"));
    }

    /// <summary>
    /// 驗證內容控制項（<c>w:sdt</c>）內的文字不會被整段丟棄。
    /// </summary>
    [Fact]
    public void DocxInlineContentControlKeepsText()
    {
        using MemoryStream docx = CreateDocx(body =>
        {
            body.Append(new WP.Paragraph(
                new WP.Run(new WP.Text("前 ") { Space = SpaceProcessingModeValues.Preserve }),
                new WP.SdtRun(new WP.SdtContentRun(new WP.Run(new WP.Text("控制項內文")))),
                new WP.Run(new WP.Text(" 後") { Space = SpaceProcessingModeValues.Preserve })));
        });

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        Assert.Equal("前 控制項內文 後", odt.Body.Paragraphs.First().TextContent);
    }

    /// <summary>
    /// 驗證 DOCX 表格的水平合併（<c>w:gridSpan</c>）與垂直合併（<c>w:vMerge</c>）轉成正確的格線位置：
    /// 合併儲存格之後的欄位不會整排錯位。
    /// </summary>
    [Fact]
    public void DocxTableMergesKeepGridPositions()
    {
        using MemoryStream docx = CreateDocx(body =>
        {
            body.Append(new WP.Table(
                new WP.TableRow(
                    Cell("跨兩欄", span: 2),
                    Cell("C1")),
                new WP.TableRow(
                    Cell("A2", verticalMerge: WP.MergedCellValues.Restart),
                    Cell("B2"),
                    Cell("C2")),
                new WP.TableRow(
                    Cell(string.Empty, verticalMerge: WP.MergedCellValues.Continue),
                    Cell("B3"),
                    Cell("C3"))));
        });

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        XElement table = XElement.Parse(SaveContentXml(odt)).Descendants(s_table + "table").Single();
        List<List<string>> grid = table.Elements(s_table + "table-row")
            .Select(row => row.Elements().Select(cell => cell.Name.LocalName == "covered-table-cell" ? "~" : cell.Value).ToList())
            .ToList();

        Assert.Equal(new[] { "跨兩欄", "~", "C1" }, grid[0]);
        Assert.Equal(new[] { "A2", "B2", "C2" }, grid[1]);
        Assert.Equal(new[] { "~", "B3", "C3" }, grid[2]);

        XElement header = table.Elements(s_table + "table-row").First().Elements().First();
        Assert.Equal("2", (string?)header.Attribute(s_table + "number-columns-spanned"));
        XElement tall = table.Elements(s_table + "table-row").Skip(1).First().Elements().First();
        Assert.Equal("2", (string?)tall.Attribute(s_table + "number-rows-spanned"));
    }

    /// <summary>
    /// 驗證儲存格內的巢狀表格（原本整個被丟棄）會轉成巢狀的 <c>table:table</c>，並保留前後段落的順序。
    /// </summary>
    [Fact]
    public void DocxNestedTableIsConvertedInsideCell()
    {
        using MemoryStream docx = CreateDocx(body =>
        {
            var inner = new WP.Table(new WP.TableRow(Cell("內層一"), Cell("內層二")));
            body.Append(new WP.Table(new WP.TableRow(
                new WP.TableCell(
                    new WP.Paragraph(new WP.Run(new WP.Text("外層前"))),
                    inner,
                    new WP.Paragraph(new WP.Run(new WP.Text("外層後")))),
                Cell("旁邊"))));
        });

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        XElement outer = XElement.Parse(SaveContentXml(odt)).Descendants(s_table + "table").First();
        XElement outerCell = outer.Elements(s_table + "table-row").Single().Elements(s_table + "table-cell").First();

        Assert.Equal(
            new[] { "p", "table", "p" },
            outerCell.Elements().Select(element => element.Name.LocalName).ToArray());
        Assert.Equal(
            new[] { "內層一", "內層二" },
            outerCell.Element(s_table + "table")!.Descendants(s_table + "table-cell").Select(cell => cell.Value).ToArray());
    }

    /// <summary>
    /// 驗證含超連結、合併與巢狀表格的 DOCX 轉出的 ODT 仍通過 ODF 1.4 schema 驗證。
    /// </summary>
    [Fact]
    public void ConvertedDocxPassesOdf14SchemaValidation()
    {
        using MemoryStream docx = CreateDocx(
            body =>
            {
                body.Append(new WP.Paragraph(
                    new WP.Run(new WP.Text("文字 ") { Space = SpaceProcessingModeValues.Preserve }),
                    new WP.Hyperlink(new WP.Run(new WP.Text("連結"))) { Id = "rIdOk" }));
                body.Append(new WP.Table(
                    new WP.TableRow(Cell("跨", span: 2), Cell("C")),
                    new WP.TableRow(
                        new WP.TableCell(
                            new WP.Paragraph(new WP.Run(new WP.Text("含巢狀"))),
                            new WP.Table(new WP.TableRow(Cell("內")))),
                        Cell("B"),
                        Cell("C"))));
            },
            main => main.AddHyperlinkRelationship(new Uri("https://example.org/"), true, "rIdOk"));

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        OdfValidationReport report = Validate14(odt);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.Message)));
    }

    private static WP.TableCell Cell(string text, int? span = null, WP.MergedCellValues? verticalMerge = null)
    {
        var properties = new WP.TableCellProperties();
        if (span is not null)
        {
            properties.Append(new WP.GridSpan { Val = span });
        }

        if (verticalMerge is not null)
        {
            properties.Append(new WP.VerticalMerge { Val = verticalMerge });
        }

        return new WP.TableCell(properties, new WP.Paragraph(new WP.Run(new WP.Text(text))));
    }

    private static MemoryStream CreateDocx(Action<WP.Body> fill, Action<MainDocumentPart>? configure = null)
    {
        var stream = new MemoryStream();
        using (WordprocessingDocument document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, autoSave: true))
        {
            MainDocumentPart main = document.AddMainDocumentPart();
            var body = new WP.Body();
            main.Document = new WP.Document(body);
            configure?.Invoke(main);
            fill(body);
        }

        stream.Position = 0;
        return stream;
    }

    private static string SaveContentXml(OdfDocument document)
    {
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        return reader.ReadToEnd();
    }

    private static OdfValidationReport Validate14(OdfDocument document)
    {
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        return OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);
    }

    private static XElement ParseContentParagraph(string contentXml, int index) =>
        XElement.Parse(contentXml).Descendants(s_text + "p").ElementAt(index);

    // 文字節點以其文字表示；元素以 s:數量、tab、line-break 表示。
    private static string[] DescribeChildren(XElement element) =>
        element.Nodes()
            .Select(node => node switch
            {
                XText text => text.Value,
                XElement child when child.Name == s_text + "s" => "s:" + ((string?)child.Attribute(s_text + "c") ?? "1"),
                XElement child => child.Name.LocalName,
                _ => string.Empty,
            })
            .ToArray();
}
