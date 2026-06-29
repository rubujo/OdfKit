namespace OdfKit.Text;

/// <summary>
/// Represents summary information for an index mark in a text document.
/// 表示文字文件中一個索引標記的摘要資訊。
/// </summary>
/// <param name="kind">The index mark kind. / 索引標記類型。</param>
/// <param name="term">The index term or display text. / 索引詞彙或顯示文字。</param>
/// <param name="key1">The primary key (alphabetical index). / 主要鍵值（字母索引）。</param>
/// <param name="key2">The secondary key (alphabetical index). / 次要鍵值（字母索引）。</param>
/// <param name="identifier">The bibliography identifier (bibliography mark). / 文獻識別碼（文獻標記）。</param>
/// <param name="bibliographyType">The bibliography type (bibliography mark). / 文獻類型（文獻標記）。</param>
public sealed class OdfDocumentIndexMarkInfo(
    OdfIndexMarkKind kind,
    string term,
    string? key1,
    string? key2,
    string? identifier,
    string? bibliographyType)
{
    /// <summary>
    /// Gets the index mark kind.
    /// 取得索引標記類型。
    /// </summary>
    public OdfIndexMarkKind Kind { get; } = kind;

    /// <summary>
    /// Gets the index term or display text.
    /// 取得索引詞彙或顯示文字。
    /// </summary>
    public string Term { get; } = term ?? string.Empty;

    /// <summary>
    /// Gets the primary key.
    /// 取得主要鍵值。
    /// </summary>
    public string? Key1 { get; } = key1;

    /// <summary>
    /// Gets the secondary key.
    /// 取得次要鍵值。
    /// </summary>
    public string? Key2 { get; } = key2;

    /// <summary>
    /// Gets the bibliography identifier.
    /// 取得文獻識別碼。
    /// </summary>
    public string? Identifier { get; } = identifier;

    /// <summary>
    /// Gets the bibliography type.
    /// 取得文獻類型。
    /// </summary>
    public string? BibliographyType { get; } = bibliographyType;
}
