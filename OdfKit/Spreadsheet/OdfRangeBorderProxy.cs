using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 提供試算表儲存格範圍的框線鏈式設定 API。
/// </summary>
public sealed class OdfRangeBorderProxy
{
    private readonly OdfTableSheet _sheet;
    private readonly OdfCellRange _range;

    internal OdfRangeBorderProxy(OdfTableSheet sheet, OdfCellRange range)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        _range = range;
    }

    /// <summary>
    /// 對範圍內每個儲存格套用四面框線。
    /// </summary>
    /// <param name="border">要套用的框線</param>
    /// <returns>目前框線代理，方便鏈式呼叫</returns>
    public OdfRangeBorderProxy SetAll(OdfBorder border)
    {
        foreach (OdfCell cell in EnumerateCells())
        {
            SetCellBorders(cell, border, border, border, border);
        }

        return this;
    }

    /// <summary>
    /// 只對範圍外側邊界套用框線。
    /// </summary>
    /// <param name="border">要套用的框線</param>
    /// <returns>目前框線代理，方便鏈式呼叫</returns>
    public OdfRangeBorderProxy SetOuter(OdfBorder border)
    {
        GetBounds(out int minRow, out int maxRow, out int minColumn, out int maxColumn);
        foreach (OdfCell cell in EnumerateCells())
        {
            OdfBorder? top = cell.Row == minRow ? border : null;
            OdfBorder? bottom = cell.Row == maxRow ? border : null;
            OdfBorder? left = cell.Column == minColumn ? border : null;
            OdfBorder? right = cell.Column == maxColumn ? border : null;
            SetCellBorders(cell, top, bottom, left, right);
        }

        return this;
    }

    /// <summary>
    /// 只對範圍內部格線套用框線。
    /// </summary>
    /// <param name="border">要套用的框線</param>
    /// <returns>目前框線代理，方便鏈式呼叫</returns>
    public OdfRangeBorderProxy SetInner(OdfBorder border)
    {
        GetBounds(out int minRow, out int maxRow, out int minColumn, out int maxColumn);
        foreach (OdfCell cell in EnumerateCells())
        {
            OdfBorder? top = cell.Row > minRow ? border : null;
            OdfBorder? bottom = cell.Row < maxRow ? border : null;
            OdfBorder? left = cell.Column > minColumn ? border : null;
            OdfBorder? right = cell.Column < maxColumn ? border : null;
            SetCellBorders(cell, top, bottom, left, right);
        }

        return this;
    }

    /// <summary>
    /// 對範圍外側與內部格線套用框線。
    /// </summary>
    /// <param name="outer">外側邊界框線</param>
    /// <param name="inner">內部格線框線</param>
    /// <returns>目前框線代理，方便鏈式呼叫</returns>
    public OdfRangeBorderProxy SetGrid(OdfBorder outer, OdfBorder inner)
    {
        SetOuter(outer);
        SetInner(inner);
        return this;
    }

    private IEnumerable<OdfCell> EnumerateCells()
    {
        GetBounds(out int minRow, out int maxRow, out int minColumn, out int maxColumn);
        for (int row = minRow; row <= maxRow; row++)
        {
            for (int column = minColumn; column <= maxColumn; column++)
            {
                yield return _sheet.GetCell(row, column);
            }
        }
    }

    private void GetBounds(out int minRow, out int maxRow, out int minColumn, out int maxColumn)
    {
        minRow = Math.Min(_range.StartAddress.Row, _range.EndAddress.Row);
        maxRow = Math.Max(_range.StartAddress.Row, _range.EndAddress.Row);
        minColumn = Math.Min(_range.StartAddress.Column, _range.EndAddress.Column);
        maxColumn = Math.Max(_range.StartAddress.Column, _range.EndAddress.Column);
    }

    private static void SetCellBorders(OdfCell cell, OdfBorder? top, OdfBorder? bottom, OdfBorder? left, OdfBorder? right)
    {
        SetBorder(cell, "border-top", top);
        SetBorder(cell, "border-bottom", bottom);
        SetBorder(cell, "border-left", left);
        SetBorder(cell, "border-right", right);
    }

    private static void SetBorder(OdfCell cell, string propertyName, OdfBorder? border)
    {
        if (!border.HasValue)
        {
            return;
        }

        cell.Document.StyleEngine.SetLocalStyleProperty(
            cell.Node,
            "table-cell",
            "table-cell-properties",
            propertyName,
            OdfNamespaces.Fo,
            border.Value.ToString(),
            "fo");
    }
}
