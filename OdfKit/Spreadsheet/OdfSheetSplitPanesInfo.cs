namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示試算表中一個工作表分割窗格設定的摘要資訊。
/// </summary>
/// <param name="sheetName">工作表名稱</param>
/// <param name="splitPanes">分割窗格設定</param>
public sealed class OdfSheetSplitPanesInfo(string sheetName, OdfSplitPanes splitPanes)
{
    /// <summary>
    /// 取得工作表名稱。
    /// </summary>
    public string SheetName { get; } = sheetName ?? string.Empty;

    /// <summary>
    /// 取得分割窗格設定。
    /// </summary>
    public OdfSplitPanes SplitPanes { get; } = splitPanes;
}
