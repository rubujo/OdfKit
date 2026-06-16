using System;

namespace OdfKit.Text;

/// <summary>
/// ODF 追蹤修訂的變更類型。
/// </summary>
public enum OdfChangeType
{
    /// <summary>插入。</summary>
    Insertion,
    /// <summary>刪除。</summary>
    Deletion,
    /// <summary>格式變更。</summary>
    FormatChange,
}

/// <summary>
/// 表示一個 ODF 追蹤修訂 (Tracked Change) 記錄。
/// </summary>
public sealed class OdfTrackedChange
{
    /// <summary>
    /// 取得變更區域 ID（text:id）。
    /// </summary>
    public string RegionId { get; init; } = string.Empty;

    /// <summary>
    /// 取得修訂的變更類型。
    /// </summary>
    public OdfChangeType ChangeType { get; init; }

    /// <summary>
    /// 取得建立該修訂的人員姓名。
    /// </summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>
    /// 取得建立該修訂的時間（UTC）。
    /// </summary>
    public DateTime ChangedAt { get; init; }

    /// <summary>
    /// 取得該修訂所涉及的文字內容。
    /// </summary>
    public string Content { get; init; } = string.Empty;
}
