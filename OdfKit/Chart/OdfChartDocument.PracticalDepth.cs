using System;
using System.Collections.Generic;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Chart;

/// <summary>
/// Provides practical high-level chart depth APIs for bubble, stock and 3D charts.
/// 提供泡泡圖、股票圖與 3D 圖表的實務高階深度 API。
/// </summary>
public partial class OdfChartDocument
{
    /// <summary>
    /// Replaces the current series with one or more bubble chart series.
    /// 以一組或多組泡泡圖序列取代目前圖表序列。
    /// </summary>
    /// <param name="series">The bubble chart series requests. / 泡泡圖序列要求。</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="series"/> is <see langword="null"/>. / 當 <paramref name="series"/> 為 <see langword="null"/> 時擲出。</exception>
    public void SetBubbleSeries(IEnumerable<OdfBubbleChartSeriesRequest> series)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(series, nameof(series));

        ChartClass = "chart:bubble";
        OdfNode plotArea = FindOrCreatePlotArea();
        RemoveSeries(plotArea);

        foreach (OdfBubbleChartSeriesRequest request in series)
        {
            ValidateRangeAddress(request.XValuesCellRangeAddress, nameof(request.XValuesCellRangeAddress));
            ValidateRangeAddress(request.YValuesCellRangeAddress, nameof(request.YValuesCellRangeAddress));
            ValidateRangeAddress(request.BubbleSizeCellRangeAddress, nameof(request.BubbleSizeCellRangeAddress));

            OdfNode seriesNode = CreateSeriesNode(
                "chart:bubble",
                request.YValuesCellRangeAddress,
                request.LabelCellAddress,
                request.StyleName);
            AppendDomain(seriesNode, request.XValuesCellRangeAddress, "x-values");
            AppendDomain(seriesNode, request.BubbleSizeCellRangeAddress, "bubble-size");
            plotArea.AppendChild(seriesNode);
        }
    }

    /// <summary>
    /// Gets bubble chart series summaries from the current chart.
    /// 從目前圖表取得泡泡圖序列摘要。
    /// </summary>
    /// <returns>The bubble chart series summaries. / 泡泡圖序列摘要。</returns>
    public IReadOnlyList<OdfBubbleChartSeriesInfo> GetBubbleSeries()
    {
        List<OdfBubbleChartSeriesInfo> result = [];
        foreach (OdfNode seriesNode in EnumerateSeriesNodes())
        {
            List<OdfNode> domains = GetDomainNodes(seriesNode);
            result.Add(new OdfBubbleChartSeriesInfo(
                domains.Count > 0 ? domains[0].GetAttribute("cell-range-address", OdfNamespaces.Table) : null,
                seriesNode.GetAttribute("values-cell-range-address", OdfNamespaces.Chart),
                domains.Count > 1 ? domains[1].GetAttribute("cell-range-address", OdfNamespaces.Table) : null,
                seriesNode.GetAttribute("label-cell-address", OdfNamespaces.Chart),
                seriesNode.GetAttribute("style-name", OdfNamespaces.Chart)));
        }

        return result;
    }

    /// <summary>
    /// Replaces the current series with one or more stock chart series.
    /// 以一組或多組股票圖序列取代目前圖表序列。
    /// </summary>
    /// <param name="series">The stock chart series requests. / 股票圖序列要求。</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="series"/> is <see langword="null"/>. / 當 <paramref name="series"/> 為 <see langword="null"/> 時擲出。</exception>
    public void SetStockSeries(IEnumerable<OdfStockChartSeriesRequest> series)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(series, nameof(series));

        ChartClass = "chart:stock";
        OdfNode plotArea = FindOrCreatePlotArea();
        RemoveSeries(plotArea);

        foreach (OdfStockChartSeriesRequest request in series)
        {
            ValidateRangeAddress(request.OpenCellRangeAddress, nameof(request.OpenCellRangeAddress));
            ValidateRangeAddress(request.HighCellRangeAddress, nameof(request.HighCellRangeAddress));
            ValidateRangeAddress(request.LowCellRangeAddress, nameof(request.LowCellRangeAddress));
            ValidateRangeAddress(request.CloseCellRangeAddress, nameof(request.CloseCellRangeAddress));

            OdfNode seriesNode = CreateSeriesNode(
                "chart:stock",
                request.CloseCellRangeAddress,
                request.LabelCellAddress,
                request.StyleName);
            AppendDomain(seriesNode, request.OpenCellRangeAddress, "open");
            AppendDomain(seriesNode, request.HighCellRangeAddress, "high");
            AppendDomain(seriesNode, request.LowCellRangeAddress, "low");
            if (!string.IsNullOrWhiteSpace(request.VolumeCellRangeAddress))
            {
                AppendDomain(seriesNode, request.VolumeCellRangeAddress!, "volume");
            }

            plotArea.AppendChild(seriesNode);
        }
    }

    /// <summary>
    /// Gets stock chart series summaries from the current chart.
    /// 從目前圖表取得股票圖序列摘要。
    /// </summary>
    /// <returns>The stock chart series summaries. / 股票圖序列摘要。</returns>
    public IReadOnlyList<OdfStockChartSeriesInfo> GetStockSeries()
    {
        List<OdfStockChartSeriesInfo> result = [];
        foreach (OdfNode seriesNode in EnumerateSeriesNodes())
        {
            List<OdfNode> domains = GetDomainNodes(seriesNode);
            result.Add(new OdfStockChartSeriesInfo(
                domains.Count > 0 ? domains[0].GetAttribute("cell-range-address", OdfNamespaces.Table) : null,
                domains.Count > 1 ? domains[1].GetAttribute("cell-range-address", OdfNamespaces.Table) : null,
                domains.Count > 2 ? domains[2].GetAttribute("cell-range-address", OdfNamespaces.Table) : null,
                seriesNode.GetAttribute("values-cell-range-address", OdfNamespaces.Chart),
                domains.Count > 3 ? domains[3].GetAttribute("cell-range-address", OdfNamespaces.Table) : null,
                seriesNode.GetAttribute("label-cell-address", OdfNamespaces.Chart),
                seriesNode.GetAttribute("style-name", OdfNamespaces.Chart)));
        }

        return result;
    }

    /// <summary>
    /// Applies practical 3D chart options to the plot area and automatic styles.
    /// 將實務 3D 圖表選項套用至繪圖區與自動樣式。
    /// </summary>
    /// <param name="options">The 3D options to apply. / 要套用的 3D 選項。</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>. / 當 <paramref name="options"/> 為 <see langword="null"/> 時擲出。</exception>
    public void Apply3DOptions(OdfChart3DOptions options)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(options, nameof(options));

        OdfChartStyle plotStyle = PlotAreaStyle;
        plotStyle.ThreeDimensional = options.Enabled;
        plotStyle.Projection = options.Projection;
        plotStyle.AngleOffset = options.AngleOffset;
        plotStyle.LightingMode = options.LightingMode;

        ClearLights();
        foreach (OdfChartLightRequest light in options.Lights)
        {
            AddLight(light.Direction, light.DiffuseColor, light.Enabled, light.Specular);
        }

        ApplySurfaceStyle(options.WallStyle, SetWallStyleName);
        ApplySurfaceStyle(options.FloorStyle, SetFloorStyleName);
    }

    /// <summary>
    /// Applies practical stock marker styles to the current chart.
    /// 將實務股票圖標記樣式套用至目前圖表。
    /// </summary>
    /// <param name="style">The stock marker style. / 股票圖標記樣式。</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="style"/> is <see langword="null"/>. / 當 <paramref name="style"/> 為 <see langword="null"/> 時擲出。</exception>
    public void ApplyStockMarkerStyle(OdfStockMarkerStyle style)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(style, nameof(style));

        ApplySurfaceStyle(style.GainStyle, SetStockGainMarkerStyleName);
        ApplySurfaceStyle(style.LossStyle, SetStockLossMarkerStyleName);
        ApplySurfaceStyle(style.RangeLineStyle, SetStockRangeLineStyleName);
    }

    private void ApplySurfaceStyle(OdfChartSurfaceStyle? style, Action<string?> applyStyleName)
    {
        if (style is null)
        {
            applyStyleName(null);
            return;
        }

        ValidateRangeAddress(style.StyleName, nameof(style.StyleName));
        OdfChartStyle chartStyle = CreateChartStyle(style.StyleName);
        chartStyle.FillColor = style.FillColor;
        chartStyle.StrokeColor = style.StrokeColor;
        chartStyle.StrokeWidth = style.StrokeWidth;
        chartStyle.Fill = style.Fill;
        chartStyle.Stroke = style.Stroke;
        applyStyleName(chartStyle.Name);
    }

    private static OdfNode CreateSeriesNode(string chartClass, string valuesRange, string? labelAddress, string? styleName)
    {
        OdfNode seriesNode = OdfNodeFactory.CreateElement("series", OdfNamespaces.Chart, "chart");
        seriesNode.SetAttribute("class", OdfNamespaces.Chart, chartClass, "chart");
        seriesNode.SetAttribute("values-cell-range-address", OdfNamespaces.Chart, valuesRange, "chart");
        if (!string.IsNullOrWhiteSpace(labelAddress))
        {
            seriesNode.SetAttribute("label-cell-address", OdfNamespaces.Chart, labelAddress!, "chart");
        }

        if (!string.IsNullOrWhiteSpace(styleName))
        {
            seriesNode.SetAttribute("style-name", OdfNamespaces.Chart, styleName!, "chart");
        }

        return seriesNode;
    }

    private static void AppendDomain(OdfNode seriesNode, string cellRangeAddress, string role)
    {
        OdfNode domain = OdfNodeFactory.CreateElement("domain", OdfNamespaces.Chart, "chart");
        domain.SetAttribute("cell-range-address", OdfNamespaces.Table, cellRangeAddress, "table");
        domain.SetAttribute("name", OdfNamespaces.Chart, role, "chart");
        seriesNode.AppendChild(domain);
    }

    private static void RemoveSeries(OdfNode plotArea)
    {
        foreach (OdfNode child in new List<OdfNode>(plotArea.Children))
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "series" &&
                child.NamespaceUri == OdfNamespaces.Chart)
            {
                plotArea.RemoveChild(child);
            }
        }
    }

    private IEnumerable<OdfNode> EnumerateSeriesNodes()
    {
        OdfNode? plotArea = FindChildElement(GetChartNode(), "plot-area", OdfNamespaces.Chart);
        if (plotArea is null)
        {
            yield break;
        }

        foreach (OdfNode child in plotArea.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "series" &&
                child.NamespaceUri == OdfNamespaces.Chart)
            {
                yield return child;
            }
        }
    }

    private static List<OdfNode> GetDomainNodes(OdfNode seriesNode)
    {
        List<OdfNode> domains = [];
        foreach (OdfNode child in seriesNode.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "domain" &&
                child.NamespaceUri == OdfNamespaces.Chart)
            {
                domains.Add(child);
            }
        }

        return domains;
    }

    private static void ValidateRangeAddress(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfChartDocument_DataCannotBeEmpty"), parameterName);
        }
    }
}
