namespace OdfKit.Chart;

/// <summary>
/// 表示 3D 圖表光源（<c>dr3d:light</c>）的摘要資訊。
/// </summary>
/// <param name="direction">光源方向向量（<c>dr3d:direction</c>，格式為 <c>(x y z)</c>）。</param>
/// <param name="diffuseColor">漫射色（<c>dr3d:diffuse-color</c>）。</param>
/// <param name="enabled">是否啟用此光源。</param>
/// <param name="specular">是否啟用反射光。</param>
public sealed class OdfChartLightInfo(string direction, string? diffuseColor, bool? enabled, bool? specular)
{
    /// <summary>
    /// 取得光源方向向量。
    /// </summary>
    public string Direction { get; } = direction;

    /// <summary>
    /// 取得漫射色。
    /// </summary>
    public string? DiffuseColor { get; } = diffuseColor;

    /// <summary>
    /// 取得是否啟用此光源。
    /// </summary>
    public bool? Enabled { get; } = enabled;

    /// <summary>
    /// 取得是否啟用反射光。
    /// </summary>
    public bool? Specular { get; } = specular;
}
