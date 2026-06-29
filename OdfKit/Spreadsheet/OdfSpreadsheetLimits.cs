namespace OdfKit.Spreadsheet;

/// <summary>
/// Defines defensive upper bounds for repeated spreadsheet rows and columns.
/// 試算表重複列與欄的防禦性上限常數。
/// </summary>
public static class OdfSpreadsheetLimits
{
    /// <summary>
    /// The maximum repeated row or column count allowed during CSV export.
    /// CSV 匯出時允許的最大重複列或欄次數。
    /// </summary>
    public const int CsvMaxRepeat = 10_000;

    /// <summary>
    /// The maximum repeated row or column count allowed during formula evaluation.
    /// 公式評估時允許的最大重複列或欄次數。
    /// </summary>
    public const int FormulaMaxRepeat = 10_000;
}
