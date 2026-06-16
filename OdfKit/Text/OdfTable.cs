using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

/// <summary>
/// 表示文字文件中的表格。
/// </summary>
public class OdfTable
{
    /// <summary>
    /// 取得與此表格相關聯的 OdfNode 節點。
    /// </summary>
    internal OdfNode Node { get; }

    private readonly TextDocument _doc;
    private readonly int _rows;
    private readonly int _cols;

    /// <summary>
    /// 初始化 <see cref="OdfTable"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">表格的 OdfNode 節點</param>
    /// <param name="rows">表格列數</param>
    /// <param name="cols">表格欄數</param>
    /// <param name="doc">所屬的文字文件</param>
    public OdfTable(OdfNode node, int rows, int cols, TextDocument doc)
    {
        Node = node;
        _rows = rows;
        _cols = cols;
        _doc = doc;
        BuildGrid();
    }

    private void BuildGrid()
    {
        for (int r = 0; r < _rows; r++)
        {
            var rNode = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");
            for (int c = 0; c < _cols; c++)
            {
                var cNode = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table");
                rNode.AppendChild(cNode);
            }
            Node.AppendChild(rNode);
        }
    }

    /// <summary>
    /// 取得或設定表格的無障礙摘要說明（對應 ODF <c>table:summary</c> 屬性）。
    /// </summary>
    public string? Summary
    {
        get => Node.GetAttribute("summary", OdfNamespaces.Table);
        set
        {
            if (string.IsNullOrEmpty(value))
                Node.RemoveAttribute("summary", OdfNamespaces.Table);
            else
                Node.SetAttribute("summary", OdfNamespaces.Table, value!, "table");
        }
    }

    /// <summary>
    /// 合併表格中的儲存格。
    /// </summary>
    /// <param name="startRow">起始列索引</param>
    /// <param name="startCol">起始欄索引</param>
    /// <param name="rowSpan">橫跨的列數</param>
    /// <param name="colSpan">橫跨的欄數</param>
    public void MergeCells(int startRow, int startCol, int rowSpan, int colSpan)
    {
        List<OdfNode> rows = [];
        foreach (var child in Node.Children)
        {
            if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
            {
                rows.Add(child);
            }
        }

        var targetRowNode = rows[startRow];
        List<OdfNode> cellsInTargetRow = [];
        foreach (var child in targetRowNode.Children)
        {
            if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") && child.NamespaceUri == OdfNamespaces.Table)
            {
                cellsInTargetRow.Add(child);
            }
        }
        var targetCell = cellsInTargetRow[startCol];
        targetCell.SetAttribute("number-rows-spanned", OdfNamespaces.Table, rowSpan.ToString(), "table");
        targetCell.SetAttribute("number-columns-spanned", OdfNamespaces.Table, colSpan.ToString(), "table");

        for (int r = startRow; r < startRow + rowSpan; r++)
        {
            var rowNode = rows[r];
            List<OdfNode> cellsInRow = [];
            foreach (var child in rowNode.Children)
            {
                if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") && child.NamespaceUri == OdfNamespaces.Table)
                {
                    cellsInRow.Add(child);
                }
            }

            for (int c = startCol; c < startCol + colSpan; c++)
            {
                if (r == startRow && c == startCol)
                    continue;

                var cellToRemove = cellsInRow[c];
                var coveredNode = new OdfNode(OdfNodeType.Element, "covered-table-cell", OdfNamespaces.Table, "table");
                rowNode.InsertBefore(coveredNode, cellToRemove);
                rowNode.RemoveChild(cellToRemove);
            }
        }
    }

    /// <summary>
    /// 在指定儲存格中新增巢狀表格。
    /// </summary>
    /// <param name="row">儲存格列索引</param>
    /// <param name="col">儲存格欄索引</param>
    /// <param name="nestedRows">巢狀表格列數</param>
    /// <param name="nestedCols">巢狀表格欄數</param>
    /// <returns>建立的巢狀表格物件</returns>
    public OdfTable AddNestedTable(int row, int col, int nestedRows, int nestedCols)
    {
        var cellNode = GetCellNode(row, col);
        var nestedTableNode = OdfNodeFactory.CreateElement("table", OdfNamespaces.Table, "table");
        cellNode.AppendChild(nestedTableNode);
        return new OdfTable(nestedTableNode, nestedRows, nestedCols, _doc);
    }

    /// <summary>
    /// 設定指定儲存格的樣式名稱。
    /// </summary>
    /// <param name="row">儲存格列索引</param>
    /// <param name="col">儲存格欄索引</param>
    /// <param name="styleName">樣式名稱</param>
    public void SetCellStyle(int row, int col, string styleName)
    {
        var cellNode = GetCellNode(row, col);
        cellNode.SetAttribute("style-name", OdfNamespaces.Table, styleName, "table");
    }

    /// <summary>
    /// 設定指定列的重複次數。
    /// </summary>
    /// <param name="row">列索引</param>
    /// <param name="repeatCount">重複次數</param>
    public void SetRowRepeat(int row, int repeatCount)
    {
        List<OdfNode> rows = [];
        foreach (var child in Node.Children)
        {
            if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
                rows.Add(child);
        }
        var rowNode = rows[row];
        rowNode.SetAttribute("number-rows-repeated", OdfNamespaces.Table, repeatCount.ToString(), "table");
    }

    private OdfNode GetCellNode(int row, int col)
    {
        List<OdfNode> rows = [];
        foreach (var child in Node.Children)
        {
            if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
                rows.Add(child);
        }
        var rowNode = rows[row];
        List<OdfNode> cells = [];
        foreach (var child in rowNode.Children)
        {
            if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") && child.NamespaceUri == OdfNamespaces.Table)
                cells.Add(child);
        }
        return cells[col];
    }

    /// <summary>
    /// 取得指定的儲存格物件。
    /// </summary>
    /// <param name="row">列索引</param>
    /// <param name="col">欄索引</param>
    /// <returns>對應的儲存格執行個體</returns>
    public OdfTableCell GetCell(int row, int col)
    {
        var cellNode = GetCellNode(row, col);
        return new OdfTableCell(cellNode, _doc);
    }

    /// <summary>
    /// 設定指定欄的欄寬。
    /// </summary>
    /// <param name="col">欄位索引</param>
    /// <param name="width">欄寬值</param>
    public void SetColumnWidth(int col, OdfLength width)
    {
        var colNode = GetOrCreateColumnNode(col);
        _doc.StyleEngine.SetLocalStyleProperty(colNode, "table-column", "table-column-properties", "column-width", OdfNamespaces.Style, width.ToString(), "style");
    }

    private OdfNode GetOrCreateColumnNode(int col)
    {
        List<OdfNode> cols = [];
        OdfNode? firstNonCol = null;
        foreach (var child in Node.Children)
        {
            if (child.NodeType == OdfNodeType.Element)
            {
                if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
                {
                    cols.Add(child);
                }
                else if (firstNonCol is null)
                {
                    firstNonCol = child;
                }
            }
        }

        while (cols.Count <= col)
        {
            var newCol = OdfNodeFactory.CreateElement("table-column", OdfNamespaces.Table, "table");
            if (firstNonCol is not null)
            {
                Node.InsertBefore(newCol, firstNonCol);
            }
            else
            {
                Node.AppendChild(newCol);
            }
            cols.Add(newCol);
        }

        return cols[col];
    }
}
