namespace OdfKit.Presentation;

/// <summary>
/// Represents speaker notes summary information for a presentation slide.
/// 表示簡報中某一投影片的主講人備忘錄摘要資訊。
/// </summary>
/// <param name="slideIndex">The slide index. / 投影片索引位置。</param>
/// <param name="slideName">The slide name. / 投影片名稱。</param>
/// <param name="notesText">The speaker notes text. / 主講人備忘錄文字。</param>
public sealed class OdfSlideSpeakerNotesInfo(int slideIndex, string slideName, string notesText)
{
    /// <summary>
    /// Gets the slide index.
    /// 取得投影片索引位置。
    /// </summary>
    public int SlideIndex { get; } = slideIndex;

    /// <summary>
    /// Gets the slide name.
    /// 取得投影片名稱。
    /// </summary>
    public string SlideName { get; } = slideName ?? string.Empty;

    /// <summary>
    /// Gets the speaker notes text.
    /// 取得主講人備忘錄文字。
    /// </summary>
    public string NotesText { get; } = notesText ?? string.Empty;
}
