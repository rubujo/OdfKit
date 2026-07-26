using System;

namespace OdfKit.Chart;

/// <summary>
/// Describes a chart series snapshot to apply at a specified index.
/// 描述要套用至指定索引的圖表序列快照。
/// </summary>
public sealed class OdfChartSeriesUpdate
{
    /// <summary>
    /// Initializes a chart series update.
    /// 初始化圖表序列更新。
    /// </summary>
    /// <param name="index">The zero-based target series index. / 目標序列索引（從 0 起算）。</param>
    /// <param name="snapshot">The desired series snapshot. / 目標序列快照。</param>
    public OdfChartSeriesUpdate(int index, OdfChartSeriesInfo snapshot)
    {
        Index = index;
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    /// <summary>
    /// Gets the zero-based target series index.
    /// 取得目標序列索引（從 0 起算）。
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the desired series snapshot.
    /// 取得目標序列快照。
    /// </summary>
    public OdfChartSeriesInfo Snapshot { get; }
}
