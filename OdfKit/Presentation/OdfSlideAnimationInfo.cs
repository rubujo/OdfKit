namespace OdfKit.Presentation;

/// <summary>
/// Represents summary information for an animation effect on a presentation slide, including the slide index.
/// 表示簡報中某一投影片上一筆動畫效果的摘要資訊（含投影片索引）。
/// </summary>
/// <param name="slideIndex">The slide index. / 投影片索引位置。</param>
/// <param name="slideName">The slide name. / 投影片名稱。</param>
/// <param name="animation">The animation effect summary. / 動畫效果摘要。</param>
public sealed class OdfSlideAnimationInfo(int slideIndex, string slideName, OdfAnimationInfo animation)
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
    /// Gets the animation effect summary.
    /// 取得動畫效果摘要。
    /// </summary>
    public OdfAnimationInfo Animation { get; } = animation;
}
