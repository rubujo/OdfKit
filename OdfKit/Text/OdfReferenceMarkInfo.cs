namespace OdfKit.Text;

/// <summary>
/// Represents summary information for a reference mark in a text document.
/// 表示文字文件中一個參考標記的摘要資訊。
/// </summary>
/// <param name="name">The reference mark name. / 參考標記名稱。</param>
public sealed class OdfReferenceMarkInfo(string name)
{
    /// <summary>
    /// Gets the reference mark name.
    /// 取得參考標記名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;
}
