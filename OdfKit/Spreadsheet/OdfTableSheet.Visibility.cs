using System;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region Visibility

    /// <summary>
    /// 設定指定列是否可見。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引</param>
    /// <param name="visible">是否顯示</param>
    public void SetRowVisible(int row, bool visible)
    {
        var rowNode = GetOrCreateRowNode(row);
        rowNode.SetAttribute("visibility", OdfNamespaces.Table, visible ? "visible" : "collapse", "table");
    }

    /// <summary>
    /// 設定指定欄是否可見。
    /// </summary>
    /// <param name="col">以 0 為基準的欄索引</param>
    /// <param name="visible">是否顯示</param>
    public void SetColumnVisible(int col, bool visible)
    {
        var colNode = GetOrCreateColumnNode(col);
        colNode.SetAttribute("visibility", OdfNamespaces.Table, visible ? "visible" : "collapse", "table");
    }

    /// <summary>
    /// 判斷指定列是否可見。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引</param>
    /// <returns>若顯示則為 true，否則為 false</returns>
    public bool IsRowVisible(int row)
    {
        int currentRowIndex = 0;
        foreach (var child in TableNode.Children)
        {
            if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
            {
                int repeatedCount = 1;
                string? repStr = child.GetAttribute("number-rows-repeated", OdfNamespaces.Table);
                if (!string.IsNullOrEmpty(repStr) && int.TryParse(repStr, out int rc))
                {
                    repeatedCount = rc;
                }

                if (row >= currentRowIndex && row < currentRowIndex + repeatedCount)
                {
                    return child.GetAttribute("visibility", OdfNamespaces.Table) != "collapse";
                }
                currentRowIndex += repeatedCount;
            }
        }
        return true;
    }

    /// <summary>
    /// 判斷指定欄是否可見。
    /// </summary>
    /// <param name="col">以 0 為基準的欄索引</param>
    /// <returns>若顯示則為 true，否則為 false</returns>
    public bool IsColumnVisible(int col)
    {
        int currentColIndex = 0;
        foreach (var child in TableNode.Children)
        {
            if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
            {
                int repeatedCount = 1;
                string? repStr = child.GetAttribute("number-columns-repeated", OdfNamespaces.Table);
                if (!string.IsNullOrEmpty(repStr) && int.TryParse(repStr, out int rc))
                {
                    repeatedCount = rc;
                }

                if (col >= currentColIndex && col < currentColIndex + repeatedCount)
                {
                    return child.GetAttribute("visibility", OdfNamespaces.Table) != "collapse";
                }
                currentColIndex += repeatedCount;
            }
        }
        return true;
    }

    /// <summary>
    /// 新增命名範圍至此工作表。
    /// </summary>
    /// <param name="name">命名範圍的名稱</param>
    /// <param name="range">儲存格範圍</param>
    /// <param name="baseCell">基準儲存格位址</param>
    public void AddNamedRange(string name, OdfCellRange range, OdfCellAddress? baseCell = null)
    {
        var namedExpressions = FindOrCreateChild(TableNode, "named-expressions", OdfNamespaces.Table, "table");
        var namedRange = new OdfNode(OdfNodeType.Element, "named-range", OdfNamespaces.Table, "table");
        namedRange.SetAttribute("name", OdfNamespaces.Table, name, "table");
        namedRange.SetAttribute("cell-range-address", OdfNamespaces.Table, range.ToOdfString(false), "table");
        if (baseCell.HasValue)
        {
            namedRange.SetAttribute("base-cell-address", OdfNamespaces.Table, baseCell.Value.ToOdfString(false), "table");
        }
        namedExpressions.AppendChild(namedRange);
    }

    /// <summary>
    /// 取得此工作表中的命名範圍清單。
    /// </summary>
    public IReadOnlyList<OdfNamedRangeInfo> NamedRanges
    {
        get
        {
            OdfNode? namedExpressions = FindChildElement(TableNode, "named-expressions", OdfNamespaces.Table);
            if (namedExpressions is null)
            {
                return [];
            }

            List<OdfNamedRangeInfo> ranges = [];
            foreach (OdfNode child in namedExpressions.Children)
            {
                if (child.NodeType is OdfNodeType.Element &&
                    child.LocalName == "named-range" &&
                    child.NamespaceUri == OdfNamespaces.Table)
                {
                    string name = child.GetAttribute("name", OdfNamespaces.Table) ?? string.Empty;
                    string address = child.GetAttribute("cell-range-address", OdfNamespaces.Table) ?? string.Empty;
                    string? baseAddress = child.GetAttribute("base-cell-address", OdfNamespaces.Table);
                    ranges.Add(new OdfNamedRangeInfo(name, address, baseAddress));
                }
            }

            return ranges.AsReadOnly();
        }
    }

    /// <summary>
    /// 新增具名運算式至此工作表。
    /// </summary>
    /// <param name="name">具名運算式的名稱</param>
    /// <param name="expression">公式運算式字串</param>
    /// <param name="baseCell">基準儲存格位址</param>
    public void AddNamedExpression(string name, string expression, OdfCellAddress? baseCell = null)
    {
        var namedExpressions = FindOrCreateChild(TableNode, "named-expressions", OdfNamespaces.Table, "table");
        var namedExpr = new OdfNode(OdfNodeType.Element, "named-expression", OdfNamespaces.Table, "table");
        namedExpr.SetAttribute("name", OdfNamespaces.Table, name, "table");
        namedExpr.SetAttribute("expression", OdfNamespaces.Table, expression, "table");
        if (baseCell.HasValue)
        {
            namedExpr.SetAttribute("base-cell-address", OdfNamespaces.Table, baseCell.Value.ToOdfString(false), "table");
        }
        namedExpressions.AppendChild(namedExpr);
    }

    /// <summary>
    /// 取得此工作表中的具名運算式清單。
    /// </summary>
    public IReadOnlyList<OdfNamedExpressionInfo> NamedExpressions
    {
        get
        {
            OdfNode? namedExpressions = FindChildElement(TableNode, "named-expressions", OdfNamespaces.Table);
            if (namedExpressions is null)
            {
                return [];
            }

            List<OdfNamedExpressionInfo> expressions = [];
            foreach (OdfNode child in namedExpressions.Children)
            {
                if (child.NodeType is OdfNodeType.Element &&
                    child.LocalName == "named-expression" &&
                    child.NamespaceUri == OdfNamespaces.Table)
                {
                    string name = child.GetAttribute("name", OdfNamespaces.Table) ?? string.Empty;
                    string expression = child.GetAttribute("expression", OdfNamespaces.Table) ?? string.Empty;
                    string? baseAddress = child.GetAttribute("base-cell-address", OdfNamespaces.Table);
                    expressions.Add(new OdfNamedExpressionInfo(name, expression, baseAddress));
                }
            }

            return expressions.AsReadOnly();
        }
    }

    private static int ParseNonNegativeInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) && result > 0
            ? result
            : 0;
    }

    private static OdfNode? FindChildElement(OdfNode parent, string localName, string ns)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == ns)
            {
                return child;
            }
        }

        return null;
    }

    private OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
    {
        foreach (var child in parent.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
                return child;
        }
        var node = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
        parent.AppendChild(node);
        return node;
    }


    #endregion
}
