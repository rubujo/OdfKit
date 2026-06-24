using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Chart;

/// <summary>
/// 表示圖表圖例的統一可編輯模型。
/// </summary>
public sealed class OdfChartLegend
{
    private readonly OdfChartDocument _document;

    internal OdfChartLegend(OdfChartDocument document)
    {
        _document = document;
    }

    /// <summary>
    /// 取得或設定圖例是否顯示。
    /// </summary>
    public bool IsVisible
    {
        get => _document.FindLegendNode() is not null;
        set
        {
            if (value)
            {
                EnsureVisible();
            }
            else
            {
                _document.RemoveLegendNode();
            }
        }
    }

    /// <summary>
    /// 取得或設定圖例位置（對應 <c>chart:legend-position</c>）。
    /// </summary>
    public string? Position
    {
        get => _document.FindLegendNode()?.GetAttribute("legend-position", OdfNamespaces.Chart);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                IsVisible = false;
                return;
            }

            OdfNode legend = EnsureVisible();
            legend.SetAttribute("legend-position", OdfNamespaces.Chart, value!, "chart");
        }
    }

    /// <summary>
    /// 取得或設定圖例對齊方式（對應 <c>chart:legend-align</c>）。
    /// </summary>
    public string? Alignment
    {
        get => _document.FindLegendNode()?.GetAttribute("legend-align", OdfNamespaces.Chart);
        set
        {
            OdfNode? legend = _document.FindLegendNode();
            if (string.IsNullOrWhiteSpace(value))
            {
                legend?.RemoveAttribute("legend-align", OdfNamespaces.Chart);
                return;
            }

            (legend ?? EnsureVisible()).SetAttribute("legend-align", OdfNamespaces.Chart, value!, "chart");
        }
    }

    /// <summary>
    /// 取得或設定圖例樣式名稱（對應 <c>chart:style-name</c>）。
    /// </summary>
    public string? StyleName
    {
        get => _document.FindLegendNode()?.GetAttribute("style-name", OdfNamespaces.Chart);
        set
        {
            OdfNode? legend = _document.FindLegendNode();
            if (string.IsNullOrWhiteSpace(value))
            {
                legend?.RemoveAttribute("style-name", OdfNamespaces.Chart);
                return;
            }

            (legend ?? EnsureVisible()).SetAttribute("style-name", OdfNamespaces.Chart, value!, "chart");
        }
    }

    /// <summary>
    /// 取得或設定圖例的圖表樣式。
    /// </summary>
    public OdfChartStyle Style
    {
        get
        {
            OdfNode legend = EnsureVisible();
            string? styleName = legend.GetAttribute("style-name", OdfNamespaces.Chart);
            if (string.IsNullOrWhiteSpace(styleName))
            {
                styleName = "legend-style";
                legend.SetAttribute("style-name", OdfNamespaces.Chart, styleName, "chart");
            }

            return _document.CreateChartStyle(styleName!);
        }
        set => StyleName = value?.Name;
    }

    internal OdfNode EnsureVisible() => _document.EnsureLegendNode();
}
