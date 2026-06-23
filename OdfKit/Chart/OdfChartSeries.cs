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
    /// <returns>資料點樣式覆蓋設定清單，依文件中出現順序排列</returns>
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
    /// <param name="repeated">此筆設定套用的連續資料點數量，預設為 1</param>
    /// <param name="styleName">套用的樣式名稱；為 <see langword="null"/> 時不寫入樣式</param>
    /// <exception cref="ArgumentOutOfRangeException">當 <paramref name="repeated"/> 小於 1 時擲出</exception>
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
    /// 取得此序列的誤差棒設定（<c>chart:error-indicator</c>）；若未設定則為 <see langword="null"/>。
    /// </summary>
    /// <returns>誤差棒設定；若序列中第一個 <c>chart:error-indicator</c> 不存在則為 <see langword="null"/></returns>
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
    /// 設定或移除此序列的誤差棒。
    /// </summary>
    /// <param name="info">誤差棒設定；傳入 <see langword="null"/> 表示移除既有設定</param>
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
    /// 取得此序列的趨勢線（迴歸曲線）設定（<c>chart:regression-curve</c>）；若未設定則為 <see langword="null"/>。
    /// </summary>
    /// <returns>趨勢線設定；若序列中第一個 <c>chart:regression-curve</c> 不存在則為 <see langword="null"/></returns>
    public OdfChartRegressionCurveInfo? GetRegressionCurve()
    {
        OdfNode? node = FindFirstChild("regression-curve");
        return node is null ? null : new OdfChartRegressionCurveInfo(node.GetAttribute("style-name", OdfNamespaces.Chart));
    }

    /// <summary>
    /// 設定或移除此序列的趨勢線（迴歸曲線）。
    /// </summary>
    /// <param name="info">趨勢線設定；傳入 <see langword="null"/> 表示移除既有設定</param>
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
    /// 取得此序列的平均值線設定（<c>chart:mean-value</c>）；若未設定則為 <see langword="null"/>。
    /// </summary>
    /// <returns>平均值線設定；若序列未定義 <c>chart:mean-value</c> 則為 <see langword="null"/></returns>
    public OdfChartMeanValueInfo? GetMeanValue()
    {
        OdfNode? node = FindFirstChild("mean-value");
        return node is null ? null : new OdfChartMeanValueInfo(node.GetAttribute("style-name", OdfNamespaces.Chart));
    }

    /// <summary>
    /// 設定或移除此序列的平均值線。
    /// </summary>
    /// <param name="info">平均值線設定；傳入 <see langword="null"/> 表示移除既有設定</param>
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
