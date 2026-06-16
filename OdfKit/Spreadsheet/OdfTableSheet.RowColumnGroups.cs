using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region 列欄群組


    /// <summary>
    /// 將指定列範圍設為可展開/收合的群組。
    /// </summary>
    /// <param name="startRow">起始列索引（0 為基準）</param>
    /// <param name="endRow">結束列索引（包含，0 為基準）</param>
    /// <param name="collapsed">是否預設為收合狀態</param>
    public void GroupRows(int startRow, int endRow, bool collapsed = false)
    {
        var rowsToWrap = new List<OdfNode>();
        for (int r = startRow; r <= endRow; r++)
            rowsToWrap.Add(GetOrCreateRowNodeInternal(r, forWrite: true));

        var groupNode = new OdfNode(OdfNodeType.Element, "table-row-group", OdfNamespaces.Table, "table");
        groupNode.SetAttribute("display", OdfNamespaces.Table, collapsed ? "false" : "true", "table");

        OdfNode firstRow = rowsToWrap[0];
        OdfNode firstRowParent = firstRow.Parent ?? TableNode;
        firstRowParent.InsertBefore(groupNode, firstRow);

        foreach (var rowNode in rowsToWrap)
        {
            (rowNode.Parent ?? TableNode).RemoveChild(rowNode);
            groupNode.AppendChild(rowNode);
        }
    }

    /// <summary>
    /// 移除包含指定列範圍的群組，將列移回工作表主體。
    /// </summary>
    /// <param name="startRow">起始列索引（0 為基準）</param>
    /// <param name="endRow">結束列索引（包含，0 為基準）</param>
    public void UngroupRows(int startRow, int endRow)
    {
        foreach (var child in new List<OdfNode>(TableNode.Children))
        {
            if (child.LocalName != "table-row-group" || child.NamespaceUri != OdfNamespaces.Table)
                continue;
            OdfNode? insertAfter = child;
            foreach (var row in new List<OdfNode>(child.Children))
            {
                if (row.LocalName != "table-row" || row.NamespaceUri != OdfNamespaces.Table)
                    continue;
                child.RemoveChild(row);
                TableNode.InsertAfter(row, insertAfter);
                insertAfter = row;
            }
            TableNode.RemoveChild(child);
        }
    }

    /// <summary>
    /// 將指定欄範圍設為可展開/收合的群組。
    /// </summary>
    /// <param name="startCol">起始欄索引（0 為基準）</param>
    /// <param name="endCol">結束欄索引（包含，0 為基準）</param>
    /// <param name="collapsed">是否預設為收合狀態</param>
    public void GroupColumns(int startCol, int endCol, bool collapsed = false)
    {
        var colsToWrap = new List<OdfNode>();
        for (int c = startCol; c <= endCol; c++)
            colsToWrap.Add(GetOrCreateColumnNode(c));

        var groupNode = new OdfNode(OdfNodeType.Element, "table-column-group", OdfNamespaces.Table, "table");
        groupNode.SetAttribute("display", OdfNamespaces.Table, collapsed ? "false" : "true", "table");

        OdfNode firstCol = colsToWrap[0];
        TableNode.InsertBefore(groupNode, firstCol);
        foreach (var colNode in colsToWrap)
        {
            TableNode.RemoveChild(colNode);
            groupNode.AppendChild(colNode);
        }
    }

    /// <summary>
    /// 移除包含指定欄範圍的群組，將欄移回工作表主體。
    /// </summary>
    /// <param name="startCol">起始欄索引（0 為基準）</param>
    /// <param name="endCol">結束欄索引（包含，0 為基準）</param>
    public void UngroupColumns(int startCol, int endCol)
    {
        foreach (var child in new List<OdfNode>(TableNode.Children))
        {
            if (child.LocalName != "table-column-group" || child.NamespaceUri != OdfNamespaces.Table)
                continue;
            OdfNode? insertAfter = child;
            foreach (var col in new List<OdfNode>(child.Children))
            {
                if (col.LocalName != "table-column" || col.NamespaceUri != OdfNamespaces.Table)
                    continue;
                child.RemoveChild(col);
                TableNode.InsertAfter(col, insertAfter);
                insertAfter = col;
            }
            TableNode.RemoveChild(child);
        }
    }


    #endregion
}
