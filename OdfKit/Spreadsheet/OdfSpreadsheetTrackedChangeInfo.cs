using System;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents summary information for a tracked change in an ODS spreadsheet.
/// 表示 ODS 試算表上一筆追蹤修訂的摘要資訊。
/// </summary>
/// <param name="changeId">The change identifier from <c>table:id</c>. / 修訂識別碼（<c>table:id</c>）。</param>
/// <param name="kind">The change kind. / 修訂種類。</param>
/// <param name="author">The author. / 作者。</param>
/// <param name="changedAt">The change time in UTC. / 修訂時間（UTC）。</param>
/// <param name="cellAddress">The affected cell address, or <see langword="null"/> for non-cell content changes. / 受影響儲存格位址；非儲存格內容變更時為 <see langword="null"/>。</param>
/// <param name="previousContent">The display text before the change, or <see langword="null"/> when unavailable. / 變更前的顯示文字；無法取得時為 <see langword="null"/>。</param>
/// <param name="acceptanceState">The acceptance state, such as <c>pending</c>, <c>accepted</c>, or <c>rejected</c>. / 接受狀態（<c>pending</c>、<c>accepted</c> 或 <c>rejected</c>）。</param>
/// <param name="previousFormula">The formula before the change from <c>table:formula</c>, or <see langword="null"/> when unavailable. / 變更前的公式（<c>table:formula</c>）；無法取得時為 <see langword="null"/>。</param>
/// <param name="sheetName">The sheet name containing the structural change. / 結構修訂所在工作表名稱。</param>
/// <param name="structuralType">The structural change type, such as <c>row</c>, <c>column</c>, or <c>table</c>. / 結構修訂類型（<c>row</c>、<c>column</c> 或 <c>table</c>）。</param>
/// <param name="structuralPosition">The zero-based start position of the structural change. / 結構修訂起始位置，採 0 為基準。</param>
/// <param name="structuralCount">The number of items affected by the structural change. / 結構修訂影響數量。</param>
/// <param name="sourceAddress">The source address of a move change. / 移動修訂來源位址。</param>
/// <param name="targetAddress">The target address of a move change. / 移動修訂目標位址。</param>
public sealed class OdfSpreadsheetTrackedChangeInfo(
    string changeId,
    OdfSpreadsheetChangeKind kind,
    string author,
    DateTime changedAt,
    OdfCellAddress? cellAddress,
    string? previousContent,
    string acceptanceState,
    string? previousFormula = null,
    string? sheetName = null,
    string? structuralType = null,
    int? structuralPosition = null,
    int? structuralCount = null,
    OdfCellAddress? sourceAddress = null,
    OdfCellAddress? targetAddress = null)
{
    /// <summary>
    /// Gets the change identifier.
    /// 取得修訂識別碼。
    /// </summary>
    public string ChangeId { get; } = changeId ?? string.Empty;

    /// <summary>
    /// Gets the change kind.
    /// 取得修訂種類。
    /// </summary>
    public OdfSpreadsheetChangeKind Kind { get; } = kind;

    /// <summary>
    /// Gets the author.
    /// 取得作者。
    /// </summary>
    public string Author { get; } = author ?? string.Empty;

    /// <summary>
    /// Gets the change time in UTC.
    /// 取得修訂時間（UTC）。
    /// </summary>
    public DateTime ChangedAt { get; } = changedAt;

    /// <summary>
    /// Gets the affected cell address.
    /// 取得受影響儲存格位址。
    /// </summary>
    public OdfCellAddress? CellAddress { get; } = cellAddress;

    /// <summary>
    /// Gets the display text before the change.
    /// 取得變更前的顯示文字。
    /// </summary>
    public string? PreviousContent { get; } = previousContent;

    /// <summary>
    /// Gets the formula before the change.
    /// 取得變更前的公式。
    /// </summary>
    public string? PreviousFormula { get; } = previousFormula;

    /// <summary>
    /// Gets the acceptance state.
    /// 取得接受狀態。
    /// </summary>
    public string AcceptanceState { get; } = acceptanceState ?? "pending";

    /// <summary>
    /// Gets the sheet name containing the structural change.
    /// 取得結構修訂所在工作表名稱。
    /// </summary>
    public string? SheetName { get; } = sheetName;

    /// <summary>
    /// Gets the structural change type.
    /// 取得結構修訂類型。
    /// </summary>
    public string? StructuralType { get; } = structuralType;

    /// <summary>
    /// Gets the start position of the structural change.
    /// 取得結構修訂起始位置。
    /// </summary>
    public int? StructuralPosition { get; } = structuralPosition;

    /// <summary>
    /// Gets the number of items affected by the structural change.
    /// 取得結構修訂影響數量。
    /// </summary>
    public int? StructuralCount { get; } = structuralCount;

    /// <summary>
    /// Gets the source address of a move change.
    /// 取得移動修訂來源位址。
    /// </summary>
    public OdfCellAddress? SourceAddress { get; } = sourceAddress;

    /// <summary>
    /// Gets the target address of a move change.
    /// 取得移動修訂目標位址。
    /// </summary>
    public OdfCellAddress? TargetAddress { get; } = targetAddress;
}
