namespace OdfKit.Drawing;

/// <summary>
/// 表示繪圖頁面上一個圖層的摘要資訊。
/// </summary>
/// <param name="pageName">所在繪圖頁面名稱。</param>
/// <param name="name">圖層名稱。</param>
/// <param name="isProtected">圖層是否受保護。</param>
/// <param name="display">圖層顯示模式原文（<c>draw:display</c>）。</param>
public sealed class OdfLayerInfo(string pageName, string name, bool isProtected, string? display)
{
    /// <summary>
    /// 取得所在繪圖頁面名稱。
    /// </summary>
    public string PageName { get; } = pageName ?? string.Empty;

    /// <summary>
    /// 取得圖層名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// 取得圖層是否受保護。
    /// </summary>
    public bool IsProtected { get; } = isProtected;

    /// <summary>
    /// 取得圖層顯示模式原文。
    /// </summary>
    public string? Display { get; } = display;
}
