using System.Globalization;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 工作表 DOM 節點查詢與建立輔助工具（內部協作者）。
/// </summary>
internal static class OdfTableSheetDomHelper
{
    /// <summary>
    /// 尋找直接子元素節點。
    /// </summary>
    internal static OdfNode? FindChildElement(OdfNode parent, string localName, string ns)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == ns)
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>
    /// 尋找或建立直接子元素節點。
    /// </summary>
    internal static OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
    {
        foreach (var child in parent.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
                return child;
        }
        var node = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
        parent.AppendChild(node);
        return node;
    }

    /// <summary>
    /// 解析非負整數；無效或非正數時回傳 0。
    /// </summary>
    internal static int ParseNonNegativeInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) && result > 0
            ? result
            : 0;
    }
}
