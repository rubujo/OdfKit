namespace OdfKit.Spreadsheet;

/// <summary>
/// Defines how formulas are copied from a template row.
/// 定義如何從模板列複製公式。
/// </summary>
public enum OdfFormulaCopyMode
{
    /// <summary>
    /// Copies the formula text without changing references.
    /// 不變更參照，直接複製公式文字。
    /// </summary>
    CopyAsIs,

    /// <summary>
    /// Shifts relative row references by the target row offset.
    /// 依目標列位移調整相對列參照。
    /// </summary>
    ShiftRelativeReferences,

    /// <summary>
    /// Clears formulas instead of copying them.
    /// 不複製公式並清除目標公式。
    /// </summary>
    Clear
}
