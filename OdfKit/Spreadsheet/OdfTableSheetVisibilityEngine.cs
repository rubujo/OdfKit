using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 工作表列欄可見性引擎（內部協作者）。
/// </summary>
internal static class OdfTableSheetVisibilityEngine
{
    internal static void SetRowVisible(OdfTableSheetMutationContext context, int row, bool visible)
    {
        var rowNode = context.GetOrCreateRow(row, forWrite: true);
        rowNode.SetAttribute("visibility", OdfNamespaces.Table, visible ? "visible" : "collapse", "table");
    }

    internal static void SetColumnVisible(OdfTableSheetMutationContext context, int col, bool visible)
    {
        var colNode = context.GetOrCreateColumn(col);
        colNode.SetAttribute("visibility", OdfNamespaces.Table, visible ? "visible" : "collapse", "table");
    }

    internal static bool IsRowVisible(OdfTableSheetMutationContext context, int row)
    {
        int currentRowIndex = 0;
        foreach (var child in context.TableNode.Children)
        {
            if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
            {
                int repeatedCount = 1;
                string? repStr = child.GetAttribute("number-rows-repeated", OdfNamespaces.Table);
                if (!string.IsNullOrEmpty(repStr) && int.TryParse(repStr, out int rc))
                    repeatedCount = rc;

                if (row >= currentRowIndex && row < currentRowIndex + repeatedCount)
                    return child.GetAttribute("visibility", OdfNamespaces.Table) != "collapse";
                currentRowIndex += repeatedCount;
            }
        }
        return true;
    }

    internal static bool IsColumnVisible(OdfTableSheetMutationContext context, int col)
    {
        int currentColIndex = 0;
        foreach (var child in context.TableNode.Children)
        {
            if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
            {
                int repeatedCount = 1;
                string? repStr = child.GetAttribute("number-columns-repeated", OdfNamespaces.Table);
                if (!string.IsNullOrEmpty(repStr) && int.TryParse(repStr, out int rc))
                    repeatedCount = rc;

                if (col >= currentColIndex && col < currentColIndex + repeatedCount)
                    return child.GetAttribute("visibility", OdfNamespaces.Table) != "collapse";
                currentColIndex += repeatedCount;
            }
        }
        return true;
    }
}
