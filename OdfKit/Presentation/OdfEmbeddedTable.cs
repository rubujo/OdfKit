using System.Globalization;
using System;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Presentation;

/// <summary>
/// Represents a table embedded inside a drawing frame.
/// 表示嵌入在圖形框架內的表格。
/// </summary>
public sealed class OdfEmbeddedTable
{
    private readonly OdfNode _tableNode;
    private readonly OdfDocument _document;

    internal OdfEmbeddedTable(OdfNode tableNode, OdfDocument document)
    {
        _tableNode = tableNode ?? throw new ArgumentNullException(nameof(tableNode));
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// Gets or sets the table template or theme style name.
    /// 取得或設定表格範本／主題樣式名稱。
    /// </summary>
    public string? TemplateName
    {
        get => _tableNode.GetAttribute("template-name", OdfNamespaces.Table);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                _tableNode.RemoveAttribute("template-name", OdfNamespaces.Table);
            }
            else
            {
                _tableNode.SetAttribute("template-name", OdfNamespaces.Table, value!, "table");
            }
        }
    }

    /// <summary>
    /// Sets the table template or theme style name and returns this embedded table for chaining.
    /// 設定表格範本／主題樣式名稱，並傳回目前嵌入表格以便鏈式呼叫。
    /// </summary>
    /// <param name="templateName">The table template name; <see langword="null"/> or whitespace clears it. / 表格範本名稱；傳入 <see langword="null"/> 或空白會清除。</param>
    /// <returns>The current embedded table. / 目前嵌入表格。</returns>
    public OdfEmbeddedTable SetTemplateName(string? templateName)
    {
        TemplateName = templateName;
        return this;
    }

    /// <summary>
    /// Sets the text of the specified cell.
    /// 設定指定儲存格的文字。
    /// </summary>
    /// <param name="row">The zero-based row index. / 採 0 為基準的列索引。</param>
    /// <param name="column">The zero-based column index. / 採 0 為基準的欄索引。</param>
    /// <param name="text">The cell text. / 儲存格文字。</param>
    /// <returns>The current embedded table. / 目前嵌入表格。</returns>
    public OdfEmbeddedTable SetCellText(int row, int column, string text)
    {
        OdfNode cell = GetCell(row, column);
        if (cell.LocalName == "covered-table-cell")
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfEmbeddedTable_CannotSetTextCovered"));

        OdfNode paragraph = cell.Children.First(child => child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text);
        paragraph.TextContent = text;
        return this;
    }

    /// <summary>
    /// Sets common text styling for the specified cell.
    /// 設定指定儲存格文字的常用樣式。
    /// </summary>
    /// <param name="row">The zero-based row index. / 採 0 為基準的列索引。</param>
    /// <param name="column">The zero-based column index. / 採 0 為基準的欄索引。</param>
    /// <param name="bold">Whether text is bold; <see langword="null"/> leaves it unchanged. / 是否為粗體；傳入 <see langword="null"/> 表示不變更。</param>
    /// <param name="italic">Whether text is italic; <see langword="null"/> leaves it unchanged. / 是否為斜體；傳入 <see langword="null"/> 表示不變更。</param>
    /// <param name="underline">Whether text is underlined; <see langword="null"/> leaves it unchanged. / 是否加上底線；傳入 <see langword="null"/> 表示不變更。</param>
    /// <param name="strikethrough">Whether text is struck through; <see langword="null"/> leaves it unchanged. / 是否加上刪除線；傳入 <see langword="null"/> 表示不變更。</param>
    /// <param name="textPosition">The text position, such as <c>super</c> or <c>sub</c>; <see langword="null"/> leaves it unchanged. / 文字位置，例如 <c>super</c> 或 <c>sub</c>；傳入 <see langword="null"/> 表示不變更。</param>
    /// <param name="fontSize">The font size, such as <c>14pt</c>; <see langword="null"/> leaves it unchanged. / 字型大小，例如 <c>14pt</c>；傳入 <see langword="null"/> 表示不變更。</param>
    /// <param name="color">The text color, such as <c>#336699</c>; <see langword="null"/> leaves it unchanged. / 文字色彩，例如 <c>#336699</c>；傳入 <see langword="null"/> 表示不變更。</param>
    /// <returns>The current embedded table. / 目前嵌入表格。</returns>
    public OdfEmbeddedTable SetCellTextStyle(
        int row,
        int column,
        bool? bold = null,
        bool? italic = null,
        bool? underline = null,
        bool? strikethrough = null,
        string? textPosition = null,
        string? fontSize = null,
        string? color = null)
    {
        OdfNode cell = GetCell(row, column);
        if (cell.LocalName == "covered-table-cell")
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfEmbeddedTable_CannotSetTextStyle"));

        OdfNode paragraph = cell.Children.First(child => child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text);
        if (bold is not null)
        {
            _document.StyleEngine.SetLocalStyleProperty(paragraph, "paragraph", "text-properties", "font-weight", OdfNamespaces.Fo, bold.Value ? "bold" : "normal", "fo");
        }

        if (italic is not null)
        {
            _document.StyleEngine.SetLocalStyleProperty(paragraph, "paragraph", "text-properties", "font-style", OdfNamespaces.Fo, italic.Value ? "italic" : "normal", "fo");
        }

        if (underline is not null)
        {
            _document.StyleEngine.SetLocalStyleProperty(paragraph, "paragraph", "text-properties", "text-underline-style", OdfNamespaces.Style, underline.Value ? "solid" : "none", "style");
        }

        if (strikethrough is not null)
        {
            _document.StyleEngine.SetLocalStyleProperty(paragraph, "paragraph", "text-properties", "text-line-through-style", OdfNamespaces.Style, strikethrough.Value ? "solid" : "none", "style");
        }

        if (textPosition is not null)
        {
            _document.StyleEngine.SetLocalStyleProperty(paragraph, "paragraph", "text-properties", "text-position", OdfNamespaces.Style, textPosition, "style");
        }

        if (fontSize is not null)
        {
            _document.StyleEngine.SetLocalStyleProperty(paragraph, "paragraph", "text-properties", "font-size", OdfNamespaces.Fo, fontSize, "fo");
        }

        if (color is not null)
        {
            _document.StyleEngine.SetLocalStyleProperty(paragraph, "paragraph", "text-properties", "color", OdfNamespaces.Fo, color, "fo");
        }

        return this;
    }

    /// <summary>
    /// Sets the span of the specified cell and converts covered cells to <c>table:covered-table-cell</c>.
    /// 設定指定儲存格的合併範圍，並將被覆蓋的格子轉為 <c>table:covered-table-cell</c>。
    /// </summary>
    /// <param name="row">The zero-based starting row index. / 採 0 為基準的起始列索引。</param>
    /// <param name="column">The zero-based starting column index. / 採 0 為基準的起始欄索引。</param>
    /// <param name="rowSpan">The number of rows to span. / 列合併數。</param>
    /// <param name="columnSpan">The number of columns to span. / 欄合併數。</param>
    /// <returns>The current embedded table. / 目前嵌入表格。</returns>
    public OdfEmbeddedTable SetCellSpan(int row, int column, int rowSpan, int columnSpan)
    {
        if (rowSpan < 1)
            throw new ArgumentOutOfRangeException(nameof(rowSpan));
        if (columnSpan < 1)
            throw new ArgumentOutOfRangeException(nameof(columnSpan));

        OdfNode origin = GetCell(row, column);
        if (origin.LocalName == "covered-table-cell")
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfEmbeddedTable_CannotSpanCoveredTable"));

        if (rowSpan > 1)
        {
            origin.SetAttribute("number-rows-spanned", OdfNamespaces.Table, rowSpan.ToString(CultureInfo.InvariantCulture), "table");
        }
        else
        {
            origin.RemoveAttribute("number-rows-spanned", OdfNamespaces.Table);
        }

        if (columnSpan > 1)
        {
            origin.SetAttribute("number-columns-spanned", OdfNamespaces.Table, columnSpan.ToString(CultureInfo.InvariantCulture), "table");
        }
        else
        {
            origin.RemoveAttribute("number-columns-spanned", OdfNamespaces.Table);
        }

        for (int rowOffset = 0; rowOffset < rowSpan; rowOffset++)
        {
            for (int columnOffset = 0; columnOffset < columnSpan; columnOffset++)
            {
                if (rowOffset == 0 && columnOffset == 0)
                {
                    continue;
                }

                ReplaceCell(row + rowOffset, column + columnOffset, CreateCoveredCell());
            }
        }

        return this;
    }

    /// <summary>
    /// Sets the background color of the specified cell.
    /// 設定指定儲存格的背景色。
    /// </summary>
    /// <param name="row">The zero-based row index. / 採 0 為基準的列索引。</param>
    /// <param name="column">The zero-based column index. / 採 0 為基準的欄索引。</param>
    /// <param name="color">The background color, such as <c>#FFFF00</c>; <see langword="null"/> or an empty string clears the style value. / 背景色，例如 <c>#FFFF00</c>；傳入 <see langword="null"/> 或空字串會清除樣式值。</param>
    /// <returns>The current embedded table. / 目前嵌入表格。</returns>
    public OdfEmbeddedTable SetCellBackgroundColor(int row, int column, string? color)
    {
        OdfNode cell = GetCell(row, column);
        if (cell.LocalName == "covered-table-cell")
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfEmbeddedTable_CannotSetStyleCovered_6"));

        _document.StyleEngine.SetLocalStyleProperty(
            cell,
            "table-cell",
            "table-cell-properties",
            "background-color",
            OdfNamespaces.Fo,
            color ?? string.Empty,
            "fo");
        return this;
    }

    /// <summary>
    /// Sets the shared border for all sides of the specified cell.
    /// 設定指定儲存格四邊共用的框線。
    /// </summary>
    /// <param name="row">The zero-based row index. / 採 0 為基準的列索引。</param>
    /// <param name="column">The zero-based column index. / 採 0 為基準的欄索引。</param>
    /// <param name="border">The border value, such as <c>0.75pt solid #336699</c>; <see langword="null"/> or an empty string clears the style value. / 框線值，例如 <c>0.75pt solid #336699</c>；傳入 <see langword="null"/> 或空字串會清除樣式值。</param>
    /// <returns>The current embedded table. / 目前嵌入表格。</returns>
    public OdfEmbeddedTable SetCellBorder(int row, int column, string? border)
    {
        OdfNode cell = GetCell(row, column);
        if (cell.LocalName == "covered-table-cell")
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfEmbeddedTable_CannotSetStyleCovered_6"));

        _document.StyleEngine.SetLocalStyleProperty(
            cell,
            "table-cell",
            "table-cell-properties",
            "border",
            OdfNamespaces.Fo,
            border ?? string.Empty,
            "fo");
        return this;
    }

    /// <summary>
    /// Sets the left border of the specified cell.
    /// 設定指定儲存格的左框線。
    /// </summary>
    /// <param name="row">The zero-based row index. / 採 0 為基準的列索引。</param>
    /// <param name="column">The zero-based column index. / 採 0 為基準的欄索引。</param>
    /// <param name="border">The border value, such as <c>0.75pt solid #336699</c>; <see langword="null"/> or an empty string clears the style value. / 框線值，例如 <c>0.75pt solid #336699</c>；傳入 <see langword="null"/> 或空字串會清除樣式值。</param>
    /// <returns>The current embedded table. / 目前嵌入表格。</returns>
    public OdfEmbeddedTable SetCellBorderLeft(int row, int column, string? border)
    {
        OdfNode cell = GetCell(row, column);
        if (cell.LocalName == "covered-table-cell")
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfEmbeddedTable_CannotSetStyleCovered_6"));

        _document.StyleEngine.SetLocalStyleProperty(
            cell,
            "table-cell",
            "table-cell-properties",
            "border-left",
            OdfNamespaces.Fo,
            border ?? string.Empty,
            "fo");
        return this;
    }

    /// <summary>
    /// Sets the right border of the specified cell.
    /// 設定指定儲存格的右框線。
    /// </summary>
    /// <param name="row">The zero-based row index. / 採 0 為基準的列索引。</param>
    /// <param name="column">The zero-based column index. / 採 0 為基準的欄索引。</param>
    /// <param name="border">The border value, such as <c>0.75pt solid #336699</c>; <see langword="null"/> or an empty string clears the style value. / 框線值，例如 <c>0.75pt solid #336699</c>；傳入 <see langword="null"/> 或空字串會清除樣式值。</param>
    /// <returns>The current embedded table. / 目前嵌入表格。</returns>
    public OdfEmbeddedTable SetCellBorderRight(int row, int column, string? border)
    {
        OdfNode cell = GetCell(row, column);
        if (cell.LocalName == "covered-table-cell")
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfEmbeddedTable_CannotSetStyleCovered_6"));

        _document.StyleEngine.SetLocalStyleProperty(
            cell,
            "table-cell",
            "table-cell-properties",
            "border-right",
            OdfNamespaces.Fo,
            border ?? string.Empty,
            "fo");
        return this;
    }

    /// <summary>
    /// Sets the top border of the specified cell.
    /// 設定指定儲存格的上框線。
    /// </summary>
    /// <param name="row">The zero-based row index. / 採 0 為基準的列索引。</param>
    /// <param name="column">The zero-based column index. / 採 0 為基準的欄索引。</param>
    /// <param name="border">The border value, such as <c>0.75pt solid #336699</c>; <see langword="null"/> or an empty string clears the style value. / 框線值，例如 <c>0.75pt solid #336699</c>；傳入 <see langword="null"/> 或空字串會清除樣式值。</param>
    /// <returns>The current embedded table. / 目前嵌入表格。</returns>
    public OdfEmbeddedTable SetCellBorderTop(int row, int column, string? border)
    {
        OdfNode cell = GetCell(row, column);
        if (cell.LocalName == "covered-table-cell")
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfEmbeddedTable_CannotSetStyleCovered_6"));

        _document.StyleEngine.SetLocalStyleProperty(
            cell,
            "table-cell",
            "table-cell-properties",
            "border-top",
            OdfNamespaces.Fo,
            border ?? string.Empty,
            "fo");
        return this;
    }

    /// <summary>
    /// Sets the bottom border of the specified cell.
    /// 設定指定儲存格的下框線。
    /// </summary>
    /// <param name="row">The zero-based row index. / 採 0 為基準的列索引。</param>
    /// <param name="column">The zero-based column index. / 採 0 為基準的欄索引。</param>
    /// <param name="border">The border value, such as <c>0.75pt solid #336699</c>; <see langword="null"/> or an empty string clears the style value. / 框線值，例如 <c>0.75pt solid #336699</c>；傳入 <see langword="null"/> 或空字串會清除樣式值。</param>
    /// <returns>The current embedded table. / 目前嵌入表格。</returns>
    public OdfEmbeddedTable SetCellBorderBottom(int row, int column, string? border)
    {
        OdfNode cell = GetCell(row, column);
        if (cell.LocalName == "covered-table-cell")
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfEmbeddedTable_CannotSetStyleCovered_6"));

        _document.StyleEngine.SetLocalStyleProperty(
            cell,
            "table-cell",
            "table-cell-properties",
            "border-bottom",
            OdfNamespaces.Fo,
            border ?? string.Empty,
            "fo");
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

    private void ReplaceCell(int row, int column, OdfNode replacement)
    {
        OdfNode current = GetCell(row, column);
        OdfNode rowNode = current.Parent!;
        int index = current.SiblingIndex;
        rowNode.Children.RemoveAt(index);
        rowNode.Children.Insert(index, replacement);
    }

    private static OdfNode CreateCoveredCell()
        => new(OdfNodeType.Element, "covered-table-cell", OdfNamespaces.Table, "table");
}
