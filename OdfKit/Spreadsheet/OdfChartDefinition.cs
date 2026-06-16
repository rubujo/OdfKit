namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示 ODS 圖表類型的列舉。
/// </summary>
public enum OdfChartType
{
    /// <summary>
    /// 條形圖（柱狀圖）。
    /// </summary>
    Bar,

    /// <summary>
    /// 折線圖。
    /// </summary>
    Line,

    /// <summary>
    /// 圓餅圖。
    /// </summary>
    Pie,

    /// <summary>
    /// 面積圖。
    /// </summary>
    Area,

    /// <summary>
    /// 散佈圖。
    /// </summary>
    Scatter,

    /// <summary>
    /// 泡泡圖。
    /// </summary>
    Bubble
}

/// <summary>
/// 定義 ODS 圖表的設定資訊。
/// </summary>
public sealed class OdfChartDefinition
{
    /// <summary>
    /// 取得或設定圖表類型。
    /// </summary>
    public OdfChartType ChartType { get; init; } = OdfChartType.Bar;

    /// <summary>
    /// 取得或設定圖表標題。
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 取得或設定圖表參照的資料儲存格範圍。
    /// </summary>
    public OdfCellRange DataRange { get; init; }

    /// <summary>
    /// 取得或設定一個值，指出是否顯示圖例。
    /// </summary>
    public bool HasLegend { get; init; } = true;
}
