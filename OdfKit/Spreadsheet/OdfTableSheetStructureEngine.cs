using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 欄結構刪除快照（欄定義與各列儲存格）。
/// </summary>
/// <param name="ColumnSnapshots">已刪除的欄定義節點快照</param>
/// <param name="RowCellSnapshots">各列已刪除儲存格快照（含列索引）</param>
internal readonly record struct ColumnDeletionSnapshots(
    IReadOnlyList<OdfNode> ColumnSnapshots,
    IReadOnlyList<(int RowIndex, OdfNode CellSnapshot)> RowCellSnapshots);

/// <summary>
/// 工作表列／欄結構變更引擎（內部協作者）。
/// </summary>
internal static class OdfTableSheetStructureEngine
{
    internal static void InsertRows(OdfNode tableNode, int position, int count)
    {
        if (count <= 0)
            return;

        List<OdfNode> rows = OdfTableSheetDomAccessEngine.GetRowsList(tableNode);
        if (position >= rows.Count)
        {
            for (int i = 0; i < count; i++)
            {
                tableNode.AppendChild(CreateEmptyRow());
            }

            return;
        }

        OdfNode referenceRow = rows[position];
        OdfNode parent = referenceRow.Parent ?? tableNode;
        for (int i = 0; i < count; i++)
            parent.InsertBefore(CreateEmptyRow(), referenceRow);
    }

    internal static IReadOnlyList<OdfNode> DeleteRows(OdfNode tableNode, int position, int count)
    {
        if (count <= 0)
            return [];

        List<OdfNode> rows = OdfTableSheetDomAccessEngine.GetRowsList(tableNode);
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

    internal static void RestoreRows(OdfNode tableNode, int position, IReadOnlyList<OdfNode> rowSnapshots)
    {
        if (rowSnapshots.Count == 0)
            return;

        List<OdfNode> rows = OdfTableSheetDomAccessEngine.GetRowsList(tableNode);
        OdfNode? insertBefore = position < rows.Count ? rows[position] : null;
        OdfNode parent = insertBefore?.Parent ?? tableNode;

        foreach (OdfNode snapshot in rowSnapshots)
        {
            OdfNode row = snapshot.CloneNode(deep: true);
            if (insertBefore is not null)
                parent.InsertBefore(row, insertBefore);
            else
                parent.AppendChild(row);
        }
    }

    internal static void InsertColumns(OdfNode tableNode, int position, int count)
    {
        if (count <= 0)
            return;

        InsertColumnDefinitions(tableNode, position, count);

        foreach (OdfNode row in OdfTableSheetDomAccessEngine.GetRowsList(tableNode))
            InsertCellsInRow(row, position, count);
    }

    internal static ColumnDeletionSnapshots DeleteColumns(OdfNode tableNode, int position, int count)
    {
        if (count <= 0)
            return new ColumnDeletionSnapshots([], []);

        IReadOnlyList<OdfNode> columnSnapshots = DeleteColumnDefinitions(tableNode, position, count);
        List<(int RowIndex, OdfNode CellSnapshot)> rowCellSnapshots = [];
        List<OdfNode> rows = OdfTableSheetDomAccessEngine.GetRowsList(tableNode);

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

    internal static void RestoreColumns(OdfNode tableNode, int position, IReadOnlyList<OdfNode> columnSnapshots)
    {
        if (columnSnapshots.Count == 0)
            return;

        List<OdfNode> columns = OdfTableSheetDomAccessEngine.GetColumnsList(tableNode);
        OdfNode? insertBefore = position < columns.Count ? columns[position] : FindFirstNonColumnChild(tableNode);
        OdfNode parent = insertBefore?.Parent ?? tableNode;

        foreach (OdfNode snapshot in columnSnapshots)
        {
            OdfNode column = snapshot.CloneNode(deep: true);
            if (insertBefore is not null)
                parent.InsertBefore(column, insertBefore);
            else
                parent.AppendChild(column);
        }
    }

    internal static void MoveCell(
        OdfNode tableNode,
        int sourceRow,
        int sourceColumn,
        int targetRow,
        int targetColumn)
    {
        if (sourceRow == targetRow && sourceColumn == targetColumn)
            return;

        OdfNode sourceCell = OdfTableSheetDomAccessEngine.GetOrCreateCellNode(tableNode, sourceRow, sourceColumn);
        OdfNode targetCell = OdfTableSheetDomAccessEngine.GetOrCreateCellNode(tableNode, targetRow, targetColumn);

        OdfNode sourceSnapshot = sourceCell.CloneNode(deep: true);
        CopyCellContent(sourceSnapshot, targetCell);
        ClearCellContent(sourceCell);
    }

    internal static void RestoreColumnCells(
        OdfNode tableNode,
        int position,
        IReadOnlyList<(int RowIndex, OdfNode CellSnapshot)> rowCellSnapshots)
    {
        List<OdfNode> rows = OdfTableSheetDomAccessEngine.GetRowsList(tableNode);

        foreach ((int rowIndex, OdfNode cellSnapshot) in rowCellSnapshots)
        {
            if (rowIndex < 0 || rowIndex >= rows.Count)
                continue;

            RestoreCellInRow(rows[rowIndex], position, cellSnapshot);
        }
    }

    private static void InsertColumnDefinitions(OdfNode tableNode, int position, int count)
    {
        List<OdfNode> columns = OdfTableSheetDomAccessEngine.GetColumnsList(tableNode);
        if (position >= columns.Count)
        {
            OdfNode? insertBefore = FindFirstNonColumnChild(tableNode);
            for (int i = 0; i < count; i++)
            {
                OdfNode column = CreateEmptyColumn();
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
            parent.InsertBefore(CreateEmptyColumn(), referenceColumn);
    }

    private static IReadOnlyList<OdfNode> DeleteColumnDefinitions(OdfNode tableNode, int position, int count)
    {
        List<OdfNode> columns = OdfTableSheetDomAccessEngine.GetColumnsList(tableNode);
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
        List<OdfNode> cells = OdfTableSheetDomAccessEngine.GetCellsInRow(rowNode);
        if (position >= cells.Count)
        {
            for (int i = 0; i < count; i++)
                rowNode.AppendChild(CreateEmptyCell());

            return;
        }

        OdfNode referenceCell = cells[position];
        for (int i = 0; i < count; i++)
            rowNode.InsertBefore(CreateEmptyCell(), referenceCell);
    }

    private static OdfNode? DeleteCellInRow(OdfNode rowNode, int position)
    {
        List<OdfNode> cells = OdfTableSheetDomAccessEngine.GetCellsInRow(rowNode);
        if (position < 0 || position >= cells.Count)
            return null;

        OdfNode cell = cells[position];
        OdfNode snapshot = cell.CloneNode(deep: true);
        cell.Parent?.RemoveChild(cell);
        return snapshot;
    }

    private static void RestoreCellInRow(OdfNode rowNode, int position, OdfNode cellSnapshot)
    {
        List<OdfNode> cells = OdfTableSheetDomAccessEngine.GetCellsInRow(rowNode);
        OdfNode restoredCell = cellSnapshot.CloneNode(deep: true);

        if (position < cells.Count)
        {
            rowNode.InsertBefore(restoredCell, cells[position]);
            return;
        }

        rowNode.AppendChild(restoredCell);
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

    private static OdfNode CreateEmptyRow() =>
        new(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");

    private static OdfNode CreateEmptyColumn() =>
        new(OdfNodeType.Element, "table-column", OdfNamespaces.Table, "table");

    private static OdfNode CreateEmptyCell() =>
        new(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table");

    private static void CopyCellContent(OdfNode source, OdfNode target)
    {
        ClearCellContent(target);
        CopyCellAttributes(source, target);

        foreach (OdfNode child in target.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Text)
                target.RemoveChild(child);
        }

        foreach (OdfNode child in source.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Text)
                target.AppendChild(child.CloneNode(deep: true));
        }
    }

    private static void ClearCellContent(OdfNode cellNode)
    {
        cellNode.RemoveAttribute("value-type", OdfNamespaces.Office);
        cellNode.RemoveAttribute("value", OdfNamespaces.Office);
        cellNode.RemoveAttribute("boolean-value", OdfNamespaces.Office);
        cellNode.RemoveAttribute("date-value", OdfNamespaces.Office);
        cellNode.RemoveAttribute("formula", OdfNamespaces.Table);

        var textChildren = new List<OdfNode>();
        foreach (OdfNode child in cellNode.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Text)
                textChildren.Add(child);
        }

        foreach (OdfNode child in textChildren)
            cellNode.RemoveChild(child);
    }

    private static void CopyCellAttributes(OdfNode source, OdfNode target)
    {
        CopyAttribute(source, target, "value-type", OdfNamespaces.Office);
        CopyAttribute(source, target, "value", OdfNamespaces.Office);
        CopyAttribute(source, target, "boolean-value", OdfNamespaces.Office);
        CopyAttribute(source, target, "date-value", OdfNamespaces.Office);
        CopyAttribute(source, target, "formula", OdfNamespaces.Table);
        CopyAttribute(source, target, "style-name", OdfNamespaces.Table);
    }

    private static void CopyAttribute(OdfNode source, OdfNode target, string localName, string namespaceUri)
    {
        string? value = source.GetAttribute(localName, namespaceUri);
        if (value is null)
            target.RemoveAttribute(localName, namespaceUri);
        else
            target.SetAttribute(localName, namespaceUri, value, OdfNamespaces.GetPrefix(namespaceUri));
    }
}
