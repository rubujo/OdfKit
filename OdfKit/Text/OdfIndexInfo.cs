namespace OdfKit.Text;

/// <summary>
/// 表示文字文件中一個索引的摘要資訊。
/// </summary>
/// <param name="kind">索引類型。</param>
/// <param name="name">索引名稱（<c>text:name</c>）。</param>
public sealed class OdfIndexInfo(OdfIndexKind kind, string name)
{
    /// <summary>
    /// 取得索引類型。
    /// </summary>
    public OdfIndexKind Kind { get; } = kind;

    /// <summary>
    /// 取得索引名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;
}
