namespace OdfKit.Presentation;

/// <summary>
/// 表示簡報中某一投影片上一個預留位置的摘要資訊（含投影片索引）。
/// </summary>
/// <param name="slideIndex">投影片索引位置。</param>
/// <param name="slideName">投影片名稱。</param>
/// <param name="placeholder">預留位置摘要。</param>
public sealed class OdfSlidePlaceholderInfo(int slideIndex, string slideName, OdfPlaceholderInfo placeholder)
{
    /// <summary>
    /// 取得投影片索引位置。
    /// </summary>
    public int SlideIndex { get; } = slideIndex;

    /// <summary>
    /// 取得投影片名稱。
    /// </summary>
    public string SlideName { get; } = slideName ?? string.Empty;

    /// <summary>
    /// 取得預留位置摘要。
    /// </summary>
    public OdfPlaceholderInfo Placeholder { get; } = placeholder;
}
