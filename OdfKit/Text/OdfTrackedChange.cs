using System;

namespace OdfKit.Text;

/// <summary>
/// 表示一個 ODF 追蹤修訂 (Tracked Change) 記錄。
/// </summary>
public class OdfTrackedChange
{
    /// <summary>
    /// 取得修訂的識別碼。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 取得修訂的類型（例如 "insertion"、"deletion" 或 "format-change"）。
    /// </summary>
    public string ChangeType { get; }

    /// <summary>
    /// 取得建立該修訂的人員姓名。
    /// </summary>
    public string Creator { get; }

    /// <summary>
    /// 取得建立該修訂的時間。
    /// </summary>
    public DateTime Date { get; }

    /// <summary>
    /// 取得該修訂所涉及的文字內容。
    /// </summary>
    public string Content { get; }

    /// <summary>
    /// 初始化 <see cref="OdfTrackedChange" /> 類別的新執行個體。
    /// </summary>
    /// <param name="id">修訂識別碼。</param>
    /// <param name="changeType">修訂類型。</param>
    /// <param name="creator">建立者姓名。</param>
    /// <param name="date">建立日期時間。</param>
    /// <param name="content">涉及的文字內容。</param>
    public OdfTrackedChange(string id, string changeType, string creator, DateTime date, string content)
    {
        Id = id;
        ChangeType = changeType;
        Creator = creator;
        Date = date;
        Content = content;
    }
}
