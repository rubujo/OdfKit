using OdfKit.Chart;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Describes a practical embedded chart series update.
/// 描述一項實務嵌入圖表序列更新。
/// </summary>
public sealed class OdfEmbeddedChartSeriesUpdate
{
    /// <summary>
    /// Gets or sets the zero-based series index.
    /// 取得或設定以 0 為基準的序列索引。
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the optional series style name.
    /// 取得或設定選用序列樣式名稱。
    /// </summary>
    public string? StyleName { get; set; }

    /// <summary>
    /// Gets or sets the optional attached axis token.
    /// 取得或設定選用附著座標軸 token。
    /// </summary>
    public string? AttachedAxis { get; set; }

    /// <summary>
    /// Gets or sets the optional data label preset.
    /// 取得或設定選用資料標籤預設。
    /// </summary>
    public OdfChartDataLabelPreset? DataLabelPreset { get; set; }

    /// <summary>
    /// Gets or sets the optional practical marker style.
    /// 取得或設定選用的實務標記樣式。
    /// </summary>
    public OdfChartMarkerStyle? MarkerStyle { get; set; }
}
