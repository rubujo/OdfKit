using System.Collections.Generic;

namespace OdfKit.Chart;

/// <summary>
/// Represents a lazily loaded snapshot of a chart's local data table.
/// 表示圖表本地資料表的延遲載入快照。
/// </summary>
public sealed class OdfChartDataCache
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OdfChartDataCache"/> class.
    /// 初始化 <see cref="OdfChartDataCache"/> 類別的新執行個體。
    /// </summary>
    /// <param name="rows">The parsed chart data rows. / 已解析的圖表資料列。</param>
    public OdfChartDataCache(IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        Rows = rows;
    }

    /// <summary>
    /// Gets the chart data rows.
    /// 取得圖表資料列。
    /// </summary>
    public IReadOnlyList<IReadOnlyList<object?>> Rows { get; }

    /// <summary>
    /// Gets whether the data snapshot has no rows.
    /// 取得資料快照是否沒有任何列。
    /// </summary>
    public bool IsEmpty => Rows.Count == 0;
}
