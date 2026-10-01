using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;
using OdfSpreadsheetDocument = OdfKit.Spreadsheet.SpreadsheetDocument;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定以真實性驗證（把 Office 風格的文件轉成 ODF 並對照已知真值）所發現的核心缺陷：
/// 空白字元編碼（ODF 1.3 §6.1.2）、超連結的 <c>xlink:type</c>、空列的 schema 有效性，
/// 以及列數多的工作表 schema 驗證耗時。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Regression)]
public sealed class RealWorldFidelityTests
{
    private static readonly XNamespace s_text = OdfNamespaces.Text;
    private static readonly XNamespace s_table = OdfNamespaces.Table;
    private static readonly XNamespace s_office = OdfNamespaces.Office;
    private static readonly XNamespace s_xlink = OdfNamespaces.XLink;

    // ---------- 空白字元編碼 ----------

    /// <summary>
    /// 驗證設定段落的 <c>TextContent</c> 時，連續空格、定位字元與換行會寫成 <c>text:s</c>、
    /// <c>text:tab</c> 與 <c>text:line-break</c>，而不是會被消費端折疊的字面空白。
    /// </summary>
    [Fact]
    public void ParagraphTextContentEncodesWhitespaceAsOdfElements()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph("a    b\tc\nd");

        XElement element = ParseContentParagraph(SaveContentXml(document), 0);
        Assert.Equal(
            new[] { "a ", "s:3", "b", "tab", "c", "line-break", "d" },
            DescribeChildren(element));
        Assert.Equal("a    b\tc\nd", paragraph.TextContent);
    }

    /// <summary>
    /// 驗證段落開頭與結尾的空格一律以 <c>text:s</c> 表示（字面的開頭與結尾空格會被消費端移除）。
    /// </summary>
    [Fact]
    public void ParagraphLeadingAndTrailingSpacesUseTextSElements()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("  lead");
        document.AddParagraph("trail  ");
        document.AddParagraph(" ");

        string contentXml = SaveContentXml(document);
        Assert.Equal(new[] { "s:2", "lead" }, DescribeChildren(ParseContentParagraph(contentXml, 0)));
        Assert.Equal(new[] { "trail", "s:2" }, DescribeChildren(ParseContentParagraph(contentXml, 1)));
        Assert.Equal(new[] { "s:1" }, DescribeChildren(ParseContentParagraph(contentXml, 2)));
    }

    /// <summary>
    /// 驗證不含需要編碼之空白的一般文字仍維持單一文字節點，不增加不必要的元素。
    /// </summary>
    [Fact]
    public void PlainParagraphTextStaysSingleTextNode()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("一般 文字 沒有 連續空白");

        Assert.Equal(
            new[] { "一般 文字 沒有 連續空白" },
            DescribeChildren(ParseContentParagraph(SaveContentXml(document), 0)));
    }

    /// <summary>
    /// 驗證含空白的段落存檔後重新載入，文字內容不變。
    /// </summary>
    [Fact]
    public void ParagraphWhitespaceSurvivesSaveAndReload()
    {
        string text = "  開頭\t定位   連續空白\n換行  ";
        using var stream = new MemoryStream();
        using (TextDocument document = TextDocument.Create())
        {
            document.AddParagraph(text);
            document.SaveToStream(stream);
        }

        stream.Position = 0;
        using TextDocument reloaded = TextDocument.Load(stream);
        Assert.Equal(text, reloaded.Body.Paragraphs.First().TextContent);
    }

    /// <summary>
    /// 驗證儲存格文字開頭與結尾的空格以 <c>text:s</c> 表示。
    /// </summary>
    [Fact]
    public void CellLeadingAndTrailingSpacesUseTextSElements()
    {
        using var document = OdfSpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("S");
        sheet.Cells["A1"].CellValue = "  lead";
        sheet.Cells["A2"].CellValue = "trail ";
        sheet.Cells["A3"].CellValue = "mid  dle";

        XElement content = XElement.Parse(SaveContentXml(document));
        List<XElement> paragraphs = content.Descendants(s_text + "p").ToList();
        Assert.Equal(new[] { "s:2", "lead" }, DescribeChildren(paragraphs[0]));
        Assert.Equal(new[] { "trail", "s:1" }, DescribeChildren(paragraphs[1]));
        Assert.Equal(new[] { "mid ", "s:1", "dle" }, DescribeChildren(paragraphs[2]));
    }

    // ---------- 超連結與 schema 有效性 ----------

    /// <summary>
    /// 驗證 <c>AddHyperlink</c> 寫出的 <c>text:a</c> 帶有 schema 必要的 <c>xlink:type="simple"</c>。
    /// </summary>
    [Fact]
    public void AddHyperlinkWritesRequiredXlinkType()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("前").AddHyperlink("https://example.org/", "連結");

        XElement anchor = XElement.Parse(SaveContentXml(document)).Descendants(s_text + "a").Single();
        Assert.Equal("simple", (string?)anchor.Attribute(s_xlink + "type"));
        Assert.Equal("https://example.org/", (string?)anchor.Attribute(s_xlink + "href"));
    }

    /// <summary>
    /// 驗證含超連結的文字文件與含跳過列的試算表都通過 ODF 1.4 schema 驗證。
    /// 兩者原本分別缺少 <c>xlink:type</c>，以及產生沒有任何儲存格的 <c>table:table-row</c>。
    /// </summary>
    [Fact]
    public void GeneratedDocumentsPassOdf14SchemaValidation()
    {
        using TextDocument text = TextDocument.Create();
        text.AddParagraph("內容").AddHyperlink("https://example.org/", "連結");
        Assert.True(Validate14(text).IsValid);

        using var sheetDocument = OdfSpreadsheetDocument.Create();
        OdfTableSheet sheet = sheetDocument.Worksheets.Add("S");
        sheet.GetCell(30, 2).CellValue = "遠端儲存格";
        Assert.True(Validate14(sheetDocument).IsValid);
    }

    /// <summary>
    /// 驗證存取試算表遠端位址所建立的中間列都至少含一個儲存格。
    /// </summary>
    [Fact]
    public void SkippedSheetRowsContainAtLeastOneCell()
    {
        using var document = OdfSpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("S");
        sheet.GetCell(40, 3).CellValue = "x";

        XElement content = XElement.Parse(SaveContentXml(document));
        List<XElement> rows = content.Descendants(s_table + "table-row").ToList();
        Assert.Equal(41, rows.Count);
        Assert.All(rows, row => Assert.NotEmpty(row.Elements()));
    }

    /// <summary>
    /// 驗證列數多的工作表可在合理時間內通過 schema 驗證。
    /// 修正前每一列都會被重複驗證與列數成正比的次數，2,000 列約需 70 秒；修正後約數秒。
    /// </summary>
    [Fact]
    public void SchemaValidationOfManyRowsCompletesInReasonableTime()
    {
        using var document = OdfSpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("S");
        for (int row = 0; row < 2000; row++)
        {
            sheet.GetCell(row, 0).CellValue = row;
        }

        var stopwatch = Stopwatch.StartNew();
        OdfValidationReport report = Validate14(document);
        stopwatch.Stop();

        Assert.True(report.IsValid);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(60), $"驗證 2,000 列耗時 {stopwatch.Elapsed.TotalSeconds:N1} 秒。");
    }

    // ---------- helpers ----------

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
