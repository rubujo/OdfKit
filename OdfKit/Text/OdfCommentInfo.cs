using System;

namespace OdfKit.Text;

/// <summary>
/// 表示文字文件中一則註解的摘要資訊。
/// </summary>
/// <param name="name">註解識別碼。</param>
/// <param name="author">作者。</param>
/// <param name="text">註解內文。</param>
/// <param name="date">註解時間（UTC）。</param>
/// <param name="replyCount">回覆數量。</param>
public sealed class OdfCommentInfo(string name, string author, string text, DateTime date, int replyCount)
{
    /// <summary>
    /// 取得註解識別碼。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// 取得作者。
    /// </summary>
    public string Author { get; } = author ?? string.Empty;

    /// <summary>
    /// 取得註解內文。
    /// </summary>
    public string Text { get; } = text ?? string.Empty;

    /// <summary>
    /// 取得註解時間（UTC）。
    /// </summary>
    public DateTime Date { get; } = date;

    /// <summary>
    /// 取得回覆數量。
    /// </summary>
    public int ReplyCount { get; } = replyCount;
}
