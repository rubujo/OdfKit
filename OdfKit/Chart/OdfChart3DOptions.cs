using System.Collections.Generic;
using OdfKit.DOM;

namespace OdfKit.Chart;

/// <summary>
/// Describes practical 3D chart appearance settings.
/// 描述實務常用的 3D 圖表外觀設定。
/// </summary>
public sealed class OdfChart3DOptions
{
    /// <summary>
    /// Gets or sets whether 3D rendering is enabled.
    /// 取得或設定是否啟用 3D 呈現。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the 3D projection mode.
    /// 取得或設定 3D 投影模式。
    /// </summary>
    public OdfDr3dProjection? Projection { get; set; }

    /// <summary>
    /// Gets or sets the chart angle offset.
    /// 取得或設定圖表角度偏移。
    /// </summary>
    public int? AngleOffset { get; set; }

    /// <summary>
    /// Gets or sets whether double-sided lighting mode is enabled.
    /// 取得或設定是否啟用雙面光照模式。
    /// </summary>
    public bool? LightingMode { get; set; }

    /// <summary>
    /// Gets the light sources to write into the chart plot area.
    /// 取得要寫入圖表繪圖區的光源清單。
    /// </summary>
    public IList<OdfChartLightRequest> Lights { get; } = new List<OdfChartLightRequest>();

    /// <summary>
    /// Gets or sets the optional wall surface style.
    /// 取得或設定選用的牆面表面樣式。
    /// </summary>
    public OdfChartSurfaceStyle? WallStyle { get; set; }

    /// <summary>
    /// Gets or sets the optional floor surface style.
    /// 取得或設定選用的地板表面樣式。
    /// </summary>
    public OdfChartSurfaceStyle? FloorStyle { get; set; }
}

/// <summary>
/// Describes one practical 3D chart light source.
/// 描述一個實務 3D 圖表光源。
/// </summary>
/// <param name="Direction">The light direction vector, formatted as <c>(x y z)</c>. / 光源方向向量，格式為 <c>(x y z)</c>。</param>
/// <param name="DiffuseColor">The optional diffuse color. / 選用的漫射色。</param>
/// <param name="Enabled">The optional enabled state. / 選用的啟用狀態。</param>
/// <param name="Specular">The optional specular state. / 選用的反射光狀態。</param>
public sealed record OdfChartLightRequest(
    string Direction,
    string? DiffuseColor = null,
    bool? Enabled = null,
    bool? Specular = null);

/// <summary>
/// Describes a practical chart wall or floor surface style.
/// 描述實務圖表牆面或地板表面樣式。
/// </summary>
/// <param name="StyleName">The style name to create and apply. / 要建立並套用的樣式名稱。</param>
/// <param name="FillColor">The optional fill color. / 選用的填滿色。</param>
/// <param name="StrokeColor">The optional stroke color. / 選用的筆觸色。</param>
/// <param name="StrokeWidth">The optional stroke width. / 選用的筆觸寬度。</param>
/// <param name="Fill">The optional fill mode. / 選用的填滿模式。</param>
/// <param name="Stroke">The optional stroke mode. / 選用的筆觸模式。</param>
public sealed record OdfChartSurfaceStyle(
    string StyleName,
    string? FillColor = null,
    string? StrokeColor = null,
    string? StrokeWidth = null,
    string? Fill = "solid",
    string? Stroke = "solid");

/// <summary>
/// Describes practical stock chart marker styles.
/// 描述實務股票圖標記樣式。
/// </summary>
/// <param name="GainStyle">The gain marker surface style. / 上漲標記表面樣式。</param>
/// <param name="LossStyle">The loss marker surface style. / 下跌標記表面樣式。</param>
/// <param name="RangeLineStyle">The range line style. / 範圍線樣式。</param>
public sealed record OdfStockMarkerStyle(
    OdfChartSurfaceStyle? GainStyle = null,
    OdfChartSurfaceStyle? LossStyle = null,
    OdfChartSurfaceStyle? RangeLineStyle = null);
