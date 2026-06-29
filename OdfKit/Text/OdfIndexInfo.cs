namespace OdfKit.Text;

/// <summary>
/// Represents odf index info.
/// 表示文字文件中一個索引的摘要資訊。
/// </summary>
/// <param name="kind">The value to use. / 索引類型</param>
/// <param name="name">The name or identifier. / 索引名稱（<c>text:name</c>）</param>
public sealed class OdfIndexInfo(OdfIndexKind kind, string name)
{
    /// <summary>
    /// Gets kind.
    /// 取得索引類型。
    /// </summary>
    public OdfIndexKind Kind { get; } = kind;

    /// <summary>
    /// Gets name.
    /// 取得索引名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;
}
