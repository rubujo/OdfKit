namespace OdfKit.WebFonts;

/// <summary>
/// Identifies one face in a trusted font source without exposing a physical path.
/// 在不暴露實體路徑的情況下識別受信任字型來源中的單一 face。
/// </summary>
public sealed class WebFontFaceIdentity
{
    /// <summary>
    /// Gets or initializes the deployment-defined opaque font source identifier.
    /// 取得或初始化由部署端定義的不透明字型來源識別碼。
    /// </summary>
    public string FontSourceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the SHA-256 digest of the complete source font or collection.
    /// 取得或初始化完整來源字型或 collection 的 SHA-256 摘要。
    /// </summary>
    public string SourceSha256 { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the zero-based face index in a font collection.
    /// 取得或初始化字型 collection 中以零為基準的 face 索引。
    /// </summary>
    public int FaceIndex { get; init; }
}
