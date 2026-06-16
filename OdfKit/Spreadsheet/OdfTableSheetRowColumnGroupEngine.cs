using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 工作表列欄群組引擎（內部協作者）。
/// </summary>
internal static class OdfTableSheetRowColumnGroupEngine
{
    /// <summary>
    /// 將指定列範圍設為可展開/收合的群組。
    /// </summary>
    internal static void GroupRows(OdfTableSheetMutationContext context, int startRow, int endRow, bool collapsed)
    {
        var rowsToWrap = new List<OdfNode>();
        for (int r = startRow; r <= endRow; r++)
            rowsToWrap.Add(context.GetOrCreateRow(r, forWrite: true));

        var groupNode = new OdfNode(OdfNodeType.Element, "table-row-group", OdfNamespaces.Table, "table");
        groupNode.SetAttribute("display", OdfNamespaces.Table, collapsed ? "false" : "true", "table");

        OdfNode firstRow = rowsToWrap[0];
        OdfNode firstRowParent = firstRow.Parent ?? context.TableNode;
        firstRowParent.InsertBefore(groupNode, firstRow);

        foreach (var rowNode in rowsToWrap)
        {
            (rowNode.Parent ?? context.TableNode).RemoveChild(rowNode);
            groupNode.AppendChild(rowNode);
        }
    }

    /// <summary>
    /// 移除包含指定列範圍的群組，將列移回工作表主體。
    /// </summary>
    internal static void UngroupRows(OdfTableSheetMutationContext context, int startRow, int endRow)
    {
        foreach (var child in new List<OdfNode>(context.TableNode.Children))
        {
            if (child.LocalName != "table-row-group" || child.NamespaceUri != OdfNamespaces.Table)
                continue;
            OdfNode? insertAfter = child;
            foreach (var row in new List<OdfNode>(child.Children))
            {
                if (row.LocalName != "table-row" || row.NamespaceUri != OdfNamespaces.Table)
                    continue;
                child.RemoveChild(row);
                context.TableNode.InsertAfter(row, insertAfter);
                insertAfter = row;
            }
            context.TableNode.RemoveChild(child);
        }
    }

    /// <summary>
    /// 將指定欄範圍設為可展開/收合的群組。
    /// </summary>
    internal static void GroupColumns(OdfTableSheetMutationContext context, int startCol, int endCol, bool collapsed)
    {
        var colsToWrap = new List<OdfNode>();
        for (int c = startCol; c <= endCol; c++)
            colsToWrap.Add(context.GetOrCreateColumn(c));

        var groupNode = new OdfNode(OdfNodeType.Element, "table-column-group", OdfNamespaces.Table, "table");
        groupNode.SetAttribute("display", OdfNamespaces.Table, collapsed ? "false" : "true", "table");

        OdfNode firstCol = colsToWrap[0];
        context.TableNode.InsertBefore(groupNode, firstCol);
        foreach (var colNode in colsToWrap)
        {
            context.TableNode.RemoveChild(colNode);
            groupNode.AppendChild(colNode);
        }
    }

    /// <summary>
    /// 移除包含指定欄範圍的群組，將欄移回工作表主體。
    /// </summary>
    internal static void UngroupColumns(OdfTableSheetMutationContext context, int startCol, int endCol)
    {
        foreach (var child in new List<OdfNode>(context.TableNode.Children))
        {
            if (child.LocalName != "table-column-group" || child.NamespaceUri != OdfNamespaces.Table)
                continue;
            OdfNode? insertAfter = child;
            foreach (var col in new List<OdfNode>(child.Children))
            {
                if (col.LocalName != "table-column" || col.NamespaceUri != OdfNamespaces.Table)
                    continue;
                child.RemoveChild(col);
                context.TableNode.InsertAfter(col, insertAfter);
                insertAfter = col;
            }
            context.TableNode.RemoveChild(child);
        }
    }
}
