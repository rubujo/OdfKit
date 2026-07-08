namespace OdfKit.Spreadsheet;

/// <summary>
/// Configures a practical spreadsheet table facade backed by an ODF database range.
/// 設定由 ODF 資料庫範圍支援的實務試算表表格 facade。
/// </summary>
public sealed class OdfSpreadsheetTableOptions
{
    /// <summary>
    /// Gets or sets whether the first row is treated as a header row.
    /// 取得或設定首列是否視為標題列。
    /// </summary>
    public bool FirstRowAsHeader { get; set; } = true;

    /// <summary>
    /// Gets or sets whether filter buttons are shown.
    /// 取得或設定是否顯示篩選按鈕。
    /// </summary>
    public bool DisplayFilterButtons { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a named range with the same name is created.
    /// 取得或設定是否建立同名的命名範圍。
    /// </summary>
    public bool CreateNamedRange { get; set; } = true;
}
