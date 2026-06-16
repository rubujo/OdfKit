using System;
using System.Collections;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region Cell & Column Access

    /// <summary>
    /// 嘗試以唯讀方式取得指定列與欄索引的儲存格 XML 節點，不修改 DOM 結構。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引</param>
    /// <param name="col">以 0 為基準的欄索引</param>
    /// <returns>儲存格 XML 節點，若不存在則為 null</returns>
    internal OdfNode? TryGetCellNode(int row, int col)
    {
        int currentRowIndex = 0;
        OdfNode? targetRowNode = null;

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
                    { targetRowNode = hr; break; }
                    currentRowIndex += rep;
                }
                if (targetRowNode is not null)
                    break;
            }
            else if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
            {
                int repeatedCount = 1;
                string? repStr = child.GetAttribute("number-rows-repeated", OdfNamespaces.Table);
                if (!string.IsNullOrEmpty(repStr) && int.TryParse(repStr, out int rc))
                    repeatedCount = rc;

                if (row >= currentRowIndex && row < currentRowIndex + repeatedCount)
                {
                    targetRowNode = child;
                    break;
                }
                currentRowIndex += repeatedCount;
            }
        }

        if (targetRowNode is null)
        {
            return null;
        }

        int currentColIndex = 0;
        foreach (var child in targetRowNode.Children)
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
                    return child;
                }
                currentColIndex += repeatedCount;
            }
        }

        return null;
    }

    private OdfNode GetOrCreateCellNode(int row, int col)
    {
        var rowNode = GetOrCreateRowNodeInternal(row, forWrite: true);
        return GetOrCreateCellNodeInternal(rowNode, col, forWrite: true);
    }

    private void ReplaceCellNode(int row, int col, OdfNode newCellNode)
    {
        var rowNode = GetOrCreateRowNodeInternal(row, forWrite: true);
        var oldCell = GetOrCreateCellNodeInternal(rowNode, col, forWrite: true);
        rowNode.InsertBefore(newCellNode, oldCell);
        rowNode.RemoveChild(oldCell);
    }

    internal OdfNode GetOrCreateColumnNode(int col)
    {
        int currentColIndex = 0;
        OdfNode? insertBeforeNode = null;
        var cols = new List<OdfNode>();

        foreach (var child in TableNode.Children)
        {
            if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
            {
                cols.Add(child);
                int repeatedCount = 1;
                string? repStr = child.GetAttribute("number-columns-repeated", OdfNamespaces.Table);
                if (!string.IsNullOrEmpty(repStr) && int.TryParse(repStr, out int rc))
                {
                    repeatedCount = rc;
                }

                if (col >= currentColIndex && col < currentColIndex + repeatedCount)
                {
                    if (repeatedCount > 1)
                    {
                        return SplitRepeatedColumn(child, col, currentColIndex, repeatedCount);
                    }
                    return child;
                }
                currentColIndex += repeatedCount;
            }
            else if (cols.Count > 0 && insertBeforeNode is null)
            {
                insertBeforeNode = child;
            }
        }

        OdfNode? lastCol = null;
        while (currentColIndex <= col)
        {
            lastCol = new OdfNode(OdfNodeType.Element, "table-column", OdfNamespaces.Table, "table");
            if (insertBeforeNode is not null)
            {
                TableNode.InsertBefore(lastCol, insertBeforeNode);
            }
            else
            {
                TableNode.AppendChild(lastCol);
            }
            currentColIndex++;
        }
        return lastCol!;
    }

    private OdfNode GetOrCreateRowNode(int row)
    {
        return GetOrCreateRowNodeInternal(row, forWrite: true);
    }

    #endregion
}
