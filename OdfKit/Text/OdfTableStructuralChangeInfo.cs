using System;

namespace OdfKit.Text;

/// <summary>
/// Represents odf table structural change info.
/// 表示 ODT 文件中一筆表格結構修訂的摘要資訊。
/// </summary>
/// <param name="changeId">The name or identifier. / 修訂識別碼（<c>table:id</c>）</param>
/// <param name="kind">The value to use. / 結構修訂種類</param>
/// <param name="structuralType">The value to use. / 結構類型（<c>row</c>、<c>column</c> 或 <c>table</c>）</param>
/// <param name="position">The numeric value. / 起始位置（以 0 為基準）</param>
/// <param name="count">The numeric value. / 影響數量</param>
/// <param name="author">The name or identifier. / 作者</param>
/// <param name="changedAt">The value to use. / 修訂時間（UTC）</param>
/// <param name="acceptanceState">The value to use. / 接受狀態（<c>pending</c>、<c>accepted</c> 或 <c>rejected</c>）</param>
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
    /// Gets change id.
    /// 取得修訂識別碼。
    /// </summary>
    public string ChangeId { get; } = changeId ?? string.Empty;

    /// <summary>
    /// Gets kind.
    /// 取得結構修訂種類。
    /// </summary>
    public OdfTableStructuralChangeKind Kind { get; } = kind;

    /// <summary>
    /// Gets structural type.
    /// 取得結構類型。
    /// </summary>
    public string StructuralType { get; } = structuralType ?? string.Empty;

    /// <summary>
    /// Gets position.
    /// 取得起始位置。
    /// </summary>
    public int Position { get; } = position;

    /// <summary>
    /// Gets count.
    /// 取得影響數量。
    /// </summary>
    public int Count { get; } = count;

    /// <summary>
    /// Gets author.
    /// 取得作者。
    /// </summary>
    public string Author { get; } = author ?? string.Empty;

    /// <summary>
    /// Gets changed at.
    /// 取得修訂時間（UTC）。
    /// </summary>
    public DateTime ChangedAt { get; } = changedAt;

    /// <summary>
    /// Gets acceptance state.
    /// 取得接受狀態。
    /// </summary>
    public string AcceptanceState { get; } = acceptanceState ?? "pending";
}
