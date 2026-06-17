using System;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件表格結構修訂記錄引擎（內部協作者）。
/// </summary>
internal static class TextDocumentTableStructuralChangeRecordingEngine
{
    internal static string RecordRowInsertion(OdfNode tableNode, int position, int count, string author, DateTime date)
    {
        OdfNode changeNode = CreateInsertionChangeNode("row", position, count, author, date);
        tableNode.AppendChild(changeNode);
        return changeNode.GetAttribute("id", OdfNamespaces.Table) ?? string.Empty;
    }

    internal static string RecordColumnInsertion(OdfNode tableNode, int position, int count, string author, DateTime date)
    {
        OdfNode changeNode = CreateInsertionChangeNode("column", position, count, author, date);
        tableNode.AppendChild(changeNode);
        return changeNode.GetAttribute("id", OdfNamespaces.Table) ?? string.Empty;
    }

    internal static string RecordRowDeletion(
        OdfNode tableNode,
        int position,
        IReadOnlyList<OdfNode> deletedRowSnapshots,
        string author,
        DateTime date)
    {
        if (deletedRowSnapshots.Count == 0)
            return string.Empty;

        OdfNode changeNode = CreateDeletionChangeNode("row", position, deletedRowSnapshots.Count, author, date);
        foreach (OdfNode snapshot in deletedRowSnapshots)
        {
            var holder = new OdfNode(OdfNodeType.Element, "deleted-row-snapshot", OdfNamespaces.Table, "table");
            holder.AppendChild(snapshot.CloneNode(deep: true));
            changeNode.AppendChild(holder);
        }

        tableNode.AppendChild(changeNode);
        return changeNode.GetAttribute("id", OdfNamespaces.Table) ?? string.Empty;
    }

    internal static string RecordColumnDeletion(
        OdfNode tableNode,
        int position,
        ColumnDeletionSnapshots deletedSnapshots,
        string author,
        DateTime date)
    {
        int deletedCount = deletedSnapshots.ColumnSnapshots.Count;
        if (deletedCount == 0 && deletedSnapshots.RowCellSnapshots.Count == 0)
            return string.Empty;

        int spanCount = deletedCount > 0 ? deletedCount : 1;
        OdfNode changeNode = CreateDeletionChangeNode("column", position, spanCount, author, date);

        foreach (OdfNode snapshot in deletedSnapshots.ColumnSnapshots)
        {
            var holder = new OdfNode(OdfNodeType.Element, "deleted-column-snapshot", OdfNamespaces.Table, "table");
            holder.AppendChild(snapshot.CloneNode(deep: true));
            changeNode.AppendChild(holder);
        }

        foreach ((int rowIndex, OdfNode cellSnapshot) in deletedSnapshots.RowCellSnapshots)
        {
            var holder = new OdfNode(OdfNodeType.Element, "deleted-cell-snapshot", OdfNamespaces.Table, "table");
            holder.SetAttribute("row", OdfNamespaces.Table, rowIndex.ToString(CultureInfo.InvariantCulture), "table");
            holder.AppendChild(cellSnapshot.CloneNode(deep: true));
            changeNode.AppendChild(holder);
        }

        tableNode.AppendChild(changeNode);
        return changeNode.GetAttribute("id", OdfNamespaces.Table) ?? string.Empty;
    }

    private static OdfNode CreateInsertionChangeNode(
        string structuralType,
        int position,
        int count,
        string author,
        DateTime date)
    {
        string changeId = "tc_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var changeNode = new OdfNode(OdfNodeType.Element, "insertion", OdfNamespaces.Table, "table");
        changeNode.SetAttribute("id", OdfNamespaces.Table, changeId, "table");
        changeNode.SetAttribute("acceptance-state", OdfNamespaces.Table, "pending", "table");
        changeNode.SetAttribute("type", OdfNamespaces.Table, structuralType, "table");
        changeNode.SetAttribute("position", OdfNamespaces.Table, position.ToString(CultureInfo.InvariantCulture), "table");
        if (count > 1)
            changeNode.SetAttribute("count", OdfNamespaces.Table, count.ToString(CultureInfo.InvariantCulture), "table");

        OdfTrackedChangeMetadataReader.Write(changeNode, author, date);
        return changeNode;
    }

    private static OdfNode CreateDeletionChangeNode(
        string structuralType,
        int position,
        int count,
        string author,
        DateTime date)
    {
        string changeId = "tc_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var changeNode = new OdfNode(OdfNodeType.Element, "deletion", OdfNamespaces.Table, "table");
        changeNode.SetAttribute("id", OdfNamespaces.Table, changeId, "table");
        changeNode.SetAttribute("acceptance-state", OdfNamespaces.Table, "pending", "table");
        changeNode.SetAttribute("type", OdfNamespaces.Table, structuralType, "table");
        changeNode.SetAttribute("position", OdfNamespaces.Table, position.ToString(CultureInfo.InvariantCulture), "table");
        if (count > 1)
            changeNode.SetAttribute("multi-deletion-spanned", OdfNamespaces.Table, count.ToString(CultureInfo.InvariantCulture), "table");

        OdfTrackedChangeMetadataReader.Write(changeNode, author, date);
        return changeNode;
    }
}
