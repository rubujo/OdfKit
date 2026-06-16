using System;
using System.Collections;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region Layout

    /// <summary>
    /// 自動調整指定欄的寬度，根據內容長度來適配。
    /// </summary>
    /// <param name="col">以 0 為基準的欄索引</param>
    public void AutoFitColumnWidth(int col)
    {
        var values = new List<string>();
        var rows = GetRowsList();
        foreach (var rowNode in rows)
        {
            var cells = GetCellsInRow(rowNode);
            if (col < cells.Count)
            {
                values.Add(cells[col].TextContent);
            }
        }

        var optimalWidth = CalculateOptimalColumnWidth(values);
        SetColumnWidth(col, optimalWidth);
    }

    /// <summary>
    /// 設定指定欄的寬度。
    /// </summary>
    /// <param name="col">以 0 為基準的欄索引</param>
    /// <param name="width">欄寬度</param>
    public void SetColumnWidth(int col, OdfLength width)
    {
        var colNode = GetOrCreateColumnNode(col);
        _doc.StyleEngine.GetOrCreateLocalStyle(colNode, "table-column").GetAttribute("name", OdfNamespaces.Style);
        _doc.StyleEngine.SetLocalStyleProperty(colNode, "table-column", "table-column-properties", "column-width", OdfNamespaces.Style, width.ToString(), "style");
    }

    /// <summary>
    /// 設定指定列是否啟用最佳自動列高 (AutoHeight)。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引</param>
    /// <param name="useOptimal">是否啟用</param>
    public void SetRowOptimalHeight(int row, bool useOptimal)
    {
        var rowNode = GetOrCreateRowNode(row);

        // 互斥防禦：如果設為 true，我們必須清除固定高度 (style:row-height)
        if (useOptimal)
        {
            _doc.StyleEngine.SetLocalStyleProperty(rowNode, "table-row", "table-row-properties", "row-height", OdfNamespaces.Style, null, propAttrPrefix: null, deferSave: true);
        }

        _doc.StyleEngine.SetLocalStyleProperty(rowNode, "table-row", "table-row-properties", "use-optimal-row-height", OdfNamespaces.Style, useOptimal ? "true" : "false", "style");
    }

    /// <summary>
    /// 判斷指定列是否啟用最佳自動列高。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引</param>
    /// <returns>是否啟用</returns>
    public bool IsRowOptimalHeight(int row)
    {
        var rowNode = GetOrCreateRowNodeInternal(row, forWrite: false);
        string? styleName = rowNode.GetAttribute("style-name", OdfNamespaces.Table);
        if (styleName is null || styleName == "")
            return false;

        string? val = _doc.StyleEngine.GetStyleProperty(styleName, "use-optimal-row-height", OdfNamespaces.Style, "table-row");
        return val == "true";
    }

    /// <summary>
    /// 設定指定列的固定高度。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引</param>
    /// <param name="height">列高度</param>
    public void SetRowHeight(int row, OdfLength? height)
    {
        var rowNode = GetOrCreateRowNode(row);

        // 互斥防禦：當手動設定固定列高時，必須將最佳高度設為 false (或移除)
        if (height != null)
        {
            _doc.StyleEngine.SetLocalStyleProperty(rowNode, "table-row", "table-row-properties", "use-optimal-row-height", OdfNamespaces.Style, "false", "style", deferSave: true);
        }
        _doc.StyleEngine.SetLocalStyleProperty(rowNode, "table-row", "table-row-properties", "row-height", OdfNamespaces.Style, height?.ToString(), "style");
    }

    /// <summary>
    /// 取得指定列的固定高度。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引</param>
    /// <returns>列高度，若未設定則為 null</returns>
    public OdfLength? GetRowHeight(int row)
    {
        var rowNode = GetOrCreateRowNodeInternal(row, forWrite: false);
        string? styleName = rowNode.GetAttribute("style-name", OdfNamespaces.Table);
        if (styleName is null || styleName == "")
            return null;

        string? val = _doc.StyleEngine.GetStyleProperty(styleName, "row-height", OdfNamespaces.Style, "table-row");
        if (string.IsNullOrEmpty(val))
            return null;
        return OdfLength.Parse(val);
    }

    private OdfLength CalculateOptimalColumnWidth(IEnumerable<string> cellValues, double fontSizePt = 10)
    {
        double maxWeight = 0;
        foreach (var value in cellValues)
        {
            double weight = 0;
            foreach (char c in value)
            {
                weight += (c <= 127) ? 1.0 : 1.85;
            }
            if (weight > maxWeight)
                maxWeight = weight;
        }

        double totalChars = maxWeight + 1.5;
        double widthInCm = totalChars * (fontSizePt / 10.0) * 0.22;
        return OdfLength.FromCentimeters(Math.Max(widthInCm, 1.0));
    }


    #endregion
}
