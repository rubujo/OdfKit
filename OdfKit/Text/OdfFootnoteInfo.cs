namespace OdfKit.Text;

/// <summary>
/// 表示文字文件中一則腳注或尾注的摘要資訊。
/// </summary>
/// <param name="id">注腳識別碼</param>
/// <param name="citation">引用標記文字</param>
/// <param name="bodyText">注腳本文內容</param>
public sealed class OdfFootnoteInfo(string id, string citation, string bodyText)
{
    /// <summary>
    /// 取得注腳識別碼。
    /// </summary>
    public string Id { get; } = id ?? string.Empty;

    /// <summary>
    /// 取得引用標記文字。
    /// </summary>
    public string Citation { get; } = citation ?? string.Empty;

    /// <summary>
    /// 取得注腳本文內容。
    /// </summary>
    public string BodyText { get; } = bodyText ?? string.Empty;
}
