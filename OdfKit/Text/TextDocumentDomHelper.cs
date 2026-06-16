using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件 DOM 節點查詢與建立輔助工具（內部協作者）。
/// </summary>
internal static class TextDocumentDomHelper
{
    /// <summary>
    /// 尋找直接子元素節點。
    /// </summary>
    internal static OdfNode? FindChildElement(OdfNode parent, string localName, string ns)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
                return child;
        }

        return null;
    }

    /// <summary>
    /// 尋找或建立直接子元素節點。
    /// </summary>
    internal static OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
                return child;
        }

        var node = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
        parent.AppendChild(node);
        return node;
    }
}
