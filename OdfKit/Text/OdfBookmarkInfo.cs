namespace OdfKit.Text;

/// <summary>
/// 表示文字文件中一個書籤的摘要資訊。
/// </summary>
/// <param name="name">書籤名稱。</param>
/// <param name="kind">書籤節點種類。</param>
public sealed class OdfBookmarkInfo(string name, OdfBookmarkKind kind)
{
    /// <summary>
    /// 取得書籤名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// 取得書籤節點種類。
    /// </summary>
    public OdfBookmarkKind Kind { get; } = kind;
}
