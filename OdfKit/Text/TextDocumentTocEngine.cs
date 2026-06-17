using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件目錄（TOC）引擎（內部協作者）。
/// </summary>
internal static class TextDocumentTocEngine
{
    /// <summary>
    /// 新增目錄至文件本文結尾。
    /// </summary>
    internal static OdfTableOfContents AddTableOfContents(
        TextDocument document,
        TextDocument.TextDocumentCoreCollaborators ctx,
        string title,
        int outlineLevel)
    {
        OdfNode tocNode = OdfNodeFactory.CreateElement("table-of-content", OdfNamespaces.Text, "text");
        tocNode.SetAttribute("name", OdfNamespaces.Text, title, "text");

        OdfNode sourceNode = OdfNodeFactory.CreateElement("table-of-content-source", OdfNamespaces.Text, "text");
        sourceNode.SetAttribute("outline-level", OdfNamespaces.Text, outlineLevel.ToString(), "text");
        tocNode.AppendChild(sourceNode);

        OdfNode bodyNode = OdfNodeFactory.CreateElement("index-body", OdfNamespaces.Text, "text");

        OdfNode titlePara = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        titlePara.SetAttribute("style-name", OdfNamespaces.Text, "Contents_20_Heading", "text");
        titlePara.TextContent = title;
        bodyNode.AppendChild(titlePara);

        tocNode.AppendChild(bodyNode);
        ctx.BodyTextRoot.AppendChild(tocNode);

        TextDocumentSettingsEngine.SetUpdateFieldsWhenOpening(ctx, true);
        return new OdfTableOfContents(tocNode, document);
    }
}
