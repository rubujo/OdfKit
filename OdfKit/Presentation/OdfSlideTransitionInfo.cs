namespace OdfKit.Presentation;

/// <summary>
/// 表示簡報中某一投影片切換效果的摘要資訊。
/// </summary>
/// <param name="slideIndex">投影片索引位置</param>
/// <param name="slideName">投影片名稱</param>
/// <param name="transition">投影片切換效果類型</param>
/// <param name="duration">切換持續時間原文（<c>smil:dur</c>）</param>
public sealed class OdfSlideTransitionInfo(
    int slideIndex,
    string slideName,
    OdfSlideTransition transition,
    string? duration = null)
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
    /// 取得投影片切換效果類型。
    /// </summary>
    public OdfSlideTransition Transition { get; } = transition;

    /// <summary>
    /// 取得切換持續時間原文。
    /// </summary>
    public string? Duration { get; } = duration;
}
