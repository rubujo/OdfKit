using System.Collections.Generic;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件表格結構修訂讀取引擎（內部協作者）。
/// </summary>
internal static class TextDocumentTableStructuralChangeReadEngine
{
    internal static IReadOnlyList<OdfTableStructuralChangeInfo> GetTableStructuralChanges(OdfNode bodyTextRoot)
    {
        List<OdfTableStructuralChangeInfo> changes = [];
        CollectTableStructuralChanges(bodyTextRoot, changes);
        return changes.AsReadOnly();
    }

    private static void CollectTableStructuralChanges(OdfNode node, List<OdfTableStructuralChangeInfo> changes)
    {
        if (node.NodeType is OdfNodeType.Element && node.NamespaceUri == OdfNamespaces.Table)
        {
            if (node.LocalName == "insertion")
                TryAddStructuralChange(node, OdfTableStructuralChangeKind.Insertion, changes);
            else if (node.LocalName == "deletion")
                TryAddStructuralChange(node, OdfTableStructuralChangeKind.Deletion, changes);
        }

        foreach (OdfNode child in node.Children)
            CollectTableStructuralChanges(child, changes);
    }

    private static void TryAddStructuralChange(
        OdfNode changeNode,
        OdfTableStructuralChangeKind kind,
        List<OdfTableStructuralChangeInfo> changes)
    {
        string? changeId = changeNode.GetAttribute("id", OdfNamespaces.Table);
        if (string.IsNullOrEmpty(changeId))
            return;

        string? structuralType = changeNode.GetAttribute("type", OdfNamespaces.Table);
        if (string.IsNullOrEmpty(structuralType))
            return;

        if (!int.TryParse(changeNode.GetAttribute("position", OdfNamespaces.Table), NumberStyles.Integer, CultureInfo.InvariantCulture, out int position))
            return;

        int count = 1;
        string? countText = changeNode.GetAttribute("count", OdfNamespaces.Table);
        if (!string.IsNullOrEmpty(countText))
            int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out count);

        (string author, System.DateTime changedAt) = OdfTrackedChangeMetadataReader.Read(changeNode);

        changes.Add(new OdfTableStructuralChangeInfo(
            changeId!,
            kind,
            structuralType!,
            position,
            count,
            author,
            changedAt,
            changeNode.GetAttribute("acceptance-state", OdfNamespaces.Table) ?? "pending"));
    }
}
