using System;

namespace OdfKit.Text;

/// <summary>
/// Represents summary information for a comment in a text document.
/// 表示文字文件中一則註解的摘要資訊。
/// </summary>
/// <param name="name">The comment identifier. / 註解識別碼。</param>
/// <param name="author">The author. / 作者。</param>
/// <param name="text">The comment body text. / 註解內文。</param>
/// <param name="date">The comment time (UTC). / 註解時間（UTC）。</param>
/// <param name="replyCount">The reply count. / 回覆數量。</param>
public sealed class OdfCommentInfo(string name, string author, string text, DateTime date, int replyCount)
{
    /// <summary>
    /// Gets the comment identifier.
    /// 取得註解識別碼。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// Gets the comment author.
    /// 取得作者。
    /// </summary>
    public string Author { get; } = author ?? string.Empty;

    /// <summary>
    /// Gets the comment body text.
    /// 取得註解內文。
    /// </summary>
    public string Text { get; } = text ?? string.Empty;

    /// <summary>
    /// Gets the comment time (UTC).
    /// 取得註解時間（UTC）。
    /// </summary>
    public DateTime Date { get; } = date;

    /// <summary>
    /// Gets the reply count.
    /// 取得回覆數量。
    /// </summary>
    public int ReplyCount { get; } = replyCount;
}
