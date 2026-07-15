namespace OdfKit.WebFonts;

/// <summary>
/// Represents a versioned manifest of immutable WebFont assets.
/// 代表不可變 WebFont 資產的版本化 manifest。
/// </summary>
public sealed class WebFontManifest
{
    /// <summary>
    /// Gets or initializes the manifest schema version.
    /// 取得或初始化 manifest schema 版本。
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Gets or initializes the deployment-defined profile and mapping version identifier.
    /// 取得或初始化由部署端定義的 profile 與 mapping 版本識別碼。
    /// </summary>
    public string ProfileId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the immutable assets in this manifest.
    /// 取得或初始化此 manifest 中的不可變資產。
    /// </summary>
    public IReadOnlyList<WebFontAsset> Assets { get; init; } = Array.Empty<WebFontAsset>();

    /// <summary>
    /// Gets or initializes the optional content-fingerprinted stylesheet file name.
    /// 取得或初始化選用的內容指紋樣式表檔名。
    /// </summary>
    public string? StylesheetFileName { get; init; }

    /// <summary>
    /// Gets or initializes the optional lowercase SHA-256 digest of the stylesheet bytes.
    /// 取得或初始化樣式表位元組的選用小寫 SHA-256 摘要。
    /// </summary>
    public string? StylesheetSha256 { get; init; }
}
