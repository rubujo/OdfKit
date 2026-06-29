namespace OdfKit.Presentation;

/// <summary>
/// Represents summary information for a placeholder on a presentation slide, including the slide index.
/// 表示簡報中某一投影片上一個預留位置的摘要資訊（含投影片索引）。
/// </summary>
/// <param name="slideIndex">The slide index. / 投影片索引位置。</param>
/// <param name="slideName">The slide name. / 投影片名稱。</param>
/// <param name="placeholder">The placeholder summary. / 預留位置摘要。</param>
public sealed class OdfSlidePlaceholderInfo(int slideIndex, string slideName, OdfPlaceholderInfo placeholder)
{
    /// <summary>
    /// Gets the slide index.
    /// 取得投影片索引位置。
    /// </summary>
    public int SlideIndex { get; } = slideIndex;

    /// <summary>
    /// Gets the slide name.
    /// 取得投影片名稱。
    /// </summary>
    public string SlideName { get; } = slideName ?? string.Empty;

    /// <summary>
    /// Gets the placeholder summary.
    /// 取得預留位置摘要。
    /// </summary>
    public OdfPlaceholderInfo Placeholder { get; } = placeholder;
}
