using System;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Chart;

public partial class OdfChartDocument
{
    /// <summary>
    /// Finds summary information for the axis of the specified dimension.
    /// 尋找指定維度座標軸的摘要資訊。
    /// </summary>
    /// <param name="dimension">The axis dimension, e.g. <c>x</c>, <c>y</c>, or <c>z</c>. / 座標軸維度，例如 <c>x</c>、<c>y</c> 或 <c>z</c>。</param>
    /// <returns>The axis summary; <see langword="null"/> if it does not exist. / 座標軸摘要；若不存在則為 <see langword="null"/>。</returns>
    public OdfChartAxisInfo? FindAxisInfo(string dimension)
    {
        ValidateAxisDimension(dimension);
        OdfNode? axis = FindAxis(dimension);
        if (axis is null)
            return null;

        return new OdfChartAxisInfo(
            dimension,
            FindAxisTitle(dimension),
            GetChartBool(axis, "logarithmic"),
            GetChartBool(axis, "reverse-direction"),
            GetChartDouble(axis, "minimum"),
            GetChartDouble(axis, "maximum"),
            GetChartBool(axis, "display-label"),
            HasAxisGrid(axis, OdfChartGridKind.Major),
            HasAxisGrid(axis, OdfChartGridKind.Minor),
            axis.GetAttribute("style-name", OdfNamespaces.Chart));
    }

    /// <summary>
    /// Sets whether the axis of the specified dimension uses a logarithmic scale.
    /// 設定指定維度座標軸是否採用對數刻度。
    /// </summary>
    /// <param name="dimension">The axis dimension. / 座標軸維度。</param>
    /// <param name="logarithmic">The logarithmic scale setting; <see langword="null"/> removes the attribute. / 對數刻度設定；<see langword="null"/> 表示移除屬性。</param>
    public void SetAxisLogarithmic(string dimension, bool? logarithmic) =>
        SetAxisChartBool(dimension, "logarithmic", logarithmic);

    /// <summary>
    /// Sets whether the axis of the specified dimension is displayed in reverse direction.
    /// 設定指定維度座標軸是否反向顯示。
    /// </summary>
    /// <param name="dimension">The axis dimension. / 座標軸維度。</param>
    /// <param name="reverseDirection">The reverse direction setting; <see langword="null"/> removes the attribute. / 反向顯示設定；<see langword="null"/> 表示移除屬性。</param>
    public void SetAxisReverseDirection(string dimension, bool? reverseDirection) =>
        SetAxisChartBool(dimension, "reverse-direction", reverseDirection);

    /// <summary>
    /// Sets whether the axis of the specified dimension displays scale labels.
    /// 設定指定維度座標軸是否顯示刻度標籤。
    /// </summary>
    /// <param name="dimension">The axis dimension. / 座標軸維度。</param>
    /// <param name="displayLabels">The display labels setting; <see langword="null"/> removes the attribute. / 顯示標籤設定；<see langword="null"/> 表示移除屬性。</param>
    public void SetAxisDisplayLabels(string dimension, bool? displayLabels) =>
        SetAxisChartBool(dimension, "display-label", displayLabels);

    /// <summary>
    /// Sets the minimum scale value of the axis for the specified dimension.
    /// 設定指定維度座標軸的刻度最小值。
    /// </summary>
    /// <param name="dimension">The axis dimension. / 座標軸維度。</param>
    /// <param name="minimum">The minimum value; <see langword="null"/> removes the attribute. / 最小值；<see langword="null"/> 表示移除屬性。</param>
    public void SetAxisMinimum(string dimension, double? minimum) =>
        SetAxisChartDouble(dimension, "minimum", minimum);

    /// <summary>
    /// Sets the maximum scale value of the axis for the specified dimension.
    /// 設定指定維度座標軸的刻度最大值。
    /// </summary>
    /// <param name="dimension">The axis dimension. / 座標軸維度。</param>
    /// <param name="maximum">The maximum value; <see langword="null"/> removes the attribute. / 最大值；<see langword="null"/> 表示移除屬性。</param>
    public void SetAxisMaximum(string dimension, double? maximum) =>
        SetAxisChartDouble(dimension, "maximum", maximum);

    /// <summary>
    /// Sets the style name of the axis for the specified dimension.
    /// 設定指定維度座標軸的樣式名稱。
    /// </summary>
    /// <param name="dimension">The axis dimension. / 座標軸維度。</param>
    /// <param name="styleName">The style name; a blank value removes the attribute. / 樣式名稱；空白值會移除屬性。</param>
    public void SetAxisStyleName(string dimension, string? styleName)
    {
        ValidateAxisDimension(dimension);
        OdfNode axis = FindOrCreateAxis(dimension);
        if (string.IsNullOrWhiteSpace(styleName))
            axis.RemoveAttribute("style-name", OdfNamespaces.Chart);
        else
            axis.SetAttribute("style-name", OdfNamespaces.Chart, styleName!, "chart");
    }

    /// <summary>
    /// Sets the grid line visibility of the axis for the specified dimension.
    /// 設定指定維度座標軸的網格線可見性。
    /// </summary>
    /// <param name="dimension">The axis dimension. / 座標軸維度。</param>
    /// <param name="gridKind">The grid line kind. / 網格線類型。</param>
    /// <param name="visible">Whether the grid line is displayed. / 是否顯示。</param>
    public void SetAxisGrid(string dimension, OdfChartGridKind gridKind, bool visible)
    {
        ValidateAxisDimension(dimension);
        OdfNode axis = FindOrCreateAxis(dimension);
        SetAxisGrid(axis, gridKind, visible);
    }

    private void SetAxisChartBool(string dimension, string attributeName, bool? value)
    {
        ValidateAxisDimension(dimension);
        OdfNode axis = FindOrCreateAxis(dimension);
        SetChartBool(axis, attributeName, value);
    }

    private void SetAxisChartDouble(string dimension, string attributeName, double? value)
    {
        ValidateAxisDimension(dimension);
        OdfNode axis = FindOrCreateAxis(dimension);
        SetChartDouble(axis, attributeName, value);
    }

    private static bool? GetChartBool(OdfNode node, string attributeName) =>
        node.GetAttribute(attributeName, OdfNamespaces.Chart) switch
        {
            "true" => true,
            "false" => false,
            _ => null,
        };

    private static void SetChartBool(OdfNode node, string attributeName, bool? value)
    {
        if (value is null)
        {
            node.RemoveAttribute(attributeName, OdfNamespaces.Chart);
            return;
        }

        node.SetAttribute(attributeName, OdfNamespaces.Chart, value.Value ? "true" : "false", "chart");
    }

    private static double? GetChartDouble(OdfNode node, string attributeName)
    {
        string? raw = node.GetAttribute(attributeName, OdfNamespaces.Chart);
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : null;
    }

    private static void SetChartDouble(OdfNode node, string attributeName, double? value)
    {
        if (value is null)
        {
            node.RemoveAttribute(attributeName, OdfNamespaces.Chart);
            return;
        }

        node.SetAttribute(attributeName, OdfNamespaces.Chart, value.Value.ToString(CultureInfo.InvariantCulture), "chart");
    }

    private static bool HasAxisGrid(OdfNode axis, OdfChartGridKind gridKind)
    {
        string token = gridKind switch
        {
            OdfChartGridKind.Minor => "minor",
            _ => "major",
        };

        foreach (OdfNode child in axis.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName is not "grid" ||
                child.NamespaceUri != OdfNamespaces.Chart)
                continue;

            if (string.Equals(child.GetAttribute("class", OdfNamespaces.Chart), token, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static void SetAxisGrid(OdfNode axis, OdfChartGridKind gridKind, bool visible)
    {
        string token = gridKind switch
        {
            OdfChartGridKind.Minor => "minor",
            _ => "major",
        };

        OdfNode? existing = null;
        foreach (OdfNode child in axis.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName is not "grid" ||
                child.NamespaceUri != OdfNamespaces.Chart)
                continue;

            if (string.Equals(child.GetAttribute("class", OdfNamespaces.Chart), token, StringComparison.Ordinal))
            {
                existing = child;
                break;
            }
        }

        if (!visible)
        {
            if (existing is not null)
                axis.RemoveChild(existing);
            return;
        }

        if (existing is not null)
            return;

        OdfNode grid = OdfNodeFactory.CreateElement("grid", OdfNamespaces.Chart, "chart");
        grid.SetAttribute("class", OdfNamespaces.Chart, token, "chart");
        axis.AppendChild(grid);
    }
}
