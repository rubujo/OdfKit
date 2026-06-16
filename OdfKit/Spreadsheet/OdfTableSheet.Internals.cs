using System;
using System.Collections;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region Internals

    private static readonly HashSet<string> RowContainerNames = new(StringComparer.Ordinal)
        { "header-rows", "table-row-group" };

    private List<OdfNode> GetRowsList()
    {
        var list = new List<OdfNode>();
        foreach (var child in TableNode.Children)
        {
            if (RowContainerNames.Contains(child.LocalName) && child.NamespaceUri == OdfNamespaces.Table)
            {
                foreach (var inner in child.Children)
                    if (inner.LocalName == "table-row" && inner.NamespaceUri == OdfNamespaces.Table)
                        list.Add(inner);
            }
            else if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
                list.Add(child);
        }
        return list;
    }

    private List<OdfNode> GetCellsInRow(OdfNode rowNode)
    {
        var list = new List<OdfNode>();
        foreach (var child in rowNode.Children)
        {
            if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") && child.NamespaceUri == OdfNamespaces.Table)
                list.Add(child);
        }
        return list;
    }

    private OdfNode SplitRepeatedRow(OdfNode rowNode, int targetRowIndex, int currentRowIndex, int repeatedCount)
    {
        int beforeCount = targetRowIndex - currentRowIndex;
        int afterCount = (currentRowIndex + repeatedCount) - (targetRowIndex + 1);
        var parent = rowNode.Parent ?? TableNode;

        OdfNode targetRowNode = rowNode;

        if (beforeCount > 0)
        {
            var beforeRow = rowNode.CloneNode(true);
            if (beforeCount > 1)
                beforeRow.SetAttribute("number-rows-repeated", OdfNamespaces.Table, beforeCount.ToString(), "table");
            else
                beforeRow.RemoveAttribute("number-rows-repeated", OdfNamespaces.Table);
            parent.InsertBefore(beforeRow, rowNode);
        }

        if (afterCount > 0)
        {
            var afterRow = rowNode.CloneNode(true);
            if (afterCount > 1)
                afterRow.SetAttribute("number-rows-repeated", OdfNamespaces.Table, afterCount.ToString(), "table");
            else
                afterRow.RemoveAttribute("number-rows-repeated", OdfNamespaces.Table);
            parent.InsertAfter(afterRow, rowNode);
        }

        targetRowNode.RemoveAttribute("number-rows-repeated", OdfNamespaces.Table);
        return targetRowNode;
    }

    private OdfNode SplitRepeatedCell(OdfNode cellNode, int targetColIndex, int currentColIndex, int repeatedCount, OdfNode rowNode)
    {
        int beforeCount = targetColIndex - currentColIndex;
        int afterCount = (currentColIndex + repeatedCount) - (targetColIndex + 1);

        OdfNode targetCellNode = cellNode;

        if (beforeCount > 0)
        {
            var beforeCell = cellNode.CloneNode(true);
            if (beforeCount > 1)
                beforeCell.SetAttribute("number-columns-repeated", OdfNamespaces.Table, beforeCount.ToString(), "table");
            else
                beforeCell.RemoveAttribute("number-columns-repeated", OdfNamespaces.Table);
            rowNode.InsertBefore(beforeCell, cellNode);
        }

        if (afterCount > 0)
        {
            var afterCell = cellNode.CloneNode(true);
            if (afterCount > 1)
                afterCell.SetAttribute("number-columns-repeated", OdfNamespaces.Table, afterCount.ToString(), "table");
            else
                afterCell.RemoveAttribute("number-columns-repeated", OdfNamespaces.Table);
            rowNode.InsertAfter(afterCell, cellNode);
        }

        targetCellNode.RemoveAttribute("number-columns-repeated", OdfNamespaces.Table);
        return targetCellNode;
    }

    private OdfNode SplitRepeatedColumn(OdfNode colNode, int targetColIndex, int currentColIndex, int repeatedCount)
    {
        int beforeCount = targetColIndex - currentColIndex;
        int afterCount = (currentColIndex + repeatedCount) - (targetColIndex + 1);

        OdfNode targetColNode = colNode;

        if (beforeCount > 0)
        {
            var beforeCol = colNode.CloneNode(true);
            if (beforeCount > 1)
                beforeCol.SetAttribute("number-columns-repeated", OdfNamespaces.Table, beforeCount.ToString(), "table");
            else
                beforeCol.RemoveAttribute("number-columns-repeated", OdfNamespaces.Table);
            TableNode.InsertBefore(beforeCol, colNode);
        }

        if (afterCount > 0)
        {
            var afterCol = colNode.CloneNode(true);
            if (afterCount > 1)
                afterCol.SetAttribute("number-columns-repeated", OdfNamespaces.Table, afterCount.ToString(), "table");
            else
                afterCol.RemoveAttribute("number-columns-repeated", OdfNamespaces.Table);
            TableNode.InsertAfter(afterCol, colNode);
        }

        targetColNode.RemoveAttribute("number-columns-repeated", OdfNamespaces.Table);
        return targetColNode;
    }

    private OdfNode GetOrCreateRowNodeInternal(int row, bool forWrite)
    {
        int currentRowIndex = 0;
        foreach (var child in TableNode.Children)
        {
            if (RowContainerNames.Contains(child.LocalName) && child.NamespaceUri == OdfNamespaces.Table)
            {
                foreach (var hr in child.Children)
                {
                    if (hr.LocalName != "table-row" || hr.NamespaceUri != OdfNamespaces.Table)
                        continue;
                    int rep = 1;
                    string? rs = hr.GetAttribute("number-rows-repeated", OdfNamespaces.Table);
                    if (!string.IsNullOrEmpty(rs) && int.TryParse(rs, out int rc))
                        rep = rc;
                    if (row >= currentRowIndex && row < currentRowIndex + rep)
                        return (forWrite && rep > 1) ? SplitRepeatedRow(hr, row, currentRowIndex, rep) : hr;
                    currentRowIndex += rep;
                }
            }
            else if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
            {
                int repeatedCount = 1;
                string? repStr = child.GetAttribute("number-rows-repeated", OdfNamespaces.Table);
                if (!string.IsNullOrEmpty(repStr) && int.TryParse(repStr, out int rc))
                    repeatedCount = rc;

                if (row >= currentRowIndex && row < currentRowIndex + repeatedCount)
                    return (forWrite && repeatedCount > 1) ? SplitRepeatedRow(child, row, currentRowIndex, repeatedCount) : child;
                currentRowIndex += repeatedCount;
            }
        }

        OdfNode? lastRow = null;
        while (currentRowIndex <= row)
        {
            lastRow = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");
            TableNode.AppendChild(lastRow);
            currentRowIndex++;
        }
        return lastRow!;
    }

    private OdfNode GetOrCreateCellNodeInternal(OdfNode rowNode, int col, bool forWrite)
    {
        int currentColIndex = 0;
        foreach (var child in rowNode.Children)
        {
            if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") && child.NamespaceUri == OdfNamespaces.Table)
            {
                int repeatedCount = 1;
                string? repStr = child.GetAttribute("number-columns-repeated", OdfNamespaces.Table);
                if (!string.IsNullOrEmpty(repStr) && int.TryParse(repStr, out int rc))
                {
                    repeatedCount = rc;
                }

                if (col >= currentColIndex && col < currentColIndex + repeatedCount)
                {
                    if (forWrite && repeatedCount > 1)
                    {
                        return SplitRepeatedCell(child, col, currentColIndex, repeatedCount, rowNode);
                    }
                    return child;
                }
                currentColIndex += repeatedCount;
            }
        }

        OdfNode? lastCell = null;
        while (currentColIndex <= col)
        {
            lastCell = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table");
            rowNode.AppendChild(lastCell);
            currentColIndex++;
        }
        return lastCell!;
    }

    private IEnumerable<(OdfNode Node, int Row, int Column)> EnumerateExistingCells()
    {
        int rowIndex = 0;
        foreach (var child in TableNode.Children)
        {
            if (RowContainerNames.Contains(child.LocalName) && child.NamespaceUri == OdfNamespaces.Table)
            {
                foreach (var row in child.Children)
                {
                    if (row.LocalName != "table-row" || row.NamespaceUri != OdfNamespaces.Table)
                        continue;
                    foreach (var item in EnumerateExistingCellsInRow(row, rowIndex))
                    {
                        yield return item;
                    }
                    rowIndex += GetRepeatCount(row, "number-rows-repeated");
                }
            }
            else if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
            {
                foreach (var item in EnumerateExistingCellsInRow(child, rowIndex))
                {
                    yield return item;
                }
                rowIndex += GetRepeatCount(child, "number-rows-repeated");
            }
        }
    }

    private IEnumerable<(OdfNode Node, int Row, int Column)> EnumerateExistingCellsInRow(OdfNode rowNode, int row)
    {
        int columnIndex = 0;
        foreach (var child in rowNode.Children)
        {
            if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") &&
                child.NamespaceUri == OdfNamespaces.Table)
            {
                yield return (child, row, columnIndex);
                columnIndex += GetRepeatCount(child, "number-columns-repeated");
            }
        }
    }

    private static int GetRepeatCount(OdfNode node, string attributeName)
    {
        string? repeatValue = node.GetAttribute(attributeName, OdfNamespaces.Table);
        return int.TryParse(repeatValue, out int count) && count > 0 ? count : 1;
    }

    private static bool IsUsedCell(OdfNode cellNode)
    {
        if (!string.IsNullOrEmpty(cellNode.TextContent))
        {
            return true;
        }

        foreach (var attribute in cellNode.Attributes)
        {
            if (attribute.Key.NamespaceUri != OdfNamespaces.Table ||
                attribute.Key.LocalName != "number-columns-repeated")
            {
                return true;
            }
        }

        return cellNode.Children.Count > 0;
    }

    #endregion
}
