namespace OdfKit.WebFonts.OpenType;

/// <summary>
/// Describes web-font features available in the current runtime.
/// 描述目前執行期可用的網頁字型功能。
/// </summary>
public static class WebFontRuntimeCapabilities
{
    /// <summary>
    /// Gets whether the current runtime provides Brotli compression and decompression for WOFF2.
    /// 取得目前執行期是否提供 WOFF2 所需的 Brotli 壓縮與解壓縮功能。
    /// </summary>
    public static bool IsWoff2Available => RuntimeBrotliCodec.IsAvailable;
}
