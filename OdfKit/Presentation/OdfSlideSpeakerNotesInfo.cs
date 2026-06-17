namespace OdfKit.Presentation;

/// <summary>
/// 表示簡報中某一投影片的主講人備忘錄摘要資訊。
/// </summary>
/// <param name="slideIndex">投影片索引位置。</param>
/// <param name="slideName">投影片名稱。</param>
/// <param name="notesText">主講人備忘錄文字。</param>
public sealed class OdfSlideSpeakerNotesInfo(int slideIndex, string slideName, string notesText)
{
    /// <summary>
    /// 取得投影片索引位置。
    /// </summary>
    public int SlideIndex { get; } = slideIndex;

    /// <summary>
    /// 取得投影片名稱。
    /// </summary>
    public string SlideName { get; } = slideName ?? string.Empty;

    /// <summary>
    /// 取得主講人備忘錄文字。
    /// </summary>
    public string NotesText { get; } = notesText ?? string.Empty;
}
