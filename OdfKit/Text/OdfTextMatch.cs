namespace OdfKit.Text;

/// <summary>
/// Describes one task-oriented text search match.
/// 描述一筆任務導向文字搜尋結果。
/// </summary>
public sealed class OdfTextMatch
{
    /// <summary>
    /// Creates a match with only a document-relative offset (no stable paragraph handle).
    /// 建立僅具文件相對 offset（無穩定段落 handle）的符合項目。
    /// </summary>
    /// <param name="index">The zero-based text index. / 從零開始的文字索引。</param>
    /// <param name="length">The matched text length. / 符合文字的長度。</param>
    /// <param name="value">The matched text. / 符合的文字。</param>
    public OdfTextMatch(int index, int length, string value)
    {
        Index = index;
        Length = length;
        Value = value;
    }

    /// <summary>
    /// Creates a match that also carries a stable paragraph range handle.
    /// 建立同時攜帶穩定段落 range handle 的符合項目。
    /// </summary>
    /// <param name="index">The zero-based text index. / 從零開始的文字索引。</param>
    /// <param name="length">The matched text length. / 符合文字的長度。</param>
    /// <param name="value">The matched text. / 符合的文字。</param>
    /// <param name="paragraph">The paragraph containing this match. / 包含此符合項目的段落。</param>
    /// <param name="paragraphOffset">The offset within the paragraph's own text. / 在段落自身文字中的 offset。</param>
    internal OdfTextMatch(int index, int length, string value, OdfParagraph paragraph, int paragraphOffset)
        : this(index, length, value)
    {
        Paragraph = paragraph;
        ParagraphOffset = paragraphOffset;
    }

    /// <summary>
    /// Gets the zero-based text index.
    /// 取得從零開始的文字索引。
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the matched text length.
    /// 取得符合文字的長度。
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the matched text.
    /// 取得符合的文字。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the paragraph containing this match, or null when no paragraph handle is available.
    /// 取得包含此符合項目的段落；若沒有可用的段落控制代碼，則為 null。
    /// </summary>
    public OdfParagraph? Paragraph { get; }

    /// <summary>
    /// Gets the zero-based offset of this match within the paragraph's concatenated text.
    /// 取得此符合項目在段落串接文字中從零開始的位移。
    /// </summary>
    public int ParagraphOffset { get; }
}
