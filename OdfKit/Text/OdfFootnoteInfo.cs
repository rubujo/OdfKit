namespace OdfKit.Text;

/// <summary>
/// Represents odf footnote info.
/// 表示文字文件中一則腳注或尾注的摘要資訊。
/// </summary>
/// <param name="id">The name or identifier. / 注腳識別碼</param>
/// <param name="citation">The value to use. / 引用標記文字</param>
/// <param name="bodyText">The text or value. / 注腳本文內容</param>
public sealed class OdfFootnoteInfo(string id, string citation, string bodyText)
{
    /// <summary>
    /// Gets id.
    /// 取得注腳識別碼。
    /// </summary>
    public string Id { get; } = id ?? string.Empty;

    /// <summary>
    /// Gets citation.
    /// 取得引用標記文字。
    /// </summary>
    public string Citation { get; } = citation ?? string.Empty;

    /// <summary>
    /// Gets body text.
    /// 取得注腳本文內容。
    /// </summary>
    public string BodyText { get; } = bodyText ?? string.Empty;
}
