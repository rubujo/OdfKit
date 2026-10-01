using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件頁碼與頁數欄位引擎（內部協作者）。
/// </summary>
internal static class TextDocumentPageFieldsEngine
{
    /// <summary>
    /// 在指定段落中新增頁碼欄位。
    /// </summary>
    internal static void AddPageNumberField(OdfParagraph paragraph)
    {
        var fNode = new OdfNode(OdfNodeType.Element, "page-number", OdfNamespaces.Text, "text");
        fNode.SetAttribute("select-page", OdfNamespaces.Text, "current", "text");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定段落中新增總頁數欄位。
    /// </summary>
    internal static void AddPageCountField(OdfParagraph paragraph)
    {
        // text:select-page 只允許 previous、current、next；總頁數是 text:page-count 元素。
        // 寫成 select-page="last" 不符 schema，LibreOffice 也當成一般頁碼（總頁數顯示成目前頁碼）。
        paragraph.Node.AppendChild(new OdfNode(OdfNodeType.Element, "page-count", OdfNamespaces.Text, "text"));
    }
}
