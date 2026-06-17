namespace OdfKit.Text;

/// <summary>
/// 表示文字文件中一個主頁面樣式（<c>style:master-page</c>）的頁首頁尾摘要資訊。
/// </summary>
/// <param name="name">主頁面樣式名稱。</param>
/// <param name="pageLayoutName">對應的頁面版面配置名稱。</param>
/// <param name="headerText">頁首文字。</param>
/// <param name="headerLeftText">左頁首文字。</param>
/// <param name="footerText">頁尾文字。</param>
/// <param name="footerLeftText">左頁尾文字。</param>
public sealed class OdfPageSetupInfo(
    string name,
    string? pageLayoutName,
    string? headerText,
    string? headerLeftText,
    string? footerText,
    string? footerLeftText)
{
    /// <summary>
    /// 取得主頁面樣式名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// 取得對應的頁面版面配置名稱。
    /// </summary>
    public string? PageLayoutName { get; } = pageLayoutName;

    /// <summary>
    /// 取得頁首文字。
    /// </summary>
    public string? HeaderText { get; } = headerText;

    /// <summary>
    /// 取得左頁首文字。
    /// </summary>
    public string? HeaderLeftText { get; } = headerLeftText;

    /// <summary>
    /// 取得頁尾文字。
    /// </summary>
    public string? FooterText { get; } = footerText;

    /// <summary>
    /// 取得左頁尾文字。
    /// </summary>
    public string? FooterLeftText { get; } = footerLeftText;
}
