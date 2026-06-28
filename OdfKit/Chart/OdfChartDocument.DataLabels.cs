using System;

namespace OdfKit.Chart;

public partial class OdfChartDocument
{
    /// <summary>
    /// 取得指定資料序列的數據標籤設定。
    /// </summary>
    /// <param name="seriesIndex">序列索引（從 0 起算）</param>
    /// <returns>數據標籤設定；若序列未定義數據標籤則為 <see langword="null"/></returns>
    /// <exception cref="ArgumentOutOfRangeException">當 <paramref name="seriesIndex"/> 超出範圍時擲出</exception>
    public OdfChartDataLabelInfo? GetSeriesDataLabels(int seriesIndex) =>
        GetSeriesEditor(seriesIndex).GetDataLabels();

    /// <summary>
    /// 設定指定資料序列的數據標籤。
    /// </summary>
    /// <param name="seriesIndex">序列索引（從 0 起算）</param>
    /// <param name="info">數據標籤設定；傳入 <see langword="null"/> 表示移除既有設定</param>
    /// <exception cref="ArgumentOutOfRangeException">當 <paramref name="seriesIndex"/> 超出範圍時擲出</exception>
    public void SetSeriesDataLabels(int seriesIndex, OdfChartDataLabelInfo? info) =>
        GetSeriesEditor(seriesIndex).SetDataLabels(info);

    /// <summary>
    /// 依常用預設組合設定指定資料序列的數據標籤。
    /// </summary>
    /// <param name="seriesIndex">序列索引（從 0 起算）</param>
    /// <param name="preset">資料標籤預設組合；<see cref="OdfChartDataLabelPreset.None"/> 表示移除既有設定</param>
    /// <exception cref="ArgumentOutOfRangeException">當 <paramref name="seriesIndex"/> 超出範圍時擲出</exception>
    public void SetSeriesDataLabelPreset(int seriesIndex, OdfChartDataLabelPreset preset) =>
        GetSeriesEditor(seriesIndex).SetDataLabelPreset(preset);

}
