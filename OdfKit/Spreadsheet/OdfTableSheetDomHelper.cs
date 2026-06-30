using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 工作表 DOM 節點查詢與建立輔助工具（內部協作者）。
/// </summary>
internal static class OdfTableSheetDomHelper
{
    /// <summary>
    /// 尋找直接子元素節點。
    /// </summary>
    internal static OdfNode? FindChildElement(OdfNode parent, string localName, string ns)
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

    /// <summary>
    /// 尋找或建立直接子元素節點。
    /// </summary>
    internal static OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
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

    /// <summary>
    /// 尋找或建立工作表中的 table:shapes，並維持 ODF table:table 內容順序。
    /// </summary>
    internal static OdfNode FindOrCreateTableShapes(OdfNode tableNode)
    {
        foreach (OdfNode child in tableNode.Children)
        {
            if (child.LocalName == "shapes" && child.NamespaceUri == OdfNamespaces.Table)
                return child;
        }

        var shapesNode = new OdfNode(OdfNodeType.Element, "shapes", OdfNamespaces.Table, "table");
        foreach (OdfNode child in tableNode.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Table &&
                child.LocalName is "table-column" or "table-columns" or "table-column-group" or
                    "table-row" or "table-rows" or "table-row-group" or "named-expressions")
            {
                tableNode.InsertBefore(shapesNode, child);
                return shapesNode;
            }
        }

        tableNode.AppendChild(shapesNode);
        return shapesNode;
    }

    /// <summary>
    /// 將工作表插入在 workbook epilogue 節點之前，避免後續新增工作表破壞 office:spreadsheet 順序。
    /// </summary>
    internal static void InsertSpreadsheetTable(OdfNode spreadsheetRoot, OdfNode tableNode)
    {
        foreach (OdfNode child in spreadsheetRoot.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Table &&
                child.LocalName is "named-expressions" or "database-ranges" or "data-pilot-tables" or
                    "consolidation" or "dde-links")
            {
                spreadsheetRoot.InsertBefore(tableNode, child);
                return;
            }
        }

        spreadsheetRoot.AppendChild(tableNode);
    }

    /// <summary>
    /// 尋找或建立 office:spreadsheet 層級的 prelude 節點。
    /// </summary>
    internal static OdfNode FindOrCreateSpreadsheetPreludeChild(
        OdfNode spreadsheetRoot,
        string localName,
        string ns,
        string prefix)
    {
        foreach (OdfNode child in spreadsheetRoot.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
                return child;
        }

        var node = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
        foreach (OdfNode child in spreadsheetRoot.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Table &&
                child.LocalName is "content-validations" or "label-ranges" or "table" or
                    "named-expressions" or "database-ranges" or "data-pilot-tables" or
                    "consolidation" or "dde-links")
            {
                spreadsheetRoot.InsertBefore(node, child);
                return node;
            }
        }

        spreadsheetRoot.AppendChild(node);
        return node;
    }

    /// <summary>
    /// 解析非負整數；無效或非正數時回傳 0。
    /// </summary>
    internal static int ParseNonNegativeInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) && result > 0
            ? result
            : 0;
    }
}
