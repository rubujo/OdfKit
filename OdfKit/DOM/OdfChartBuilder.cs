using System;
using System.Collections.Generic;
using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Spreadsheet;

namespace OdfKit.DOM;

/// <summary>
/// 提供 ODF 圖表的資料繫結與建置輔助功能。
/// </summary>
public sealed class OdfChartBuilder
{
    private readonly ChartDocument _chartDocument;

    /// <summary>
    /// 初始化 <see cref="OdfChartBuilder"/> 類別的新執行個體。
    /// </summary>
    /// <param name="chartDocument">要進行建置的圖表文件</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="chartDocument"/> 為 null 時擲出</exception>
    public OdfChartBuilder(ChartDocument chartDocument)
    {
        _chartDocument = chartDocument ?? throw new ArgumentNullException(nameof(chartDocument));
    }

    /// <summary>
    /// 將圖表資料來源繫結至指定的試算表表格與儲存格範圍，並自動同步圖表內嵌之本地 ODS 數據。
    /// </summary>
    /// <param name="table">來源試算表表格元素</param>
    /// <param name="range">儲存格範圍字串（例如 "A1:C5"）</param>
    /// <returns>目前的 <see cref="OdfChartBuilder"/> 執行個體</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="table"/> 為 null 時擲出</exception>
    /// <exception cref="ArgumentException">當 <paramref name="range"/> 為空值時擲出</exception>
    public OdfChartBuilder BindData(TableTableElement table, string range)
    {
        if (table is null)
            throw new ArgumentNullException(nameof(table));
        if (string.IsNullOrEmpty(range))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_ChartBuilder_RangeCannotBeEmpty"), nameof(range));

        var cellRange = OdfCellRange.ParseExcel(range);
        int startRow = cellRange.StartAddress.Row;
        int startCol = cellRange.StartAddress.Column;
        int endRow = cellRange.EndAddress.Row;
        int endCol = cellRange.EndAddress.Column;

        var data = new List<List<object?>>();
        for (int r = startRow; r <= endRow; r++)
        {
            var rowData = new List<object?>();
            for (int c = startCol; c <= endCol; c++)
            {
                var cell = table[r, c];
                rowData.Add(GetCellValue(cell));
            }
            data.Add(rowData);
        }

        _chartDocument.UpdateData(data);

        var localRange = new OdfCellRange(0, 0, endRow - startRow, endCol - startCol, "LocalTable");
        _chartDocument.SetDataRange("LocalTable", localRange, firstRowAsHeader: true, firstColumnAsLabel: true);

        return this;
    }

    private static object? GetCellValue(TableTableCellElement cell)
    {
        string? valueType = cell.ValueType;
        if (string.IsNullOrEmpty(valueType))
        {
            return cell.TextContent;
        }

        return valueType switch
        {
            "boolean" => cell.GetAttribute("boolean-value", OdfNamespaces.Office) == "true",
            "date" => DateTime.TryParse(cell.GetAttribute("date-value", OdfNamespaces.Office), out var dt) ? dt : cell.TextContent,
            "float" => double.TryParse(cell.GetAttribute("value", OdfNamespaces.Office), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : cell.TextContent,
            _ => cell.TextContent
        };
    }
}
