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
    public void Export_TextDocument_ContainsHeadingsAndParagraphs()
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
    public void Export_FragmentMode_DoesNotContainDoctype()
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
    public void Export_FootnoteInParagraph_RendersSupElement()
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
    public void Export_NestedSpansAndNamespaceChecks_PreservesFormattingAndChecksNamespace()
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
}
