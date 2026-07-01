using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 工作表列／欄／儲存格 DOM 存取引擎（內部協作者）。
/// </summary>
internal static class OdfTableSheetDomAccessEngine
{
    internal static readonly HashSet<string> RowContainerNames = new(System.StringComparer.Ordinal)
        { "header-rows", "table-row-group" };

    /// <summary>
    /// 取得工作表中所有欄定義節點的清單（僅含連續的 <c>table:table-column</c>）。
    /// </summary>
    internal static List<OdfNode> GetColumnsList(OdfNode tableNode)
    {
        var list = new List<OdfNode>();
        foreach (OdfNode child in tableNode.Children)
        {
            if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
                list.Add(child);
            else
                break;
        }

        return list;
    }

    /// <summary>
    /// 依「邏輯欄索引」（已展開 <c>number-columns-repeated</c>）尋找對應的欄定義節點；
    /// 若索引落在某個重複欄節點範圍內，會先將該節點拆分為單一節點再回傳。索引超出既有欄位定義範圍時回傳 <see langword="null"/>。
    /// </summary>
    internal static OdfNode? TryGetColumnNodeAtLogicalIndex(OdfNode tableNode, int position)
    {
        if (position < 0)
            return null;

        int currentColumnIndex = 0;
        foreach (OdfNode child in tableNode.Children)
        {
            if (child.LocalName != "table-column" || child.NamespaceUri != OdfNamespaces.Table)
                break;

            int repeatedCount = OdfTableSheetRepeatSplitEngine.GetRepeatCount(child, "number-columns-repeated");
            if (position >= currentColumnIndex && position < currentColumnIndex + repeatedCount)
            {
                return repeatedCount > 1
                    ? OdfTableSheetRepeatSplitEngine.SplitRepeatedColumn(tableNode, child, position, currentColumnIndex, repeatedCount)
                    : child;
            }

            currentColumnIndex += repeatedCount;
        }

        return null;
    }

    /// <summary>
    /// 依「邏輯欄索引」（已展開 <c>number-columns-repeated</c>）尋找列節點中對應的儲存格節點；
    /// 若索引落在某個重複儲存格節點範圍內，會先將該節點拆分為單一節點再回傳。索引超出既有儲存格範圍時回傳 <see langword="null"/>。
    /// </summary>
    internal static OdfNode? TryGetCellNodeAtLogicalIndex(OdfNode rowNode, int col)
    {
        if (col < 0)
            return null;

        int currentColIndex = 0;
        foreach (OdfNode child in rowNode.Children)
        {
            if ((child.LocalName != "table-cell" && child.LocalName != "covered-table-cell") || child.NamespaceUri != OdfNamespaces.Table)
                continue;

            int repeatedCount = OdfTableSheetRepeatSplitEngine.GetRepeatCount(child, "number-columns-repeated");
            if (col >= currentColIndex && col < currentColIndex + repeatedCount)
            {
                return repeatedCount > 1
                    ? OdfTableSheetRepeatSplitEngine.SplitRepeatedCell(child, col, currentColIndex, repeatedCount, rowNode)
                    : child;
            }

            currentColIndex += repeatedCount;
        }

        return null;
    }

    /// <summary>
    /// 取得工作表中所有列節點的清單（展開列容器）。
    /// </summary>
    internal static List<OdfNode> GetRowsList(OdfNode tableNode)
    {
        var list = new List<OdfNode>();
        foreach (var child in tableNode.Children)
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

    /// <summary>
    /// 取得指定列節點中的所有儲存格節點。
    /// </summary>
    internal static List<OdfNode> GetCellsInRow(OdfNode rowNode)
    {
        var list = new List<OdfNode>();
        foreach (var child in rowNode.Children)
        {
            if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") && child.NamespaceUri == OdfNamespaces.Table)
                list.Add(child);
        }
        return list;
    }

    /// <summary>
    /// 取得或建立指定列索引的列節點。
    /// </summary>
    internal static OdfNode GetOrCreateRowNode(OdfNode tableNode, int row, bool forWrite)
    {
        int currentRowIndex = 0;
        foreach (var child in tableNode.Children)
        {
            if (RowContainerNames.Contains(child.LocalName) && child.NamespaceUri == OdfNamespaces.Table)
            {
                foreach (var hr in child.Children)
                {
                    if (hr.LocalName != "table-row" || hr.NamespaceUri != OdfNamespaces.Table)
                        continue;
                    int rep = OdfTableSheetRepeatSplitEngine.GetRepeatCount(hr, "number-rows-repeated");
                    if (row >= currentRowIndex && row < currentRowIndex + rep)
                        return (forWrite && rep > 1)
                            ? OdfTableSheetRepeatSplitEngine.SplitRepeatedRow(tableNode, hr, row, currentRowIndex, rep)
                            : hr;
                    currentRowIndex += rep;
                }
            }
            else if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
            {
                int repeatedCount = OdfTableSheetRepeatSplitEngine.GetRepeatCount(child, "number-rows-repeated");

                if (row >= currentRowIndex && row < currentRowIndex + repeatedCount)
                    return (forWrite && repeatedCount > 1)
                        ? OdfTableSheetRepeatSplitEngine.SplitRepeatedRow(tableNode, child, row, currentRowIndex, repeatedCount)
                        : child;
                currentRowIndex += repeatedCount;
            }
        }

        OdfNode? lastRow = null;
        while (currentRowIndex <= row)
        {
            lastRow = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");
            tableNode.AppendChild(lastRow);
            currentRowIndex++;
        }
        return lastRow!;
    }

    /// <summary>
    /// 唯讀查找已存在的列節點，不會像 <see cref="GetOrCreateRowNode"/> 一樣在索引超出範圍時建立新列。
    /// </summary>
    internal static OdfNode? TryFindRowNode(OdfNode tableNode, int row)
    {
        int currentRowIndex = 0;
        foreach (var child in tableNode.Children)
        {
            if (RowContainerNames.Contains(child.LocalName) && child.NamespaceUri == OdfNamespaces.Table)
            {
                foreach (var hr in child.Children)
                {
                    if (hr.LocalName != "table-row" || hr.NamespaceUri != OdfNamespaces.Table)
                        continue;
                    int rep = OdfTableSheetRepeatSplitEngine.GetRepeatCount(hr, "number-rows-repeated");
                    if (row >= currentRowIndex && row < currentRowIndex + rep)
                        return hr;
                    currentRowIndex += rep;
                }
            }
            else if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
            {
                int repeatedCount = OdfTableSheetRepeatSplitEngine.GetRepeatCount(child, "number-rows-repeated");

                if (row >= currentRowIndex && row < currentRowIndex + repeatedCount)
                    return child;
                currentRowIndex += repeatedCount;
            }
        }

        return null;
    }

    /// <summary>
    /// 取得或建立指定欄索引的儲存格節點。
    /// </summary>
    internal static OdfNode GetOrCreateCellNode(OdfNode rowNode, int col, bool forWrite)
    {
        int currentColIndex = 0;
        foreach (var child in rowNode.Children)
        {
            if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") && child.NamespaceUri == OdfNamespaces.Table)
            {
                int repeatedCount = OdfTableSheetRepeatSplitEngine.GetRepeatCount(child, "number-columns-repeated");

                if (col >= currentColIndex && col < currentColIndex + repeatedCount)
                {
                    if (forWrite && repeatedCount > 1)
                    {
                        return OdfTableSheetRepeatSplitEngine.SplitRepeatedCell(child, col, currentColIndex, repeatedCount, rowNode);
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

    /// <summary>
    /// 取得或建立指定列與欄索引的儲存格節點。
    /// </summary>
    internal static OdfNode GetOrCreateCellNode(OdfNode tableNode, int row, int col)
    {
        EnsureColumnDefinitions(tableNode, col);
        var rowNode = GetOrCreateRowNode(tableNode, row, forWrite: true);
        return GetOrCreateCellNode(rowNode, col, forWrite: true);
    }

    /// <summary>
    /// 確保表格至少宣告到指定索引的欄定義節點（<c>table:table-column</c>）。
    /// </summary>
    internal static void EnsureColumnDefinitions(OdfNode tableNode, int col)
    {
        int currentColumnIndex = 0;
        OdfNode? lastColumn = null;
        foreach (OdfNode child in tableNode.Children)
        {
            if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
            {
                int repeatedCount = OdfTableSheetRepeatSplitEngine.GetRepeatCount(child, "number-columns-repeated");
                currentColumnIndex += repeatedCount;
                lastColumn = child;
                continue;
            }

            if (child.NamespaceUri == OdfNamespaces.Table &&
                child.LocalName is "table-columns" or "table-column-group")
            {
                currentColumnIndex += CountColumnsInGroup(child);
                lastColumn = child;
                continue;
            }

            if (child.NamespaceUri == OdfNamespaces.Table &&
                child.LocalName is "table-row" or "table-rows" or "table-row-group" or "named-expressions")
            {
                break;
            }
        }

        while (currentColumnIndex <= col)
        {
            var columnNode = new OdfNode(OdfNodeType.Element, "table-column", OdfNamespaces.Table, "table");
            InsertColumnDefinition(tableNode, columnNode, lastColumn);
            lastColumn = columnNode;
            currentColumnIndex++;
        }
    }

    private static int CountColumnsInGroup(OdfNode groupNode)
    {
        int count = 0;
        foreach (OdfNode child in groupNode.Children)
        {
            if (child.NamespaceUri != OdfNamespaces.Table)
                continue;

            if (child.LocalName == "table-column")
            {
                count += OdfTableSheetRepeatSplitEngine.GetRepeatCount(child, "number-columns-repeated");
            }
            else if (child.LocalName is "table-columns" or "table-column-group")
            {
                count += CountColumnsInGroup(child);
            }
        }
        return count;
    }

    private static void InsertColumnDefinition(OdfNode tableNode, OdfNode columnNode, OdfNode? lastColumn)
    {
        if (lastColumn is not null)
        {
            int lastIndex = tableNode.Children.IndexOf(lastColumn);
            if (lastIndex >= 0 && lastIndex + 1 < tableNode.Children.Count)
            {
                tableNode.InsertBefore(columnNode, tableNode.Children[lastIndex + 1]);
                return;
            }

            tableNode.AppendChild(columnNode);
            return;
        }

        foreach (OdfNode child in tableNode.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Table &&
                child.LocalName is "table-row" or "table-rows" or "table-row-group" or "named-expressions")
            {
                tableNode.InsertBefore(columnNode, child);
                return;
            }
        }

        tableNode.AppendChild(columnNode);
    }

    /// <summary>
    /// 以新儲存格節點取代指定位置的儲存格。
    /// </summary>
    internal static void ReplaceCellNode(OdfNode tableNode, int row, int col, OdfNode newCellNode)
    {
        var rowNode = GetOrCreateRowNode(tableNode, row, forWrite: true);
        var oldCell = GetOrCreateCellNode(rowNode, col, forWrite: true);
        rowNode.InsertBefore(newCellNode, oldCell);
        rowNode.RemoveChild(oldCell);
    }

    /// <summary>
    /// 嘗試以唯讀方式取得指定列與欄索引的儲存格節點。
    /// </summary>
    internal static OdfNode? TryGetCellNode(OdfNode tableNode, int row, int col)
    {
        int currentRowIndex = 0;
        OdfNode? targetRowNode = null;

        foreach (var child in tableNode.Children)
        {
            if (RowContainerNames.Contains(child.LocalName) && child.NamespaceUri == OdfNamespaces.Table)
            {
                foreach (var hr in child.Children)
                {
                    if (hr.LocalName != "table-row" || hr.NamespaceUri != OdfNamespaces.Table)
                        continue;
                    int rep = OdfTableSheetRepeatSplitEngine.GetRepeatCount(hr, "number-rows-repeated");
                    if (row >= currentRowIndex && row < currentRowIndex + rep)
                    { targetRowNode = hr; break; }
                    currentRowIndex += rep;
                }
                if (targetRowNode is not null)
                    break;
            }
            else if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
            {
                int repeatedCount = OdfTableSheetRepeatSplitEngine.GetRepeatCount(child, "number-rows-repeated");

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
                int repeatedCount = OdfTableSheetRepeatSplitEngine.GetRepeatCount(child, "number-columns-repeated");

                if (col >= currentColIndex && col < currentColIndex + repeatedCount)
                {
                    return child;
                }
                currentColIndex += repeatedCount;
            }
        }

        return null;
    }

    /// <summary>
    /// 取得或建立指定欄索引的欄節點。
    /// </summary>
    internal static OdfNode GetOrCreateColumnNode(OdfNode tableNode, int col)
    {
        int currentColIndex = 0;
        OdfNode? insertBeforeNode = null;
        var cols = new List<OdfNode>();

        foreach (var child in tableNode.Children)
        {
            if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
            {
                cols.Add(child);
                int repeatedCount = OdfTableSheetRepeatSplitEngine.GetRepeatCount(child, "number-columns-repeated");

                if (col >= currentColIndex && col < currentColIndex + repeatedCount)
                {
                    if (repeatedCount > 1)
                    {
                        return OdfTableSheetRepeatSplitEngine.SplitRepeatedColumn(tableNode, child, col, currentColIndex, repeatedCount);
                    }
                    return child;
                }
                currentColIndex += repeatedCount;
            }
            else if (insertBeforeNode is null &&
                child.NamespaceUri == OdfNamespaces.Table &&
                child.LocalName is "table-row" or "table-rows" or "table-row-group" or "named-expressions")
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
                tableNode.InsertBefore(lastCol, insertBeforeNode);
            }
            else
            {
                tableNode.AppendChild(lastCol);
            }
            currentColIndex++;
        }
        return lastCol!;
    }

    /// <summary>
    /// 唯讀查找已存在的欄節點，不會像 <see cref="GetOrCreateColumnNode"/> 一樣在索引超出範圍時建立新欄。
    /// </summary>
    internal static OdfNode? TryFindColumnNode(OdfNode tableNode, int col)
    {
        int currentColIndex = 0;

        foreach (var child in tableNode.Children)
        {
            if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
            {
                int repeatedCount = OdfTableSheetRepeatSplitEngine.GetRepeatCount(child, "number-columns-repeated");

                if (col >= currentColIndex && col < currentColIndex + repeatedCount)
                {
                    return child;
                }

                currentColIndex += repeatedCount;
            }
        }

        return null;
    }

    /// <summary>
    /// 列舉工作表中所有既有的儲存格節點及其座標。
    /// </summary>
    internal static IEnumerable<(OdfNode Node, int Row, int Column)> EnumerateExistingCells(OdfNode tableNode)
    {
        int rowIndex = 0;
        foreach (var child in tableNode.Children)
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
                    rowIndex += OdfTableSheetRepeatSplitEngine.GetRepeatCount(row, "number-rows-repeated");
                }
            }
            else if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
            {
                foreach (var item in EnumerateExistingCellsInRow(child, rowIndex))
                {
                    yield return item;
                }
                rowIndex += OdfTableSheetRepeatSplitEngine.GetRepeatCount(child, "number-rows-repeated");
            }
        }
    }

    /// <summary>
    /// 判斷儲存格節點是否含有實際內容或屬性。
    /// </summary>
    internal static bool IsUsedCell(OdfNode cellNode)
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

    private static IEnumerable<(OdfNode Node, int Row, int Column)> EnumerateExistingCellsInRow(OdfNode rowNode, int row)
    {
        int columnIndex = 0;
        foreach (var child in rowNode.Children)
        {
            if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") &&
                child.NamespaceUri == OdfNamespaces.Table)
            {
                yield return (child, row, columnIndex);
                columnIndex += OdfTableSheetRepeatSplitEngine.GetRepeatCount(child, "number-columns-repeated");
            }
        }
    }
}
