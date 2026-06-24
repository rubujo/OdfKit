using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Chart;

public partial class OdfChartDocument
{
    /// <summary>
    /// 取得或設定繪圖區的圖表自動樣式。
    /// </summary>
    public OdfChartStyle PlotAreaStyle
    {
        get
        {
            OdfNode plotArea = FindOrCreatePlotArea();
            string? name = plotArea.GetAttribute("style-name", OdfNamespaces.Chart);
            if (string.IsNullOrEmpty(name))
            {
                name = "plot-area-style";
                plotArea.SetAttribute("style-name", OdfNamespaces.Chart, name, "chart");
            }
            return CreateChartStyle(name!);
        }
        set
        {
            OdfNode plotArea = FindOrCreatePlotArea();
            if (value is null)
            {
                plotArea.RemoveAttribute("style-name", OdfNamespaces.Chart);
            }
            else
            {
                plotArea.SetAttribute("style-name", OdfNamespaces.Chart, value.Name, "chart");
            }
        }
    }

    /// <summary>
    /// 取得或設定主要 Y 軸格線的圖表自動樣式。
    /// </summary>
    public OdfChartStyle GridStyle
    {
        get
        {
            OdfNode axis = FindOrCreateAxis("y");
            OdfNode grid = FindOrCreateChild(axis, "grid", OdfNamespaces.Chart, "chart");
            string? name = grid.GetAttribute("style-name", OdfNamespaces.Chart);
            if (string.IsNullOrEmpty(name))
            {
                name = "grid-style";
                grid.SetAttribute("style-name", OdfNamespaces.Chart, name, "chart");
            }
            return CreateChartStyle(name!);
        }
        set
        {
            OdfNode axis = FindOrCreateAxis("y");
            OdfNode grid = FindOrCreateChild(axis, "grid", OdfNamespaces.Chart, "chart");
            if (value is null)
            {
                grid.RemoveAttribute("style-name", OdfNamespaces.Chart);
            }
            else
            {
                grid.SetAttribute("style-name", OdfNamespaces.Chart, value.Name, "chart");
            }
        }
    }

    /// <summary>
    /// 取得或設定圖例的圖表自動樣式（背景色、邊框等）。
    /// </summary>
    public OdfChartStyle LegendStyle
    {
        get => Legend.Style;
        set
        {
            if (value is null)
            {
                Legend.EnsureVisible();
                Legend.StyleName = null;
            }
            else
            {
                Legend.Style = value;
            }
        }
    }

    /// <summary>
    /// 建立或取得指定名稱的圖表自動樣式。
    /// </summary>
    /// <param name="name">樣式名稱</param>
    /// <returns>可編輯的圖表樣式物件</returns>
    /// <exception cref="ArgumentException">當 <paramref name="name"/> 為空白時擲出</exception>
    /// <exception cref="InvalidOperationException">當名稱已被非圖表樣式使用時擲出</exception>
    public OdfChartStyle CreateChartStyle(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfChartDocument_StyleCannotBeEmpty_2"), nameof(name));
        }

        OdfNode? existing = FindAutomaticStyleNode(name);
        if (existing is not null)
        {
            string? family = existing.GetAttribute("family", OdfNamespaces.Style);
            if (!string.Equals(family, "chart", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfChartDocument_StyleNameAlreadyExists", name));
            }

            return new OdfChartStyle(this, existing);
        }

        OdfNode autoStyles = GetOrCreateAutomaticStylesNode();
        var styleNode = new OdfNode(OdfNodeType.Element, "style", OdfNamespaces.Style, "style");
        styleNode.SetAttribute("name", OdfNamespaces.Style, name, "style");
        styleNode.SetAttribute("family", OdfNamespaces.Style, "chart", "style");
        autoStyles.AppendChild(styleNode);
        StyleEngine.RebuildStyleIndex();
        return new OdfChartStyle(this, styleNode);
    }

    /// <summary>
    /// 嘗試取得指定名稱的圖表樣式摘要。
    /// </summary>
    /// <param name="name">樣式名稱</param>
    /// <returns>圖表樣式摘要；若不存在或不是圖表樣式則為 <see langword="null"/></returns>
    public OdfChartStyleInfo? TryGetChartStyle(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        OdfNode? styleNode = FindAutomaticStyleNode(name);
        if (styleNode is null ||
            !string.Equals(styleNode.GetAttribute("family", OdfNamespaces.Style), "chart", StringComparison.Ordinal))
        {
            return null;
        }

        string? fillVal = StyleEngine.GetStyleProperty(name, "fill", OdfNamespaces.Draw, "chart");
        string? strokeVal = StyleEngine.GetStyleProperty(name, "stroke", OdfNamespaces.Draw, "chart");
        string? threeDVal = StyleEngine.GetStyleProperty(name, "three-dimensional", OdfNamespaces.Chart, "chart");
        string? angleOffsetVal = StyleEngine.GetStyleProperty(name, "angle-offset", OdfNamespaces.Chart, "chart");

        bool? threeD = bool.TryParse(threeDVal, out bool b) ? b : null;
        int? angleOffset = int.TryParse(angleOffsetVal, out int i) ? i : null;

        return new OdfChartStyleInfo(
            name,
            StyleEngine.GetStyleProperty(name, "fill-color", OdfNamespaces.Draw, "chart"),
            StyleEngine.GetStyleProperty(name, "stroke-color", OdfNamespaces.Svg, "chart"),
            StyleEngine.GetStyleProperty(name, "stroke-width", OdfNamespaces.Svg, "chart"),
            fillVal,
            strokeVal,
            threeD,
            angleOffset);
    }

    /// <summary>
    /// 移除指定名稱的圖表自動樣式。
    /// </summary>
    /// <param name="name">樣式名稱</param>
    /// <returns>若成功移除則為 <see langword="true"/>；找不到或非圖表樣式時為 <see langword="false"/></returns>
    public bool RemoveChartStyle(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfChartDocument_StyleCannotBeEmpty_2"), nameof(name));
        }

        OdfNode? styleNode = FindAutomaticStyleNode(name);
        if (styleNode is null ||
            !string.Equals(styleNode.GetAttribute("family", OdfNamespaces.Style), "chart", StringComparison.Ordinal))
        {
            return false;
        }

        styleNode.Parent?.RemoveChild(styleNode);
        StyleEngine.RebuildStyleIndex();
        return true;
    }

    /// <summary>
    /// 取得文件中所有圖表自動樣式的摘要清單。
    /// </summary>
    /// <returns>圖表樣式摘要清單</returns>
    public IReadOnlyList<OdfChartStyleInfo> GetChartStyles()
    {
        var styles = new List<OdfChartStyleInfo>();
        OdfNode? autoStyles = ContentDom.FindChildElement("automatic-styles", OdfNamespaces.Office);
        if (autoStyles is null)
        {
            return styles;
        }

        foreach (OdfNode child in autoStyles.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName is not "style" ||
                child.NamespaceUri != OdfNamespaces.Style)
            {
                continue;
            }

            string? name = child.GetAttribute("name", OdfNamespaces.Style);
            if (string.IsNullOrEmpty(name) ||
                !string.Equals(child.GetAttribute("family", OdfNamespaces.Style), "chart", StringComparison.Ordinal))
            {
                continue;
            }

            string? fillVal = StyleEngine.GetStyleProperty(name!, "fill", OdfNamespaces.Draw, "chart");
            string? strokeVal = StyleEngine.GetStyleProperty(name!, "stroke", OdfNamespaces.Draw, "chart");
            string? threeDVal = StyleEngine.GetStyleProperty(name!, "three-dimensional", OdfNamespaces.Chart, "chart");
            string? angleOffsetVal = StyleEngine.GetStyleProperty(name!, "angle-offset", OdfNamespaces.Chart, "chart");

            bool? threeD = bool.TryParse(threeDVal, out bool b) ? b : null;
            int? angleOffset = int.TryParse(angleOffsetVal, out int i) ? i : null;

            styles.Add(new OdfChartStyleInfo(
                name!,
                StyleEngine.GetStyleProperty(name!, "fill-color", OdfNamespaces.Draw, "chart"),
                StyleEngine.GetStyleProperty(name!, "stroke-color", OdfNamespaces.Svg, "chart"),
                StyleEngine.GetStyleProperty(name!, "stroke-width", OdfNamespaces.Svg, "chart"),
                fillVal,
                strokeVal,
                threeD,
                angleOffset));
        }

        return styles;
    }

    private OdfNode GetOrCreateAutomaticStylesNode()
    {
        OdfNode? autoStyles = ContentDom.FindChildElement("automatic-styles", OdfNamespaces.Office);
        if (autoStyles is not null)
        {
            return autoStyles;
        }

        autoStyles = new OdfNode(OdfNodeType.Element, "automatic-styles", OdfNamespaces.Office, "office");
        if (ContentDom.Children.Count > 0)
        {
            ContentDom.InsertBefore(autoStyles, ContentDom.Children[0]);
        }
        else
        {
            ContentDom.AppendChild(autoStyles);
        }

        return autoStyles;
    }

    private OdfNode? FindAutomaticStyleNode(string name)
    {
        OdfNode? autoStyles = ContentDom.FindChildElement("automatic-styles", OdfNamespaces.Office);
        if (autoStyles is null)
        {
            return null;
        }

        foreach (OdfNode child in autoStyles.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "style" &&
                child.NamespaceUri == OdfNamespaces.Style &&
                string.Equals(child.GetAttribute("name", OdfNamespaces.Style), name, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }
}
