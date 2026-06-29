namespace OdfKit.Text;

/// <summary>
/// Represents summary information for a bookmark in a text document.
/// 表示文字文件中一個書籤的摘要資訊。
/// </summary>
/// <param name="name">The bookmark name. / 書籤名稱。</param>
/// <param name="kind">The bookmark node kind. / 書籤節點種類。</param>
public sealed class OdfBookmarkInfo(string name, OdfBookmarkKind kind)
{
    /// <summary>
    /// Gets the bookmark name.
    /// 取得書籤名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// Gets the bookmark node kind.
    /// 取得書籤節點種類。
    /// </summary>
    public OdfBookmarkKind Kind { get; } = kind;
}
