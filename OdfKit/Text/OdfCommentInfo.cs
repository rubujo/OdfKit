using System;

namespace OdfKit.Text;

/// <summary>
/// Represents odf comment info.
/// 表示文字文件中一則註解的摘要資訊。
/// </summary>
/// <param name="name">The name or identifier. / 註解識別碼</param>
/// <param name="author">The name or identifier. / 作者</param>
/// <param name="text">The text or value. / 註解內文</param>
/// <param name="date">The value to use. / 註解時間（UTC）</param>
/// <param name="replyCount">The numeric value. / 回覆數量</param>
public sealed class OdfCommentInfo(string name, string author, string text, DateTime date, int replyCount)
{
    /// <summary>
    /// Gets name.
    /// 取得註解識別碼。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// Gets author.
    /// 取得作者。
    /// </summary>
    public string Author { get; } = author ?? string.Empty;

    /// <summary>
    /// Gets text.
    /// 取得註解內文。
    /// </summary>
    public string Text { get; } = text ?? string.Empty;

    /// <summary>
    /// Gets date.
    /// 取得註解時間（UTC）。
    /// </summary>
    public DateTime Date { get; } = date;

    /// <summary>
    /// Gets reply count.
    /// 取得回覆數量。
    /// </summary>
    public int ReplyCount { get; } = replyCount;
}
