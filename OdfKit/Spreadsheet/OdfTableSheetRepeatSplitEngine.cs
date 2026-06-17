using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 工作表重複列／欄／儲存格拆分引擎（內部協作者）。
/// </summary>
internal static class OdfTableSheetRepeatSplitEngine
{
    /// <summary>
    /// 解析節點的重複計數屬性。
    /// </summary>
    internal static int GetRepeatCount(OdfNode node, string attributeName)
    {
        string? repeatValue = node.GetAttribute(attributeName, OdfNamespaces.Table);
        return int.TryParse(repeatValue, out int count) && count > 0 ? count : 1;
    }

    /// <summary>
    /// 將含重複屬性的列節點拆分為目標列索引對應的單一列節點。
    /// </summary>
    internal static OdfNode SplitRepeatedRow(
        OdfNode tableNode,
        OdfNode rowNode,
        int targetRowIndex,
        int currentRowIndex,
        int repeatedCount)
    {
        int beforeCount = targetRowIndex - currentRowIndex;
        int afterCount = (currentRowIndex + repeatedCount) - (targetRowIndex + 1);
        var parent = rowNode.Parent ?? tableNode;

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

    /// <summary>
    /// 將含重複屬性的儲存格節點拆分為目標欄索引對應的單一儲存格節點。
    /// </summary>
    internal static OdfNode SplitRepeatedCell(
        OdfNode cellNode,
        int targetColIndex,
        int currentColIndex,
        int repeatedCount,
        OdfNode rowNode)
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

    /// <summary>
    /// 將含重複屬性的欄節點拆分為目標欄索引對應的單一欄節點。
    /// </summary>
    internal static OdfNode SplitRepeatedColumn(
        OdfNode tableNode,
        OdfNode colNode,
        int targetColIndex,
        int currentColIndex,
        int repeatedCount)
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
            tableNode.InsertBefore(beforeCol, colNode);
        }

        if (afterCount > 0)
        {
            var afterCol = colNode.CloneNode(true);
            if (afterCount > 1)
                afterCol.SetAttribute("number-columns-repeated", OdfNamespaces.Table, afterCount.ToString(), "table");
            else
                afterCol.RemoveAttribute("number-columns-repeated", OdfNamespaces.Table);
            tableNode.InsertAfter(afterCol, colNode);
        }

        targetColNode.RemoveAttribute("number-columns-repeated", OdfNamespaces.Table);
        return targetColNode;
    }
}
