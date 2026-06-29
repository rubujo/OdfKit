namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents summary information for frozen pane settings on a worksheet in a spreadsheet.
/// 表示試算表中一個工作表凍結窗格設定的摘要資訊。
/// </summary>
/// <param name="sheetName">The sheet name. / 工作表名稱。</param>
/// <param name="frozenPanes">The frozen pane settings. / 凍結窗格設定。</param>
public sealed class OdfSheetFrozenPanesInfo(string sheetName, OdfFrozenPanes frozenPanes)
{
    /// <summary>
    /// Gets the sheet name.
    /// 取得工作表名稱。
    /// </summary>
    public string SheetName { get; } = sheetName ?? string.Empty;

    /// <summary>
    /// Gets the frozen pane settings.
    /// 取得凍結窗格設定。
    /// </summary>
    public OdfFrozenPanes FrozenPanes { get; } = frozenPanes;
}
