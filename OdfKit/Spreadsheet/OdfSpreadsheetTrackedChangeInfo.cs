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
/// <param name="previousFormula">變更前的公式（<c>table:formula</c>）；無法取得時為 <see langword="null"/>。</param>
/// <param name="sheetName">結構修訂所在工作表名稱。</param>
/// <param name="structuralType">結構修訂類型（<c>row</c>、<c>column</c> 或 <c>table</c>）。</param>
/// <param name="structuralPosition">結構修訂起始位置（以 0 為基準）。</param>
/// <param name="structuralCount">結構修訂影響數量。</param>
/// <param name="sourceAddress">移動修訂來源位址。</param>
/// <param name="targetAddress">移動修訂目標位址。</param>
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
    /// 取得變更前的公式。
    /// </summary>
    public string? PreviousFormula { get; } = previousFormula;

    /// <summary>
    /// 取得接受狀態。
    /// </summary>
    public string AcceptanceState { get; } = acceptanceState ?? "pending";

    /// <summary>
    /// 取得結構修訂所在工作表名稱。
    /// </summary>
    public string? SheetName { get; } = sheetName;

    /// <summary>
    /// 取得結構修訂類型。
    /// </summary>
    public string? StructuralType { get; } = structuralType;

    /// <summary>
    /// 取得結構修訂起始位置。
    /// </summary>
    public int? StructuralPosition { get; } = structuralPosition;

    /// <summary>
    /// 取得結構修訂影響數量。
    /// </summary>
    public int? StructuralCount { get; } = structuralCount;

    /// <summary>
    /// 取得移動修訂來源位址。
    /// </summary>
    public OdfCellAddress? SourceAddress { get; } = sourceAddress;

    /// <summary>
    /// 取得移動修訂目標位址。
    /// </summary>
    public OdfCellAddress? TargetAddress { get; } = targetAddress;
}
