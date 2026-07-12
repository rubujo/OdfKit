namespace OdfKit.Text;

/// <summary>
/// Describes one task-oriented text search match.
/// 描述一筆任務導向文字搜尋結果。
/// </summary>
/// <param name="index">The zero-based text index. / 從零開始的文字索引。</param>
/// <param name="length">The matched text length. / 符合文字的長度。</param>
/// <param name="value">The matched text. / 符合的文字。</param>
public sealed class OdfTextMatch(int index, int length, string value)
{
    /// <summary>
    /// Gets the zero-based text index.
    /// 取得從零開始的文字索引。
    /// </summary>
    public int Index { get; } = index;

    /// <summary>
    /// Gets the matched text length.
    /// 取得符合文字的長度。
    /// </summary>
    public int Length { get; } = length;

    /// <summary>
    /// Gets the matched text.
    /// 取得符合的文字。
    /// </summary>
    public string Value { get; } = value;
}
