namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents summary information for split pane settings on a worksheet in a spreadsheet.
/// 表示試算表中一個工作表分割窗格設定的摘要資訊。
/// </summary>
/// <param name="sheetName">The sheet name. / 工作表名稱。</param>
/// <param name="splitPanes">The split pane settings. / 分割窗格設定。</param>
public sealed class OdfSheetSplitPanesInfo(string sheetName, OdfSplitPanes splitPanes)
{
    /// <summary>
    /// Gets the sheet name.
    /// 取得工作表名稱。
    /// </summary>
    public string SheetName { get; } = sheetName ?? string.Empty;

    /// <summary>
    /// Gets the split pane settings.
    /// 取得分割窗格設定。
    /// </summary>
    public OdfSplitPanes SplitPanes { get; } = splitPanes;
}
