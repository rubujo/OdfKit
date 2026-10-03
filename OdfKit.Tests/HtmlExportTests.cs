using System;
using System.IO;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Export;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 ODF 轉換至 HTML 匯出 API。
/// </summary>
public class HtmlExportTests
{
    /// <summary>
    /// 驗證段落與標題可正確匯出為 HTML。
    /// </summary>
    [Fact]
    public void ExportTextDocumentContainsHeadingsAndParagraphs()
    {
        using var doc = TextDocument.Create();
        doc.AddHeading("主標題", 1);
        doc.AddParagraph("第一段落內容。");
        doc.AddHeading("次標題", 2);
        doc.AddParagraph("第二段落內容。");

        string html = OdfHtmlExporter.Export(doc);

        Assert.Contains("<h1>", html);
        Assert.Contains("主標題", html);
        Assert.Contains("<h2>", html);
        Assert.Contains("次標題", html);
        Assert.Contains("<p>", html);
        Assert.Contains("第一段落內容。", html);
    }

    /// <summary>
    /// 驗證 FullPage 為 false 時僅輸出 body 片段。
    /// </summary>
    [Fact]
    public void ExportFragmentModeDoesNotContainDoctype()
    {
        using var doc = TextDocument.Create();
        doc.AddParagraph("片段內容");

        var options = new OdfHtmlExportOptions { FullPage = false };
        string html = OdfHtmlExporter.Export(doc, options);

        Assert.DoesNotContain("<!DOCTYPE", html);
        Assert.DoesNotContain("<html", html);
        Assert.Contains("片段內容", html);
    }

    /// <summary>
    /// 驗證腳注引用以 sup 呈現。
    /// </summary>
    [Fact]
    public void ExportFootnoteInParagraphRendersSupElement()
    {
        using var doc = TextDocument.Create();
        var para = doc.AddParagraph("本文內容");
        para.AddFootnote("1", "腳注說明。");

        string html = OdfHtmlExporter.Export(doc);

        Assert.Contains("<sup", html);
        Assert.Contains(">1<", html);
    }

    /// <summary>
    /// 驗證巢狀的 span 元素可正確遞迴處理，且非 text 命名空間的 span 節點會被忽略。
    /// </summary>
    [Fact]
    public void ExportNestedSpansAndNamespaceChecksPreservesFormattingAndChecksNamespace()
    {
        using var doc = TextDocument.Create();
        var para = doc.AddParagraph();

        // 建立巢狀 span: <text:span><text:span>巢狀內容</text:span></text:span>
        var outerSpanNode = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text");
        var innerSpanNode = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text");
        var textNode = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty, string.Empty) { TextContent = "巢狀內容" };

        innerSpanNode.AppendChild(textNode);
        outerSpanNode.AppendChild(innerSpanNode);
        para.Node.AppendChild(outerSpanNode);

        // 建立非 text 命名空間的 span (例如 table:span): <table:span>忽略內容</table:span>
        var invalidSpanNode = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Table, "table");
        var invalidText = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty, string.Empty) { TextContent = "忽略內容" };
        invalidSpanNode.AppendChild(invalidText);
        para.Node.AppendChild(invalidSpanNode);

        var options = new OdfHtmlExportOptions { FullPage = false };
        string html = OdfHtmlExporter.Export(doc, options);

        // 必須包含巢狀結構 <span><span>巢狀內容</span></span>
        Assert.Contains("<span><span>巢狀內容</span></span>", html);
        // 必須忽略 invalidSpanNode，即不包含 "忽略內容"
        Assert.DoesNotContain("忽略內容", html);
    }

    private const string FlatOdtForHtml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <office:document xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
            xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
            xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0"
            xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0"
            xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0"
            xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0"
            xmlns:xlink="http://www.w3.org/1999/xlink"
            office:version="1.3" office:mimetype="application/vnd.oasis.opendocument.text">
          <office:automatic-styles>
            <text:list-style style:name="Bullets">
              <text:list-level-style-bullet text:level="1" text:bullet-char="•"/>
            </text:list-style>
            <text:list-style style:name="Steps">
              <text:list-level-style-number text:level="1" style:num-format="a" text:start-value="3"/>
              <text:list-level-style-bullet text:level="2" text:bullet-char="o"/>
            </text:list-style>
            <style:style style:name="Centered" style:family="paragraph">
              <style:paragraph-properties fo:text-align="center"/>
            </style:style>
            <style:style style:name="Bold" style:family="text">
              <style:text-properties style:font-weight-asian="bold"/>
            </style:style>
          </office:automatic-styles>
          <office:body><office:text>
            <text:p text:style-name="Centered">連結：<text:a xlink:href="https://example.org/a?x=1&amp;y=2" xlink:type="simple">安全連結</text:a>、<text:a xlink:href="javascript:alert(1)" xlink:type="simple">危險連結</text:a>、<text:a xlink:href="#書籤" xlink:type="simple">頁內連結</text:a></text:p>
            <text:p>前<text:s text:c="3"/>後<text:tab/>定位<text:line-break/>換行<text:bookmark text:name="書籤"/><text:span text:style-name="Bold">亞洲粗體</text:span></text:p>
            <text:list text:style-name="Bullets">
              <text:list-item><text:p>項目甲</text:p>
                <text:list><text:list-item><text:p>子項乙</text:p></text:list-item></text:list>
              </text:list-item>
            </text:list>
            <text:list text:style-name="Steps">
              <text:list-item><text:p>步驟一</text:p>
                <text:p>步驟一的第二段</text:p>
                <text:list><text:list-item><text:p>步驟一之一</text:p></text:list-item></text:list>
              </text:list-item>
            </text:list>
            <table:table>
              <table:table-header-rows><table:table-row>
                <table:table-cell><text:p>標題甲</text:p></table:table-cell>
                <table:table-cell><text:p>標題乙</text:p></table:table-cell>
              </table:table-row></table:table-header-rows>
              <table:table-row>
                <table:table-cell table:number-columns-spanned="2"><text:p>合併儲存格</text:p></table:table-cell>
                <table:covered-table-cell/>
              </table:table-row>
              <table:table-row>
                <table:table-cell table:number-rows-spanned="2"><text:p>縱向合併</text:p></table:table-cell>
                <table:table-cell><text:list><text:list-item><text:p>儲存格內清單</text:p></text:list-item></text:list></table:table-cell>
              </table:table-row>
              <table:table-row>
                <table:covered-table-cell/>
                <table:table-cell><text:p>末格</text:p></table:table-cell>
              </table:table-row>
              <table:table-row table:number-rows-repeated="500000"><table:table-cell table:number-columns-repeated="1024"/></table:table-row>
            </table:table>
          </office:text></office:body>
        </office:document>
        """;

    /// <summary>
    /// 驗證 HTML 匯出涵蓋 LibreOffice 常見的結構：超連結（含危險協定與頁內錨點）、連續空格、定位字元、換行、書籤、
    /// 亞洲字型粗體、有序與無序及巢狀清單、含標題列與合併儲存格的表格。
    /// 修正前連結文字整個消失、巢狀清單被壓平、有序清單變成無序清單、表格被輸出成一堆段落。
    /// </summary>
    [Fact]
    public void ExportCoversLinksWhitespaceListsAndTables()
    {
        using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(FlatOdtForHtml));
        using var doc = (TextDocument)OdfDocument.Load(source, "structures.fodt");

        string html = OdfHtmlExporter.Export(doc, new OdfHtmlExportOptions { FullPage = false });

        Assert.Contains("<p style=\"text-align:center\">", html);
        Assert.Contains("<a href=\"https://example.org/a?x=1&amp;y=2\">安全連結</a>", html);
        Assert.Contains("<a href=\"#書籤\">頁內連結</a>", html);
        Assert.Contains("危險連結", html);
        Assert.DoesNotContain("javascript:", html, StringComparison.Ordinal);

        Assert.Contains("前&nbsp;&nbsp;&nbsp;後", html);
        Assert.Contains("<span style=\"white-space:pre\">\t</span>定位<br>換行", html);
        Assert.Contains("<a id=\"書籤\"></a>", html);
        Assert.Contains("<span style=\"font-weight:bold\">亞洲粗體</span>", html);

        Assert.Contains("<ul><li>項目甲<ul><li>子項乙</li></ul></li></ul>", html);
        Assert.Contains("<ol type=\"a\" start=\"3\"><li>步驟一<p>步驟一的第二段</p><ul><li>步驟一之一</li></ul></li></ol>", html);

        Assert.Contains("<thead><tr><th><p>標題甲</p></th><th><p>標題乙</p></th></tr></thead>", html);
        Assert.Contains("<td colspan=\"2\"><p>合併儲存格</p></td>", html);
        Assert.Contains("<td rowspan=\"2\"><p>縱向合併</p></td>", html);
        Assert.Contains("<td><ul><li>儲存格內清單</li></ul></td>", html);

        // 試算表風格的尾端空白重複列不展開成數十萬列。
        Assert.True(html.Length < 4000, $"輸出 {html.Length} 字元。");
    }

    /// <summary>
    /// 驗證 PDF 匯出涵蓋與 HTML 相同的結構（清單、表格、連結）而不中斷：安全連結成為 URI 註解，
    /// 危險協定的連結不寫入 PDF，含合併儲存格與尾端空白重複列的表格可完成匯出。
    /// 修正前清單只輸出單層、表格與連結整個被略過。
    /// </summary>
    [Fact]
    public void PdfExportCoversLinksListsAndTables()
    {
        using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(FlatOdtForHtml));
        using var doc = (TextDocument)OdfDocument.Load(source, "structures.fodt");

        using var pdf = new MemoryStream();
        OdfPdfExporter.ExportToStream(doc, pdf);
        byte[] bytes = pdf.ToArray();
        string text = System.Text.Encoding.Latin1.GetString(bytes);

        Assert.StartsWith("%PDF-", text, StringComparison.Ordinal);
        Assert.Contains("https://example.org/a?x=1&y=2", text, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript", text, StringComparison.OrdinalIgnoreCase);
        Assert.True(bytes.Length < 2_000_000, $"PDF {bytes.Length} 位元組；尾端空白重複列不應展開。");
    }
}
