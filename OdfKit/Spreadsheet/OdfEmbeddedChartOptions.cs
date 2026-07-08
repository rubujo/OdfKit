using System.Collections.Generic;
using OdfKit.Chart;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Configures insertion of an embedded spreadsheet chart.
/// 設定試算表嵌入圖表的插入行為。
/// </summary>
public sealed class OdfEmbeddedChartOptions
{
    /// <summary>
    /// Gets or sets the chart preset.
    /// 取得或設定圖表預設。
    /// </summary>
    public OdfChartPreset Preset { get; set; } = OdfChartPreset.Bar;

    /// <summary>
    /// Gets or sets the optional chart title.
    /// 取得或設定選用的圖表標題。
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the frame width.
    /// 取得或設定圖表框架寬度。
    /// </summary>
    public OdfLength? Width { get; set; }

    /// <summary>
    /// Gets or sets the frame height.
    /// 取得或設定圖表框架高度。
    /// </summary>
    public OdfLength? Height { get; set; }

    /// <summary>
    /// Gets or sets whether the first row is treated as series labels.
    /// 取得或設定首列是否視為序列標籤。
    /// </summary>
    public bool FirstRowAsHeader { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the first column is treated as category labels.
    /// 取得或設定首欄是否視為分類標籤。
    /// </summary>
    public bool FirstColumnAsLabel { get; set; } = true;

    /// <summary>
    /// Gets or sets the optional legend position. Use <see langword="null"/> to hide the legend.
    /// 取得或設定選用圖例位置；使用 <see langword="null"/> 隱藏圖例。
    /// </summary>
    public string? LegendPosition { get; set; } = "end";

    /// <summary>
    /// Gets or sets the optional X-axis title.
    /// 取得或設定選用的 X 軸標題。
    /// </summary>
    public string? XAxisTitle { get; set; }

    /// <summary>
    /// Gets or sets the optional Y-axis title.
    /// 取得或設定選用的 Y 軸標題。
    /// </summary>
    public string? YAxisTitle { get; set; }

    /// <summary>
    /// Gets or sets the optional data label preset applied to each generated series.
    /// 取得或設定套用至每個產生序列的選用資料標籤預設。
    /// </summary>
    public OdfChartDataLabelPreset? DataLabelPreset { get; set; }

    /// <summary>
    /// Gets or sets whether major grid lines are shown on the Y axis.
    /// 取得或設定是否顯示 Y 軸主網格線。
    /// </summary>
    public bool? ShowMajorGridLines { get; set; }

    /// <summary>
    /// Gets or sets whether minor grid lines are shown on the Y axis.
    /// 取得或設定是否顯示 Y 軸次網格線。
    /// </summary>
    public bool? ShowMinorGridLines { get; set; }

    /// <summary>
    /// Gets the optional series style names applied by series index.
    /// 取得依序列索引套用的選用序列樣式名稱。
    /// </summary>
    public IList<string> SeriesStyleNames { get; } = new List<string>();

    /// <summary>
    /// Gets the optional fill colors applied by series index.
    /// 取得依序列索引套用的選用填滿色彩。
    /// </summary>
    public IList<string> Palette { get; } = new List<string>();

    /// <summary>
    /// Gets practical marker styles applied by series index.
    /// 取得依序列索引套用的實務標記樣式。
    /// </summary>
    public IList<OdfChartMarkerStyle> MarkerStyles { get; } = new List<OdfChartMarkerStyle>();

    /// <summary>
    /// Gets or sets the optional X-axis number format data style name.
    /// 取得或設定選用的 X 軸數字格式資料樣式名稱。
    /// </summary>
    public string? XAxisNumberFormat { get; set; }

    /// <summary>
    /// Gets or sets the optional Y-axis number format data style name.
    /// 取得或設定選用的 Y 軸數字格式資料樣式名稱。
    /// </summary>
    public string? YAxisNumberFormat { get; set; }

    /// <summary>
    /// Gets or sets practical 3D options to apply after chart creation.
    /// 取得或設定圖表建立後要套用的實務 3D 選項。
    /// </summary>
    public OdfChart3DOptions? ThreeDOptions { get; set; }
}
