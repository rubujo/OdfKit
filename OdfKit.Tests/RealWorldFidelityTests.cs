using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
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
/// 列數多的工作表 schema 驗證耗時，以及延遲載入與驗證器對 LibreOffice 真實文件的處理。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Regression)]
public sealed class RealWorldFidelityTests
{
    private static readonly XNamespace s_text = OdfNamespaces.Text;
    private static readonly XNamespace s_style = OdfNamespaces.Style;
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
            ["a ", "s:3", "b", "tab", "c", "line-break", "d"],
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
        Assert.Equal(["s:2", "lead"], DescribeChildren(ParseContentParagraph(contentXml, 0)));
        Assert.Equal(["trail", "s:2"], DescribeChildren(ParseContentParagraph(contentXml, 1)));
        Assert.Equal(["s:1"], DescribeChildren(ParseContentParagraph(contentXml, 2)));
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
            ["一般 文字 沒有 連續空白"],
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
        Assert.Equal(["s:2", "lead"], DescribeChildren(paragraphs[0]));
        Assert.Equal(["trail", "s:1"], DescribeChildren(paragraphs[1]));
        Assert.Equal(["mid ", "s:1", "dle"], DescribeChildren(paragraphs[2]));
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
    /// 修正前每一列都會被重複驗證與列數成正比的次數，2,000 列約需 70 秒，依平方成長推估 5,000 列超過 7 分鐘；
    /// 修正後 4,000 列在一般開發機約數秒；CI 開啟覆蓋率並使用較慢的執行器，實測可慢十倍以上，
    /// 因此上限取 120 秒，仍遠低於修正前的耗時（依平方成長推估 4,000 列在一般開發機約需 5 分鐘）。
    /// </summary>
    [Fact]
    public void SchemaValidationOfManyRowsCompletesInReasonableTime()
    {
        using var document = OdfSpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("S");
        for (int row = 0; row < 4000; row++)
        {
            sheet.GetCell(row, 0).CellValue = row;
        }

        var stopwatch = Stopwatch.StartNew();
        OdfValidationReport report = Validate14(document);
        stopwatch.Stop();

        Assert.True(report.IsValid);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(120), $"驗證 4,000 列耗時 {stopwatch.Elapsed.TotalSeconds:N1} 秒。");
    }

    /// <summary>
    /// 驗證 schema 驗證的工作量隨列數近乎線性成長：列數變成兩倍，序列推進走訪的位置數不應接近四倍（平方成長）。
    /// 序列中的重複節點會回傳與列數同階的位置集合，而同一個序列又從每個起點被詢問；
    /// 逐一走訪輸入集合使總成本隨列數平方成長（修正前 20,000 列約十四秒、80,000 列約兩分鐘）。
    /// 以走訪的位置數而不是耗時判斷：位置數是確定的，不受執行器速度與並行測試影響。
    /// </summary>
    [Fact]
    public void SchemaValidationWorkGrowsLinearlyWithRowCount()
    {
        static long VisitsFor(int rows)
        {
            using var document = OdfSpreadsheetDocument.Create();
            OdfTableSheet sheet = document.Worksheets.Add("S");
            for (int row = 0; row < rows; row++)
            {
                sheet.GetCell(row, 0).CellValue = row;
            }

            OdfSchemaPatternContentMatcher.SequenceStepPositionVisits = 0;
            Assert.True(Validate14(document).IsValid);
            return OdfSchemaPatternContentMatcher.SequenceStepPositionVisits;
        }

        long small = VisitsFor(2000);
        long large = VisitsFor(4000);

        // 線性約 2 倍、平方約 4 倍；小規模時固定成本占比較高，因此門檻取 3。
        Assert.True(large < small * 3, $"2,000 列走訪 {small:N0} 個位置、4,000 列走訪 {large:N0} 個位置。");
        Assert.True(large < 4000L * 200, $"4,000 列走訪 {large:N0} 個位置，超過每列 200 個。");
    }

    // ---------- 延遲載入與驗證器對真實 LibreOffice 文件的處理 ----------

    private const string CalcExtNamespace = "urn:org:documentfoundation:names:experimental:calc:xmlns:calcext:1.0";
    private const string LoExtNamespace = "urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0";

    /// <summary>
    /// 驗證 8 KB 以上的 <c>table:table</c>（延遲具現化的子樹）含有宣告在文件根元素的額外前綴
    /// （LibreOffice 在每個儲存格寫 <c>calcext:value-type</c>）時，載入後資料完整。
    /// 修正前具現化用的外殼只宣告七個前綴，額外前綴未宣告使解析失敗，寬鬆模式再把失敗搶救成空的表格，
    /// 整張工作表的資料靜默遺失（儲存後即永久遺失）。
    /// </summary>
    [Fact]
    public void LazyLoadedTableKeepsCellsUsingExtraNamespacePrefixes()
    {
        using var original = new MemoryStream();
        using (var document = OdfSpreadsheetDocument.Create())
        {
            OdfTableSheet sheet = document.Worksheets.Add("S");
            for (int row = 0; row < 60; row++)
            {
                for (int column = 0; column < 6; column++)
                {
                    sheet.GetCell(row, column).CellValue = $"R{row}C{column} 資料內容";
                }
            }

            document.SaveToStream(original);
        }

        original.Position = 0;
        using MemoryStream rewritten = RewriteContentXml(original, xml =>
            xml.Replace("<office:document-content ", $"<office:document-content xmlns:calcext=\"{CalcExtNamespace}\" ")
                .Replace("<table:table-cell ", "<table:table-cell calcext:value-type=\"string\" "));
        Assert.True(ReadContentXml(rewritten).Length > 8192);

        rewritten.Position = 0;
        using OdfSpreadsheetDocument loaded = OdfSpreadsheetDocument.Load(rewritten);
        OdfTableSheet loadedSheet = loaded.GetSheets().Single(sheet => sheet.Name == "S");
        Assert.Equal(360, loadedSheet.GetUsedCells().Count());
        Assert.Equal("R59C5 資料內容", loadedSheet.GetCell(59, 5).CellValue);

        using var saved = new MemoryStream();
        loaded.SaveToStream(saved);
        saved.Position = 0;
        using OdfSpreadsheetDocument reloaded = OdfSpreadsheetDocument.Load(saved);
        Assert.Equal(360, reloaded.GetSheets().Single(sheet => sheet.Name == "S").GetUsedCells().Count());
    }

    /// <summary>
    /// 驗證 8 KB 以上的 <c>text:p</c>（延遲具現化的子樹）含有宣告在文件根元素的額外前綴屬性時，載入後文字完整。
    /// </summary>
    [Fact]
    public void LazyLoadedParagraphKeepsContentUsingExtraNamespacePrefixes()
    {
        string expected = string.Concat(Enumerable.Range(0, 400).Select(i => $"片段{i};"));
        using var original = new MemoryStream();
        using (TextDocument document = TextDocument.Create())
        {
            OdfParagraph paragraph = document.AddParagraph(string.Empty);
            for (int i = 0; i < 400; i++)
            {
                paragraph.AddTextRun($"片段{i};");
            }

            document.SaveToStream(original);
        }

        original.Position = 0;
        using MemoryStream rewritten = RewriteContentXml(original, xml =>
            xml.Replace("<office:document-content ", $"<office:document-content xmlns:loext=\"{LoExtNamespace}\" ")
                .Replace("<text:span", "<text:span loext:marker=\"1\""));
        Assert.True(ReadContentXml(rewritten).Length > 8192);

        rewritten.Position = 0;
        using TextDocument loaded = TextDocument.Load(rewritten);
        Assert.Equal(expected, loaded.Body.Paragraphs.First().TextContent);
    }

    /// <summary>
    /// 驗證 schema 驗證器接受屬性值為空字串的 <c>styleNameRef</c>（規格為 NCName 或 empty，
    /// LibreOffice 會寫出 <c>style:list-style-name=""</c>），也不會因空字串丟出例外。
    /// </summary>
    [Fact]
    public void SchemaValidationAcceptsEmptyStyleNameReference()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph("內容");
        paragraph.Node.SetAttribute("style-name", OdfNamespaces.Text, string.Empty, "text");

        OdfValidationReport report = Validate14(document);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.Message)));
    }

    /// <summary>
    /// 驗證只允許 NCName 的屬性值為空字串（<c>style:name=""</c>）時，驗證器回報不符而不是丟出例外
    /// （<c>XmlConvert.VerifyNCName</c> 對空字串擲出 <see cref="ArgumentException"/> 而非 <see cref="System.Xml.XmlException"/>）。
    /// </summary>
    [Fact]
    public void SchemaValidationReportsEmptyNcNameInsteadOfThrowing()
    {
        using var original = new MemoryStream();
        using (TextDocument document = TextDocument.Create())
        {
            document.AddParagraph("內容");
            document.SaveToStream(original);
        }

        original.Position = 0;
        using MemoryStream rewritten = RewriteContentXml(original, xml =>
            xml.Replace(
                "<office:body>",
                "<office:automatic-styles><style:style style:name=\"\" style:family=\"paragraph\"/></office:automatic-styles><office:body>"));
        Assert.Contains("style:name=\"\"", ReadContentXml(rewritten));

        rewritten.Position = 0;
        using OdfPackage package = OdfPackage.Open(rewritten, leaveOpen: true);
        OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);
        Assert.False(report.IsValid);
    }

    /// <summary>
    /// 驗證 UTF-8 快速解析器保留段落內元素之間的空白：<c>&lt;/text:span&gt; 與 &lt;text:span&gt;</c> 之間的空格、
    /// 以及僅含空白的結尾文字。修正前解析器在每個標記前跳過所有空白，文字節點的開頭空白與僅含空白的文字節點全部遺失，
    /// 與 <c>XmlReader</c> 路徑的結果不一致，載入 LibreOffice 文件再儲存會把「粗體 與 斜體」變成「粗體與 斜體」。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ParserKeepsWhitespaceBetweenInlineElements(bool allowLazyLoading)
    {
        const string Xml =
            "<text:p xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">a <text:span>b</text:span> c <text:span>d</text:span> </text:p>";
        var options = new OdfLoadOptions { AllowLazyLoading = allowLazyLoading };

        OdfNode fromBytes = OdfXmlReader.Parse(Encoding.UTF8.GetBytes(Xml), options);
        Assert.Equal("a b c d ", fromBytes.TextContent);
        Assert.Equal(
            ["a ", "b", " c ", "d", " "],
            fromBytes.Children.Select(child => child.TextContent).ToArray());

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Xml));
        OdfNode fromStream = OdfXmlReader.Parse(stream, options);
        Assert.Equal(fromStream.TextContent, fromBytes.TextContent);
    }

    /// <summary>
    /// 驗證結構性元素之間的縮排空白不會成為子節點，只有段落內容元素保留僅含空白的文字；
    /// UTF-8 快速路徑與 <c>XmlReader</c> 路徑的結果一致。
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void ParserDropsIndentationBetweenStructuralElements(bool allowLazyLoading, bool fromStream)
    {
        const string Xml = """
            <office:body xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                         xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0">
              <office:text>
                <text:p>內容</text:p>
                <text:p>前 <text:span>粗</text:span> 後</text:p>
              </office:text>
            </office:body>
            """;
        var options = new OdfLoadOptions { AllowLazyLoading = allowLazyLoading };

        OdfNode body;
        if (fromStream)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Xml));
            body = OdfXmlReader.Parse(stream, options);
        }
        else
        {
            body = OdfXmlReader.Parse(Encoding.UTF8.GetBytes(Xml), options);
        }

        OdfNode text = Assert.Single(body.Children);
        Assert.Equal("text", text.LocalName);
        Assert.Equal(["p", "p"], text.Children.Select(child => child.LocalName).ToArray());
        Assert.Equal("前 粗 後", text.Children.Last().TextContent);
    }

    /// <summary>
    /// 驗證 <c>XmlReader</c> 路徑延遲具現化的大段落（8 KB 以上）保留 <c>text:span</c> 之間僅含單一空格的文字，
    /// 而延遲具現化的結構性子樹（大表格）仍略過縮排空白。
    /// </summary>
    [Fact]
    public void StreamParserLazySubtreesKeepSpacesOnlyInParagraphContent()
    {
        string spans = string.Join(" ", Enumerable.Range(0, 500).Select(i => $"<text:span>片段{i}</text:span>"));
        string expected = string.Join(" ", Enumerable.Range(0, 500).Select(i => $"片段{i}"));
        string rows = string.Concat(Enumerable.Range(0, 400).Select(i =>
            $"\n    <table:table-row><table:table-cell><text:p>列{i}</text:p></table:table-cell></table:table-row>"));
        string xml =
            "<office:body xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
            "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\">\n" +
            $"  <text:p>{spans}</text:p>\n" +
            $"  <table:table table:name=\"T\">{rows}\n  </table:table>\n</office:body>";
        Assert.True(spans.Length > 8192);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        OdfNode body = OdfXmlReader.Parse(stream, new OdfLoadOptions { AllowLazyLoading = true });

        OdfNode paragraph = body.Children.First(child => child.LocalName == "p");
        paragraph.EnsureMaterialized();
        Assert.Equal(expected, paragraph.TextContent);

        OdfNode table = body.Children.First(child => child.LocalName == "table");
        table.EnsureMaterialized();
        Assert.Equal(400, table.Children.Count);
        Assert.All(table.Children, child => Assert.Equal("table-row", child.LocalName));
    }

    /// <summary>
    /// 驗證載入含「粗體 與 斜體」（兩個 <c>text:span</c> 之間為單一空格）的段落後儲存再載入，空格仍在。
    /// </summary>
    [Fact]
    public void SaveAndReloadKeepsSpaceBetweenSpans()
    {
        using var original = new MemoryStream();
        using (TextDocument document = TextDocument.Create())
        {
            document.AddParagraph("佔位");
            document.SaveToStream(original);
        }

        original.Position = 0;
        using MemoryStream rewritten = RewriteContentXml(original, xml =>
            xml.Replace(
                "<text:p>佔位</text:p>",
                "<text:p>前 <text:span>粗體</text:span> 與 <text:span>斜體</text:span> 後 </text:p>"));
        Assert.Contains("</text:span> 與 <text:span>", ReadContentXml(rewritten));

        rewritten.Position = 0;
        using TextDocument loaded = TextDocument.Load(rewritten);
        Assert.Equal("前 粗體 與 斜體 後 ", loaded.Body.Paragraphs.First().TextContent);

        using var saved = new MemoryStream();
        loaded.SaveToStream(saved);
        Assert.Contains("</text:span> 與 <text:span", ReadContentXml(saved));
        saved.Position = 0;
        using TextDocument reloaded = TextDocument.Load(saved);
        Assert.Equal("前 粗體 與 斜體 後 ", reloaded.Body.Paragraphs.First().TextContent);
    }

    /// <summary>
    /// 驗證 <c>AddListWithStyle</c> 寫出的清單層級樣式符合 ODF 1.4 schema：編號格式、前綴、後綴在
    /// <c>style</c> 命名空間（<c>style:num-format</c>、<c>style:num-prefix</c>、<c>style:num-suffix</c>）。
    /// 修正前寫成 <c>fo:num-format</c> 與 <c>text:num-prefix</c>／<c>text:num-suffix</c>，不符 schema，
    /// LibreOffice 也忽略這些屬性（<c>a)</c> 與 <c>(I)</c> 都顯示成 <c>1</c>）。
    /// </summary>
    [Fact]
    public void ListStyleLevelsPassOdf14SchemaValidation()
    {
        using TextDocument document = TextDocument.Create();
        OdfList list = document.AddListWithStyle(
            "LS",
            new[]
            {
                new OdfListLevelStyle { Level = 1, Type = OdfListLevelType.Number, NumFormat = "a", NumSuffix = ")" },
                new OdfListLevelStyle { Level = 2, Type = OdfListLevelType.Number, NumFormat = "I", NumPrefix = "(", NumSuffix = ")" },
                new OdfListLevelStyle { Level = 3, Type = OdfListLevelType.Bullet, BulletChar = "•" },
            });
        list.AddItem("第一項", 1);
        list.AddItem("巢狀", 2);
        list.AddItem("深層", 3);

        string stylesXml;
        using (var stream = new MemoryStream())
        {
            document.SaveToStream(stream);
            stream.Position = 0;
            using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
            using var reader = new StreamReader(package.GetEntryStream("styles.xml"));
            stylesXml = reader.ReadToEnd();
        }

        Assert.Contains("style:num-format=\"a\"", stylesXml);
        Assert.Contains("style:num-suffix=\")\"", stylesXml);
        Assert.Contains("style:num-prefix=\"(\"", stylesXml);
        Assert.DoesNotContain("fo:num-format", stylesXml);

        OdfValidationReport report = Validate14(document);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.Message)));
    }

    /// <summary>
    /// 驗證頁首頁尾區域以 schema 規定的順序（header、header-left、header-first、footer、footer-left、
    /// footer-first）放進 <c>style:master-page</c>，不論建立的先後。修正前每個區域都附加在最後，
    /// 先設定首頁頁首或頁尾再設定預設頁首會使文件不符合 schema。
    /// </summary>
    [Fact]
    public void PageSetupRegionsFollowSchemaOrderRegardlessOfCreationOrder()
    {
        using TextDocument document = TextDocument.Create();
        OdfPageSetup setup = document.GetDefaultPageSetup();
        setup.Footer.Text = "頁尾";
        setup.HeaderFirst.Text = "首頁頁首";
        setup.HeaderLeft.Text = "左頁頁首";
        setup.HeaderText = "頁首";
        setup.FooterFirst.Text = "首頁頁尾";

        string stylesXml;
        using (var stream = new MemoryStream())
        {
            document.SaveToStream(stream);
            stream.Position = 0;
            using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
            using var reader = new StreamReader(package.GetEntryStream("styles.xml"));
            stylesXml = reader.ReadToEnd();
        }

        XElement masterPage = XElement.Parse(stylesXml).Descendants(s_style + "master-page").First();
        Assert.Equal(
            ["header", "header-left", "header-first", "footer", "footer-first"],
            masterPage.Elements().Select(element => element.Name.LocalName).ToArray());

        OdfValidationReport report = Validate14(document);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.Message)));
    }

    /// <summary>
    /// 驗證 <c>AddPageCountField</c> 寫出 <c>text:page-count</c>，而不是 <c>text:select-page="last"</c>
    /// （schema 只允許 previous、current、next；LibreOffice 把 last 當成一般頁碼，總頁數會顯示成目前頁碼）。
    /// </summary>
    [Fact]
    public void PageCountFieldUsesPageCountElement()
    {
        using TextDocument document = TextDocument.Create();
        OdfPageSetup setup = document.GetDefaultPageSetup();
        setup.Footer.AddPageNumberField();
        setup.Footer.AddPageCountField();

        string stylesXml;
        using (var stream = new MemoryStream())
        {
            document.SaveToStream(stream);
            stream.Position = 0;
            using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
            using var reader = new StreamReader(package.GetEntryStream("styles.xml"));
            stylesXml = reader.ReadToEnd();
        }

        XElement footer = XElement.Parse(stylesXml).Descendants(s_style + "footer").Single();
        XElement pageNumber = footer.Descendants(s_text + "page-number").Single();
        Assert.Equal("current", (string?)pageNumber.Attribute(s_text + "select-page"));
        Assert.Single(footer.Descendants(s_text + "page-count"));
        Assert.DoesNotContain("select-page=\"last\"", stylesXml);

        OdfValidationReport report = Validate14(document);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.Message)));
    }

    // ---------- helpers ----------

    private static MemoryStream RewriteContentXml(MemoryStream source, Func<string, string> transform)
    {
        var result = new MemoryStream();
        using (var input = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true))
        using (var output = new ZipArchive(result, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (ZipArchiveEntry entry in input.Entries)
            {
                ZipArchiveEntry copy = output.CreateEntry(
                    entry.FullName,
                    entry.FullName == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
                using Stream inputStream = entry.Open();
                using Stream outputStream = copy.Open();
                if (entry.FullName == "content.xml")
                {
                    using var reader = new StreamReader(inputStream, Encoding.UTF8);
                    byte[] bytes = new UTF8Encoding(false).GetBytes(transform(reader.ReadToEnd()));
                    outputStream.Write(bytes, 0, bytes.Length);
                }
                else
                {
                    inputStream.CopyTo(outputStream);
                }
            }
        }

        result.Position = 0;
        return result;
    }

    private static string ReadContentXml(MemoryStream package)
    {
        long position = package.Position;
        try
        {
            package.Position = 0;
            using var archive = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
            using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open(), Encoding.UTF8);
            return reader.ReadToEnd();
        }
        finally
        {
            package.Position = position;
        }
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
