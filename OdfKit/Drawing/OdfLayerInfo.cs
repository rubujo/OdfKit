namespace OdfKit.Drawing;

/// <summary>
/// Represents summary information for a layer on a drawing page.
/// 表示繪圖頁面上一個圖層的摘要資訊。
/// </summary>
/// <param name="pageName">The name of the drawing page. / 所在繪圖頁面名稱。</param>
/// <param name="name">The layer name. / 圖層名稱。</param>
/// <param name="isProtected">Whether the layer is protected. / 圖層是否受保護。</param>
/// <param name="display">The raw layer display mode text (<c>draw:display</c>). / 圖層顯示模式原文（<c>draw:display</c>）。</param>
public sealed class OdfLayerInfo(string pageName, string name, bool isProtected, string? display)
{
    /// <summary>
    /// Gets the name of the drawing page.
    /// 取得所在繪圖頁面名稱。
    /// </summary>
    public string PageName { get; } = pageName ?? string.Empty;

    /// <summary>
    /// Gets the layer name.
    /// 取得圖層名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// Gets whether the layer is protected.
    /// 取得圖層是否受保護。
    /// </summary>
    public bool IsProtected { get; } = isProtected;

    /// <summary>
    /// Gets the raw layer display mode text.
    /// 取得圖層顯示模式原文。
    /// </summary>
    public string? Display { get; } = display;
}
