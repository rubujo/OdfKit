namespace OdfKit.Text;

/// <summary>
/// 表示文字文件中一個參考標記的摘要資訊。
/// </summary>
/// <param name="name">參考標記名稱</param>
public sealed class OdfReferenceMarkInfo(string name)
{
    /// <summary>
    /// 取得參考標記名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;
}
