using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Chart;

public partial class OdfChartDocument
{
    #region Series & Axis

    /// <summary>
    /// 設定 X 軸分類標籤的儲存格範圍位址。
    /// </summary>
    /// <param name="cellRangeAddress">分類標籤的儲存格範圍位址。</param>
    public void SetCategories(string cellRangeAddress)
    {
        if (string.IsNullOrWhiteSpace(cellRangeAddress))
        {
            throw new ArgumentException("分類標籤範圍位址不能為空。", nameof(cellRangeAddress));
        }

        OdfNode axis = FindOrCreateAxis("x");
        OdfNode categories = FindOrCreateChild(axis, "categories", OdfNamespaces.Chart, "chart");
        categories.SetAttribute("cell-range-address", OdfNamespaces.Table, cellRangeAddress, "table");
    }

    /// <summary>
    /// 取得指定維度座標軸的標題。
    /// </summary>
    /// <param name="dimension">座標軸維度，例如 x、y 或 z。</param>
    /// <returns>座標軸標題；若未設定則為 <see langword="null"/>。</returns>
    public string? GetAxisTitle(string dimension)
    {
        ValidateAxisDimension(dimension);

        OdfNode? axis = FindAxis(dimension);
        OdfNode? title = axis is null ? null : FindChildElement(axis, "title", OdfNamespaces.Chart);
        OdfNode? paragraph = title is null ? null : FindChildElement(title, "p", OdfNamespaces.Text);
        string? text = paragraph?.TextContent;
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>
    /// 設定指定維度座標軸的標題。
    /// </summary>
    /// <param name="dimension">座標軸維度，例如 x、y 或 z。</param>
    /// <param name="title">座標軸標題；空白值會移除既有標題。</param>
    public void SetAxisTitle(string dimension, string? title)
    {
        ValidateAxisDimension(dimension);

        OdfNode? existingAxis = FindAxis(dimension);
        if (string.IsNullOrWhiteSpace(title))
        {
            OdfNode? existingTitle = existingAxis is null
                ? null
                : FindChildElement(existingAxis, "title", OdfNamespaces.Chart);
            if (existingTitle is not null)
            {
                existingAxis!.RemoveChild(existingTitle);
            }

            return;
        }

        OdfNode axis = existingAxis ?? FindOrCreateAxis(dimension);
        OdfNode titleNode = FindOrCreateAxisTitle(axis);
        OdfNode paragraph = FindOrCreateChild(titleNode, "p", OdfNamespaces.Text, "text");
        paragraph.TextContent = title!;
    }

    /// <summary>
    /// 新增資料序列佔位節點。
    /// </summary>
    /// <param name="valuesCellRangeAddress">資料值儲存格範圍位址。</param>
    /// <param name="labelCellAddress">選用的標籤儲存格位址。</param>
    /// <returns>新增的序列節點。</returns>
    public OdfNode AddSeries(string valuesCellRangeAddress, string? labelCellAddress = null)
    {
        if (string.IsNullOrWhiteSpace(valuesCellRangeAddress))
        {
            throw new ArgumentException("資料範圍位址不能為空。", nameof(valuesCellRangeAddress));
        }

        OdfNode plotArea = FindOrCreatePlotArea();
        OdfNode series = OdfNodeFactory.CreateElement("series", OdfNamespaces.Chart, "chart");
        series.SetAttribute("values-cell-range-address", OdfNamespaces.Chart, valuesCellRangeAddress, "chart");
        if (!string.IsNullOrWhiteSpace(labelCellAddress))
        {
            series.SetAttribute("label-cell-address", OdfNamespaces.Chart, labelCellAddress!, "chart");
        }

        plotArea.AppendChild(series);
        return series;
    }

    private OdfNode FindOrCreatePlotArea()
    {
        return FindOrCreateChild(GetChartNode(), "plot-area", OdfNamespaces.Chart, "chart");
    }

    private OdfNode FindOrCreateAxis(string dimension)
    {
        OdfNode plotArea = FindOrCreatePlotArea();
        OdfNode? existingAxis = FindAxis(plotArea, dimension);
        if (existingAxis is not null)
        {
            return existingAxis;
        }

        OdfNode axis = OdfNodeFactory.CreateElement("axis", OdfNamespaces.Chart, "chart");
        axis.SetAttribute("dimension", OdfNamespaces.Chart, dimension, "chart");
        OdfNode? firstSeries = FindChildElement(plotArea, "series", OdfNamespaces.Chart);
        if (firstSeries is null)
        {
            plotArea.AppendChild(axis);
        }
        else
        {
            plotArea.InsertBefore(axis, firstSeries);
        }

        return axis;
    }

    private OdfNode? FindAxis(string dimension)
    {
        OdfNode? plotArea = FindChildElement(GetChartNode(), "plot-area", OdfNamespaces.Chart);
        return plotArea is null ? null : FindAxis(plotArea, dimension);
    }

    private static OdfNode? FindAxis(OdfNode plotArea, string dimension)
    {
        foreach (OdfNode child in plotArea.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "axis" &&
                child.NamespaceUri == OdfNamespaces.Chart &&
                string.Equals(child.GetAttribute("dimension", OdfNamespaces.Chart), dimension, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static OdfNode FindOrCreateAxisTitle(OdfNode axis)
    {
        OdfNode? existingTitle = FindChildElement(axis, "title", OdfNamespaces.Chart);
        if (existingTitle is not null)
        {
            return existingTitle;
        }

        OdfNode title = OdfNodeFactory.CreateElement("title", OdfNamespaces.Chart, "chart");
        OdfNode? categories = FindChildElement(axis, "categories", OdfNamespaces.Chart);
        if (categories is not null)
        {
            axis.InsertBefore(title, categories);
        }
        else
        {
            axis.AppendChild(title);
        }

        return title;
    }

    private OdfNode? FindCategoriesNode()
    {
        OdfNode? plotArea = FindChildElement(GetChartNode(), "plot-area", OdfNamespaces.Chart);
        if (plotArea is null)
        {
            return null;
        }

        OdfNode? fallbackCategories = null;
        foreach (OdfNode child in plotArea.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName != "axis" ||
                child.NamespaceUri != OdfNamespaces.Chart)
            {
                continue;
            }

            OdfNode? categories = FindChildElement(child, "categories", OdfNamespaces.Chart);
            if (categories is null)
            {
                continue;
            }

            fallbackCategories ??= categories;
            if (string.Equals(child.GetAttribute("dimension", OdfNamespaces.Chart), "x", StringComparison.Ordinal))
            {
                return categories;
            }
        }

        return fallbackCategories;
    }

    private IReadOnlyList<OdfChartSeriesInfo> GetSeries()
    {
        OdfNode? plotArea = FindChildElement(GetChartNode(), "plot-area", OdfNamespaces.Chart);
        if (plotArea is null)
        {
            return [];
        }

        List<OdfChartSeriesInfo> series = [];
        foreach (OdfNode child in plotArea.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName != "series" ||
                child.NamespaceUri != OdfNamespaces.Chart)
            {
                continue;
            }

            string? valuesCellRangeAddress = child.GetAttribute("values-cell-range-address", OdfNamespaces.Chart);
            if (string.IsNullOrWhiteSpace(valuesCellRangeAddress))
            {
                continue;
            }

            string rangeAddress = valuesCellRangeAddress!;
            series.Add(new OdfChartSeriesInfo(
                rangeAddress,
                child.GetAttribute("label-cell-address", OdfNamespaces.Chart),
                child.GetAttribute("class", OdfNamespaces.Chart),
                child.GetAttribute("style-name", OdfNamespaces.Chart),
                child.GetAttribute("attached-axis", OdfNamespaces.Chart)));
        }

        return series;
    }

    private static void ValidateAxisDimension(string dimension)
    {
        if (string.IsNullOrWhiteSpace(dimension))
        {
            throw new ArgumentException("座標軸維度不能為空。", nameof(dimension));
        }
    }

    #endregion
}
