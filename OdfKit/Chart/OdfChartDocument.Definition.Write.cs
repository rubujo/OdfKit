using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;

namespace OdfKit.Chart;

public partial class OdfChartDocument
{
    /// <summary>
    /// Sets the chart type.
    /// 設定圖表類型。
    /// </summary>
    /// <param name="chartType">The chart type. / 圖表類型。</param>
    public void SetChartType(OdfChartType chartType)
    {
        ChartClass = chartType switch
        {
            OdfChartType.Line => "chart:line",
            OdfChartType.Pie => "chart:circle",
            OdfChartType.Area => "chart:area",
            OdfChartType.Scatter => "chart:scatter",
            OdfChartType.Bubble => "chart:bubble",
            OdfChartType.Ring => "chart:ring",
            OdfChartType.Radar => "chart:radar",
            OdfChartType.Stock => "chart:stock",
            _ => "chart:bar",
        };
    }

    /// <summary>
    /// Removes all data series nodes from the chart.
    /// 清除圖表中所有資料序列節點。
    /// </summary>
    public void ClearSeries()
    {
        OdfNode? plotArea = FindChildElement(GetChartNode(), "plot-area", OdfNamespaces.Chart);
        if (plotArea is null)
            return;

        List<OdfNode> toRemove = [];
        foreach (OdfNode child in plotArea.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "series" &&
                child.NamespaceUri == OdfNamespaces.Chart)
                toRemove.Add(child);
        }

        foreach (OdfNode series in toRemove)
            plotArea.RemoveChild(series);
    }

    /// <summary>
    /// Applies an <see cref="OdfChartDefinition"/> to the current chart.
    /// 將 <see cref="OdfChartDefinition"/> 套用至目前圖表。
    /// </summary>
    /// <param name="definition">The chart definition. / 圖表定義。</param>
    public void ApplyDefinition(OdfChartDefinition definition)
    {
        if (definition is null)
            throw new ArgumentNullException(nameof(definition));

        SetChartType(definition.ChartType);
        ChartTitle = definition.Title;

        if (definition.HasLegend)
            SetLegend("top");
        else
            LegendPosition = null;

        string? sheetName = definition.DataRange.StartAddress.SheetName;
        if (!string.IsNullOrEmpty(sheetName) &&
            definition.DataRange.StartAddress.Row >= 0 &&
            definition.DataRange.StartAddress.Column >= 0)
        {
            SetDataRange(sheetName!, definition.DataRange);
        }
    }
}
