using System;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
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
        ?? throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfChartStyle_ChartNotFound"));

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
    /// 取得或設定填滿樣式（對應 <c>draw:fill</c>）。
    /// </summary>
    public string? Fill
    {
        get => _document.StyleEngine.GetStyleProperty(Name, "fill", OdfNamespaces.Draw, "chart");
        set => SetGraphicProperty("fill", OdfNamespaces.Draw, value, "draw");
    }

    /// <summary>
    /// 取得或設定筆觸樣式（對應 <c>draw:stroke</c>）。
    /// </summary>
    public string? Stroke
    {
        get => _document.StyleEngine.GetStyleProperty(Name, "stroke", OdfNamespaces.Draw, "chart");
        set => SetGraphicProperty("stroke", OdfNamespaces.Draw, value, "draw");
    }

    /// <summary>
    /// 取得或設定圖表是否為 3D 顯示（對應 <c>chart:three-dimensional</c>）。
    /// </summary>
    public bool? ThreeDimensional
    {
        get
        {
            string? val = _document.StyleEngine.GetStyleProperty(Name, "three-dimensional", OdfNamespaces.Chart, "chart");
            if (bool.TryParse(val, out bool result))
                return result;
            return null;
        }
        set => SetChartProperty("three-dimensional", OdfNamespaces.Chart, value?.ToString().ToLowerInvariant(), "chart");
    }

    /// <summary>
    /// 取得或設定 3D 投影的旋轉角度偏置（對應 <c>chart:angle-offset</c>）。
    /// </summary>
    public int? AngleOffset
    {
        get
        {
            string? val = _document.StyleEngine.GetStyleProperty(Name, "angle-offset", OdfNamespaces.Chart, "chart");
            if (int.TryParse(val, out int result))
                return result;
            return null;
        }
        set => SetChartProperty("angle-offset", OdfNamespaces.Chart, value?.ToString(CultureInfo.InvariantCulture), "chart");
    }

    /// <summary>
    /// 取得或設定座標軸刻度標籤的位置（對應 <c>chart:label-position</c>）。
    /// </summary>
    /// <remarks>常見值包含 <c>near-axis</c>、<c>near-axis-other-side</c>、<c>outside-start</c> 與 <c>outside-end</c>。</remarks>
    public string? LabelPosition
    {
        get => _document.StyleEngine.GetStyleProperty(Name, "label-position", OdfNamespaces.Chart, "chart");
        set => SetChartProperty("label-position", OdfNamespaces.Chart, value, "chart");
    }

    /// <summary>
    /// 取得或設定座標軸負值刻度標籤的位置（對應 <c>chart:label-position-negative</c>）。
    /// </summary>
    public string? LabelPositionNegative
    {
        get => _document.StyleEngine.GetStyleProperty(Name, "label-position-negative", OdfNamespaces.Chart, "chart");
        set => SetChartProperty("label-position-negative", OdfNamespaces.Chart, value, "chart");
    }

    /// <summary>
    /// 取得或設定座標軸標籤相對於座標軸的對齊位置（對應 <c>chart:axis-label-position</c>）。
    /// </summary>
    /// <remarks>常見值包含 <c>near-axis</c>、<c>near-axis-other-side</c>、<c>outside-start</c> 與 <c>outside-end</c>。</remarks>
    public string? AxisLabelPosition
    {
        get => _document.StyleEngine.GetStyleProperty(Name, "axis-label-position", OdfNamespaces.Chart, "chart");
        set => SetChartProperty("axis-label-position", OdfNamespaces.Chart, value, "chart");
    }

    /// <summary>
    /// 取得或設定 3D 投影模式（對應 <c>dr3d:projection</c>）。
    /// </summary>
    public OdfDr3dProjection? Projection
    {
        get
        {
            string? val = _document.StyleEngine.GetStyleProperty(Name, "projection", OdfNamespaces.Dr3d, "chart");
            return val switch
            {
                "parallel" => OdfDr3dProjection.Parallel,
                "perspective" => OdfDr3dProjection.Perspective,
                _ => null,
            };
        }
        set
        {
            string? token = value switch
            {
                OdfDr3dProjection.Parallel => "parallel",
                OdfDr3dProjection.Perspective => "perspective",
                _ => null,
            };
            SetGraphicProperty("projection", OdfNamespaces.Dr3d, token, "dr3d");
        }
    }

    /// <summary>
    /// 取得或設定是否啟用雙面光照模式（對應 <c>dr3d:lighting-mode</c>）。
    /// </summary>
    public bool? LightingMode
    {
        get
        {
            string? val = _document.StyleEngine.GetStyleProperty(Name, "lighting-mode", OdfNamespaces.Dr3d, "chart");
            return bool.TryParse(val, out bool result) ? result : null;
        }
        set => SetGraphicProperty("lighting-mode", OdfNamespaces.Dr3d, value?.ToString().ToLowerInvariant(), "dr3d");
    }

    /// <summary>
    /// 建立此樣式的高階摘要。
    /// </summary>
    /// <returns>圖表樣式摘要。</returns>
    public OdfChartStyleInfo ToInfo() =>
        new(Name, FillColor, StrokeColor, StrokeWidth, Fill, Stroke, ThreeDimensional, AngleOffset);

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

    private void SetChartProperty(string attributeName, string namespaceUri, string? value, string prefix)
    {
        OdfNode properties = GetOrCreateChartProperties();
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

    private OdfNode GetOrCreateChartProperties()
    {
        foreach (OdfNode child in _styleNode.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "chart-properties" &&
                child.NamespaceUri == OdfNamespaces.Style)
            {
                return child;
            }
        }

        var properties = new OdfNode(OdfNodeType.Element, "chart-properties", OdfNamespaces.Style, "style");
        _styleNode.AppendChild(properties);
        return properties;
    }
}
