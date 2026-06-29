using System;

namespace OdfKit.Chart;

public partial class OdfChartDocument
{
    /// <summary>
    /// Finds the data label setting for the specified data series.
    /// 尋找指定資料序列的數據標籤設定。
    /// </summary>
    /// <param name="seriesIndex">The zero-based series index. / 序列索引（從 0 起算）。</param>
    /// <returns>The data label setting; <see langword="null"/> if the series does not define one. / 數據標籤設定；若序列未定義數據標籤則為 <see langword="null"/>。</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="seriesIndex"/> is out of range. / 當 <paramref name="seriesIndex"/> 超出範圍時擲出。</exception>
    public OdfChartDataLabelInfo? FindSeriesDataLabels(int seriesIndex) =>
        GetSeriesEditor(seriesIndex).FindDataLabels();

    /// <summary>
    /// Sets the data label for the specified data series.
    /// 設定指定資料序列的數據標籤。
    /// </summary>
    /// <param name="seriesIndex">The zero-based series index. / 序列索引（從 0 起算）。</param>
    /// <param name="info">The data label setting; pass <see langword="null"/> to remove the existing setting. / 數據標籤設定；傳入 <see langword="null"/> 表示移除既有設定。</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="seriesIndex"/> is out of range. / 當 <paramref name="seriesIndex"/> 超出範圍時擲出。</exception>
    public void SetSeriesDataLabels(int seriesIndex, OdfChartDataLabelInfo? info) =>
        GetSeriesEditor(seriesIndex).SetDataLabels(info);

    /// <summary>
    /// Sets the data label for the specified data series according to a common preset combination.
    /// 依常用預設組合設定指定資料序列的數據標籤。
    /// </summary>
    /// <param name="seriesIndex">The zero-based series index. / 序列索引（從 0 起算）。</param>
    /// <param name="preset">The data label preset combination; <see cref="OdfChartDataLabelPreset.None"/> removes the existing setting. / 資料標籤預設組合；<see cref="OdfChartDataLabelPreset.None"/> 表示移除既有設定。</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="seriesIndex"/> is out of range. / 當 <paramref name="seriesIndex"/> 超出範圍時擲出。</exception>
    public void SetSeriesDataLabelPreset(int seriesIndex, OdfChartDataLabelPreset preset) =>
        GetSeriesEditor(seriesIndex).SetDataLabelPreset(preset);

}
