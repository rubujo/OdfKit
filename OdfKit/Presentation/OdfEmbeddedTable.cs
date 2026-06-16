using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Presentation;

/// <summary>
/// 表示嵌入在圖形框架內的表格。
/// </summary>
public sealed class OdfEmbeddedTable
{
    private readonly OdfNode _tableNode;

    internal OdfEmbeddedTable(OdfNode tableNode)
    {
        _tableNode = tableNode ?? throw new ArgumentNullException(nameof(tableNode));
    }

    /// <summary>
    /// 設定指定儲存格的文字。
    /// </summary>
    /// <param name="row">列索引，採 0 為基準。</param>
    /// <param name="column">欄索引，採 0 為基準。</param>
    /// <param name="text">儲存格文字。</param>
    /// <returns>目前嵌入表格。</returns>
    public OdfEmbeddedTable SetCellText(int row, int column, string text)
    {
        OdfNode cell = GetCell(row, column);
        OdfNode paragraph = cell.Children.First(child => child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text);
        paragraph.TextContent = text;
        return this;
    }

    private OdfNode GetCell(int row, int column)
    {
        if (row < 0)
            throw new ArgumentOutOfRangeException(nameof(row));
        if (column < 0)
            throw new ArgumentOutOfRangeException(nameof(column));
        if (row >= _tableNode.Children.Count)
            throw new ArgumentOutOfRangeException(nameof(row));

        OdfNode rowNode = _tableNode.Children[row];
        if (column >= rowNode.Children.Count)
            throw new ArgumentOutOfRangeException(nameof(column));
        return rowNode.Children[column];
    }
}
