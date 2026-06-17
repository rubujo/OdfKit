using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件索引讀取引擎（內部協作者）。
/// </summary>
internal static class TextDocumentIndexReadEngine
{
    internal static IReadOnlyList<OdfIndexInfo> GetIndexInfos(OdfNode bodyTextRoot)
    {
        List<OdfIndexInfo> indexes = [];
        CollectIndexInfos(bodyTextRoot, indexes);
        return indexes.AsReadOnly();
    }

    internal static IReadOnlyList<OdfDocumentIndexMarkInfo> GetIndexMarks(OdfNode bodyTextRoot)
    {
        List<OdfDocumentIndexMarkInfo> marks = [];
        CollectIndexMarks(bodyTextRoot, marks);
        return marks.AsReadOnly();
    }

    private static void CollectIndexInfos(OdfNode node, List<OdfIndexInfo> indexes)
    {
        if (node.NodeType is OdfNodeType.Element && node.NamespaceUri == OdfNamespaces.Text)
        {
            OdfIndexKind? kind = node.LocalName switch
            {
                "table-of-content" => OdfIndexKind.TableOfContents,
                "alphabetical-index" => OdfIndexKind.AlphabeticalIndex,
                "bibliography" => OdfIndexKind.Bibliography,
                "table-index" => OdfIndexKind.TableIndex,
                _ => null,
            };

            if (kind.HasValue)
            {
                indexes.Add(new OdfIndexInfo(
                    kind.Value,
                    node.GetAttribute("name", OdfNamespaces.Text) ?? string.Empty));
            }
        }

        foreach (OdfNode child in node.Children)
            CollectIndexInfos(child, indexes);
    }

    private static void CollectIndexMarks(OdfNode node, List<OdfDocumentIndexMarkInfo> marks)
    {
        if (node.NodeType is OdfNodeType.Element && node.NamespaceUri == OdfNamespaces.Text)
        {
            if (node.LocalName == "alphabetical-index-mark" ||
                node.LocalName == "alphabetical-index-mark-start")
            {
                string term = node.GetAttribute("string-value", OdfNamespaces.Text) ?? node.TextContent ?? string.Empty;
                if (node.LocalName == "alphabetical-index-mark-start" && string.IsNullOrEmpty(term))
                    term = node.GetAttribute("id", OdfNamespaces.Text) ?? "Range";

                marks.Add(new OdfDocumentIndexMarkInfo(
                    OdfIndexMarkKind.Alphabetical,
                    term,
                    node.GetAttribute("key1", OdfNamespaces.Text),
                    node.GetAttribute("key2", OdfNamespaces.Text),
                    null,
                    null));
            }
            else if (node.LocalName == "bibliography-mark")
            {
                marks.Add(new OdfDocumentIndexMarkInfo(
                    OdfIndexMarkKind.Bibliography,
                    node.GetAttribute("title", OdfNamespaces.Text) ?? string.Empty,
                    null,
                    null,
                    node.GetAttribute("identifier", OdfNamespaces.Text),
                    node.GetAttribute("bibliography-type", OdfNamespaces.Text)));
            }
        }

        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "index-body" &&
                child.NamespaceUri == OdfNamespaces.Text)
                continue;

            CollectIndexMarks(child, marks);
        }
    }
}
