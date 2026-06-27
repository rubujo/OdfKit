using System.Collections.Generic;

namespace OdfKit.Chart;

/// <summary>
/// 表示圖表本地資料表的延遲載入快照。
/// </summary>
public sealed class OdfChartDataCache
{
    /// <summary>
    /// 初始化 <see cref="OdfChartDataCache"/> 類別的新執行個體。
    /// </summary>
    /// <param name="rows">已解析的圖表資料列</param>
    public OdfChartDataCache(IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        Rows = rows;
    }

    /// <summary>
    /// 取得圖表資料列。
    /// </summary>
    public IReadOnlyList<IReadOnlyList<object?>> Rows { get; }

    /// <summary>
    /// 取得資料快照是否沒有任何列。
    /// </summary>
    public bool IsEmpty => Rows.Count == 0;
}
