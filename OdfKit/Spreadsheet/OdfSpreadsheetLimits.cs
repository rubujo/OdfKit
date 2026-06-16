namespace OdfKit.Spreadsheet;

/// <summary>
/// 試算表重複列與欄的防禦性上限常數。
/// </summary>
public static class OdfSpreadsheetLimits
{
    /// <summary>
    /// CSV 匯出時允許的最大重複列或欄次數。
    /// </summary>
    public const int CsvMaxRepeat = 10_000;

    /// <summary>
    /// 公式評估時允許的最大重複列或欄次數。
    /// </summary>
    public const int FormulaMaxRepeat = 10_000;
}
