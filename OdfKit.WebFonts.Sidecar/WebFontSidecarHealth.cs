namespace OdfKit.WebFonts.Sidecar;

/// <summary>
/// Describes the negotiated capabilities of a running WebFont sidecar.
/// 描述執行中 WebFont sidecar 協商完成的能力。
/// </summary>
public sealed class WebFontSidecarHealth
{
    /// <summary>
    /// Gets the negotiated binary protocol version.
    /// 取得協商完成的二進位協定版本。
    /// </summary>
    public int ProtocolVersion { get; init; }

    /// <summary>
    /// Gets a value indicating whether the sidecar can generate and decode WOFF2.
    /// 取得 sidecar 是否可產生及解碼 WOFF2 的值。
    /// </summary>
    public bool IsWoff2Available { get; init; }

    /// <summary>
    /// Gets the sidecar runtime identifier.
    /// 取得 sidecar 的 Runtime Identifier。
    /// </summary>
    public string RuntimeIdentifier { get; init; } = string.Empty;
}
