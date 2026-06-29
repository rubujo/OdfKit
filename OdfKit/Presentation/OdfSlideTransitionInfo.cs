namespace OdfKit.Presentation;

/// <summary>
/// Represents transition summary information for a presentation slide.
/// 表示簡報中某一投影片切換效果的摘要資訊。
/// </summary>
/// <param name="slideIndex">The slide index. / 投影片索引位置。</param>
/// <param name="slideName">The slide name. / 投影片名稱。</param>
/// <param name="transition">The slide transition effect type. / 投影片切換效果類型。</param>
/// <param name="duration">The raw transition duration (<c>smil:dur</c>). / 切換持續時間原文（<c>smil:dur</c>）。</param>
public sealed class OdfSlideTransitionInfo(
    int slideIndex,
    string slideName,
    OdfSlideTransition transition,
    string? duration = null)
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
    /// Gets the slide transition effect type.
    /// 取得投影片切換效果類型。
    /// </summary>
    public OdfSlideTransition Transition { get; } = transition;

    /// <summary>
    /// Gets the raw transition duration.
    /// 取得切換持續時間原文。
    /// </summary>
    public string? Duration { get; } = duration;
}
