using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Chart;

public partial class OdfChartDocument
{
    /// <summary>
    /// 取得 3D 圖表牆面（chart:wall）的樣式名稱。
    /// </summary>
    /// <returns>樣式名稱；若未設定則為 <see langword="null"/>。</returns>
    public string? GetWallStyleName() =>
        FindChildElement(FindOrCreatePlotArea(), "wall", OdfNamespaces.Chart)?.GetAttribute("style-name", OdfNamespaces.Chart);

    /// <summary>
    /// 設定 3D 圖表牆面（chart:wall）的樣式名稱。
    /// </summary>
    /// <param name="styleName">樣式名稱；<see langword="null"/> 或空白會移除 chart:wall 元素。</param>
    public void SetWallStyleName(string? styleName) =>
        SetPlotAreaPartStyleName("wall", styleName);

    /// <summary>
    /// 取得 3D 圖表地板（chart:floor）的樣式名稱。
    /// </summary>
    /// <returns>樣式名稱；若未設定則為 <see langword="null"/>。</returns>
    public string? GetFloorStyleName() =>
        FindChildElement(FindOrCreatePlotArea(), "floor", OdfNamespaces.Chart)?.GetAttribute("style-name", OdfNamespaces.Chart);

    /// <summary>
    /// 設定 3D 圖表地板（chart:floor）的樣式名稱。
    /// </summary>
    /// <param name="styleName">樣式名稱；<see langword="null"/> 或空白會移除 chart:floor 元素。</param>
    public void SetFloorStyleName(string? styleName) =>
        SetPlotAreaPartStyleName("floor", styleName);

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
