using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Chart;

/// <summary>
/// 表示可編輯的圖表自動樣式（<c>style:family="chart"</c>）。
/// </summary>
public sealed class OdfChartStyle
{
    private readonly OdfChartDocument _document;
    private readonly OdfNode _styleNode;

    internal OdfChartStyle(OdfChartDocument document, OdfNode styleNode)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _styleNode = styleNode ?? throw new ArgumentNullException(nameof(styleNode));
    }

    /// <summary>
    /// 取得樣式名稱。
    /// </summary>
    public string Name =>
        _styleNode.GetAttribute("name", OdfNamespaces.Style)
        ?? throw new InvalidOperationException("圖表樣式節點缺少 style:name 屬性。");

    /// <summary>
    /// 取得或設定填滿色（對應 <c>draw:fill-color</c>）。
    /// </summary>
    public string? FillColor
    {
        get => _document.StyleEngine.GetStyleProperty(Name, "fill-color", OdfNamespaces.Draw, "chart");
        set => SetGraphicProperty("fill-color", OdfNamespaces.Draw, value, "draw");
    }

    /// <summary>
    /// 取得或設定筆觸色（對應 <c>svg:stroke-color</c>）。
    /// </summary>
    public string? StrokeColor
    {
        get => _document.StyleEngine.GetStyleProperty(Name, "stroke-color", OdfNamespaces.Svg, "chart");
        set => SetGraphicProperty("stroke-color", OdfNamespaces.Svg, value, "svg");
    }

    /// <summary>
    /// 取得或設定筆觸寬度（對應 <c>svg:stroke-width</c>）。
    /// </summary>
    public string? StrokeWidth
    {
        get => _document.StyleEngine.GetStyleProperty(Name, "stroke-width", OdfNamespaces.Svg, "chart");
        set => SetGraphicProperty("stroke-width", OdfNamespaces.Svg, value, "svg");
    }

    /// <summary>
    /// 建立此樣式的高階摘要。
    /// </summary>
    /// <returns>圖表樣式摘要。</returns>
    public OdfChartStyleInfo ToInfo() =>
        new(Name, FillColor, StrokeColor, StrokeWidth);

    private void SetGraphicProperty(string attributeName, string namespaceUri, string? value, string prefix)
    {
        OdfNode properties = GetOrCreateGraphicProperties();
        if (string.IsNullOrEmpty(value))
        {
            properties.RemoveAttribute(attributeName, namespaceUri);
            if (properties.Attributes.Count == 0 && properties.Children.Count == 0)
            {
                _styleNode.RemoveChild(properties);
            }
        }
        else
        {
            properties.SetAttribute(attributeName, namespaceUri, value!, prefix);
        }

        _document.StyleEngine.RebuildStyleIndex();
    }

    private OdfNode GetOrCreateGraphicProperties()
    {
        foreach (OdfNode child in _styleNode.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "graphic-properties" &&
                child.NamespaceUri == OdfNamespaces.Style)
            {
                return child;
            }
        }

        var properties = new OdfNode(OdfNodeType.Element, "graphic-properties", OdfNamespaces.Style, "style");
        _styleNode.AppendChild(properties);
        return properties;
    }
}
