using System;

namespace OdfKit.Text;

/// <summary>
/// 表示 ODT 文件中一筆表格結構修訂的摘要資訊。
/// </summary>
/// <param name="changeId">修訂識別碼（<c>table:id</c>）。</param>
/// <param name="kind">結構修訂種類。</param>
/// <param name="structuralType">結構類型（<c>row</c>、<c>column</c> 或 <c>table</c>）。</param>
/// <param name="position">起始位置（以 0 為基準）。</param>
/// <param name="count">影響數量。</param>
/// <param name="author">作者。</param>
/// <param name="changedAt">修訂時間（UTC）。</param>
/// <param name="acceptanceState">接受狀態（<c>pending</c>、<c>accepted</c> 或 <c>rejected</c>）。</param>
public sealed class OdfTableStructuralChangeInfo(
    string changeId,
    OdfTableStructuralChangeKind kind,
    string structuralType,
    int position,
    int count,
    string author,
    DateTime changedAt,
    string acceptanceState)
{
    /// <summary>
    /// 取得修訂識別碼。
    /// </summary>
    public string ChangeId { get; } = changeId ?? string.Empty;

    /// <summary>
    /// 取得結構修訂種類。
    /// </summary>
    public OdfTableStructuralChangeKind Kind { get; } = kind;

    /// <summary>
    /// 取得結構類型。
    /// </summary>
    public string StructuralType { get; } = structuralType ?? string.Empty;

    /// <summary>
    /// 取得起始位置。
    /// </summary>
    public int Position { get; } = position;

    /// <summary>
    /// 取得影響數量。
    /// </summary>
    public int Count { get; } = count;

    /// <summary>
    /// 取得作者。
    /// </summary>
    public string Author { get; } = author ?? string.Empty;

    /// <summary>
    /// 取得修訂時間（UTC）。
    /// </summary>
    public DateTime ChangedAt { get; } = changedAt;

    /// <summary>
    /// 取得接受狀態。
    /// </summary>
    public string AcceptanceState { get; } = acceptanceState ?? "pending";
}
