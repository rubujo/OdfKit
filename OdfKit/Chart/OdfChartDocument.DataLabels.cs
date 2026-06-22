using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Chart;

public partial class OdfChartDocument
{
    /// <summary>
    /// 取得指定資料序列的數據標籤設定。
    /// </summary>
    /// <param name="seriesIndex">序列索引（從 0 起算）。</param>
    /// <returns>數據標籤設定；若序列未定義數據標籤則為 <see langword="null"/>。</returns>
    /// <exception cref="ArgumentOutOfRangeException">當 <paramref name="seriesIndex"/> 超出範圍時擲出。</exception>
    public OdfChartDataLabelInfo? GetSeriesDataLabels(int seriesIndex)
    {
        OdfNode seriesNode = GetSeriesNodeOrThrow(seriesIndex);
        OdfNode? dataLabel = FindChildElement(seriesNode, "data-label", OdfNamespaces.Chart);
        if (dataLabel is null)
        {
            return null;
        }

        string? numberKind = dataLabel.GetAttribute("data-label-number", OdfNamespaces.Chart);
        bool showValue = numberKind is "value" or "value-and-percentage";
        bool showPercentage = numberKind is "percentage" or "value-and-percentage";
        bool showCategoryName = string.Equals(dataLabel.GetAttribute("data-label-text", OdfNamespaces.Chart), "true", StringComparison.Ordinal);
        bool showLegendKey = string.Equals(dataLabel.GetAttribute("data-label-symbol", OdfNamespaces.Chart), "true", StringComparison.Ordinal);

        return new OdfChartDataLabelInfo(showValue, showPercentage, showCategoryName, showLegendKey);
    }

    /// <summary>
    /// 設定指定資料序列的數據標籤。
    /// </summary>
    /// <param name="seriesIndex">序列索引（從 0 起算）。</param>
    /// <param name="info">數據標籤設定；傳入 <see langword="null"/> 表示移除既有設定。</param>
    /// <exception cref="ArgumentOutOfRangeException">當 <paramref name="seriesIndex"/> 超出範圍時擲出。</exception>
    public void SetSeriesDataLabels(int seriesIndex, OdfChartDataLabelInfo? info)
    {
        OdfNode seriesNode = GetSeriesNodeOrThrow(seriesIndex);
        OdfNode? existing = FindChildElement(seriesNode, "data-label", OdfNamespaces.Chart);

        if (info is null)
        {
            if (existing is not null)
            {
                seriesNode.RemoveChild(existing);
            }

            return;
        }

        OdfNode dataLabel = existing ?? OdfNodeFactory.CreateElement("data-label", OdfNamespaces.Chart, "chart");
        if (existing is null)
        {
            seriesNode.AppendChild(dataLabel);
        }

        string numberKind = (info.ShowValue, info.ShowPercentage) switch
        {
            (true, true) => "value-and-percentage",
            (true, false) => "value",
            (false, true) => "percentage",
            _ => "none",
        };
        dataLabel.SetAttribute("data-label-number", OdfNamespaces.Chart, numberKind, "chart");
        dataLabel.SetAttribute("data-label-text", OdfNamespaces.Chart, info.ShowCategoryName ? "true" : "false", "chart");
        dataLabel.SetAttribute("data-label-symbol", OdfNamespaces.Chart, info.ShowLegendKey ? "true" : "false", "chart");
    }

    private OdfNode GetSeriesNodeOrThrow(int seriesIndex)
    {
        IReadOnlyList<OdfNode> nodes = GetSeriesNodes();
        if (seriesIndex < 0 || seriesIndex >= nodes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(seriesIndex), $"序列索引 {seriesIndex} 超出範圍（共 {nodes.Count} 筆）。");
        }

        return nodes[seriesIndex];
    }
}
