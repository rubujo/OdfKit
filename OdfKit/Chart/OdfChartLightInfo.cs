namespace OdfKit.Chart;

/// <summary>
/// Represents summary information for a 3D chart light source (<c>dr3d:light</c>).
/// 表示 3D 圖表光源（<c>dr3d:light</c>）的摘要資訊。
/// </summary>
/// <param name="direction">The light direction vector (<c>dr3d:direction</c>, formatted as <c>(x y z)</c>). / 光源方向向量（<c>dr3d:direction</c>，格式為 <c>(x y z)</c>）。</param>
/// <param name="diffuseColor">The diffuse color (<c>dr3d:diffuse-color</c>). / 漫射色（<c>dr3d:diffuse-color</c>）。</param>
/// <param name="enabled">Whether this light source is enabled. / 是否啟用此光源。</param>
/// <param name="specular">Whether specular reflection is enabled. / 是否啟用反射光。</param>
public sealed class OdfChartLightInfo(string direction, string? diffuseColor, bool? enabled, bool? specular)
{
    /// <summary>
    /// Gets the light direction vector.
    /// 取得光源方向向量。
    /// </summary>
    public string Direction { get; } = direction;

    /// <summary>
    /// Gets the diffuse color.
    /// 取得漫射色。
    /// </summary>
    public string? DiffuseColor { get; } = diffuseColor;

    /// <summary>
    /// Gets whether this light source is enabled.
    /// 取得是否啟用此光源。
    /// </summary>
    public bool? Enabled { get; } = enabled;

    /// <summary>
    /// Gets whether specular reflection is enabled.
    /// 取得是否啟用反射光。
    /// </summary>
    public bool? Specular { get; } = specular;
}
