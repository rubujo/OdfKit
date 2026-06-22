using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 工作表版面配置引擎（內部協作者）。
/// </summary>
internal static class OdfTableSheetLayoutEngine
{
    internal static void AutoFitColumnWidth(OdfTableSheetMutationContext context, int col)
    {
        var values = new List<string>();
        foreach (var rowNode in context.GetRowsList())
        {
            var cells = context.GetCellsInRow(rowNode);
            if (col < cells.Count)
                values.Add(cells[col].TextContent);
        }
        SetColumnWidth(context, col, CalculateOptimalColumnWidth(values));
    }

    internal static void SetColumnWidth(OdfTableSheetMutationContext context, int col, OdfLength width)
    {
        var colNode = context.GetOrCreateColumn(col);
        context.Document.StyleEngine.GetOrCreateLocalStyle(colNode, "table-column").GetAttribute("name", OdfNamespaces.Style);
        context.Document.StyleEngine.SetLocalStyleProperty(
            colNode, "table-column", "table-column-properties", "column-width", OdfNamespaces.Style, width.ToString(), "style");
    }

    internal static void SetRowOptimalHeight(OdfTableSheetMutationContext context, int row, bool useOptimal)
    {
        var rowNode = context.GetOrCreateRow(row, forWrite: true);
        if (useOptimal)
        {
            context.Document.StyleEngine.SetLocalStyleProperty(
                rowNode, "table-row", "table-row-properties", "row-height", OdfNamespaces.Style, null, propAttrPrefix: null, deferSave: true);
        }
        context.Document.StyleEngine.SetLocalStyleProperty(
            rowNode, "table-row", "table-row-properties", "use-optimal-row-height", OdfNamespaces.Style, useOptimal ? "true" : "false", "style");
    }

    internal static bool IsRowOptimalHeight(OdfTableSheetMutationContext context, int row)
    {
        // 唯讀查詢不應建立新列，避免在查詢超出現有資料範圍的列索引時，意外於文件中插入大量空白列。
        OdfNode? rowNode = OdfTableSheetDomAccessEngine.TryFindRowNode(context.TableNode, row);
        if (rowNode is null)
            return false;
        string? styleName = rowNode.GetAttribute("style-name", OdfNamespaces.Table);
        if (styleName is null || styleName == "")
            return false;
        string? val = context.Document.StyleEngine.GetStyleProperty(styleName, "use-optimal-row-height", OdfNamespaces.Style, "table-row");
        return val == "true";
    }

    internal static void SetRowHeight(OdfTableSheetMutationContext context, int row, OdfLength? height)
    {
        var rowNode = context.GetOrCreateRow(row, forWrite: true);
        if (height != null)
        {
            context.Document.StyleEngine.SetLocalStyleProperty(
                rowNode, "table-row", "table-row-properties", "use-optimal-row-height", OdfNamespaces.Style, "false", "style", deferSave: true);
        }
        context.Document.StyleEngine.SetLocalStyleProperty(
            rowNode, "table-row", "table-row-properties", "row-height", OdfNamespaces.Style, height?.ToString(), "style");
    }

    internal static OdfLength? GetRowHeight(OdfTableSheetMutationContext context, int row)
    {
        // 唯讀查詢不應建立新列，避免在查詢超出現有資料範圍的列索引時，意外於文件中插入大量空白列。
        OdfNode? rowNode = OdfTableSheetDomAccessEngine.TryFindRowNode(context.TableNode, row);
        if (rowNode is null)
            return null;
        string? styleName = rowNode.GetAttribute("style-name", OdfNamespaces.Table);
        if (styleName is null || styleName == "")
            return null;
        string? val = context.Document.StyleEngine.GetStyleProperty(styleName, "row-height", OdfNamespaces.Style, "table-row");
        if (string.IsNullOrEmpty(val))
            return null;
        return OdfLength.Parse(val);
    }

    internal static OdfLength? GetColumnWidth(OdfTableSheetMutationContext context, int col)
    {
        OdfNode? colNode = FindExistingColumnNode(context.TableNode, col);
        string? styleName = colNode?.GetAttribute("style-name", OdfNamespaces.Table);
        if (string.IsNullOrEmpty(styleName))
            return null;
        string? val = context.Document.StyleEngine.GetStyleProperty(styleName!, "column-width", OdfNamespaces.Style, "table-column");
        if (string.IsNullOrEmpty(val))
            return null;
        return OdfLength.Parse(val);
    }

    /// <summary>
    /// 唯讀查找已存在的欄節點，不會像 <c>GetOrCreateColumnNode</c> 一樣建立新欄。
    /// </summary>
    private static OdfNode? FindExistingColumnNode(OdfNode tableNode, int col)
    {
        int currentColIndex = 0;
        foreach (var child in tableNode.Children)
        {
            if (child.LocalName != "table-column" || child.NamespaceUri != OdfNamespaces.Table)
                continue;

            int repeatedCount = OdfTableSheetRepeatSplitEngine.GetRepeatCount(child, "number-columns-repeated");
            if (col >= currentColIndex && col < currentColIndex + repeatedCount)
                return child;
            currentColIndex += repeatedCount;
        }

        return null;
    }

    private static OdfLength CalculateOptimalColumnWidth(IEnumerable<string> cellValues, double fontSizePt = 10)
    {
        double maxWeight = 0;
        foreach (var value in cellValues)
        {
            double weight = 0;
            foreach (char c in value)
                weight += c <= 127 ? 1.0 : 1.85;
            if (weight > maxWeight)
                maxWeight = weight;
        }
        double totalChars = maxWeight + 1.5;
        double widthInCm = totalChars * (fontSizePt / 10.0) * 0.22;
        return OdfLength.FromCentimeters(Math.Max(widthInCm, 1.0));
    }
}
