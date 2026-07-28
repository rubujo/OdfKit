using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 試算表 DDE 連結讀取引擎（內部協作者）。
/// </summary>
internal static class SpreadsheetDocumentDdeLinkReadEngine
{
    internal static bool ContainsDdeLinks(SpreadsheetDocument document)
    {
        foreach (OdfNode child in document.SheetsRoot.Children)
        {
            if (IsElement(child, "dde-links", OdfNamespaces.Table) &&
                FindChildElement(child, "dde-link", OdfNamespaces.Table) is not null)
            {
                return true;
            }
        }

        return false;
    }

    internal static IReadOnlyList<OdfDdeLinkInfo> GetDdeLinks(SpreadsheetDocument document)
    {
        List<OdfDdeLinkInfo> links = [];

        foreach (OdfNode child in document.SheetsRoot.Children)
        {
            if (!IsElement(child, "dde-links", OdfNamespaces.Table))
                continue;

            foreach (OdfNode linkNode in child.Children)
            {
                if (!IsElement(linkNode, "dde-link", OdfNamespaces.Table))
                    continue;

                OdfNode? source = FindChildElement(linkNode, "dde-source", OdfNamespaces.Office);
                OdfNode? cachedTable = FindChildElement(linkNode, "table", OdfNamespaces.Table);
                links.Add(new OdfDdeLinkInfo(
                    source?.GetAttribute("dde-application", OdfNamespaces.Office),
                    source?.GetAttribute("dde-topic", OdfNamespaces.Office),
                    source?.GetAttribute("dde-item", OdfNamespaces.Office),
                    source?.GetAttribute("name", OdfNamespaces.Office),
                    source?.GetAttribute("conversion-mode", OdfNamespaces.Office),
                    ParseBoolean(source?.GetAttribute("automatic-update", OdfNamespaces.Office)),
                    cachedTable is not null,
                    cachedTable?.GetAttribute("name", OdfNamespaces.Table)));
            }
        }

        return links.AsReadOnly();
    }

    private static OdfNode? FindChildElement(OdfNode parent, string localName, string namespaceUri)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (IsElement(child, localName, namespaceUri))
                return child;
        }

        return null;
    }

    private static bool IsElement(OdfNode node, string localName, string namespaceUri) =>
        node.NodeType is OdfNodeType.Element &&
        node.LocalName == localName &&
        node.NamespaceUri == namespaceUri;

    private static bool? ParseBoolean(string? value) =>
        value switch
        {
            "true" or "1" => true,
            "false" or "0" => false,
            _ => null,
        };
}
