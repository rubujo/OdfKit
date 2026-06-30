using System;
using System.Collections.Generic;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;

using OdfKit.Compliance;
namespace OdfKit.Chart;

public partial class OdfChartDocument
{
    #region Data Range Binding

    /// <summary>
    /// Binds the chart data source to a spreadsheet cell range.
    /// 將圖表資料來源綁定至試算表的儲存格範圍。
    /// </summary>
    /// <param name="sheetName">The sheet name. / 工作表名稱。</param>
    /// <param name="range">The cell range. / 儲存格範圍。</param>
    /// <param name="firstRowAsHeader">Whether the first row is treated as the series header; defaults to true. / 首列作為序列標題（header），預設 true。</param>
    /// <param name="firstColumnAsLabel">Whether the first column is treated as the category label (X-axis); defaults to true. / 首欄作為分類標籤（X 軸），預設 true。</param>
    public void SetDataRange(string sheetName, OdfKit.Spreadsheet.OdfCellRange range,
        bool firstRowAsHeader = true, bool firstColumnAsLabel = true)
    {
        if (string.IsNullOrEmpty(sheetName))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfChartDocument_WorksheetCannotBeEmpty"), nameof(sheetName));

        OdfNode chart = GetChartNode();

        // 1. 設定 <chart:chart table:cell-range-address="...">
        string fullRange = BuildAbsoluteRange(sheetName, range.StartAddress.Row, range.StartAddress.Column,
                                               range.EndAddress.Row, range.EndAddress.Column);
        chart.SetAttribute("cell-range-address", OdfNamespaces.Table, fullRange, "table");

        // 2. 清除現有 <chart:series>
        OdfNode plotArea = FindOrCreatePlotArea();
        plotArea.SetAttribute("data-source-has-labels", OdfNamespaces.Chart,
            GetDataSourceLabelToken(firstRowAsHeader, firstColumnAsLabel), "chart");
        var toRemove = new List<OdfNode>();
        foreach (var child in plotArea.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.LocalName == "series" &&
                child.NamespaceUri == OdfNamespaces.Chart)
                toRemove.Add(child);
        }
        foreach (var n in toRemove)
            plotArea.RemoveChild(n);

        int dataRowStart = firstRowAsHeader ? range.StartAddress.Row + 1 : range.StartAddress.Row;
        int dataColStart = firstColumnAsLabel ? range.StartAddress.Column + 1 : range.StartAddress.Column;

        // 3. 設定 X 軸分類範圍
        if (firstColumnAsLabel && dataRowStart <= range.EndAddress.Row)
        {
            OdfNode xAxis = FindOrCreateAxis("x");
            OdfNode? existingCat = FindChildElement(xAxis, "categories", OdfNamespaces.Chart);
            if (existingCat is not null)
                xAxis.RemoveChild(existingCat);

            string catRange = BuildAbsoluteRange(sheetName,
                dataRowStart, range.StartAddress.Column,
                range.EndAddress.Row, range.StartAddress.Column);
            OdfNode categories = OdfNodeFactory.CreateElement("categories", OdfNamespaces.Chart, "chart");
            categories.SetAttribute("cell-range-address", OdfNamespaces.Table, catRange, "table");
            xAxis.AppendChild(categories);
        }

        // 4. 為每個資料欄新增 <chart:series>
        for (int col = dataColStart; col <= range.EndAddress.Column; col++)
        {
            if (dataRowStart > range.EndAddress.Row)
                break;

            string dataRange = BuildAbsoluteRange(sheetName,
                dataRowStart, col, range.EndAddress.Row, col);

            OdfNode series = OdfNodeFactory.CreateElement("series", OdfNamespaces.Chart, "chart");
            series.SetAttribute("values-cell-range-address", OdfNamespaces.Chart, dataRange, "chart");
            series.SetAttribute("cell-range-address", OdfNamespaces.Table, dataRange, "table");

            if (firstRowAsHeader)
            {
                string labelAddr = BuildAbsoluteCell(sheetName, range.StartAddress.Row, col);
                series.SetAttribute("label-cell-address", OdfNamespaces.Chart, labelAddr, "chart");
            }

            plotArea.AppendChild(series);
        }
    }

    /// <summary>
    /// Gets the spreadsheet cell range currently bound to the chart.
    /// 取得圖表目前綁定的試算表儲存格範圍。
    /// </summary>
    /// <returns>A tuple of the sheet name and cell range; both are null if not set. / 工作表名稱與儲存格範圍的元組；若未設定則兩者均為 null。</returns>
    public (string? SheetName, OdfKit.Spreadsheet.OdfCellRange? Range) GetDataRange()
    {
        string? addr = ChartNode.GetAttribute("cell-range-address", OdfNamespaces.Table);
        if (string.IsNullOrEmpty(addr))
            return (null, null);

        string s = addr!.Trim();
        if (s.StartsWith("[", StringComparison.Ordinal))
            s = s.Substring(1);
        if (s.EndsWith("]", StringComparison.Ordinal))
            s = s.Substring(0, s.Length - 1);

        int colon = s.IndexOf(':');
        if (colon < 0)
            return (null, null);

        string startPart = s.Substring(0, colon);
        string endPart = s.Substring(colon + 1);

        if (!TryParseOdfCell(startPart, out string? sheetName, out int startRow, out int startCol))
            return (null, null);
        if (!TryParseOdfCell(endPart, out _, out int endRow, out int endCol))
            return (null, null);

        var range = new OdfKit.Spreadsheet.OdfCellRange(startRow, startCol, endRow, endCol, sheetName);
        return (sheetName, range);
    }

    // ── 私有輔助方法 ──────────────────────────────────────────────────────────

    private static string GetDataSourceLabelToken(bool firstRowAsHeader, bool firstColumnAsLabel)
    {
        if (firstRowAsHeader && firstColumnAsLabel)
            return "both";
        if (firstRowAsHeader)
            return "row";
        if (firstColumnAsLabel)
            return "column";
        return "none";
    }

    private static string BuildAbsoluteCell(string sheetName, int row, int col)
    {
        string colName = ColumnIndexToName(col);
        string prefix = string.IsNullOrEmpty(sheetName) ? "." : $"{EscapeSheetName(sheetName)}.";
        return $"{prefix}${colName}${row + 1}";
    }

    private static string BuildAbsoluteRange(string sheetName, int startRow, int startCol, int endRow, int endCol)
    {
        string start = BuildAbsoluteCell(sheetName, startRow, startCol);
        string end = BuildAbsoluteCell(string.Empty, endRow, endCol);
        return $"{start}:{end}";
    }

    private static string EscapeSheetName(string name)
    {
        bool needsQuotes = name.Contains(' ') || name.Contains('\'') || name.Contains('-') || name.Contains('.');
        if (!needsQuotes)
            return name;
        return "'" + name.Replace("'", "''") + "'";
    }

    private static string ColumnIndexToName(int index)
    {
        int n = index + 1;
        var sb = new StringBuilder();
        while (n > 0)
        {
            int rem = (n - 1) % 26;
            sb.Insert(0, (char)('A' + rem));
            n = (n - 1) / 26;
        }
        return sb.ToString();
    }

    private static bool TryParseOdfCell(string part, out string? sheetName, out int row, out int col)
    {
        sheetName = null;
        row = 0;
        col = 0;
        string s = part.Trim();

        // 剝除前置 $ (絕對工作表參照)
        if (s.StartsWith("$", StringComparison.Ordinal))
            s = s.Substring(1);

        // 分離 sheet 與 cell：以第一個 '.' 為分隔
        int dot = s.IndexOf('.');
        if (dot < 0)
            return false;

        string sheetPart = s.Substring(0, dot);
        string cellPart = s.Substring(dot + 1);

        // 處理帶引號的工作表名稱
        if (sheetPart.StartsWith("'", StringComparison.Ordinal) &&
            sheetPart.EndsWith("'", StringComparison.Ordinal))
            sheetPart = sheetPart.Substring(1, sheetPart.Length - 2).Replace("''", "'");

        sheetName = string.IsNullOrEmpty(sheetPart) ? null : sheetPart;

        // 解析儲存格：去除 $，分離字母與數字
        cellPart = cellPart.Replace("$", "");
        int i = 0;
        while (i < cellPart.Length && char.IsLetter(cellPart[i]))
            i++;
        if (i == 0 || i >= cellPart.Length)
            return false;

        string colStr = cellPart.Substring(0, i);
        if (!int.TryParse(cellPart.Substring(i), out int rowNum) || rowNum < 1)
            return false;

        col = ColumnNameToIndex(colStr);
        row = rowNum - 1;
        return true;
    }

    private static int ColumnNameToIndex(string name)
    {
        int result = 0;
        foreach (char c in name.ToUpperInvariant())
            result = result * 26 + (c - 'A' + 1);
        return result - 1;
    }

    #endregion
}
