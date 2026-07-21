namespace OdfKit.WebFonts;

/// <summary>
/// Filters requested text to the glyph coverage of one trusted font face.
/// 依單一受信任字型 face 的 glyph 覆蓋範圍篩選要求文字。
/// </summary>
public interface IWebFontTextCoverageFilter
{
    /// <summary>
    /// Returns contiguous text sequences supported by the selected face.
    /// 回傳所選 face 支援的連續文字序列。
    /// </summary>
    /// <param name="face">The trusted font face. / 受信任的字型 face。</param>
    /// <param name="sequences">The requested text sequences. / 要求的文字序列。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The supported contiguous sequences; an empty collection means the face has no requested glyphs. / 支援的連續序列；空集合表示該 face 不含任何要求的 glyph。</returns>
    Task<IReadOnlyList<WebFontTextSequence>> FilterSupportedSequencesAsync(
        WebFontFaceIdentity face,
        IReadOnlyList<WebFontTextSequence> sequences,
        CancellationToken cancellationToken = default);
}
