namespace OdfKit.Text;

/// <summary>
/// 表示文字文件中一個索引標記的摘要資訊。
/// </summary>
/// <param name="kind">索引標記類型</param>
/// <param name="term">索引詞彙或顯示文字</param>
/// <param name="key1">主要鍵值（字母索引）</param>
/// <param name="key2">次要鍵值（字母索引）</param>
/// <param name="identifier">文獻識別碼（文獻標記）</param>
/// <param name="bibliographyType">文獻類型（文獻標記）</param>
public sealed class OdfDocumentIndexMarkInfo(
    OdfIndexMarkKind kind,
    string term,
    string? key1,
    string? key2,
    string? identifier,
    string? bibliographyType)
{
    /// <summary>
    /// 取得索引標記類型。
    /// </summary>
    public OdfIndexMarkKind Kind { get; } = kind;

    /// <summary>
    /// 取得索引詞彙或顯示文字。
    /// </summary>
    public string Term { get; } = term ?? string.Empty;

    /// <summary>
    /// 取得主要鍵值。
    /// </summary>
    public string? Key1 { get; } = key1;

    /// <summary>
    /// 取得次要鍵值。
    /// </summary>
    public string? Key2 { get; } = key2;

    /// <summary>
    /// 取得文獻識別碼。
    /// </summary>
    public string? Identifier { get; } = identifier;

    /// <summary>
    /// 取得文獻類型。
    /// </summary>
    public string? BibliographyType { get; } = bibliographyType;
}
