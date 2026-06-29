namespace OdfKit.Text;

/// <summary>
/// Represents summary information for a hyperlink in a text document.
/// 表示文字文件中一個超連結的摘要資訊。
/// </summary>
/// <param name="url">The link URL (<c>xlink:href</c>). / 連結 URL（<c>xlink:href</c>）。</param>
/// <param name="displayText">The display text content. / 顯示文字內容。</param>
public sealed class OdfHyperlinkInfo(string url, string displayText)
{
    /// <summary>
    /// Gets the link URL.
    /// 取得連結 URL。
    /// </summary>
    public string Url { get; } = url ?? string.Empty;

    /// <summary>
    /// Gets the display text content.
    /// 取得顯示文字內容。
    /// </summary>
    public string DisplayText { get; } = displayText ?? string.Empty;
}
