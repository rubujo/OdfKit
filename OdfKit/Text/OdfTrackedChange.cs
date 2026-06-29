using System;

namespace OdfKit.Text;

/// <summary>
/// Represents the change type of an ODF tracked change.
/// ODF 追蹤修訂的變更類型。
/// </summary>
public enum OdfChangeType
{
    /// <summary>
    /// An insertion.
    /// 插入。
    /// </summary>
    Insertion,
    /// <summary>
    /// A deletion.
    /// 刪除。
    /// </summary>
    Deletion,
    /// <summary>
    /// A format change.
    /// 格式變更。
    /// </summary>
    FormatChange,
}

/// <summary>
/// Represents an ODF tracked change record.
/// 表示一個 ODF 追蹤修訂 (Tracked Change) 記錄。
/// </summary>
public sealed class OdfTrackedChange
{
    /// <summary>
    /// Gets the change region ID (text:id).
    /// 取得變更區域 ID（text:id）。
    /// </summary>
    public string RegionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the change type of this revision.
    /// 取得修訂的變更類型。
    /// </summary>
    public OdfChangeType ChangeType { get; init; }

    /// <summary>
    /// Gets the name of the person who made this revision.
    /// 取得建立該修訂的人員姓名。
    /// </summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>
    /// Gets the UTC time this revision was made.
    /// 取得建立該修訂的時間（UTC）。
    /// </summary>
    public DateTime ChangedAt { get; init; }

    /// <summary>
    /// Gets the text content involved in this revision.
    /// 取得該修訂所涉及的文字內容。
    /// </summary>
    public string Content { get; init; } = string.Empty;
}
