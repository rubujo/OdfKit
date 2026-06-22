namespace OdfKit.Text;

/// <summary>
/// 表示文字文件中一個超連結的摘要資訊。
/// </summary>
/// <param name="url">連結 URL（<c>xlink:href</c>）</param>
/// <param name="displayText">顯示文字內容</param>
public sealed class OdfHyperlinkInfo(string url, string displayText)
{
    /// <summary>
    /// 取得連結 URL。
    /// </summary>
    public string Url { get; } = url ?? string.Empty;

    /// <summary>
    /// 取得顯示文字內容。
    /// </summary>
    public string DisplayText { get; } = displayText ?? string.Empty;
}
