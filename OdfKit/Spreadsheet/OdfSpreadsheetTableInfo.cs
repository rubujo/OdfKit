namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents summary information for a practical spreadsheet table.
/// 表示實務試算表表格的摘要資訊。
/// </summary>
/// <param name="Name">The table name. / 表格名稱。</param>
/// <param name="TargetRangeAddress">The target range address. / 目標範圍位址。</param>
/// <param name="FirstRowAsHeader">Whether the first row is treated as a header row. / 首列是否視為標題列。</param>
/// <param name="DisplayFilterButtons">Whether filter buttons are displayed. / 是否顯示篩選按鈕。</param>
public sealed record OdfSpreadsheetTableInfo(
    string Name,
    string TargetRangeAddress,
    bool FirstRowAsHeader,
    bool DisplayFilterButtons);
