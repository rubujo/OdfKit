namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示試算表中一個工作表凍結窗格設定的摘要資訊。
/// </summary>
/// <param name="sheetName">工作表名稱</param>
/// <param name="frozenPanes">凍結窗格設定</param>
public sealed class OdfSheetFrozenPanesInfo(string sheetName, OdfFrozenPanes frozenPanes)
{
    /// <summary>
    /// 取得工作表名稱。
    /// </summary>
    public string SheetName { get; } = sheetName ?? string.Empty;

    /// <summary>
    /// 取得凍結窗格設定。
    /// </summary>
    public OdfFrozenPanes FrozenPanes { get; } = frozenPanes;
}
