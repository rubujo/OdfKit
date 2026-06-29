namespace OdfKit.Presentation;

/// <summary>
/// Represents summary information for the layout of a presentation slide.
/// 表示簡報中某一投影片版面配置的摘要資訊。
/// </summary>
/// <param name="slideIndex">The slide index. / 投影片索引位置。</param>
/// <param name="slideName">The slide name. / 投影片名稱。</param>
/// <param name="layoutName">The raw layout name (<c>presentation:page-layout-name</c>). / 原始版面配置名稱（<c>presentation:page-layout-name</c>）。</param>
/// <param name="layout">The high-level layout type. / 高階版面配置型態。</param>
public sealed class OdfSlideLayoutInfo(
    int slideIndex,
    string slideName,
    string? layoutName,
    OdfPresentationLayout layout)
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
    /// Gets the raw layout name.
    /// 取得原始版面配置名稱。
    /// </summary>
    public string? LayoutName { get; } = layoutName;

    /// <summary>
    /// Gets the high-level layout type.
    /// 取得高階版面配置型態。
    /// </summary>
    public OdfPresentationLayout Layout { get; } = layout;
}
