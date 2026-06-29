using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Represents odf text table info.
/// 表示文字文件中的表格摘要。
/// </summary>
public sealed class OdfTextTableInfo
{
    private OdfTextTableInfo(string? name, int rowCount, int columnCount)
    {
        Name = name;
        RowCount = rowCount;
        ColumnCount = columnCount;
    }

    /// <summary>
    /// Gets name.
    /// 取得表格名稱。
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets row count.
    /// 取得表格列數。
    /// </summary>
    public int RowCount { get; }

    /// <summary>
    /// Gets column count.
    /// 取得表格最大欄數。
    /// </summary>
    public int ColumnCount { get; }

    internal static OdfTextTableInfo FromNode(OdfNode tableNode)
    {
        int rowCount = 0;
        int columnCount = 0;
        foreach (OdfNode row in tableNode.Children)
        {
            if (row.NodeType is not OdfNodeType.Element ||
                row.LocalName != "table-row" ||
                row.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }

            rowCount++;
            int cells = 0;
            foreach (OdfNode cell in row.Children)
            {
                if (cell.NodeType is OdfNodeType.Element &&
                    (cell.LocalName == "table-cell" || cell.LocalName == "covered-table-cell") &&
                    cell.NamespaceUri == OdfNamespaces.Table)
                {
                    cells++;
                }
            }

            columnCount = Math.Max(columnCount, cells);
        }

        return new OdfTextTableInfo(tableNode.GetAttribute("name", OdfNamespaces.Table), rowCount, columnCount);
    }
}
