using System;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Chart;

/// <summary>
/// Represents an editable chart data series.
/// 表示可編輯的圖表資料序列。
/// </summary>
public sealed class OdfChartSeries
{
    private readonly OdfChartDocument _document;
    private readonly OdfNode _node;

    internal OdfChartSeries(OdfChartDocument document, OdfNode seriesNode, int index)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _node = seriesNode ?? throw new ArgumentNullException(nameof(seriesNode));
        Index = index;
    }

    /// <summary>
    /// Gets the zero-based index of this series within the chart.
    /// 取得此序列在圖表中的索引（從 0 起算）。
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets or sets the data value cell range address.
    /// 取得或設定資料值儲存格範圍位址。
    /// </summary>
    public string ValuesCellRangeAddress
    {
        get => _node.GetAttribute("values-cell-range-address", OdfNamespaces.Chart) ?? string.Empty;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfChartSeries_DataCannotBeEmpty"), nameof(value));

            _node.SetAttribute("values-cell-range-address", OdfNamespaces.Chart, value, "chart");
        }
    }

    /// <summary>
    /// Gets or sets the label cell address.
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
    /// Gets or sets the series class (e.g. <c>chart:line</c>, <c>chart:bar</c>).
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
    /// Gets or sets the series style name.
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
    /// Gets or sets the name of the attached axis (e.g. <c>primary-y</c>).
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

    /// <summary>
    /// Gets all data point style override entries (<c>chart:data-point</c>) in this series.
    /// 取得此序列中所有資料點樣式覆蓋設定（<c>chart:data-point</c>）。
    /// </summary>
    /// <returns>The list of data point style override entries, in document order. / 資料點樣式覆蓋設定清單，依文件中出現順序排列。</returns>
    public IReadOnlyList<OdfChartDataPointInfo> GetDataPoints()
    {
        List<OdfChartDataPointInfo> points = [];
        foreach (OdfNode child in _node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "data-point" &&
                child.NamespaceUri == OdfNamespaces.Chart)
            {
                string? repeatedAttr = child.GetAttribute("repeated", OdfNamespaces.Chart);
                int repeated = int.TryParse(repeatedAttr, out int value) ? value : 1;
                points.Add(new OdfChartDataPointInfo(repeated, child.GetAttribute("style-name", OdfNamespaces.Chart)));
            }
        }

        return points;
    }

    /// <summary>
    /// Adds a data point style override entry (<c>chart:data-point</c>).
    /// 新增一筆資料點樣式覆蓋設定（<c>chart:data-point</c>）。
    /// </summary>
    /// <param name="repeated">The number of consecutive data points this entry applies to; defaults to 1. / 此筆設定套用的連續資料點數量，預設為 1。</param>
    /// <param name="styleName">The applied style name; no style is written when <see langword="null"/>. / 套用的樣式名稱；為 <see langword="null"/> 時不寫入樣式。</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="repeated"/> is less than 1. / 當 <paramref name="repeated"/> 小於 1 時擲出。</exception>
    public void AddDataPoint(int repeated = 1, string? styleName = null)
    {
        if (repeated < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(repeated), OdfLocalizer.GetMessage("Err_OdfChartSeries_NumberRepetitionsLeast1"));
        }

        OdfNode dataPoint = OdfNodeFactory.CreateElement("data-point", OdfNamespaces.Chart, "chart");
        if (repeated > 1)
        {
            dataPoint.SetAttribute("repeated", OdfNamespaces.Chart, repeated.ToString(CultureInfo.InvariantCulture), "chart");
        }

        if (!string.IsNullOrWhiteSpace(styleName))
        {
            dataPoint.SetAttribute("style-name", OdfNamespaces.Chart, styleName!, "chart");
        }

        _node.AppendChild(dataPoint);
    }

    /// <summary>
    /// Removes all data point style override entries from this series.
    /// 移除此序列中所有資料點樣式覆蓋設定。
    /// </summary>
    public void ClearDataPoints()
    {
        foreach (OdfNode child in new List<OdfNode>(_node.Children))
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "data-point" &&
                child.NamespaceUri == OdfNamespaces.Chart)
            {
                _node.RemoveChild(child);
            }
        }
    }

    /// <summary>
    /// Gets the data label setting (<c>chart:data-label</c>) for this series; <see langword="null"/> if not set.
    /// 取得此序列的資料標籤設定（<c>chart:data-label</c>）；若未設定則為 <see langword="null"/>。
    /// </summary>
    /// <returns>The data label setting; <see langword="null"/> if no <c>chart:data-label</c> exists in this series. / 資料標籤設定；若序列中第一個 <c>chart:data-label</c> 不存在則為 <see langword="null"/>。</returns>
    public OdfChartDataLabelInfo? GetDataLabels()
    {
        OdfNode? dataLabel = FindFirstChild("data-label");
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
    /// Sets or removes the data label for this series.
    /// 設定或移除此序列的資料標籤。
    /// </summary>
    /// <param name="info">The data label setting; pass <see langword="null"/> to remove the existing setting. / 資料標籤設定；傳入 <see langword="null"/> 表示移除既有設定。</param>
    public void SetDataLabels(OdfChartDataLabelInfo? info)
    {
        RemoveAllChildren("data-label");
        if (info is null)
        {
            return;
        }

        OdfNode dataLabel = OdfNodeFactory.CreateElement("data-label", OdfNamespaces.Chart, "chart");
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

        InsertChildInSchemaOrder(dataLabel, "data-label");
    }

    /// <summary>
    /// Sets or removes the data label for this series according to a common preset combination.
    /// 依常用預設組合設定或移除此序列的資料標籤。
    /// </summary>
    /// <param name="preset">The data label preset combination; <see cref="OdfChartDataLabelPreset.None"/> removes the existing setting. / 資料標籤預設組合；<see cref="OdfChartDataLabelPreset.None"/> 表示移除既有設定。</param>
    public void SetDataLabelPreset(OdfChartDataLabelPreset preset) =>
        SetDataLabels(preset == OdfChartDataLabelPreset.None ? null : OdfChartDataLabelInfo.FromPreset(preset));

    /// <summary>
    /// Gets the error bar setting (<c>chart:error-indicator</c>) for this series; <see langword="null"/> if not set.
    /// 取得此序列的誤差棒設定（<c>chart:error-indicator</c>）；若未設定則為 <see langword="null"/>。
    /// </summary>
    /// <returns>The error bar setting; <see langword="null"/> if no <c>chart:error-indicator</c> exists in this series. / 誤差棒設定；若序列中第一個 <c>chart:error-indicator</c> 不存在則為 <see langword="null"/>。</returns>
    public OdfChartErrorIndicatorInfo? GetErrorIndicator()
    {
        OdfNode? node = FindFirstChild("error-indicator");
        if (node is null)
        {
            return null;
        }

        return new OdfChartErrorIndicatorInfo(
            node.GetAttribute("dimension", OdfNamespaces.Chart),
            node.GetAttribute("style-name", OdfNamespaces.Chart));
    }

    /// <summary>
    /// Sets or removes the error bar for this series.
    /// 設定或移除此序列的誤差棒。
    /// </summary>
    /// <param name="info">The error bar setting; pass <see langword="null"/> to remove the existing setting. / 誤差棒設定；傳入 <see langword="null"/> 表示移除既有設定。</param>
    public void SetErrorIndicator(OdfChartErrorIndicatorInfo? info)
    {
        RemoveAllChildren("error-indicator");
        if (info is null)
        {
            return;
        }

        OdfNode node = OdfNodeFactory.CreateElement("error-indicator", OdfNamespaces.Chart, "chart");
        if (!string.IsNullOrWhiteSpace(info.Dimension))
        {
            node.SetAttribute("dimension", OdfNamespaces.Chart, info.Dimension!, "chart");
        }

        if (!string.IsNullOrWhiteSpace(info.StyleName))
        {
            node.SetAttribute("style-name", OdfNamespaces.Chart, info.StyleName!, "chart");
        }

        InsertChildInSchemaOrder(node, "error-indicator");
    }

    /// <summary>
    /// Gets the trend line (regression curve) setting (<c>chart:regression-curve</c>) for this series; <see langword="null"/> if not set.
    /// 取得此序列的趨勢線（迴歸曲線）設定（<c>chart:regression-curve</c>）；若未設定則為 <see langword="null"/>。
    /// </summary>
    /// <returns>The trend line setting; <see langword="null"/> if no <c>chart:regression-curve</c> exists in this series. / 趨勢線設定；若序列中第一個 <c>chart:regression-curve</c> 不存在則為 <see langword="null"/>。</returns>
    public OdfChartRegressionCurveInfo? GetRegressionCurve()
    {
        OdfNode? node = FindFirstChild("regression-curve");
        return node is null ? null : new OdfChartRegressionCurveInfo(node.GetAttribute("style-name", OdfNamespaces.Chart));
    }

    /// <summary>
    /// Sets or removes the trend line (regression curve) for this series.
    /// 設定或移除此序列的趨勢線（迴歸曲線）。
    /// </summary>
    /// <param name="info">The trend line setting; pass <see langword="null"/> to remove the existing setting. / 趨勢線設定；傳入 <see langword="null"/> 表示移除既有設定。</param>
    public void SetRegressionCurve(OdfChartRegressionCurveInfo? info)
    {
        RemoveAllChildren("regression-curve");
        if (info is null)
        {
            return;
        }

        OdfNode node = OdfNodeFactory.CreateElement("regression-curve", OdfNamespaces.Chart, "chart");
        if (!string.IsNullOrWhiteSpace(info.StyleName))
        {
            node.SetAttribute("style-name", OdfNamespaces.Chart, info.StyleName!, "chart");
        }

        InsertChildInSchemaOrder(node, "regression-curve");
    }

    /// <summary>
    /// Gets the mean value line setting (<c>chart:mean-value</c>) for this series; <see langword="null"/> if not set.
    /// 取得此序列的平均值線設定（<c>chart:mean-value</c>）；若未設定則為 <see langword="null"/>。
    /// </summary>
    /// <returns>The mean value line setting; <see langword="null"/> if this series does not define <c>chart:mean-value</c>. / 平均值線設定；若序列未定義 <c>chart:mean-value</c> 則為 <see langword="null"/>。</returns>
    public OdfChartMeanValueInfo? GetMeanValue()
    {
        OdfNode? node = FindFirstChild("mean-value");
        return node is null ? null : new OdfChartMeanValueInfo(node.GetAttribute("style-name", OdfNamespaces.Chart));
    }

    /// <summary>
    /// Sets or removes the mean value line for this series.
    /// 設定或移除此序列的平均值線。
    /// </summary>
    /// <param name="info">The mean value line setting; pass <see langword="null"/> to remove the existing setting. / 平均值線設定；傳入 <see langword="null"/> 表示移除既有設定。</param>
    public void SetMeanValue(OdfChartMeanValueInfo? info)
    {
        RemoveAllChildren("mean-value");
        if (info is null)
        {
            return;
        }

        OdfNode node = OdfNodeFactory.CreateElement("mean-value", OdfNamespaces.Chart, "chart");
        if (!string.IsNullOrWhiteSpace(info.StyleName))
        {
            node.SetAttribute("style-name", OdfNamespaces.Chart, info.StyleName!, "chart");
        }

        InsertChildInSchemaOrder(node, "mean-value");
    }

    /// <summary>
    /// The required child element order of <c>chart:series</c> per the OASIS ODF 1.4 schema:
    /// <c>chart:domain*</c>, <c>chart:mean-value?</c>, <c>chart:regression-curve*</c>,
    /// <c>chart:error-indicator*</c>, <c>chart:data-point*</c>, <c>chart:data-label?</c>.
    /// <c>chart:series</c> 子元素依 OASIS ODF 1.4 schema 規定的順序：
    /// <c>chart:domain*</c>、<c>chart:mean-value?</c>、<c>chart:regression-curve*</c>、
    /// <c>chart:error-indicator*</c>、<c>chart:data-point*</c>、<c>chart:data-label?</c>。
    /// </summary>
    private static readonly string[] SeriesChildOrder =
        ["domain", "mean-value", "regression-curve", "error-indicator", "data-point", "data-label"];

    private OdfNode? FindFirstChild(string localName)
    {
        foreach (OdfNode child in _node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == OdfNamespaces.Chart)
            {
                return child;
            }
        }

        return null;
    }

    private void RemoveAllChildren(string localName)
    {
        foreach (OdfNode child in new List<OdfNode>(_node.Children))
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == OdfNamespaces.Chart)
            {
                _node.RemoveChild(child);
            }
        }
    }

    private void InsertChildInSchemaOrder(OdfNode newChild, string localName)
    {
        int rank = Array.IndexOf(SeriesChildOrder, localName);
        foreach (OdfNode sibling in _node.Children)
        {
            if (sibling.NodeType is not OdfNodeType.Element || sibling.NamespaceUri != OdfNamespaces.Chart)
            {
                continue;
            }

            int siblingRank = Array.IndexOf(SeriesChildOrder, sibling.LocalName);
            if (siblingRank > rank)
            {
                _node.InsertBefore(newChild, sibling);
                return;
            }
        }

        _node.AppendChild(newChild);
    }

    /// <summary>
    /// Gets or sets the chart automatic style of this data series.
    /// 取得或設定此資料序列的圖表自動樣式。
    /// </summary>
    public OdfChartStyle Style
    {
        get
        {
            string? name = StyleName;
            if (string.IsNullOrEmpty(name))
            {
                name = $"series-style-{Index + 1}";
                StyleName = name;
            }
            return _document.CreateChartStyle(name!);
        }
        set
        {
            if (value is null)
            {
                StyleName = null;
            }
            else
            {
                StyleName = value.Name;
            }
        }
    }
}
