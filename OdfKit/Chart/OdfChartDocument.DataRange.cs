using System;
using System.Collections.Generic;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;

using OdfKit.Compliance;
namespace OdfKit.Chart;
/// <summary>
/// Provides the OdfChartDocument API.
/// 提供 OdfChartDocument API。
/// </summary>

public partial class OdfChartDocument
{
    #region Data Range Binding
    /// <summary>
    /// Short overload of SetDataRange that accepts sheetName and range; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 sheetName 與 range；其餘可選參數使用預設值並轉呼叫最長 SetDataRange 多載。
    /// </summary>
    public void SetDataRange(string sheetName, OdfKit.Spreadsheet.OdfCellRange range) => SetDataRange(sheetName, range, true, true);

    /// <summary>
    /// Short overload of SetDataRange that accepts sheetName, range, and firstRowAsHeader; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 sheetName、range 與 firstRowAsHeader；其餘可選參數使用預設值並轉呼叫最長 SetDataRange 多載。
    /// </summary>
    public void SetDataRange(string sheetName, OdfKit.Spreadsheet.OdfCellRange range, bool firstRowAsHeader) => SetDataRange(sheetName, range, firstRowAsHeader, true);


    /// <summary>
    /// Binds the chart data source to a spreadsheet cell range.
    /// 將圖表資料來源綁定至試算表的儲存格範圍。
    /// </summary>
    /// <param name="sheetName">The sheet name. / 工作表名稱。</param>
    /// <param name="range">The cell range. / 儲存格範圍。</param>
    /// <param name="firstRowAsHeader">Whether the first row is treated as the series header; defaults to true. / 首列作為序列標題（header），預設 true。</param>
    /// <param name="firstColumnAsLabel">Whether the first column is treated as the category label (X-axis); defaults to true. / 首欄作為分類標籤（X 軸），預設 true。</param>
    public void SetDataRange(string sheetName, OdfKit.Spreadsheet.OdfCellRange range, bool firstRowAsHeader, bool firstColumnAsLabel)
    {
        if (string.IsNullOrEmpty(sheetName))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfChartDocument_WorksheetCannotBeEmpty"), nameof(sheetName));

        OdfNode chart = GetChartNode();
        chart.RemoveAttribute("cell-range-address", OdfNamespaces.Table);

        // 1. 清除現有 <chart:series>
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

        // 2. 設定 X 軸分類範圍
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

        // 3. 為每個資料欄新增 <chart:series>
        for (int col = dataColStart; col <= range.EndAddress.Column; col++)
        {
            if (dataRowStart > range.EndAddress.Row)
                break;

            string dataRange = BuildAbsoluteRange(sheetName,
                dataRowStart, col, range.EndAddress.Row, col);

            OdfNode series = OdfNodeFactory.CreateElement("series", OdfNamespaces.Chart, "chart");
            series.SetAttribute("values-cell-range-address", OdfNamespaces.Chart, dataRange, "chart");

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
        if (!string.IsNullOrWhiteSpace(addr) && OdfCellRange.TryParse(addr!, out OdfCellRange legacyRange))
        {
            return (legacyRange.StartAddress.SheetName, legacyRange);
        }

        List<OdfCellRange> ranges = [];
        foreach (OdfNode node in ChartNode.Descendants())
        {
            AddRange(node.GetAttribute("values-cell-range-address", OdfNamespaces.Chart), ranges);
            AddRange(node.GetAttribute("label-cell-address", OdfNamespaces.Chart), ranges);
            if (node.LocalName is "categories" or "domain" && node.NamespaceUri == OdfNamespaces.Chart)
            {
                AddRange(node.GetAttribute("cell-range-address", OdfNamespaces.Table), ranges);
            }
        }

        if (ranges.Count == 0)
        {
            return (null, null);
        }

        string? sheetName = ranges[0].StartAddress.SheetName ?? ranges[0].EndAddress.SheetName;
        int minRow = int.MaxValue;
        int minColumn = int.MaxValue;
        int maxRow = int.MinValue;
        int maxColumn = int.MinValue;
        foreach (OdfCellRange range in ranges)
        {
            minRow = Math.Min(minRow, Math.Min(range.StartAddress.Row, range.EndAddress.Row));
            minColumn = Math.Min(minColumn, Math.Min(range.StartAddress.Column, range.EndAddress.Column));
            maxRow = Math.Max(maxRow, Math.Max(range.StartAddress.Row, range.EndAddress.Row));
            maxColumn = Math.Max(maxColumn, Math.Max(range.StartAddress.Column, range.EndAddress.Column));
        }

        var combined = new OdfCellRange(minRow, minColumn, maxRow, maxColumn, sheetName);
        return (sheetName, combined);
    }

    private static void AddRange(string? address, List<OdfCellRange> ranges)
    {
        if (!string.IsNullOrWhiteSpace(address) && OdfCellRange.TryParse(address!, out OdfCellRange range))
        {
            ranges.Add(range);
        }
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

    #endregion
}
