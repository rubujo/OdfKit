using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件表格列／欄結構變更引擎（內部協作者）。
/// </summary>
internal static class OdfTextTableStructureEngine
{
    internal static void InsertRows(OdfNode tableNode, int position, int count)
    {
        if (count <= 0)
            return;

        List<OdfNode> rows = GetRowsList(tableNode);
        int columnCount = rows.Count > 0 ? GetCellsInRow(rows[0]).Count : 0;

        if (position >= rows.Count)
        {
            for (int i = 0; i < count; i++)
                tableNode.AppendChild(CreateRow(columnCount));

            return;
        }

        OdfNode referenceRow = rows[position];
        OdfNode parent = referenceRow.Parent ?? tableNode;
        for (int i = 0; i < count; i++)
            parent.InsertBefore(CreateRow(columnCount), referenceRow);
    }

    internal static IReadOnlyList<OdfNode> DeleteRows(OdfNode tableNode, int position, int count)
    {
        if (count <= 0)
            return [];

        List<OdfNode> rows = GetRowsList(tableNode);
        List<OdfNode> deletedSnapshots = [];

        int end = position + count - 1;
        for (int index = end; index >= position; index--)
        {
            if (index < 0 || index >= rows.Count)
                continue;

            OdfNode row = rows[index];
            deletedSnapshots.Insert(0, row.CloneNode(deep: true));
            row.Parent?.RemoveChild(row);
        }

        return deletedSnapshots.AsReadOnly();
    }

    internal static void InsertColumns(OdfNode tableNode, int position, int count)
    {
        if (count <= 0)
            return;

        InsertColumnDefinitions(tableNode, position, count);

        foreach (OdfNode row in GetRowsList(tableNode))
            InsertCellsInRow(row, position, count);
    }

    internal static ColumnDeletionSnapshots DeleteColumns(OdfNode tableNode, int position, int count)
    {
        if (count <= 0)
            return new ColumnDeletionSnapshots([], []);

        IReadOnlyList<OdfNode> columnSnapshots = DeleteColumnDefinitions(tableNode, position, count);
        List<(int RowIndex, OdfNode CellSnapshot)> rowCellSnapshots = [];
        List<OdfNode> rows = GetRowsList(tableNode);

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            for (int offset = count - 1; offset >= 0; offset--)
            {
                OdfNode? deletedCell = DeleteCellInRow(rows[rowIndex], position);
                if (deletedCell is not null)
                    rowCellSnapshots.Add((rowIndex, deletedCell));
            }
        }

        return new ColumnDeletionSnapshots(columnSnapshots, rowCellSnapshots.AsReadOnly());
    }

    private static List<OdfNode> GetRowsList(OdfNode tableNode)
    {
        List<OdfNode> list = [];
        foreach (OdfNode child in tableNode.Children)
        {
            if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
                list.Add(child);
        }

        return list;
    }

    private static List<OdfNode> GetColumnsList(OdfNode tableNode)
    {
        List<OdfNode> list = [];
        foreach (OdfNode child in tableNode.Children)
        {
            if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
                list.Add(child);
            else
                break;
        }

        return list;
    }

    private static List<OdfNode> GetCellsInRow(OdfNode rowNode)
    {
        List<OdfNode> list = [];
        foreach (OdfNode child in rowNode.Children)
        {
            if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") &&
                child.NamespaceUri == OdfNamespaces.Table)
                list.Add(child);
        }

        return list;
    }

    private static void InsertColumnDefinitions(OdfNode tableNode, int position, int count)
    {
        List<OdfNode> columns = GetColumnsList(tableNode);
        if (position >= columns.Count)
        {
            OdfNode? insertBefore = FindFirstNonColumnChild(tableNode);
            for (int i = 0; i < count; i++)
            {
                OdfNode column = CreateColumn();
                if (insertBefore is not null)
                    tableNode.InsertBefore(column, insertBefore);
                else
                    tableNode.AppendChild(column);
            }

            return;
        }

        OdfNode referenceColumn = columns[position];
        OdfNode parent = referenceColumn.Parent ?? tableNode;
        for (int i = 0; i < count; i++)
            parent.InsertBefore(CreateColumn(), referenceColumn);
    }

    private static IReadOnlyList<OdfNode> DeleteColumnDefinitions(OdfNode tableNode, int position, int count)
    {
        List<OdfNode> columns = GetColumnsList(tableNode);
        List<OdfNode> deletedSnapshots = [];

        int end = position + count - 1;
        for (int index = end; index >= position; index--)
        {
            if (index < 0 || index >= columns.Count)
                continue;

            OdfNode column = columns[index];
            deletedSnapshots.Insert(0, column.CloneNode(deep: true));
            column.Parent?.RemoveChild(column);
        }

        return deletedSnapshots.AsReadOnly();
    }

    private static void InsertCellsInRow(OdfNode rowNode, int position, int count)
    {
        List<OdfNode> cells = GetCellsInRow(rowNode);
        if (position >= cells.Count)
        {
            for (int i = 0; i < count; i++)
                rowNode.AppendChild(CreateCell());

            return;
        }

        OdfNode referenceCell = cells[position];
        for (int i = 0; i < count; i++)
            rowNode.InsertBefore(CreateCell(), referenceCell);
    }

    private static OdfNode? DeleteCellInRow(OdfNode rowNode, int position)
    {
        List<OdfNode> cells = GetCellsInRow(rowNode);
        if (position < 0 || position >= cells.Count)
            return null;

        OdfNode cell = cells[position];
        OdfNode snapshot = cell.CloneNode(deep: true);
        cell.Parent?.RemoveChild(cell);
        return snapshot;
    }

    private static OdfNode? FindFirstNonColumnChild(OdfNode tableNode)
    {
        foreach (OdfNode child in tableNode.Children)
        {
            if (child.LocalName != "table-column" || child.NamespaceUri != OdfNamespaces.Table)
                return child;
        }

        return null;
    }

    private static OdfNode CreateRow(int columnCount)
    {
        OdfNode row = new(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");
        for (int i = 0; i < columnCount; i++)
            row.AppendChild(CreateCell());

        return row;
    }

    private static OdfNode CreateColumn() =>
        new(OdfNodeType.Element, "table-column", OdfNamespaces.Table, "table");

    private static OdfNode CreateCell() =>
        new(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table");
}

/// <summary>
/// 欄結構刪除快照（欄定義與各列儲存格）。
/// </summary>
/// <param name="ColumnSnapshots">The numeric value. / 已刪除的欄定義節點快照</param>
/// <param name="RowCellSnapshots">The numeric value. / 各列已刪除儲存格快照（含列索引）</param>
internal readonly record struct ColumnDeletionSnapshots(
    IReadOnlyList<OdfNode> ColumnSnapshots,
    IReadOnlyList<(int RowIndex, OdfNode CellSnapshot)> RowCellSnapshots);
