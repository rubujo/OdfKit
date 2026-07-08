using OdfKit.Spreadsheet;

namespace OdfKit.Chart;

/// <summary>
/// Defines task-oriented chart presets for common high-level chart creation.
/// 定義常見高階圖表建立流程使用的任務導向圖表預設。
/// </summary>
public enum OdfChartPreset
{
    /// <summary>
    /// A bar or column chart.
    /// 條形圖或柱狀圖。
    /// </summary>
    Bar,

    /// <summary>
    /// A line chart.
    /// 折線圖。
    /// </summary>
    Line,

    /// <summary>
    /// A pie chart.
    /// 圓餅圖。
    /// </summary>
    Pie,

    /// <summary>
    /// An area chart.
    /// 面積圖。
    /// </summary>
    Area,

    /// <summary>
    /// A scatter chart.
    /// 散佈圖。
    /// </summary>
    Scatter,

    /// <summary>
    /// A bubble chart.
    /// 泡泡圖。
    /// </summary>
    Bubble,

    /// <summary>
    /// A stock chart.
    /// 股票圖。
    /// </summary>
    Stock,

    /// <summary>
    /// A 3D column chart.
    /// 3D 柱狀圖。
    /// </summary>
    Column3D,

    /// <summary>
    /// A 3D bar chart.
    /// 3D 條形圖。
    /// </summary>
    Bar3D,

    /// <summary>
    /// A 3D pie chart.
    /// 3D 圓餅圖。
    /// </summary>
    Pie3D
}

internal static class OdfChartPresetExtensions
{
    internal static OdfChartType ToChartType(this OdfChartPreset preset) =>
        preset switch
        {
            OdfChartPreset.Line => OdfChartType.Line,
            OdfChartPreset.Pie or OdfChartPreset.Pie3D => OdfChartType.Pie,
            OdfChartPreset.Area => OdfChartType.Area,
            OdfChartPreset.Scatter => OdfChartType.Scatter,
            OdfChartPreset.Bubble => OdfChartType.Bubble,
            OdfChartPreset.Stock => OdfChartType.Stock,
            _ => OdfChartType.Bar
        };

    internal static bool IsThreeDimensional(this OdfChartPreset preset) =>
        preset is OdfChartPreset.Column3D or OdfChartPreset.Bar3D or OdfChartPreset.Pie3D;
}
