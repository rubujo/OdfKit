namespace OdfKit.Text;

/// <summary>
/// Represents odf bookmark info.
/// 表示文字文件中一個書籤的摘要資訊。
/// </summary>
/// <param name="name">The name or identifier. / 書籤名稱</param>
/// <param name="kind">The value to use. / 書籤節點種類</param>
public sealed class OdfBookmarkInfo(string name, OdfBookmarkKind kind)
{
    /// <summary>
    /// Gets name.
    /// 取得書籤名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// Gets kind.
    /// 取得書籤節點種類。
    /// </summary>
    public OdfBookmarkKind Kind { get; } = kind;
}
