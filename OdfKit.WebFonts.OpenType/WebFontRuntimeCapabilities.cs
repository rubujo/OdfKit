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

    /// <summary>
    /// Gets whether the managed subset engine supports Apple Advanced Typography layout closure.
    /// 取得受控子集引擎是否支援 Apple Advanced Typography 排版閉包。
    /// </summary>
    public static bool IsAatLayoutSupported => false;

    /// <summary>
    /// Gets whether the managed subset engine supports Graphite layout closure.
    /// 取得受控子集引擎是否支援 Graphite 排版閉包。
    /// </summary>
    public static bool IsGraphiteLayoutSupported => false;

    /// <summary>
    /// Gets whether the managed delivery stack implements Incremental Font Transfer.
    /// 取得受控傳遞堆疊是否實作 Incremental Font Transfer。
    /// </summary>
    public static bool IsIncrementalFontTransferSupported => false;
}
