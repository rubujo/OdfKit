namespace OdfKit.Text;

/// <summary>
/// Represents summary information for a footnote or endnote in a text document.
/// 表示文字文件中一則腳注或尾注的摘要資訊。
/// </summary>
/// <param name="id">The footnote identifier. / 注腳識別碼。</param>
/// <param name="citation">The citation marker text. / 引用標記文字。</param>
/// <param name="bodyText">The footnote body content. / 注腳本文內容。</param>
public sealed class OdfFootnoteInfo(string id, string citation, string bodyText)
{
    /// <summary>
    /// Gets the footnote identifier.
    /// 取得注腳識別碼。
    /// </summary>
    public string Id { get; } = id ?? string.Empty;

    /// <summary>
    /// Gets the citation marker text.
    /// 取得引用標記文字。
    /// </summary>
    public string Citation { get; } = citation ?? string.Empty;

    /// <summary>
    /// Gets the footnote body content.
    /// 取得注腳本文內容。
    /// </summary>
    public string BodyText { get; } = bodyText ?? string.Empty;
}
