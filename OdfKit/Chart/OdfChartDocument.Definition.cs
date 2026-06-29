using System;
using OdfKit.Core;
using OdfKit.Spreadsheet;

namespace OdfKit.Chart;

public partial class OdfChartDocument
{
    /// <summary>
    /// Gets the configuration definition information for the current chart.
    /// 取得目前圖表的設定定義資訊。
    /// </summary>
    /// <returns>An <see cref="OdfChartDefinition"/> instance containing the chart properties. / 包含圖表屬性的 <see cref="OdfChartDefinition"/> 執行個體。</returns>
    public OdfChartDefinition GetChartDefinition()
    {
        OdfChartType chartType = ParseChartType(ChartClass);

        OdfCellRange dataRange = default;
        (string? sheetName, OdfCellRange? range) = GetDataRange();
        if (range.HasValue)
        {
            dataRange = range.Value;
        }
        else
        {
            string? rangeAddress = ChartNode.GetAttribute("cell-range-address", OdfNamespaces.Table);
            if (!string.IsNullOrEmpty(rangeAddress))
            {
                string cleanAddress = rangeAddress!;
                if (cleanAddress.StartsWith("[", StringComparison.Ordinal) && cleanAddress.EndsWith("]", StringComparison.Ordinal))
                {
                    cleanAddress = cleanAddress.Substring(1, cleanAddress.Length - 2);
                }

                dataRange = OdfCellRange.ParseOdf(cleanAddress);
            }
        }

        if (sheetName is not null && string.IsNullOrEmpty(dataRange.StartAddress.SheetName))
        {
            dataRange = new OdfCellRange(
                dataRange.StartAddress.Row,
                dataRange.StartAddress.Column,
                dataRange.EndAddress.Row,
                dataRange.EndAddress.Column,
                sheetName);
        }

        return new OdfChartDefinition
        {
            ChartType = chartType,
            Title = ChartTitle ?? string.Empty,
            HasLegend = LegendPosition is not null,
            DataRange = dataRange,
        };
    }

    private static OdfChartType ParseChartType(string? chartClass) => chartClass switch
    {
        "chart:line" or "line" => OdfChartType.Line,
        "chart:circle" or "circle" or "chart:pie" or "pie" => OdfChartType.Pie,
        "chart:area" or "area" => OdfChartType.Area,
        "chart:scatter" or "scatter" => OdfChartType.Scatter,
        "chart:bubble" or "bubble" => OdfChartType.Bubble,
        "chart:ring" or "ring" => OdfChartType.Ring,
        "chart:radar" or "radar" => OdfChartType.Radar,
        "chart:stock" or "stock" => OdfChartType.Stock,
        _ => OdfChartType.Bar,
    };
}
