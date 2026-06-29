using System;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region View

    /// <summary>
    /// Freezes the specified number of top rows and left columns.
    /// 凍結指定數量的上方列與左側欄。
    /// </summary>
    /// <param name="frozenRows">The number of rows to freeze. / 要凍結的列數。</param>
    /// <param name="frozenColumns">The number of columns to freeze. / 要凍結的欄數。</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the row or column count is less than 0. / 當列數或欄數小於 0 時擲出。</exception>
    public void FreezePanes(int frozenRows, int frozenColumns) =>
        OdfTableSheetViewEngine.FreezePanes(MutationContext, frozenRows, frozenColumns);

    /// <summary>
    /// Splits worksheet panes in split mode, without freezing.
    /// 以分割模式（非凍結）分割工作表窗格。
    /// </summary>
    /// <param name="splitRow">The row index of the horizontal split line (0 means no split). / 水平分割線所在的列索引（0 表示不分割）。</param>
    /// <param name="splitColumn">The column index of the vertical split line (0 means no split). / 垂直分割線所在的欄索引（0 表示不分割）。</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the row or column index is less than 0. / 當列索引或欄索引小於 0 時拋出。</exception>
    public void SplitPanes(int splitRow, int splitColumn) =>
        OdfTableSheetViewEngine.SplitPanes(MutationContext, splitRow, splitColumn);

    /// <summary>
    /// Splits the worksheet window in split mode, without freezing.
    /// 以分割模式（非凍結）分割工作表視窗。
    /// </summary>
    /// <param name="splitRow">The row index of the horizontal split line (0 means no split). / 水平分割線所在的列索引（0 表示不分割）。</param>
    /// <param name="splitColumn">The column index of the vertical split line (0 means no split). / 垂直分割線所在的欄索引（0 表示不分割）。</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the row or column index is less than 0. / 當列索引或欄索引小於 0 時擲出。</exception>
    public void SplitWindow(int splitRow, int splitColumn) =>
        SplitPanes(splitRow, splitColumn);

    /// <summary>
    /// Gets the frozen pane settings for the current worksheet.
    /// 取得目前工作表的凍結窗格設定。
    /// </summary>
    public OdfFrozenPanes FrozenPanes =>
        OdfTableSheetViewEngine.GetFrozenPanes(MutationContext);

    /// <summary>
    /// Gets the split pane settings for the current worksheet in non-frozen mode.
    /// 取得目前工作表的分割窗格設定（非凍結模式）。
    /// </summary>
    public OdfSplitPanes ViewSplitPanes =>
        OdfTableSheetViewEngine.GetSplitPanes(MutationContext);

    /// <summary>
    /// Adds list-based data validation and applies it to the specified range.
    /// 新增清單型資料驗證，並套用到指定範圍。
    /// </summary>
    /// <param name="range">The cell range to apply to. / 要套用的儲存格範圍。</param>
    /// <param name="name">The validation rule name. / 驗證規則名稱。</param>
    /// <param name="allowedValues">The allowed values. / 允許的值。</param>
    public void AddValidationList(OdfCellRange range, string name, params string[] allowedValues) =>
        OdfTableSheetViewEngine.AddValidationList(MutationContext, range, name, allowedValues);

    #endregion
}
