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
        var fNode = new OdfNode(OdfNodeType.Element, "page-number", OdfNamespaces.Text, "text");
        fNode.SetAttribute("select-page", OdfNamespaces.Text, "last", "text");
        paragraph.Node.AppendChild(fNode);
    }
}
