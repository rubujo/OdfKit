using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Chart;

public partial class OdfChartDocument
{
    /// <summary>
    /// Gets the style name of the 3D chart wall (chart:wall).
    /// 取得 3D 圖表牆面（chart:wall）的樣式名稱。
    /// </summary>
    /// <returns>The style name; <see langword="null"/> if not set. / 樣式名稱；若未設定則為 <see langword="null"/>。</returns>
    public string? GetWallStyleName() =>
        FindChildElement(FindOrCreatePlotArea(), "wall", OdfNamespaces.Chart)?.GetAttribute("style-name", OdfNamespaces.Chart);

    /// <summary>
    /// Sets the style name of the 3D chart wall (chart:wall).
    /// 設定 3D 圖表牆面（chart:wall）的樣式名稱。
    /// </summary>
    /// <param name="styleName">The style name; <see langword="null"/> or blank removes the chart:wall element. / 樣式名稱；<see langword="null"/> 或空白會移除 chart:wall 元素。</param>
    public void SetWallStyleName(string? styleName) =>
        SetPlotAreaPartStyleName("wall", styleName);

    /// <summary>
    /// Gets the style name of the 3D chart floor (chart:floor).
    /// 取得 3D 圖表地板（chart:floor）的樣式名稱。
    /// </summary>
    /// <returns>The style name; <see langword="null"/> if not set. / 樣式名稱；若未設定則為 <see langword="null"/>。</returns>
    public string? GetFloorStyleName() =>
        FindChildElement(FindOrCreatePlotArea(), "floor", OdfNamespaces.Chart)?.GetAttribute("style-name", OdfNamespaces.Chart);

    /// <summary>
    /// Sets the style name of the 3D chart floor (chart:floor).
    /// 設定 3D 圖表地板（chart:floor）的樣式名稱。
    /// </summary>
    /// <param name="styleName">The style name; <see langword="null"/> or blank removes the chart:floor element. / 樣式名稱；<see langword="null"/> 或空白會移除 chart:floor 元素。</param>
    public void SetFloorStyleName(string? styleName) =>
        SetPlotAreaPartStyleName("floor", styleName);

    /// <summary>
    /// Gets the style name of the stock chart gain marker (chart:stock-gain-marker).
    /// 取得股票圖上漲標記（chart:stock-gain-marker）的樣式名稱。
    /// </summary>
    /// <returns>The style name; <see langword="null"/> if not set. / 樣式名稱；若未設定則為 <see langword="null"/>。</returns>
    public string? GetStockGainMarkerStyleName() =>
        FindChildElement(FindOrCreatePlotArea(), "stock-gain-marker", OdfNamespaces.Chart)?.GetAttribute("style-name", OdfNamespaces.Chart);

    /// <summary>
    /// Sets the style name of the stock chart gain marker (chart:stock-gain-marker).
    /// 設定股票圖上漲標記（chart:stock-gain-marker）的樣式名稱。
    /// </summary>
    /// <param name="styleName">The style name; <see langword="null"/> or blank removes the chart:stock-gain-marker element. / 樣式名稱；<see langword="null"/> 或空白會移除 chart:stock-gain-marker 元素。</param>
    public void SetStockGainMarkerStyleName(string? styleName) =>
        SetPlotAreaPartStyleName("stock-gain-marker", styleName);

    /// <summary>
    /// Gets the style name of the stock chart loss marker (chart:stock-loss-marker).
    /// 取得股票圖下跌標記（chart:stock-loss-marker）的樣式名稱。
    /// </summary>
    /// <returns>The style name; <see langword="null"/> if not set. / 樣式名稱；若未設定則為 <see langword="null"/>。</returns>
    public string? GetStockLossMarkerStyleName() =>
        FindChildElement(FindOrCreatePlotArea(), "stock-loss-marker", OdfNamespaces.Chart)?.GetAttribute("style-name", OdfNamespaces.Chart);

    /// <summary>
    /// Sets the style name of the stock chart loss marker (chart:stock-loss-marker).
    /// 設定股票圖下跌標記（chart:stock-loss-marker）的樣式名稱。
    /// </summary>
    /// <param name="styleName">The style name; <see langword="null"/> or blank removes the chart:stock-loss-marker element. / 樣式名稱；<see langword="null"/> 或空白會移除 chart:stock-loss-marker 元素。</param>
    public void SetStockLossMarkerStyleName(string? styleName) =>
        SetPlotAreaPartStyleName("stock-loss-marker", styleName);

    /// <summary>
    /// Gets the style name of the stock chart range line (chart:stock-range-line).
    /// 取得股票圖範圍線（chart:stock-range-line）的樣式名稱。
    /// </summary>
    /// <returns>The style name; <see langword="null"/> if not set. / 樣式名稱；若未設定則為 <see langword="null"/>。</returns>
    public string? GetStockRangeLineStyleName() =>
        FindChildElement(FindOrCreatePlotArea(), "stock-range-line", OdfNamespaces.Chart)?.GetAttribute("style-name", OdfNamespaces.Chart);

    /// <summary>
    /// Sets the style name of the stock chart range line (chart:stock-range-line).
    /// 設定股票圖範圍線（chart:stock-range-line）的樣式名稱。
    /// </summary>
    /// <param name="styleName">The style name; <see langword="null"/> or blank removes the chart:stock-range-line element. / 樣式名稱；<see langword="null"/> 或空白會移除 chart:stock-range-line 元素。</param>
    public void SetStockRangeLineStyleName(string? styleName) =>
        SetPlotAreaPartStyleName("stock-range-line", styleName);

    private void SetPlotAreaPartStyleName(string localName, string? styleName)
    {
        OdfNode plotArea = FindOrCreatePlotArea();
        OdfNode? existing = FindChildElement(plotArea, localName, OdfNamespaces.Chart);

        if (string.IsNullOrWhiteSpace(styleName))
        {
            if (existing is not null)
            {
                plotArea.RemoveChild(existing);
            }

            return;
        }

        OdfNode part = existing ?? OdfNodeFactory.CreateElement(localName, OdfNamespaces.Chart, "chart");
        if (existing is null)
        {
            plotArea.AppendChild(part);
        }

        part.SetAttribute("style-name", OdfNamespaces.Chart, styleName!, "chart");
    }
}
