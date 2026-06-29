namespace OdfKit.Text;

/// <summary>
/// Represents odf page setup info.
/// 表示文字文件中一個主頁面樣式（<c>style:master-page</c>）的頁首頁尾摘要資訊。
/// </summary>
/// <param name="name">The name or identifier. / 主頁面樣式名稱</param>
/// <param name="pageLayoutName">The name or identifier. / 對應的頁面版面配置名稱</param>
/// <param name="headerText">The text or value. / 頁首文字</param>
/// <param name="headerLeftText">The text or value. / 左頁首文字</param>
/// <param name="footerText">The text or value. / 頁尾文字</param>
/// <param name="footerLeftText">The text or value. / 左頁尾文字</param>
/// <param name="headerFirstText">The text or value. / 首頁頁首文字</param>
/// <param name="footerFirstText">The text or value. / 首頁頁尾文字</param>
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
    /// Gets name.
    /// 取得主頁面樣式名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// Gets page layout name.
    /// 取得對應的頁面版面配置名稱。
    /// </summary>
    public string? PageLayoutName { get; } = pageLayoutName;

    /// <summary>
    /// Gets header text.
    /// 取得頁首文字。
    /// </summary>
    public string? HeaderText { get; } = headerText;

    /// <summary>
    /// Gets header left text.
    /// 取得左頁首文字。
    /// </summary>
    public string? HeaderLeftText { get; } = headerLeftText;

    /// <summary>
    /// Gets footer text.
    /// 取得頁尾文字。
    /// </summary>
    public string? FooterText { get; } = footerText;

    /// <summary>
    /// Gets footer left text.
    /// 取得左頁尾文字。
    /// </summary>
    public string? FooterLeftText { get; } = footerLeftText;

    /// <summary>
    /// Gets header first text.
    /// 取得首頁頁首文字。
    /// </summary>
    public string? HeaderFirstText { get; } = headerFirstText;

    /// <summary>
    /// Gets footer first text.
    /// 取得首頁頁尾文字。
    /// </summary>
    public string? FooterFirstText { get; } = footerFirstText;
}
