namespace OdfKit.Presentation;

/// <summary>
/// Represents summary information for a presentation page layout (<c>style:presentation-page-layout</c>).
/// 表示簡報投影片版面配置（<c>style:presentation-page-layout</c>）的摘要資訊。
/// </summary>
/// <param name="name">The page layout name. / 版面配置名稱。</param>
/// <param name="placeholderCount">The placeholder template count. / 預留位置範本數量。</param>
public sealed class OdfPresentationPageLayoutInfo(string name, int placeholderCount)
{
    /// <summary>
    /// Gets the page layout name.
    /// 取得版面配置名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// Gets the placeholder template count.
    /// 取得預留位置範本數量。
    /// </summary>
    public int PlaceholderCount { get; } = placeholderCount;
}
