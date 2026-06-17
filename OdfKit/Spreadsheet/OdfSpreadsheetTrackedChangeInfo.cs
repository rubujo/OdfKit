using System;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示 ODS 試算表上一筆追蹤修訂的摘要資訊。
/// </summary>
/// <param name="changeId">修訂識別碼（<c>table:id</c>）。</param>
/// <param name="kind">修訂種類。</param>
/// <param name="author">作者。</param>
/// <param name="changedAt">修訂時間（UTC）。</param>
/// <param name="cellAddress">受影響儲存格位址；非儲存格內容變更時為 <see langword="null"/>。</param>
/// <param name="previousContent">變更前的顯示文字；無法取得時為 <see langword="null"/>。</param>
/// <param name="acceptanceState">接受狀態（<c>pending</c>、<c>accepted</c> 或 <c>rejected</c>）。</param>
public sealed class OdfSpreadsheetTrackedChangeInfo(
    string changeId,
    OdfSpreadsheetChangeKind kind,
    string author,
    DateTime changedAt,
    OdfCellAddress? cellAddress,
    string? previousContent,
    string acceptanceState)
{
    /// <summary>
    /// 取得修訂識別碼。
    /// </summary>
    public string ChangeId { get; } = changeId ?? string.Empty;

    /// <summary>
    /// 取得修訂種類。
    /// </summary>
    public OdfSpreadsheetChangeKind Kind { get; } = kind;

    /// <summary>
    /// 取得作者。
    /// </summary>
    public string Author { get; } = author ?? string.Empty;

    /// <summary>
    /// 取得修訂時間（UTC）。
    /// </summary>
    public DateTime ChangedAt { get; } = changedAt;

    /// <summary>
    /// 取得受影響儲存格位址。
    /// </summary>
    public OdfCellAddress? CellAddress { get; } = cellAddress;

    /// <summary>
    /// 取得變更前的顯示文字。
    /// </summary>
    public string? PreviousContent { get; } = previousContent;

    /// <summary>
    /// 取得接受狀態。
    /// </summary>
    public string AcceptanceState { get; } = acceptanceState ?? "pending";
}
