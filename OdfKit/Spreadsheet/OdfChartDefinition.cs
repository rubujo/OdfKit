namespace OdfKit.Spreadsheet;

/// <summary>
/// Specifies ODS chart types.
/// 表示 ODS 圖表類型的列舉。
/// </summary>
public enum OdfChartType
{
    /// <summary>
    /// A bar or column chart.
    /// 條形圖（柱狀圖）。
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
    /// A ring chart.
    /// 環圈圖。
    /// </summary>
    Ring,

    /// <summary>
    /// A radar chart, also known as a spider or web chart.
    /// 雷達圖（蜘蛛圖／網圖）。
    /// </summary>
    Radar,

    /// <summary>
    /// A stock chart; after creation, stock-specific marker styles can be configured with <see cref="OdfKit.Chart.OdfChartDocument"/> members.
    /// 股票圖；建立後可搭配 <see cref="OdfKit.Chart.OdfChartDocument"/> 的
    /// <c>SetStockGainMarkerStyleName</c>／<c>SetStockLossMarkerStyleName</c>／
    /// <c>SetStockRangeLineStyleName</c> 設定股票圖專屬標記樣式。
    /// </summary>
    Stock
}

/// <summary>
/// Defines settings for an ODS chart.
/// 定義 ODS 圖表的設定資訊。
/// </summary>
public sealed class OdfChartDefinition
{
    /// <summary>
    /// Gets or sets the chart type.
    /// 取得或設定圖表類型。
    /// </summary>
    public OdfChartType ChartType { get; init; } = OdfChartType.Bar;

    /// <summary>
    /// Gets or sets the chart title.
    /// 取得或設定圖表標題。
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the data cell range referenced by the chart.
    /// 取得或設定圖表參照的資料儲存格範圍。
    /// </summary>
    public OdfCellRange DataRange { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to show the legend.
    /// 取得或設定一個值，指出是否顯示圖例。
    /// </summary>
    public bool HasLegend { get; init; } = true;
}
