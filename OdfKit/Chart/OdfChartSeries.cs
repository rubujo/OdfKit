using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Chart;

/// <summary>
/// 表示可編輯的圖表資料序列。
/// </summary>
public sealed class OdfChartSeries
{
    private readonly OdfNode _node;

    internal OdfChartSeries(OdfNode seriesNode, int index)
    {
        _node = seriesNode ?? throw new ArgumentNullException(nameof(seriesNode));
        Index = index;
    }

    /// <summary>
    /// 取得此序列在圖表中的索引（從 0 起算）。
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// 取得或設定資料值儲存格範圍位址。
    /// </summary>
    public string ValuesCellRangeAddress
    {
        get => _node.GetAttribute("values-cell-range-address", OdfNamespaces.Chart) ?? string.Empty;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("資料值儲存格範圍位址不可為空白。", nameof(value));

            _node.SetAttribute("values-cell-range-address", OdfNamespaces.Chart, value, "chart");
        }
    }

    /// <summary>
    /// 取得或設定標籤儲存格位址。
    /// </summary>
    public string? LabelCellAddress
    {
        get => _node.GetAttribute("label-cell-address", OdfNamespaces.Chart);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                _node.RemoveAttribute("label-cell-address", OdfNamespaces.Chart);
            else
                _node.SetAttribute("label-cell-address", OdfNamespaces.Chart, value!, "chart");
        }
    }

    /// <summary>
    /// 取得或設定序列類型（例如 <c>chart:line</c>、<c>chart:bar</c>）。
    /// </summary>
    public string? SeriesClass
    {
        get => _node.GetAttribute("class", OdfNamespaces.Chart);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                _node.RemoveAttribute("class", OdfNamespaces.Chart);
            else
                _node.SetAttribute("class", OdfNamespaces.Chart, value!, "chart");
        }
    }

    /// <summary>
    /// 取得或設定序列樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => _node.GetAttribute("style-name", OdfNamespaces.Chart);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                _node.RemoveAttribute("style-name", OdfNamespaces.Chart);
            else
                _node.SetAttribute("style-name", OdfNamespaces.Chart, value!, "chart");
        }
    }

    /// <summary>
    /// 取得或設定附著的座標軸名稱（例如 <c>primary-y</c>）。
    /// </summary>
    public string? AttachedAxis
    {
        get => _node.GetAttribute("attached-axis", OdfNamespaces.Chart);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                _node.RemoveAttribute("attached-axis", OdfNamespaces.Chart);
            else
                _node.SetAttribute("attached-axis", OdfNamespaces.Chart, value!, "chart");
        }
    }
}
