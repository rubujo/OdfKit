using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件頁面設定讀取引擎（內部協作者）。
/// </summary>
internal static class TextDocumentPageSetupReadEngine
{
    internal static IReadOnlyList<OdfPageSetupInfo> GetPageSetups(OdfNode stylesDom)
    {
        OdfNode? masterStyles = TextDocumentDomHelper.FindChildElement(
            stylesDom, "master-styles", OdfNamespaces.Office);
        if (masterStyles is null)
            return [];

        List<OdfPageSetupInfo> setups = [];
        foreach (OdfNode child in masterStyles.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName is not "master-page" ||
                child.NamespaceUri != OdfNamespaces.Style)
                continue;

            string? name = child.GetAttribute("name", OdfNamespaces.Style);
            if (string.IsNullOrEmpty(name))
                continue;

            setups.Add(new OdfPageSetupInfo(
                name!,
                child.GetAttribute("page-layout-name", OdfNamespaces.Style),
                ReadHeaderFooterText(child, "header"),
                ReadHeaderFooterText(child, "header-left"),
                ReadHeaderFooterText(child, "footer"),
                ReadHeaderFooterText(child, "footer-left")));
        }

        return setups.AsReadOnly();
    }

    private static string? ReadHeaderFooterText(OdfNode masterPageNode, string localName)
    {
        foreach (OdfNode child in masterPageNode.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName != localName ||
                child.NamespaceUri != OdfNamespaces.Style)
                continue;

            foreach (OdfNode paragraph in child.Children)
            {
                if (paragraph.NodeType is OdfNodeType.Element &&
                    paragraph.LocalName is "p" &&
                    paragraph.NamespaceUri == OdfNamespaces.Text)
                    return paragraph.TextContent;
            }
        }

        return null;
    }
}
