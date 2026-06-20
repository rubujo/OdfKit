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
    private readonly OdfDocument _document;

    internal OdfEmbeddedTable(OdfNode tableNode, OdfDocument document)
    {
        _tableNode = tableNode ?? throw new ArgumentNullException(nameof(tableNode));
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
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
    /// 設定表格範本／主題樣式名稱，並傳回目前嵌入表格以便鏈式呼叫。
    /// </summary>
    /// <param name="templateName">表格範本名稱；傳入 null 或空白會清除。</param>
    /// <returns>目前嵌入表格。</returns>
    public OdfEmbeddedTable SetTemplateName(string? templateName)
    {
        TemplateName = templateName;
        return this;
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
        if (cell.LocalName == "covered-table-cell")
            throw new InvalidOperationException("Cannot set text on a covered table cell.");

        OdfNode paragraph = cell.Children.First(child => child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text);
        paragraph.TextContent = text;
        return this;
    }

    /// <summary>
    /// 設定指定儲存格文字的常用樣式。
    /// </summary>
    /// <param name="row">列索引，採 0 為基準。</param>
    /// <param name="column">欄索引，採 0 為基準。</param>
    /// <param name="bold">是否為粗體；傳入 <see langword="null"/> 表示不變更。</param>
    /// <param name="italic">是否為斜體；傳入 <see langword="null"/> 表示不變更。</param>
    /// <param name="underline">是否加上底線；傳入 <see langword="null"/> 表示不變更。</param>
    /// <param name="strikethrough">是否加上刪除線；傳入 <see langword="null"/> 表示不變更。</param>
    /// <param name="textPosition">文字位置，例如 <c>super</c> 或 <c>sub</c>；傳入 <see langword="null"/> 表示不變更。</param>
    /// <param name="fontSize">字型大小，例如 <c>14pt</c>；傳入 <see langword="null"/> 表示不變更。</param>
    /// <param name="color">文字色彩，例如 <c>#336699</c>；傳入 <see langword="null"/> 表示不變更。</param>
    /// <returns>目前嵌入表格。</returns>
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
            throw new InvalidOperationException("Cannot set text style on a covered table cell.");

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
    /// 設定指定儲存格的合併範圍，並將被覆蓋的格子轉為 <c>table:covered-table-cell</c>。
    /// </summary>
    /// <param name="row">起始列索引，採 0 為基準。</param>
    /// <param name="column">起始欄索引，採 0 為基準。</param>
    /// <param name="rowSpan">列合併數。</param>
    /// <param name="columnSpan">欄合併數。</param>
    /// <returns>目前嵌入表格。</returns>
    public OdfEmbeddedTable SetCellSpan(int row, int column, int rowSpan, int columnSpan)
    {
        if (rowSpan < 1)
            throw new ArgumentOutOfRangeException(nameof(rowSpan));
        if (columnSpan < 1)
            throw new ArgumentOutOfRangeException(nameof(columnSpan));

        OdfNode origin = GetCell(row, column);
        if (origin.LocalName == "covered-table-cell")
            throw new InvalidOperationException("Cannot span from a covered table cell.");

        if (rowSpan > 1)
        {
            origin.SetAttribute("number-rows-spanned", OdfNamespaces.Table, rowSpan.ToString(System.Globalization.CultureInfo.InvariantCulture), "table");
        }
        else
        {
            origin.RemoveAttribute("number-rows-spanned", OdfNamespaces.Table);
        }

        if (columnSpan > 1)
        {
            origin.SetAttribute("number-columns-spanned", OdfNamespaces.Table, columnSpan.ToString(System.Globalization.CultureInfo.InvariantCulture), "table");
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
    /// 設定指定儲存格的背景色。
    /// </summary>
    /// <param name="row">列索引，採 0 為基準。</param>
    /// <param name="column">欄索引，採 0 為基準。</param>
    /// <param name="color">背景色，例如 <c>#FFFF00</c>；傳入 <see langword="null"/> 或空字串會清除樣式值。</param>
    /// <returns>目前嵌入表格。</returns>
    public OdfEmbeddedTable SetCellBackgroundColor(int row, int column, string? color)
    {
        OdfNode cell = GetCell(row, column);
        if (cell.LocalName == "covered-table-cell")
            throw new InvalidOperationException("Cannot set style on a covered table cell.");

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
    /// 設定指定儲存格四邊共用的框線。
    /// </summary>
    /// <param name="row">列索引，採 0 為基準。</param>
    /// <param name="column">欄索引，採 0 為基準。</param>
    /// <param name="border">框線值，例如 <c>0.75pt solid #336699</c>；傳入 <see langword="null"/> 或空字串會清除樣式值。</param>
    /// <returns>目前嵌入表格。</returns>
    public OdfEmbeddedTable SetCellBorder(int row, int column, string? border)
    {
        OdfNode cell = GetCell(row, column);
        if (cell.LocalName == "covered-table-cell")
            throw new InvalidOperationException("Cannot set style on a covered table cell.");

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
    /// 設定指定儲存格的左框線。
    /// </summary>
    /// <param name="row">列索引，採 0 為基準。</param>
    /// <param name="column">欄索引，採 0 為基準。</param>
    /// <param name="border">框線值，例如 <c>0.75pt solid #336699</c>；傳入 <see langword="null"/> 或空字串會清除樣式值。</param>
    /// <returns>目前嵌入表格。</returns>
    public OdfEmbeddedTable SetCellBorderLeft(int row, int column, string? border)
    {
        OdfNode cell = GetCell(row, column);
        if (cell.LocalName == "covered-table-cell")
            throw new InvalidOperationException("Cannot set style on a covered table cell.");

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
    /// 設定指定儲存格的右框線。
    /// </summary>
    /// <param name="row">列索引，採 0 為基準。</param>
    /// <param name="column">欄索引，採 0 為基準。</param>
    /// <param name="border">框線值，例如 <c>0.75pt solid #336699</c>；傳入 <see langword="null"/> 或空字串會清除樣式值。</param>
    /// <returns>目前嵌入表格。</returns>
    public OdfEmbeddedTable SetCellBorderRight(int row, int column, string? border)
    {
        OdfNode cell = GetCell(row, column);
        if (cell.LocalName == "covered-table-cell")
            throw new InvalidOperationException("Cannot set style on a covered table cell.");

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
    /// 設定指定儲存格的上框線。
    /// </summary>
    /// <param name="row">列索引，採 0 為基準。</param>
    /// <param name="column">欄索引，採 0 為基準。</param>
    /// <param name="border">框線值，例如 <c>0.75pt solid #336699</c>；傳入 <see langword="null"/> 或空字串會清除樣式值。</param>
    /// <returns>目前嵌入表格。</returns>
    public OdfEmbeddedTable SetCellBorderTop(int row, int column, string? border)
    {
        OdfNode cell = GetCell(row, column);
        if (cell.LocalName == "covered-table-cell")
            throw new InvalidOperationException("Cannot set style on a covered table cell.");

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
    /// 設定指定儲存格的下框線。
    /// </summary>
    /// <param name="row">列索引，採 0 為基準。</param>
    /// <param name="column">欄索引，採 0 為基準。</param>
    /// <param name="border">框線值，例如 <c>0.75pt solid #336699</c>；傳入 <see langword="null"/> 或空字串會清除樣式值。</param>
    /// <returns>目前嵌入表格。</returns>
    public OdfEmbeddedTable SetCellBorderBottom(int row, int column, string? border)
    {
        OdfNode cell = GetCell(row, column);
        if (cell.LocalName == "covered-table-cell")
            throw new InvalidOperationException("Cannot set style on a covered table cell.");

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
