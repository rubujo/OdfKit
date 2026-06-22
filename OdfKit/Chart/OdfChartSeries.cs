using System;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Chart;

/// <summary>
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
                throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfChartSeries_DataCannotBeEmpty"), nameof(value));

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

    /// <summary>
    /// 取得此序列中所有資料點樣式覆蓋設定（<c>chart:data-point</c>）。
    /// </summary>
    /// <returns>資料點樣式覆蓋設定清單，依文件中出現順序排列。</returns>
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
    /// 新增一筆資料點樣式覆蓋設定（<c>chart:data-point</c>）。
    /// </summary>
    /// <param name="repeated">此筆設定套用的連續資料點數量，預設為 1。</param>
    /// <param name="styleName">套用的樣式名稱；為 <see langword="null"/> 時不寫入樣式。</param>
    /// <exception cref="ArgumentOutOfRangeException">當 <paramref name="repeated"/> 小於 1 時擲出。</exception>
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
