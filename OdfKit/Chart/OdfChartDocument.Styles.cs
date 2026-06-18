using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Chart;

public partial class OdfChartDocument
{
    /// <summary>
    /// 建立或取得指定名稱的圖表自動樣式。
    /// </summary>
    /// <param name="name">樣式名稱。</param>
    /// <returns>可編輯的圖表樣式物件。</returns>
    /// <exception cref="ArgumentException">當 <paramref name="name"/> 為空白時擲出。</exception>
    /// <exception cref="InvalidOperationException">當名稱已被非圖表樣式使用時擲出。</exception>
    public OdfChartStyle CreateChartStyle(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("樣式名稱不可為空白。", nameof(name));
        }

        OdfNode? existing = FindAutomaticStyleNode(name);
        if (existing is not null)
        {
            string? family = existing.GetAttribute("family", OdfNamespaces.Style);
            if (!string.Equals(family, "chart", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"樣式名稱 '{name}' 已存在且不是圖表樣式。");
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
    /// <param name="name">樣式名稱。</param>
    /// <returns>圖表樣式摘要；若不存在或不是圖表樣式則為 <see langword="null"/>。</returns>
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

        return new OdfChartStyleInfo(
            name,
            StyleEngine.GetStyleProperty(name, "fill-color", OdfNamespaces.Draw, "chart"),
            StyleEngine.GetStyleProperty(name, "stroke-color", OdfNamespaces.Svg, "chart"),
            StyleEngine.GetStyleProperty(name, "stroke-width", OdfNamespaces.Svg, "chart"));
    }

    /// <summary>
    /// 移除指定名稱的圖表自動樣式。
    /// </summary>
    /// <param name="name">樣式名稱。</param>
    /// <returns>若成功移除則為 <see langword="true"/>；找不到或非圖表樣式時為 <see langword="false"/>。</returns>
    public bool RemoveChartStyle(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("樣式名稱不可為空白。", nameof(name));
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
    /// <returns>圖表樣式摘要清單。</returns>
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

            styles.Add(new OdfChartStyleInfo(
                name!,
                StyleEngine.GetStyleProperty(name!, "fill-color", OdfNamespaces.Draw, "chart"),
                StyleEngine.GetStyleProperty(name!, "stroke-color", OdfNamespaces.Svg, "chart"),
                StyleEngine.GetStyleProperty(name!, "stroke-width", OdfNamespaces.Svg, "chart")));
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
