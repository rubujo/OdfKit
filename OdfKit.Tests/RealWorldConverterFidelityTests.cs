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
    private static readonly XNamespace s_style = OdfNamespaces.Style;
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

        Assert.Equal(["2017-09-23T00:00:00", "2024-02-29T13:45:30"], values);
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

        Assert.Equal(["跨兩欄", "~", "C1"], grid[0]);
        Assert.Equal(["A2", "B2", "C2"], grid[1]);
        Assert.Equal(["~", "B3", "C3"], grid[2]);

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
            ["p", "table", "p"],
            outerCell.Elements().Select(element => element.Name.LocalName).ToArray());
        Assert.Equal(
            ["內層一", "內層二"],
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

    /// <summary>
    /// 驗證 DOCX 的清單（<c>w:numPr</c>）轉成巢狀的 <c>text:list</c>／<c>text:list-item</c>，
    /// 並依編號定義建立 <c>text:list-style</c>（專案符號、編號格式、前綴與後綴）；
    /// 被其他內容打斷後回到同一份編號時延續編號。轉出的文件通過 ODF 1.4 schema 驗證。
    /// </summary>
    [Fact]
    public void DocxListsConvertToNestedListsWithListStyles()
    {
        using MemoryStream docx = CreateDocx(
            body =>
            {
                body.Append(ListParagraph("項目一", numberingId: 1, level: 0));
                body.Append(ListParagraph("子項 a", numberingId: 1, level: 1));
                body.Append(ListParagraph("子項 b", numberingId: 1, level: 1));
                body.Append(ListParagraph("項目二", numberingId: 1, level: 0));
                body.Append(new WP.Paragraph(new WP.Run(new WP.Text("插入的普通段落"))));
                body.Append(ListParagraph("回到編號", numberingId: 1, level: 0));
            },
            main =>
            {
                NumberingDefinitionsPart numbering = main.AddNewPart<NumberingDefinitionsPart>();
                numbering.Numbering = new WP.Numbering(
                    new WP.AbstractNum(
                        new WP.Level(
                            new WP.StartNumberingValue { Val = 1 },
                            new WP.NumberingFormat { Val = WP.NumberFormatValues.Bullet },
                            new WP.LevelText { Val = "•" },
                            new WP.PreviousParagraphProperties(new WP.Indentation { Left = "720", Hanging = "360" }))
                        { LevelIndex = 0 },
                        new WP.Level(
                            new WP.StartNumberingValue { Val = 3 },
                            new WP.NumberingFormat { Val = WP.NumberFormatValues.LowerLetter },
                            new WP.LevelText { Val = "(%2)" },
                            new WP.PreviousParagraphProperties(new WP.Indentation { Left = "1440", Hanging = "360" }))
                        { LevelIndex = 1 })
                    { AbstractNumberId = 0 },
                    new WP.NumberingInstance(new WP.AbstractNumId { Val = 0 }) { NumberID = 1 });
                numbering.Numbering.Save();
            });

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        XElement content = XElement.Parse(SaveContentXml(odt));

        XElement[] topLists = content.Descendants(s_text + "list").Where(list => list.Parent!.Name != s_text + "list-item").ToArray();
        Assert.Equal(2, topLists.Length);
        Assert.Equal("DocxList1", (string?)topLists[0].Attribute(s_text + "style-name"));
        Assert.Null(topLists[0].Attribute(s_text + "continue-numbering"));
        Assert.Equal("true", (string?)topLists[1].Attribute(s_text + "continue-numbering"));

        XElement[] firstItems = topLists[0].Elements(s_text + "list-item").ToArray();
        Assert.Equal(2, firstItems.Length);
        Assert.Equal("項目一", firstItems[0].Element(s_text + "p")!.Value);
        XElement nested = Assert.Single(firstItems[0].Elements(s_text + "list"));
        Assert.Equal(["子項 a", "子項 b"], nested.Elements(s_text + "list-item").Select(item => item.Value).ToArray());
        Assert.Equal("回到編號", Assert.Single(topLists[1].Elements(s_text + "list-item")).Value);

        XElement listStyle = content.Descendants(s_text + "list-style").Single();
        XElement bullet = listStyle.Elements(s_text + "list-level-style-bullet").First();
        Assert.Equal("1", (string?)bullet.Attribute(s_text + "level"));
        Assert.Equal("•", (string?)bullet.Attribute(s_text + "bullet-char"));
        XElement numbered = listStyle.Elements(s_text + "list-level-style-number").First();
        Assert.Equal("2", (string?)numbered.Attribute(s_text + "level"));
        XNamespace style = OdfNamespaces.Style;
        Assert.Equal("a", (string?)numbered.Attribute(style + "num-format"));
        Assert.Equal("(", (string?)numbered.Attribute(style + "num-prefix"));
        Assert.Equal(")", (string?)numbered.Attribute(style + "num-suffix"));
        Assert.Equal("3", (string?)numbered.Attribute(s_text + "start-value"));

        OdfValidationReport report = Validate14(odt);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.Message)));
    }

    /// <summary>
    /// 驗證 DOCX 的註腳與章節附註（原本整個被丟棄）轉成 <c>text:note</c>：引用標記依序編號，
    /// 多個段落的內文各自成為 <c>text:p</c>，內文開頭的標記空白被移除。
    /// </summary>
    [Fact]
    public void DocxFootnotesAndEndnotesConvertToNotes()
    {
        using MemoryStream docx = CreateDocx(
            body =>
            {
                body.Append(new WP.Paragraph(
                    new WP.Run(new WP.Text("前文")),
                    new WP.Run(new WP.FootnoteReference { Id = 2 }),
                    new WP.Run(new WP.Text("中間")),
                    new WP.Run(new WP.EndnoteReference { Id = 2 }),
                    new WP.Run(new WP.Text("結尾"))));
            },
            main =>
            {
                FootnotesPart footnotes = main.AddNewPart<FootnotesPart>();
                footnotes.Footnotes = new WP.Footnotes(
                    new WP.Footnote(new WP.Paragraph(new WP.Run(new WP.Text("分隔線")))) { Type = WP.FootnoteEndnoteValues.Separator, Id = -1 },
                    new WP.Footnote(
                        new WP.Paragraph(new WP.Run(new WP.FootnoteReferenceMark()), new WP.Run(new WP.Text(" 註腳第一段"))),
                        new WP.Paragraph(new WP.Run(new WP.Text("註腳第二段"))))
                    { Id = 2 });
                footnotes.Footnotes.Save();

                EndnotesPart endnotes = main.AddNewPart<EndnotesPart>();
                endnotes.Endnotes = new WP.Endnotes(
                    new WP.Endnote(new WP.Paragraph(new WP.Run(new WP.EndnoteReferenceMark()), new WP.Run(new WP.Text(" 章節附註內文")))) { Id = 2 });
                endnotes.Endnotes.Save();
            });

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        XElement content = XElement.Parse(SaveContentXml(odt));

        XElement[] notes = content.Descendants(s_text + "note").ToArray();
        Assert.Equal(2, notes.Length);

        XElement footnote = notes.Single(note => (string?)note.Attribute(s_text + "note-class") == "footnote");
        Assert.Equal("1", footnote.Element(s_text + "note-citation")!.Value);
        Assert.Equal(
            ["註腳第一段", "註腳第二段"],
            footnote.Element(s_text + "note-body")!.Elements(s_text + "p").Select(paragraph => paragraph.Value).ToArray());

        XElement endnote = notes.Single(note => (string?)note.Attribute(s_text + "note-class") == "endnote");
        Assert.Equal("1", endnote.Element(s_text + "note-citation")!.Value);
        Assert.Equal("章節附註內文", endnote.Element(s_text + "note-body")!.Element(s_text + "p")!.Value);

        OdfValidationReport report = Validate14(odt);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.Message)));
    }

    /// <summary>
    /// 驗證 DOCX 的分頁符號（<c>w:br w:type="page"</c>）與 <c>w:pageBreakBefore</c> 轉成
    /// <c>fo:break-before="page"</c>：只承載分頁符號的段落不產生空白段落，段落中間的分頁符號把段落切成兩段。
    /// </summary>
    [Fact]
    public void DocxPageBreaksConvertToBreakBeforeStyles()
    {
        using MemoryStream docx = CreateDocx(body =>
        {
            body.Append(new WP.Paragraph(new WP.Run(new WP.Text("第一頁"))));
            body.Append(new WP.Paragraph(new WP.Run(new WP.Break { Type = WP.BreakValues.Page })));
            body.Append(new WP.Paragraph(new WP.Run(new WP.Text("第二頁"))));
            body.Append(new WP.Paragraph(
                new WP.Run(new WP.Text("切開前")),
                new WP.Run(new WP.Break { Type = WP.BreakValues.Page }, new WP.Text("切開後"))));
            body.Append(new WP.Paragraph(
                new WP.ParagraphProperties(new WP.PageBreakBefore()),
                new WP.Run(new WP.Text("段前分頁"))));
        });

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        XElement content = XElement.Parse(SaveContentXml(odt));
        XNamespace fo = OdfNamespaces.Fo;
        XNamespace style = OdfNamespaces.Style;

        XElement[] paragraphs = content.Descendants(s_text + "p").ToArray();
        Assert.Equal(["第一頁", "第二頁", "切開前", "切開後", "段前分頁"], paragraphs.Select(paragraph => paragraph.Value).ToArray());

        HashSet<string> breakStyles = content.Descendants(style + "style")
            .Where(item => item.Element(style + "paragraph-properties")?.Attribute(fo + "break-before")?.Value == "page")
            .Select(item => (string)item.Attribute(style + "name")!)
            .ToHashSet();
        string[] styleNames = paragraphs
            .Select(paragraph => (string?)paragraph.Attribute(s_text + "style-name") ?? string.Empty)
            .ToArray();

        Assert.Equal(string.Empty, styleNames[0]);
        Assert.Contains(styleNames[1], breakStyles);
        Assert.Equal(string.Empty, styleNames[2]);
        Assert.Contains(styleNames[3], breakStyles);
        Assert.Contains(styleNames[4], breakStyles);
    }

    /// <summary>
    /// 驗證轉換時建立的自動樣式（段落對齊、縮排）放在 <c>office:body</c> 之前，文件通過 ODF 1.4 schema 驗證。
    /// 修正前 <c>office:automatic-styles</c> 以附加方式建立在 <c>office:body</c> 之後，
    /// 違反 <c>office:document-content</c> 的子元素順序。
    /// </summary>
    [Fact]
    public void DocxParagraphLayoutStylesPrecedeBodyAndPassSchemaValidation()
    {
        using MemoryStream docx = CreateDocx(body =>
        {
            body.Append(new WP.Paragraph(
                new WP.ParagraphProperties(
                    new WP.Justification { Val = WP.JustificationValues.Center },
                    new WP.Indentation { Left = "720" }),
                new WP.Run(new WP.Text("置中且縮排"))));
        });

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        XElement root = XElement.Parse(SaveContentXml(odt));
        XNamespace office = OdfNamespaces.Office;
        string[] order = root.Elements().Select(element => element.Name.LocalName).ToArray();
        Assert.True(
            Array.IndexOf(order, "automatic-styles") >= 0 && Array.IndexOf(order, "automatic-styles") < Array.IndexOf(order, "body"),
            string.Join(" > ", order));

        OdfValidationReport report = Validate14(odt);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.Message)));
    }

    /// <summary>
    /// 驗證表格儲存格內的段落走與本文相同的段落轉換：標題階層、定位字元與換行、超連結、清單與註腳都保留，
    /// 而不是被壓成純文字。轉出的文件通過 ODF 1.4 schema 驗證。
    /// </summary>
    [Fact]
    public void DocxTableCellsKeepRichParagraphContent()
    {
        using MemoryStream docx = CreateDocx(
            body =>
            {
                var heading = new WP.Paragraph(
                    new WP.ParagraphProperties(new WP.ParagraphStyleId { Val = "Heading1" }),
                    new WP.Run(new WP.Text("儲存格標題")));
                var rich = new WP.Paragraph(
                    new WP.Run(new WP.Text("前")),
                    new WP.Run(new WP.TabChar()),
                    new WP.Run(new WP.Text("後")),
                    new WP.Hyperlink(new WP.Run(new WP.Text("連結"))) { Id = "rIdCell" });
                var withNote = new WP.Paragraph(
                    new WP.Run(new WP.Text("有註腳")),
                    new WP.Run(new WP.FootnoteReference { Id = 2 }));
                body.Append(new WP.Table(
                    new WP.TableRow(
                        new WP.TableCell(heading, rich),
                        new WP.TableCell(
                            ListParagraph("清單一", numberingId: 1, level: 0),
                            ListParagraph("清單二", numberingId: 1, level: 0))),
                    new WP.TableRow(
                        new WP.TableCell(withNote),
                        new WP.TableCell(new WP.Paragraph(new WP.Run(new WP.Text("純文字")))))));
            },
            main =>
            {
                main.AddHyperlinkRelationship(new Uri("https://example.org/cell"), true, "rIdCell");

                NumberingDefinitionsPart numbering = main.AddNewPart<NumberingDefinitionsPart>();
                numbering.Numbering = new WP.Numbering(
                    new WP.AbstractNum(
                        new WP.Level(
                            new WP.NumberingFormat { Val = WP.NumberFormatValues.Bullet },
                            new WP.LevelText { Val = "•" })
                        { LevelIndex = 0 })
                    { AbstractNumberId = 0 },
                    new WP.NumberingInstance(new WP.AbstractNumId { Val = 0 }) { NumberID = 1 });
                numbering.Numbering.Save();

                FootnotesPart footnotes = main.AddNewPart<FootnotesPart>();
                footnotes.Footnotes = new WP.Footnotes(
                    new WP.Footnote(new WP.Paragraph(new WP.Run(new WP.FootnoteReferenceMark()), new WP.Run(new WP.Text(" 儲存格註腳")))) { Id = 2 });
                footnotes.Footnotes.Save();

                StyleDefinitionsPart styles = main.AddNewPart<StyleDefinitionsPart>();
                styles.Styles = new WP.Styles(
                    new WP.Style(new WP.StyleName { Val = "heading 1" }) { Type = WP.StyleValues.Paragraph, StyleId = "Heading1" });
                styles.Styles.Save();
            });

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        XElement content = XElement.Parse(SaveContentXml(odt));
        XElement[] rows = content.Descendants(s_table + "table-row").ToArray();
        XElement[] firstRowCells = rows[0].Elements(s_table + "table-cell").ToArray();

        XElement heading = Assert.Single(firstRowCells[0].Elements(s_text + "h"));
        Assert.Equal("1", (string?)heading.Attribute(s_text + "outline-level"));
        Assert.Equal("儲存格標題", heading.Value);

        XElement rich = firstRowCells[0].Elements(s_text + "p").Single();
        Assert.Contains("tab", DescribeChildren(rich));
        XElement anchor = Assert.Single(rich.Elements(s_text + "a"));
        Assert.Equal("https://example.org/cell", (string?)anchor.Attribute(s_xlink + "href"));

        XElement list = Assert.Single(firstRowCells[1].Elements(s_text + "list"));
        Assert.Equal(["清單一", "清單二"], list.Elements(s_text + "list-item").Select(item => item.Value).ToArray());

        XElement note = Assert.Single(rows[1].Descendants(s_text + "note"));
        Assert.Equal("儲存格註腳", note.Element(s_text + "note-body")!.Value);

        OdfValidationReport report = Validate14(odt);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.Message)));
    }

    /// <summary>
    /// 驗證頁首與頁尾走與本文相同的段落轉換（原本只取純文字串接）：超連結（關係屬於頁首部件）、
    /// 頁碼與總頁數轉成 ODF 欄位（複雜欄位與簡單欄位，Word 儲存的結果文字不會變成固定的「1」），
    /// <c>w:titlePg</c> 與 <c>w:evenAndOddHeaders</c> 建立首頁與偶數頁的版本；沒有實際內容的預設頁首不建立區域。
    /// </summary>
    [Fact]
    public void DocxHeadersAndFootersKeepRichContentAndPageFields()
    {
        using MemoryStream docx = CreateDocx(
            body =>
            {
                body.Append(new WP.Paragraph(new WP.Run(new WP.Text("本文"))));
            },
            main =>
            {
                HeaderPart header = main.AddNewPart<HeaderPart>();
                header.AddHyperlinkRelationship(new Uri("https://example.org/header"), true, "rIdHeaderLink");
                header.Header = new WP.Header(new WP.Paragraph(
                    new WP.Run(new WP.Text("頁首")),
                    new WP.Hyperlink(new WP.Run(new WP.Text("連結"))) { Id = "rIdHeaderLink" }));
                header.Header.Save();

                HeaderPart firstHeader = main.AddNewPart<HeaderPart>();
                firstHeader.Header = new WP.Header(new WP.Paragraph(new WP.Run(new WP.Text("首頁頁首"))));
                firstHeader.Header.Save();

                HeaderPart evenHeader = main.AddNewPart<HeaderPart>();
                evenHeader.Header = new WP.Header(new WP.Paragraph(new WP.Run(new WP.Text("偶數頁頁首"))));
                evenHeader.Header.Save();

                FooterPart footer = main.AddNewPart<FooterPart>();
                footer.Footer = new WP.Footer(new WP.Paragraph(
                    new WP.Run(new WP.Text("第 ") { Space = SpaceProcessingModeValues.Preserve }),
                    new WP.Run(new WP.FieldChar { FieldCharType = WP.FieldCharValues.Begin }),
                    new WP.Run(new WP.FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve }),
                    new WP.Run(new WP.FieldChar { FieldCharType = WP.FieldCharValues.Separate }),
                    new WP.Run(new WP.Text("1")),
                    new WP.Run(new WP.FieldChar { FieldCharType = WP.FieldCharValues.End }),
                    new WP.Run(new WP.Text(" 頁，共 ") { Space = SpaceProcessingModeValues.Preserve }),
                    new WP.SimpleField(new WP.Run(new WP.Text("9"))) { Instruction = " NUMPAGES " },
                    new WP.Run(new WP.Text(" 頁") { Space = SpaceProcessingModeValues.Preserve })));
                footer.Footer.Save();

                // 預設頁首之外再放一個只有空段落的首頁頁尾：不應建立區域。
                FooterPart emptyFirstFooter = main.AddNewPart<FooterPart>();
                emptyFirstFooter.Footer = new WP.Footer(new WP.Paragraph());
                emptyFirstFooter.Footer.Save();

                DocumentSettingsPart settings = main.AddNewPart<DocumentSettingsPart>();
                settings.Settings = new WP.Settings(new WP.EvenAndOddHeaders());
                settings.Settings.Save();

                main.Document!.Body!.Append(new WP.SectionProperties(
                    new WP.HeaderReference { Type = WP.HeaderFooterValues.Default, Id = main.GetIdOfPart(header) },
                    new WP.HeaderReference { Type = WP.HeaderFooterValues.First, Id = main.GetIdOfPart(firstHeader) },
                    new WP.HeaderReference { Type = WP.HeaderFooterValues.Even, Id = main.GetIdOfPart(evenHeader) },
                    new WP.FooterReference { Type = WP.HeaderFooterValues.Default, Id = main.GetIdOfPart(footer) },
                    new WP.FooterReference { Type = WP.HeaderFooterValues.First, Id = main.GetIdOfPart(emptyFirstFooter) },
                    new WP.TitlePage()));
            });

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        XElement styles = XElement.Parse(SaveStylesXml(odt));
        XElement masterPage = styles.Descendants(s_style + "master-page").First();

        XElement header = masterPage.Element(s_style + "header")!;
        Assert.StartsWith("頁首", header.Value, StringComparison.Ordinal);
        XElement headerLink = header.Descendants(s_text + "a").Single();
        Assert.Equal("https://example.org/header", (string?)headerLink.Attribute(s_xlink + "href"));
        Assert.Equal("首頁頁首", masterPage.Element(s_style + "header-first")!.Value);
        Assert.Equal("偶數頁頁首", masterPage.Element(s_style + "header-left")!.Value);

        XElement footer = masterPage.Element(s_style + "footer")!;
        XElement pageNumber = footer.Descendants(s_text + "page-number").Single();
        Assert.Equal("current", (string?)pageNumber.Attribute(s_text + "select-page"));
        Assert.Single(footer.Descendants(s_text + "page-count"));
        Assert.Equal(
            ["第", "s:1", "page-number", "s:1", "頁，共", "s:1", "page-count", "s:1", "頁"],
            DescribeChildren(footer.Element(s_text + "p")!));
        Assert.Null(masterPage.Element(s_style + "footer-first"));

        OdfValidationReport report = Validate14(odt);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.Message)));
    }

    /// <summary>
    /// 驗證沒有章節屬性參照的 DOCX 仍會沿用第一個頁首部件作為預設頁首（保持原本的容錯行為）。
    /// </summary>
    [Fact]
    public void DocxHeaderWithoutSectionReferenceIsUsedAsDefault()
    {
        using MemoryStream docx = CreateDocx(
            body => body.Append(new WP.Paragraph(new WP.Run(new WP.Text("本文")))),
            main =>
            {
                HeaderPart header = main.AddNewPart<HeaderPart>();
                header.Header = new WP.Header(new WP.Paragraph(new WP.Run(new WP.Text("無參照頁首"))));
                header.Header.Save();
            });

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        XElement styles = XElement.Parse(SaveStylesXml(odt));
        Assert.Equal("無參照頁首", styles.Descendants(s_style + "header").Single().Value);
    }

    /// <summary>
    /// 驗證多個章節各自的頁首、頁面大小與繼承：第一個章節使用預設主頁面；之後頁面設定不同的章節
    /// 建立自己的主頁面並套用在該章節的第一個段落；沒有自己參照的章節沿用前一個章節的頁首，
    /// 與前一個章節相同的章節不再建立主頁面。修正前只採用文件最後一個章節的設定。
    /// </summary>
    [Fact]
    public void DocxSectionsKeepTheirOwnHeadersAndPageGeometry()
    {
        using MemoryStream docx = CreateDocx(
            body =>
            {
                body.Append(new WP.Paragraph(
                    new WP.ParagraphProperties(new WP.SectionProperties(
                        new WP.PageSize { Width = 11906U, Height = 16838U },
                        new WP.PageMargin { Top = 1440, Bottom = 1440, Left = 1134U, Right = 1134U })),
                    new WP.Run(new WP.Text("第一章"))));
                body.Append(new WP.Paragraph(
                    new WP.ParagraphProperties(new WP.SectionProperties(
                        new WP.PageSize { Width = 16838U, Height = 11906U, Orient = WP.PageOrientationValues.Landscape })),
                    new WP.Run(new WP.Text("第二章"))));
                body.Append(new WP.Paragraph(new WP.Run(new WP.Text("第三章"))));
            },
            main =>
            {
                HeaderPart first = main.AddNewPart<HeaderPart>();
                first.Header = new WP.Header(new WP.Paragraph(new WP.Run(new WP.Text("頁首一"))));
                first.Header.Save();
                HeaderPart third = main.AddNewPart<HeaderPart>();
                third.Header = new WP.Header(new WP.Paragraph(new WP.Run(new WP.Text("頁首三"))));
                third.Header.Save();

                // 第一個章節與最後一個章節各有頁首；中間的章節沒有參照，繼承第一個章節的頁首。
                WP.SectionProperties firstSection = main.Document!.Body!
                    .Elements<WP.Paragraph>().First().ParagraphProperties!.SectionProperties!;
                firstSection.InsertAt(new WP.HeaderReference { Type = WP.HeaderFooterValues.Default, Id = main.GetIdOfPart(first) }, 0);
                main.Document.Body.Append(new WP.SectionProperties(
                    new WP.HeaderReference { Type = WP.HeaderFooterValues.Default, Id = main.GetIdOfPart(third) },
                    new WP.PageSize { Width = 16838U, Height = 11906U, Orient = WP.PageOrientationValues.Landscape }));
            });

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        XElement styles = XElement.Parse(SaveStylesXml(odt));
        XElement[] masterPages = styles.Descendants(s_style + "master-page").ToArray();

        XElement standard = masterPages.Single(item => (string?)item.Attribute(s_style + "name") == "Standard");
        Assert.Equal("頁首一", standard.Element(s_style + "header")!.Value);

        // 第二章（橫向、繼承頁首一）與第三章（橫向、頁首三）頁面設定不同，各自建立主頁面。
        XElement second = masterPages.Single(item => (string?)item.Attribute(s_style + "name") == "DocxSection1");
        Assert.Equal("頁首一", second.Element(s_style + "header")!.Value);
        XElement third = masterPages.Single(item => (string?)item.Attribute(s_style + "name") == "DocxSection2");
        Assert.Equal("頁首三", third.Element(s_style + "header")!.Value);
        Assert.Equal(3, masterPages.Length);

        XNamespace fo = OdfNamespaces.Fo;
        double WidthOf(XElement master)
        {
            string layoutName = (string)master.Attribute(s_style + "page-layout-name")!;
            XElement layout = styles.Descendants(s_style + "page-layout")
                .Single(item => (string?)item.Attribute(s_style + "name") == layoutName);
            string width = (string)layout.Element(s_style + "page-layout-properties")!.Attribute(fo + "page-width")!;
            return double.Parse(width.Replace("cm", string.Empty), System.Globalization.CultureInfo.InvariantCulture);
        }

        Assert.InRange(WidthOf(standard), 20.9, 21.1);
        Assert.InRange(WidthOf(second), 29.6, 29.8);
        string marginLeft = (string)styles.Descendants(s_style + "page-layout")
            .Single(item => (string?)item.Attribute(s_style + "name") == (string)standard.Attribute(s_style + "page-layout-name")!)
            .Element(s_style + "page-layout-properties")!.Attribute(fo + "margin-left")!;
        Assert.StartsWith("2", marginLeft, StringComparison.Ordinal);

        // 內容：第一章沒有主頁面；第二章與第三章的第一個段落分別套用各自的主頁面。
        XElement content = XElement.Parse(SaveContentXml(odt));
        XElement[] paragraphs = content.Descendants(s_text + "p").ToArray();
        Assert.Equal(["第一章", "第二章", "第三章"], paragraphs.Select(paragraph => paragraph.Value).ToArray());

        string? MasterOf(XElement paragraph)
        {
            string? styleName = (string?)paragraph.Attribute(s_text + "style-name");
            return content.Descendants(s_style + "style")
                .FirstOrDefault(item => (string?)item.Attribute(s_style + "name") == styleName)
                ?.Attribute(s_style + "master-page-name")?.Value;
        }

        Assert.Null(MasterOf(paragraphs[0]));
        Assert.Equal("DocxSection1", MasterOf(paragraphs[1]));
        Assert.Equal("DocxSection2", MasterOf(paragraphs[2]));

        OdfValidationReport report = Validate14(odt);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.Message)));
    }

    /// <summary>
    /// 驗證頁面設定完全相同的章節（含繼承來的頁首）不會建立多餘的主頁面。
    /// </summary>
    [Fact]
    public void DocxSectionsWithIdenticalPageSetupShareTheMasterPage()
    {
        using MemoryStream docx = CreateDocx(body =>
        {
            body.Append(new WP.Paragraph(
                new WP.ParagraphProperties(new WP.SectionProperties(new WP.PageSize { Width = 11906U, Height = 16838U })),
                new WP.Run(new WP.Text("甲"))));
            body.Append(new WP.Paragraph(new WP.Run(new WP.Text("乙"))));
            body.Append(new WP.SectionProperties(new WP.PageSize { Width = 11906U, Height = 16838U }));
        });

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        XElement styles = XElement.Parse(SaveStylesXml(odt));
        Assert.Single(styles.Descendants(s_style + "master-page"));
        Assert.DoesNotContain("master-page-name", SaveContentXml(odt), StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證超連結內的分頁符號：連結在分頁處切成兩段，兩段各自保留相同的目標，後一段從新的一頁開始。
    /// 修正前只處理段落直接子層的分頁符號，連結內的分頁符號被丟棄而兩段文字黏在一起。
    /// </summary>
    [Fact]
    public void DocxPageBreakInsideHyperlinkSplitsTheLink()
    {
        using MemoryStream docx = CreateDocx(
            body =>
            {
                body.Append(new WP.Paragraph(
                    new WP.Hyperlink(
                        new WP.Run(new WP.Text("前段")),
                        new WP.Run(new WP.Break { Type = WP.BreakValues.Page }),
                        new WP.Run(new WP.Text("後段"))) { Id = "rIdLink" }));
            },
            main => main.AddHyperlinkRelationship(new Uri("https://example.org/split"), true, "rIdLink"));

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        XElement content = XElement.Parse(SaveContentXml(odt));
        XElement[] paragraphs = content.Descendants(s_text + "p").ToArray();
        Assert.Equal(["前段", "後段"], paragraphs.Select(paragraph => paragraph.Value).ToArray());
        Assert.All(
            paragraphs,
            paragraph => Assert.Equal(
                "https://example.org/split",
                (string?)paragraph.Element(s_text + "a")!.Attribute(s_xlink + "href")));

        XNamespace fo = OdfNamespaces.Fo;
        string? secondStyle = (string?)paragraphs[1].Attribute(s_text + "style-name");
        Assert.Equal(
            "page",
            content.Descendants(s_style + "style")
                .Single(item => (string?)item.Attribute(s_style + "name") == secondStyle)
                .Element(s_style + "paragraph-properties")!.Attribute(fo + "break-before")!.Value);
        Assert.Null(paragraphs[0].Attribute(s_text + "style-name"));
    }

    /// <summary>
    /// 驗證超連結欄位（<c>HYPERLINK</c>，Word 常以複雜欄位儲存）轉成 <c>text:a</c>、日期與文件屬性欄位轉成對應的
    /// ODF 欄位，且文件通過 ODF 1.4 schema 驗證。修正前這些欄位只留下結果文字，連結與自動更新都遺失。
    /// </summary>
    [Fact]
    public void DocxHyperlinkAndMetadataFieldsConvertToOdfFields()
    {
        static WP.Run Begin() => new(new WP.FieldChar { FieldCharType = WP.FieldCharValues.Begin });
        static WP.Run Separate() => new(new WP.FieldChar { FieldCharType = WP.FieldCharValues.Separate });
        static WP.Run End() => new(new WP.FieldChar { FieldCharType = WP.FieldCharValues.End });
        static WP.Run Code(string text) => new(new WP.FieldCode(text) { Space = SpaceProcessingModeValues.Preserve });
        static WP.Run Text(string text) => new(new WP.Text(text) { Space = SpaceProcessingModeValues.Preserve });

        using MemoryStream docx = CreateDocx(body =>
        {
            body.Append(new WP.Paragraph(
                Text("見："),
                Begin(), Code(" HYPERLINK \"https://example.org/field\" "), Separate(), Text("欄位連結"), End(),
                Text("；"),
                Begin(), Code(" HYPERLINK \\l \"mark\" "), Separate(), Text("書籤連結"), End()));
            body.Append(new WP.Paragraph(
                Begin(), Code(" HYPERLINK \"javascript:alert(1)\" "), Separate(), Text("危險連結"), End()));
            body.Append(new WP.Paragraph(
                Text("日期："),
                new WP.SimpleField(Text("2026年1月2日")) { Instruction = " DATE \\@ \"yyyy年M月d日\" " },
                Text(" 標題："),
                Begin(), Code(" TITLE "), Separate(), Text("範例標題"), End()));
        });

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        XElement content = XElement.Parse(SaveContentXml(odt));
        XElement[] paragraphs = content.Descendants(s_text + "p").ToArray();

        XElement[] anchors = paragraphs[0].Elements(s_text + "a").ToArray();
        Assert.Equal(["https://example.org/field", "#mark"], anchors.Select(item => (string?)item.Attribute(s_xlink + "href")).ToArray());
        Assert.Equal(["欄位連結", "書籤連結"], anchors.Select(item => item.Value).ToArray());
        Assert.Equal("見：欄位連結；書籤連結", paragraphs[0].Value);

        Assert.Empty(paragraphs[1].Descendants(s_text + "a"));
        Assert.Equal("危險連結", paragraphs[1].Value);

        XElement date = paragraphs[2].Element(s_text + "date")!;
        Assert.Equal("false", (string?)date.Attribute(s_text + "fixed"));
        Assert.Equal("2026年1月2日", date.Value);
        Assert.Equal("範例標題", paragraphs[2].Element(s_text + "title")!.Value);

        OdfValidationReport report = Validate14(odt);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.Message)));
    }

    private static WP.Run FieldBegin() => new(new WP.FieldChar { FieldCharType = WP.FieldCharValues.Begin });

    private static WP.Run FieldSeparate() => new(new WP.FieldChar { FieldCharType = WP.FieldCharValues.Separate });

    private static WP.Run FieldEnd() => new(new WP.FieldChar { FieldCharType = WP.FieldCharValues.End });

    private static WP.Run FieldCode(string text) => new(new WP.FieldCode(text) { Space = SpaceProcessingModeValues.Preserve });

    private static WP.Run PlainRun(string text) => new(new WP.Text(text) { Space = SpaceProcessingModeValues.Preserve });

    /// <summary>
    /// 驗證書籤與交互參照：書籤起訖轉成 <c>text:bookmark-start</c> 與 <c>text:bookmark-end</c>（終點以編號配對名稱），
    /// <c>REF</c> 與 <c>PAGEREF</c> 欄位轉成 <c>text:bookmark-ref</c>，內部超連結的 <c>#書籤</c> 目標因此不再懸空。
    /// 修正前書籤整個被丟棄，所有內部連結與交互參照都指向不存在的目標。
    /// </summary>
    [Fact]
    public void DocxBookmarksAndCrossReferencesConvert()
    {
        using MemoryStream docx = CreateDocx(body =>
        {
            body.Append(new WP.Paragraph(
                new WP.BookmarkStart { Id = "1", Name = "Target" },
                PlainRun("目標文字"),
                new WP.BookmarkEnd { Id = "1" },
                new WP.BookmarkStart { Id = "2", Name = "_GoBack" },
                new WP.BookmarkEnd { Id = "2" }));
            body.Append(new WP.Paragraph(
                PlainRun("見"),
                FieldBegin(), FieldCode(" REF Target \\h "), FieldSeparate(), PlainRun("目標文字"), FieldEnd(),
                PlainRun("，第"),
                FieldBegin(), FieldCode(" PAGEREF Target \\h "), FieldSeparate(), PlainRun("3"), FieldEnd(),
                PlainRun("頁；"),
                new WP.Hyperlink(new WP.Run(new WP.Text("內部連結"))) { Anchor = "Target" }));
        });

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        XElement content = XElement.Parse(SaveContentXml(odt));
        XElement[] paragraphs = content.Descendants(s_text + "p").ToArray();

        Assert.Equal("Target", (string?)paragraphs[0].Element(s_text + "bookmark-start")!.Attribute(s_text + "name"));
        Assert.Equal("Target", (string?)paragraphs[0].Element(s_text + "bookmark-end")!.Attribute(s_text + "name"));
        Assert.DoesNotContain("_GoBack", SaveContentXml(odt), StringComparison.Ordinal);

        XElement[] references = paragraphs[1].Elements(s_text + "bookmark-ref").ToArray();
        Assert.Equal(["text", "page"], references.Select(item => (string?)item.Attribute(s_text + "reference-format")).ToArray());
        Assert.All(references, item => Assert.Equal("Target", (string?)item.Attribute(s_text + "ref-name")));
        Assert.Equal(["目標文字", "3"], references.Select(item => item.Value).ToArray());
        Assert.Equal("#Target", (string?)paragraphs[1].Element(s_text + "a")!.Attribute(s_xlink + "href"));

        OdfValidationReport report = Validate14(odt);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.Message)));
    }

    /// <summary>
    /// 驗證區塊層級的內容控制項（<c>w:sdt</c>）內的段落與表格視為本文：封面、書目與目錄常包在其中，
    /// 修正前整個區塊被丟棄；頁首頁尾內的頁碼建置區塊也一樣包在內容控制項裡。
    /// </summary>
    [Fact]
    public void DocxBlockLevelContentControlsKeepTheirContent()
    {
        using MemoryStream docx = CreateDocx(
            body =>
            {
                body.Append(new WP.SdtBlock(new WP.SdtContentBlock(
                    new WP.Paragraph(PlainRun("內容控制項段落")),
                    new WP.SdtBlock(new WP.SdtContentBlock(new WP.Paragraph(PlainRun("巢狀內容控制項段落")))))));
                body.Append(new WP.Paragraph(PlainRun("一般段落")));
            },
            main =>
            {
                FooterPart footer = main.AddNewPart<FooterPart>();
                footer.Footer = new WP.Footer(new WP.SdtBlock(new WP.SdtContentBlock(new WP.Paragraph(
                    PlainRun("第"),
                    FieldBegin(), FieldCode(" PAGE "), FieldSeparate(), PlainRun("1"), FieldEnd(),
                    PlainRun("頁")))));
                footer.Footer.Save();
                main.Document!.Body!.Append(new WP.SectionProperties(
                    new WP.FooterReference { Type = WP.HeaderFooterValues.Default, Id = main.GetIdOfPart(footer) }));
            });

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        XElement content = XElement.Parse(SaveContentXml(odt));
        Assert.Equal(
            ["內容控制項段落", "巢狀內容控制項段落", "一般段落"],
            content.Descendants(s_text + "p").Select(paragraph => paragraph.Value).ToArray());

        XElement styles = XElement.Parse(SaveStylesXml(odt));
        Assert.Single(styles.Descendants(s_style + "footer").Descendants(s_text + "page-number"));
    }

    /// <summary>
    /// 驗證目錄欄位（<c>TOC</c>）轉成 <c>text:table-of-content</c>：標題層級範圍寫入來源，Word 儲存的目錄項目
    /// （含超連結與巢狀的 <c>PAGEREF</c>）放在 <c>text:index-body</c>，欄位起訖字元不產生空白段落。
    /// 修正前目錄項目只剩一般段落，欄位的目錄語意與更新能力都遺失。
    /// </summary>
    [Fact]
    public void DocxTableOfContentsConvertsToIndex()
    {
        static WP.Paragraph Entry(string style, string text, string bookmark, string page, bool first, bool last)
        {
            var paragraph = new WP.Paragraph(new WP.ParagraphProperties(new WP.ParagraphStyleId { Val = style }));
            if (first)
            {
                paragraph.Append(FieldBegin(), FieldCode(" TOC \\o \"1-3\" \\h \\z \\u "), FieldSeparate());
            }

            paragraph.Append(new WP.Hyperlink(
                new WP.Run(new WP.Text(text)),
                new WP.Run(new WP.TabChar()),
                FieldBegin(), FieldCode(" PAGEREF " + bookmark + " \\h "), FieldSeparate(), PlainRun(page), FieldEnd()) { Anchor = bookmark });
            if (last)
            {
                paragraph.Append(FieldEnd());
            }

            return paragraph;
        }

        using MemoryStream docx = CreateDocx(body =>
        {
            body.Append(new WP.SdtBlock(new WP.SdtContentBlock(
                new WP.Paragraph(PlainRun("目錄")),
                Entry("TOC1", "第一章", "_Toc1", "1", first: true, last: false),
                Entry("TOC2", "第一節", "_Toc2", "2", first: false, last: false),
                new WP.Paragraph(FieldEnd()))));
            body.Append(new WP.Paragraph(
                new WP.ParagraphProperties(new WP.ParagraphStyleId { Val = "Heading1" }),
                new WP.BookmarkStart { Id = "10", Name = "_Toc1" },
                PlainRun("第一章"),
                new WP.BookmarkEnd { Id = "10" }));
            body.Append(new WP.Paragraph(new WP.Run(new WP.Text("內文"))));
        });

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        XElement content = XElement.Parse(SaveContentXml(odt));

        XElement toc = content.Descendants(s_text + "table-of-content").Single();
        XElement source = toc.Element(s_text + "table-of-content-source")!;
        Assert.Equal("3", (string?)source.Attribute(s_text + "outline-level"));
        Assert.Equal("true", (string?)source.Attribute(s_text + "use-outline-level"));

        XElement[] entries = toc.Element(s_text + "index-body")!.Elements(s_text + "p").ToArray();
        Assert.Equal(2, entries.Length);
        Assert.Equal(["#_Toc1", "#_Toc2"], entries.Select(entry => (string?)entry.Descendants(s_text + "a").Single().Attribute(s_xlink + "href")).ToArray());

        // 目錄之外：標題與內文照常轉換，標題上的 _Toc 書籤保留（目錄連結的目標）。
        Assert.Equal("目錄", content.Descendants(s_text + "p").First().Value);
        Assert.Single(content.Descendants(s_text + "h"));
        Assert.Equal("_Toc1", (string?)content.Descendants(s_text + "bookmark-start").Single().Attribute(s_text + "name"));

        OdfValidationReport report = Validate14(odt);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.Message)));
    }

    /// <summary>
    /// 驗證分欄：多欄章節的內容包進套用 <c>style:columns</c> 的 <c>text:section</c>（欄數、欄距、分隔線與不等寬的欄寬），
    /// 單欄章節不包。修正前分欄設定完全被忽略。
    /// </summary>
    [Fact]
    public void DocxColumnsConvertToSectionsWithColumnStyles()
    {
        using MemoryStream docx = CreateDocx(body =>
        {
            body.Append(new WP.Paragraph(
                new WP.ParagraphProperties(new WP.SectionProperties(new WP.Columns { ColumnCount = 1 })),
                PlainRun("單欄")));
            body.Append(new WP.Paragraph(PlainRun("雙欄甲")));
            body.Append(new WP.Paragraph(
                new WP.ParagraphProperties(new WP.SectionProperties(
                    new WP.SectionType { Val = WP.SectionMarkValues.Continuous },
                    new WP.Columns { ColumnCount = 2, Space = "720", Separator = true })),
                PlainRun("雙欄乙")));
            body.Append(new WP.Paragraph(
                new WP.ParagraphProperties(new WP.SectionProperties(
                    new WP.SectionType { Val = WP.SectionMarkValues.Continuous },
                    new WP.Columns(
                        new WP.Column { Width = "2000", Space = "400" },
                        new WP.Column { Width = "6000" }) { ColumnCount = 2, EqualWidth = false })),
                PlainRun("不等寬")));
            body.Append(new WP.Paragraph(PlainRun("結尾")));
            body.Append(new WP.SectionProperties(new WP.SectionType { Val = WP.SectionMarkValues.Continuous }));
        });

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        XElement content = XElement.Parse(SaveContentXml(odt));
        XNamespace fo = OdfNamespaces.Fo;

        XElement[] sections = content.Descendants(s_text + "section").ToArray();
        Assert.Equal(2, sections.Length);
        Assert.Equal(["雙欄甲", "雙欄乙"], sections[0].Elements(s_text + "p").Select(paragraph => paragraph.Value).ToArray());
        Assert.Equal(["不等寬"], sections[1].Elements(s_text + "p").Select(paragraph => paragraph.Value).ToArray());

        XElement Columns(XElement section)
        {
            string styleName = (string)section.Attribute(s_text + "style-name")!;
            return content.Descendants(s_style + "style")
                .Single(item => (string?)item.Attribute(s_style + "name") == styleName)
                .Element(s_style + "section-properties")!.Element(s_style + "columns")!;
        }

        XElement equal = Columns(sections[0]);
        Assert.Equal("2", (string?)equal.Attribute(fo + "column-count"));
        Assert.StartsWith("1.27", (string?)equal.Attribute(fo + "column-gap"), StringComparison.Ordinal);
        Assert.NotNull(equal.Element(s_style + "column-sep"));

        XElement unequal = Columns(sections[1]);
        Assert.Equal(["2000*", "6000*"], unequal.Elements(s_style + "column").Select(item => (string?)item.Attribute(s_style + "rel-width")).ToArray());

        // 單欄與結尾章節不包進 text:section。
        Assert.Equal(["單欄", "結尾"], content.Element(s_office + "body")!.Element(s_office + "text")!.Elements(s_text + "p").Select(paragraph => paragraph.Value).ToArray());

        OdfValidationReport report = Validate14(odt);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.Message)));
    }

    /// <summary>
    /// 驗證章節起點：頁面設定相同的下一頁章節仍換頁（<c>fo:break-before</c>）、連續章節不換頁、
    /// <c>w:pgNumType</c> 的起始頁碼寫成 <c>style:page-number</c>（搭配主頁面）。修正前同設定的章節沒有換頁，頁碼起始值被忽略。
    /// </summary>
    [Fact]
    public void DocxSectionStartsBreakPagesAndRestartPageNumbers()
    {
        using MemoryStream docx = CreateDocx(body =>
        {
            body.Append(new WP.Paragraph(
                new WP.ParagraphProperties(new WP.SectionProperties(new WP.PageSize { Width = 11906U, Height = 16838U })),
                PlainRun("第一節")));
            body.Append(new WP.Paragraph(
                new WP.ParagraphProperties(new WP.SectionProperties(
                    new WP.PageSize { Width = 11906U, Height = 16838U },
                    new WP.PageNumberType { Start = 5 })),
                PlainRun("第二節")));
            body.Append(new WP.Paragraph(
                new WP.ParagraphProperties(new WP.SectionProperties(
                    new WP.SectionType { Val = WP.SectionMarkValues.Continuous },
                    new WP.PageSize { Width = 11906U, Height = 16838U })),
                PlainRun("第三節連續")));
            body.Append(new WP.Paragraph(PlainRun("第四節")));
            body.Append(new WP.SectionProperties(new WP.PageSize { Width = 11906U, Height = 16838U }));
        });

        using TextDocument odt = DocxToOdtConverter.Convert(docx);
        XElement content = XElement.Parse(SaveContentXml(odt));
        XNamespace fo = OdfNamespaces.Fo;
        XElement[] paragraphs = content.Descendants(s_text + "p").ToArray();

        XElement? StyleOf(XElement paragraph)
        {
            string? name = (string?)paragraph.Attribute(s_text + "style-name");
            return content.Descendants(s_style + "style").FirstOrDefault(item => (string?)item.Attribute(s_style + "name") == name);
        }

        Assert.Null(StyleOf(paragraphs[0]));

        // 重新起算頁碼的章節以帶主頁面的換頁表示（LibreOffice 只在這種換頁上套用起始頁碼）。
        XElement secondStyle = StyleOf(paragraphs[1])!;
        Assert.Equal("Standard", (string?)secondStyle.Attribute(s_style + "master-page-name"));
        Assert.Equal("5", (string?)secondStyle.Element(s_style + "paragraph-properties")!.Attribute(s_style + "page-number"));

        // 連續章節沒有任何起點設定；最後一個下一頁章節（設定相同）換頁。
        Assert.Null(StyleOf(paragraphs[2]));
        Assert.Equal("page", (string?)StyleOf(paragraphs[3])!.Element(s_style + "paragraph-properties")!.Attribute(fo + "break-before"));

        OdfValidationReport report = Validate14(odt);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.Message)));
    }

    private const string FlatOdtWithLists = """
        <?xml version="1.0" encoding="UTF-8"?>
        <office:document xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
            xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
            xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0"
            xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0"
            office:version="1.3" office:mimetype="application/vnd.oasis.opendocument.text">
          <office:automatic-styles>
            <text:list-style style:name="Bullets">
              <text:list-level-style-bullet text:level="1" text:bullet-char="&#xF0B7;">
                <style:list-level-properties text:space-before="0.75cm" text:min-label-width="0.5cm"/>
              </text:list-level-style-bullet>
              <text:list-level-style-bullet text:level="2" text:bullet-char="o">
                <style:list-level-properties text:space-before="2cm" text:min-label-width="0.5cm"/>
              </text:list-level-style-bullet>
            </text:list-style>
            <text:list-style style:name="Steps">
              <text:list-level-style-number text:level="1" style:num-suffix=")" style:num-format="a" text:start-value="3">
                <style:list-level-properties text:space-before="0.5cm" text:min-label-width="0.6cm"/>
              </text:list-level-style-number>
              <text:list-level-style-number text:level="2" style:num-format="I" text:display-levels="2">
                <style:list-level-properties text:list-level-position-and-space-mode="label-alignment">
                  <style:list-level-label-alignment text:label-followed-by="listtab" fo:margin-left="2cm" fo:text-indent="-0.5cm"/>
                </style:list-level-properties>
              </text:list-level-style-number>
            </text:list-style>
          </office:automatic-styles>
          <office:body><office:text>
            <text:list text:style-name="Bullets">
              <text:list-item><text:p>項目甲</text:p>
                <text:list><text:list-item><text:p>子項乙</text:p></text:list-item></text:list>
              </text:list-item>
              <text:list-item><text:p>項目丙</text:p></text:list-item>
            </text:list>
            <text:p>清單之間的段落</text:p>
            <text:list text:style-name="Steps">
              <text:list-item><text:p>步驟一</text:p>
                <text:list><text:list-item><text:p>步驟一之一</text:p></text:list-item></text:list>
              </text:list-item>
            </text:list>
            <text:list text:style-name="Steps">
              <text:list-item><text:p>另一份編號清單</text:p></text:list-item>
            </text:list>
          </office:text></office:body>
        </office:document>
        """;

    /// <summary>
    /// 驗證 ODT → DOCX 的清單：ODF 清單樣式轉成編號定義（項目符號、字母與羅馬數字格式、前後綴、起始值、縮排），
    /// 項目轉成帶 <c>w:numPr</c> 的段落且層級正確，各自獨立的清單重新編號，輸出通過 Open XML SDK 驗證。
    /// 修正前轉換器完全不處理 <c>text:list</c>，所有清單項目整個消失。
    /// </summary>
    [Fact]
    public void OdtListsConvertToDocxNumbering()
    {
        using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(FlatOdtWithLists));
        using var odt = (TextDocument)OdfDocument.Load(source, "lists.fodt");

        using var docx = new MemoryStream();
        OdfToDocxConverter.Convert(odt, docx);
        docx.Position = 0;

        using WordprocessingDocument document = WordprocessingDocument.Open(docx, false);
        WP.Paragraph[] paragraphs = document.MainDocumentPart!.Document!.Body!.Elements<WP.Paragraph>().ToArray();
        Assert.Equal(
            ["項目甲", "子項乙", "項目丙", "清單之間的段落", "步驟一", "步驟一之一", "另一份編號清單"],
            paragraphs.Select(paragraph => paragraph.InnerText).ToArray());

        (int? Level, int? Id) Numbering(WP.Paragraph paragraph) =>
            (paragraph.ParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value,
             paragraph.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value);

        Assert.Equal(new int?[] { 0, 1, 0 }, paragraphs.Take(3).Select(paragraph => Numbering(paragraph).Level).ToArray());
        Assert.Null(Numbering(paragraphs[3]).Id);
        Assert.Equal(new int?[] { 0, 1 }, paragraphs.Skip(4).Take(2).Select(paragraph => Numbering(paragraph).Level).ToArray());

        // 兩份「Steps」清單共用同一個樣式定義但各自重新編號（不同的 w:num）。
        int? firstSteps = Numbering(paragraphs[4]).Id;
        int? secondSteps = Numbering(paragraphs[6]).Id;
        Assert.NotNull(firstSteps);
        Assert.NotEqual(firstSteps, secondSteps);
        Assert.NotEqual(Numbering(paragraphs[0]).Id, firstSteps);

        WP.Numbering numbering = document.MainDocumentPart.NumberingDefinitionsPart!.Numbering!;
        WP.AbstractNum[] abstracts = numbering.Elements<WP.AbstractNum>().ToArray();
        Assert.Equal(2, abstracts.Length);

        WP.Level[] bulletLevels = abstracts[0].Elements<WP.Level>().ToArray();
        Assert.Equal("bullet", bulletLevels[0].NumberingFormat!.Val!.InnerText);
        Assert.Equal("•", bulletLevels[0].LevelText!.Val!.Value);
        Assert.Equal("o", bulletLevels[1].LevelText!.Val!.Value);
        Assert.Equal("708", bulletLevels[0].PreviousParagraphProperties!.Indentation!.Left!.Value);
        Assert.Equal("283", bulletLevels[0].PreviousParagraphProperties!.Indentation!.Hanging!.Value);

        WP.Level[] stepLevels = abstracts[1].Elements<WP.Level>().ToArray();
        Assert.Equal("lowerLetter", stepLevels[0].NumberingFormat!.Val!.InnerText);
        Assert.Equal("%1)", stepLevels[0].LevelText!.Val!.Value);
        Assert.Equal(3, stepLevels[0].StartNumberingValue!.Val!.Value);
        Assert.Equal("upperRoman", stepLevels[1].NumberingFormat!.Val!.InnerText);
        Assert.Equal("%1.%2", stepLevels[1].LevelText!.Val!.Value);
        Assert.Equal("1134", stepLevels[1].PreviousParagraphProperties!.Indentation!.Left!.Value);
        Assert.Equal("283", stepLevels[1].PreviousParagraphProperties!.Indentation!.Hanging!.Value);

        var validator = new DocumentFormat.OpenXml.Validation.OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Office2019);
        Assert.Empty(validator.Validate(document, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 驗證 ODT → DOCX 的輸出符合 ECMA-376 的元素順序並有表格欄格線：段落屬性的 <c>w:spacing</c> 在 <c>w:ind</c> 之前、
    /// 表格邊框依上左下右排列、樣式的 <c>w:b</c> 在 <c>w:sz</c> 之前、表格有 <c>w:tblGrid</c>。
    /// 修正前輸出違反 schema（Word 對元素順序很嚴格，可能提示檔案損毀）。
    /// </summary>
    [Fact]
    public void OdtToDocxOutputFollowsSchemaOrderAndHasTableGrids()
    {
        using var odt = TextDocument.Create();
        odt.AddHeading("標題", 1);
        OdfParagraph paragraph = odt.AddParagraph("段落");
        paragraph.Node.SetAttribute("style-name", OdfNamespaces.Text, "Indented", "text");
        odt.AddHeading("三級標題", 3);
        OdfTable table = odt.AddTable(2, 3);
        table.GetCell(0, 0).TextContent = "甲";
        table.GetCell(1, 2).TextContent = "乙";

        using var docx = new MemoryStream();
        OdfToDocxConverter.Convert(odt, docx);
        docx.Position = 0;
        using WordprocessingDocument document = WordprocessingDocument.Open(docx, false);

        WP.Table wordTable = document.MainDocumentPart!.Document!.Body!.Elements<WP.Table>().Single();
        Assert.Equal(3, wordTable.Elements<WP.TableGrid>().Single().Elements<WP.GridColumn>().Count());
        Assert.IsType<WP.TableProperties>(wordTable.ChildElements[0]);
        Assert.IsType<WP.TableGrid>(wordTable.ChildElements[1]);
        Assert.Equal(
            ["top", "left", "bottom", "right", "insideH", "insideV"],
            wordTable.TableProperties!.TableBorders!.ChildElements.Select(child => child.LocalName).ToArray());

        WP.Style heading3 = document.MainDocumentPart.StyleDefinitionsPart!.Styles!.Elements<WP.Style>()
            .Single(style => style.StyleId?.Value == "Heading3");
        Assert.Equal(["b", "sz"], heading3.StyleRunProperties!.ChildElements.Select(child => child.LocalName).ToArray());

        var validator = new DocumentFormat.OpenXml.Validation.OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Office2019);
        Assert.Empty(validator.Validate(document, TestContext.Current.CancellationToken));
    }

    private const string FlatOdsFromLibreOffice = """
        <?xml version="1.0" encoding="UTF-8"?>
        <office:document xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
            xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
            xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0"
            xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0"
            xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0"
            xmlns:number="urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0"
            office:version="1.3" office:mimetype="application/vnd.oasis.opendocument.spreadsheet">
          <office:automatic-styles>
            <number:date-style style:name="N49">
              <number:year number:style="long"/><number:text>-</number:text><number:month number:style="long"/><number:text>-</number:text><number:day number:style="long"/>
            </number:date-style>
            <number:number-style style:name="N4"><number:number number:decimal-places="2" number:min-integer-digits="1" number:grouping="true"/></number:number-style>
            <number:percentage-style style:name="N11"><number:number number:decimal-places="1" number:min-integer-digits="1"/><number:text>%</number:text></number:percentage-style>
            <number:currency-style style:name="N105"><number:currency-symbol>NT$</number:currency-symbol><number:number number:decimal-places="0" number:grouping="true"/></number:currency-style>
            <style:style style:name="ceDate" style:family="table-cell" style:data-style-name="N49"/>
            <style:style style:name="ceNumber" style:family="table-cell" style:data-style-name="N4"/>
            <style:style style:name="cePercent" style:family="table-cell" style:data-style-name="N11"/>
            <style:style style:name="ceCurrency" style:family="table-cell" style:data-style-name="N105"/>
          </office:automatic-styles>
          <office:body><office:spreadsheet>
            <table:table table:name="資料">
              <table:table-row>
                <table:table-cell table:style-name="ceDate" office:value-type="date" office:date-value="2024-01-03"><text:p>2024-01-03</text:p></table:table-cell>
                <table:table-cell table:style-name="ceNumber" office:value-type="float" office:value="1234567.891"><text:p>1,234,567.89</text:p></table:table-cell>
                <table:table-cell table:style-name="cePercent" office:value-type="percentage" office:value="0.256"><text:p>25.6%</text:p></table:table-cell>
                <table:table-cell table:style-name="ceCurrency" office:value-type="currency" office:currency="TWD" office:value="1500"><text:p>NT$1,500</text:p></table:table-cell>
                <table:table-cell office:value-type="time" office:time-value="PT01H30M00S"><text:p>01:30:00</text:p></table:table-cell>
                <table:table-cell table:number-columns-spanned="2" table:number-rows-spanned="2" office:value-type="string"><text:p>合併</text:p></table:table-cell>
                <table:covered-table-cell/>
              </table:table-row>
              <table:table-row>
                <table:table-cell table:formula="of:=[.B1]*2" office:value-type="float" office:value="2469135.782"><text:p>2469135.78</text:p></table:table-cell>
                <table:table-cell table:formula="of:=SUM([.B1:.B1];[$第二頁.A1])" office:value-type="float" office:value="1234568.891"><text:p>1234568.89</text:p></table:table-cell>
                <table:table-cell table:formula="of:=IF([.A1]&gt;0;&quot;a;b&quot;;[$'含 空白'.C3])" office:value-type="string"><text:p>a;b</text:p></table:table-cell>
              </table:table-row>
              <table:table-row table:number-rows-repeated="1048000">
                <table:table-cell table:style-name="ceDate" table:number-columns-repeated="1024"/>
              </table:table-row>
            </table:table>
            <table:table table:name="第二頁">
              <table:table-row><table:table-cell office:value-type="float" office:value="1"><text:p>1</text:p></table:table-cell></table:table-row>
            </table:table>
          </office:spreadsheet></office:body>
        </office:document>
        """;

    /// <summary>
    /// 驗證 ODS → XLSX 涵蓋 LibreOffice 儲存的試算表常見的結構：OpenFormula 方括號參照（含跨工作表與需要引號的名稱）與分號分隔、
    /// 日期、時間、百分比與貨幣值、合併儲存格、數字格式（日期、千分位小數、百分比、貨幣），
    /// 以及「只有樣式的重複列」（LibreOffice 對有欄格式的資料會一路寫到第 1,048,576 列），輸出通過 Open XML SDK 驗證。
    /// 修正前公式保留方括號（Excel 無法計算）、日期變成文字、合併儲存格遺失、數字格式遺失，
    /// 且只有樣式的重複列使轉換幾乎不會結束。
    /// </summary>
    [Fact(Timeout = 120_000)]
    public void OdsFromLibreOfficeConvertsFormulasDatesMergesAndNumberFormats()
    {
        using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(FlatOdsFromLibreOffice));
        using var ods = (OdfSpreadsheetDocument)OdfDocument.Load(source, "libreoffice.fods");

        var stopwatch = Stopwatch.StartNew();
        using var xlsx = new MemoryStream();
        OdfToXlsxConverter.Convert(ods, xlsx);
        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(60), $"轉換耗時 {stopwatch.Elapsed.TotalSeconds:N1} 秒。");

        xlsx.Position = 0;
        using var workbook = new XLWorkbook(xlsx);
        IXLWorksheet sheet = workbook.Worksheet("資料");

        Assert.Equal(ClosedXML.Excel.XLDataType.DateTime, sheet.Cell("A1").DataType);
        Assert.Equal(new DateTime(2024, 1, 3), sheet.Cell("A1").GetDateTime());
        Assert.Equal("yyyy-mm-dd", sheet.Cell("A1").Style.DateFormat.Format);

        Assert.Equal(1234567.891, sheet.Cell("B1").GetDouble(), 3);
        Assert.Equal("#,##0.00", sheet.Cell("B1").Style.NumberFormat.Format);
        Assert.Equal(0.256, sheet.Cell("C1").GetDouble(), 3);
        Assert.Equal("0.0%", sheet.Cell("C1").Style.NumberFormat.Format);
        Assert.Equal(1500, sheet.Cell("D1").GetDouble(), 3);
        Assert.Equal("NT$#,##0", sheet.Cell("D1").Style.NumberFormat.Format.Replace("\"", string.Empty));
        Assert.Equal(ClosedXML.Excel.XLDataType.TimeSpan, sheet.Cell("E1").DataType);
        Assert.Equal("1:30:00", sheet.Cell("E1").GetString());
        Assert.Equal("[h]:mm:ss", sheet.Cell("E1").Style.NumberFormat.Format);

        Assert.Contains(sheet.MergedRanges, range => range.RangeAddress.ToStringRelative() == "F1:G2");

        Assert.Equal("B1*2", sheet.Cell("A2").FormulaA1);
        Assert.Equal("SUM(B1:B1,第二頁!A1)", sheet.Cell("B2").FormulaA1);
        Assert.Equal("IF(A1>0,\"a;b\",'含 空白'!C3)", sheet.Cell("C2").FormulaA1);

        // 只有樣式的重複列轉成整欄的預設格式，而不是數百萬個空白儲存格。
        Assert.Equal("yyyy-mm-dd", sheet.Column(1).Style.DateFormat.Format);
        Assert.True(sheet.CellsUsed().Count() < 50, $"已使用儲存格 {sheet.CellsUsed().Count()} 個。");

        xlsx.Position = 0;
        using DocumentFormat.OpenXml.Packaging.SpreadsheetDocument package = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(xlsx, false);
        var validator = new DocumentFormat.OpenXml.Validation.OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Office2019);
        Assert.Empty(validator.Validate(package, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 驗證簡報的表格與版面配置符合 ODF 1.4 schema：嵌入表格有 <c>table:table-column</c>（欄定義在列之前），
    /// 版面配置（<c>style:presentation-page-layout</c>）寫在 <c>office:styles</c> 而不是 <c>office:automatic-styles</c>，
    /// 版面配置的預留位置以 <c>presentation:object</c> 標示類型；經 PPTX 往返後（PPTX → ODP 的表格放在
    /// <c>draw:frame</c> 內而不是 <c>draw:rect</c>）仍然合法。修正前這些都違反 schema，嚴格驗證回報整份文件不符。
    /// </summary>
    [Fact]
    public void PresentationTablesAndLayoutsAreSchemaValidAndSurvivePptxRoundTrip()
    {
        using var presentation = OdfKit.Presentation.PresentationDocument.Create();
        OdfKit.Presentation.OdfSlide first = presentation.AddSlide();
        first.AddTextBox(
            OdfKit.Styles.OdfLength.FromCentimeters(2),
            OdfKit.Styles.OdfLength.FromCentimeters(1),
            OdfKit.Styles.OdfLength.FromCentimeters(20),
            OdfKit.Styles.OdfLength.FromCentimeters(3),
            "標題");
        OdfKit.Presentation.OdfSlide second = presentation.AddSlide();
        second.AddTable(
            2,
            3,
            OdfKit.Styles.OdfLength.FromCentimeters(2),
            OdfKit.Styles.OdfLength.FromCentimeters(5),
            OdfKit.Styles.OdfLength.FromCentimeters(15),
            OdfKit.Styles.OdfLength.FromCentimeters(4));
        presentation.CreatePresentationPageLayout("LayoutTitle").AddPlaceholder(
            OdfKit.Presentation.OdfPlaceholderType.Title,
            OdfKit.Styles.OdfLength.FromCentimeters(2),
            OdfKit.Styles.OdfLength.FromCentimeters(1.5),
            OdfKit.Styles.OdfLength.FromCentimeters(24),
            OdfKit.Styles.OdfLength.FromCentimeters(3));

        OdfValidationReport original = Validate14(presentation);
        Assert.True(original.IsValid, string.Join("; ", original.Issues.Select(issue => issue.Message)));

        XElement stylesXml = XElement.Parse(SaveStylesXml(presentation));
        XElement layout = stylesXml.Element(s_office + "styles")!.Element(s_style + "presentation-page-layout")!;
        Assert.Equal("LayoutTitle", (string?)layout.Attribute(s_style + "name"));
        XNamespace presentationNs = OdfNamespaces.Presentation;
        Assert.Equal("title", (string?)layout.Element(presentationNs + "placeholder")!.Attribute(presentationNs + "object"));
        Assert.Empty(stylesXml.Element(s_office + "automatic-styles")?.Elements(s_style + "presentation-page-layout") ?? []);
        Assert.NotNull(presentation.FindPresentationPageLayout("LayoutTitle"));

        XElement table = XElement.Parse(SaveContentXml(presentation)).Descendants(s_table + "table").Single();
        Assert.Equal("3", (string?)table.Element(s_table + "table-column")!.Attribute(s_table + "number-columns-repeated"));
        Assert.Equal(s_table + "table-column", table.Elements().First().Name);

        using var pptx = new MemoryStream();
        OdpToPptxConverter.Convert(presentation, pptx);
        pptx.Position = 0;
        using OdfKit.Presentation.PresentationDocument roundTrip = PptxToOdpConverter.Convert(pptx);
        OdfValidationReport report = Validate14(roundTrip);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.Message)));

        XElement roundTripContent = XElement.Parse(SaveContentXml(roundTrip));
        XElement roundTripTable = roundTripContent.Descendants(s_table + "table").Single();
        Assert.Equal(XName.Get("frame", OdfNamespaces.Draw), roundTripTable.Parent!.Name);
    }

    // ---------- helpers ----------

    private static WP.Paragraph ListParagraph(string text, int numberingId, int level) =>
        new(
            new WP.ParagraphProperties(
                new WP.NumberingProperties(
                    new WP.NumberingLevelReference { Val = level },
                    new WP.NumberingId { Val = numberingId })),
            new WP.Run(new WP.Text(text)));

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
            fill(body);
            configure?.Invoke(main);
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

    private static string SaveStylesXml(OdfDocument document)
    {
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream stylesStream = package.GetEntryStream("styles.xml");
        using var reader = new StreamReader(stylesStream);
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
