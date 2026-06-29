namespace OdfKit.Text;

/// <summary>
/// Represents summary information for an index in a text document.
/// 表示文字文件中一個索引的摘要資訊。
/// </summary>
/// <param name="kind">The index kind. / 索引類型。</param>
/// <param name="name">The index name (<c>text:name</c>). / 索引名稱（<c>text:name</c>）。</param>
public sealed class OdfIndexInfo(OdfIndexKind kind, string name)
{
    /// <summary>
    /// Gets the index kind.
    /// 取得索引類型。
    /// </summary>
    public OdfIndexKind Kind { get; } = kind;

    /// <summary>
    /// Gets the index name.
    /// 取得索引名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;
}
