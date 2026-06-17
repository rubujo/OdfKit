using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件超連結讀取引擎（內部協作者）。
/// </summary>
internal static class TextDocumentHyperlinkReadEngine
{
    internal static IReadOnlyList<OdfHyperlinkInfo> GetHyperlinks(OdfNode bodyTextRoot)
    {
        List<OdfHyperlinkInfo> hyperlinks = [];
        ScanHyperlinks(bodyTextRoot, hyperlinks);
        return hyperlinks.AsReadOnly();
    }

    private static void ScanHyperlinks(OdfNode node, List<OdfHyperlinkInfo> hyperlinks)
    {
        if (node.NodeType is OdfNodeType.Element &&
            node.LocalName is "a" &&
            node.NamespaceUri == OdfNamespaces.Text)
        {
            string? url = node.GetAttribute("href", OdfNamespaces.XLink);
            if (!string.IsNullOrEmpty(url))
                hyperlinks.Add(new OdfHyperlinkInfo(url!, node.TextContent ?? string.Empty));
        }

        foreach (OdfNode child in node.Children)
            ScanHyperlinks(child, hyperlinks);
    }
}
