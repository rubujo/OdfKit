namespace OdfKit.Text;

/// <summary>
/// Represents summary information for the header and footer of a master page style (<c>style:master-page</c>) in a text document.
/// 表示文字文件中一個主頁面樣式（<c>style:master-page</c>）的頁首頁尾摘要資訊。
/// </summary>
/// <param name="name">The master page style name. / 主頁面樣式名稱。</param>
/// <param name="pageLayoutName">The corresponding page layout name. / 對應的頁面版面配置名稱。</param>
/// <param name="headerText">The header text. / 頁首文字。</param>
/// <param name="headerLeftText">The left-page header text. / 左頁首文字。</param>
/// <param name="footerText">The footer text. / 頁尾文字。</param>
/// <param name="footerLeftText">The left-page footer text. / 左頁尾文字。</param>
/// <param name="headerFirstText">The first-page header text. / 首頁頁首文字。</param>
/// <param name="footerFirstText">The first-page footer text. / 首頁頁尾文字。</param>
public sealed class OdfPageSetupInfo(
    string name,
    string? pageLayoutName,
    string? headerText,
    string? headerLeftText,
    string? footerText,
    string? footerLeftText,
    string? headerFirstText,
    string? footerFirstText)
{
    /// <summary>
    /// Gets the master page style name.
    /// 取得主頁面樣式名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// Gets the corresponding page layout name.
    /// 取得對應的頁面版面配置名稱。
    /// </summary>
    public string? PageLayoutName { get; } = pageLayoutName;

    /// <summary>
    /// Gets the header text.
    /// 取得頁首文字。
    /// </summary>
    public string? HeaderText { get; } = headerText;

    /// <summary>
    /// Gets the left-page header text.
    /// 取得左頁首文字。
    /// </summary>
    public string? HeaderLeftText { get; } = headerLeftText;

    /// <summary>
    /// Gets the footer text.
    /// 取得頁尾文字。
    /// </summary>
    public string? FooterText { get; } = footerText;

    /// <summary>
    /// Gets the left-page footer text.
    /// 取得左頁尾文字。
    /// </summary>
    public string? FooterLeftText { get; } = footerLeftText;

    /// <summary>
    /// Gets the first-page header text.
    /// 取得首頁頁首文字。
    /// </summary>
    public string? HeaderFirstText { get; } = headerFirstText;

    /// <summary>
    /// Gets the first-page footer text.
    /// 取得首頁頁尾文字。
    /// </summary>
    public string? FooterFirstText { get; } = footerFirstText;
}
