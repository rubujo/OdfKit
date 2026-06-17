namespace OdfKit.Presentation;

/// <summary>
/// 表示簡報中某一投影片版面配置的摘要資訊。
/// </summary>
/// <param name="slideIndex">投影片索引位置。</param>
/// <param name="slideName">投影片名稱。</param>
/// <param name="layoutName">原始版面配置名稱（<c>presentation:page-layout-name</c>）。</param>
/// <param name="layout">高階版面配置型態。</param>
public sealed class OdfSlideLayoutInfo(
    int slideIndex,
    string slideName,
    string? layoutName,
    OdfPresentationLayout layout)
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
    /// 取得原始版面配置名稱。
    /// </summary>
    public string? LayoutName { get; } = layoutName;

    /// <summary>
    /// 取得高階版面配置型態。
    /// </summary>
    public OdfPresentationLayout Layout { get; } = layout;
}
