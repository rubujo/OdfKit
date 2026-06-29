using System;

namespace OdfKit.Text;

/// <summary>
/// Represents summary information for a table structural change in an ODT document.
/// 表示 ODT 文件中一筆表格結構修訂的摘要資訊。
/// </summary>
/// <param name="changeId">The change identifier (<c>table:id</c>). / 修訂識別碼（<c>table:id</c>）。</param>
/// <param name="kind">The kind of structural change. / 結構修訂種類。</param>
/// <param name="structuralType">The structural type (<c>row</c>, <c>column</c>, or <c>table</c>). / 結構類型（<c>row</c>、<c>column</c> 或 <c>table</c>）。</param>
/// <param name="position">The zero-based start position. / 起始位置（以 0 為基準）。</param>
/// <param name="count">The number of affected rows/columns. / 影響數量。</param>
/// <param name="author">The author. / 作者。</param>
/// <param name="changedAt">The UTC time of the change. / 修訂時間（UTC）。</param>
/// <param name="acceptanceState">The acceptance state (<c>pending</c>, <c>accepted</c>, or <c>rejected</c>). / 接受狀態（<c>pending</c>、<c>accepted</c> 或 <c>rejected</c>）。</param>
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
    /// Gets the change identifier.
    /// 取得修訂識別碼。
    /// </summary>
    public string ChangeId { get; } = changeId ?? string.Empty;

    /// <summary>
    /// Gets the kind of structural change.
    /// 取得結構修訂種類。
    /// </summary>
    public OdfTableStructuralChangeKind Kind { get; } = kind;

    /// <summary>
    /// Gets the structural type.
    /// 取得結構類型。
    /// </summary>
    public string StructuralType { get; } = structuralType ?? string.Empty;

    /// <summary>
    /// Gets the start position.
    /// 取得起始位置。
    /// </summary>
    public int Position { get; } = position;

    /// <summary>
    /// Gets the number of affected rows/columns.
    /// 取得影響數量。
    /// </summary>
    public int Count { get; } = count;

    /// <summary>
    /// Gets the author who made the structural table change.
    /// 取得作者。
    /// </summary>
    public string Author { get; } = author ?? string.Empty;

    /// <summary>
    /// Gets the UTC time of the change.
    /// 取得修訂時間（UTC）。
    /// </summary>
    public DateTime ChangedAt { get; } = changedAt;

    /// <summary>
    /// Gets the acceptance state.
    /// 取得接受狀態。
    /// </summary>
    public string AcceptanceState { get; } = acceptanceState ?? "pending";
}
