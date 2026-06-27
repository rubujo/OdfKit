using System;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region View

    /// <summary>
    /// 凍結指定數量的上方列與左側欄。
    /// </summary>
    /// <param name="frozenRows">要凍結的列數</param>
    /// <param name="frozenColumns">要凍結的欄數</param>
    /// <exception cref="ArgumentOutOfRangeException">當列數或欄數小於 0 時擲出</exception>
    public void FreezePanes(int frozenRows, int frozenColumns) =>
        OdfTableSheetViewEngine.FreezePanes(MutationContext, frozenRows, frozenColumns);

    /// <summary>
    /// 以分割模式（非凍結）分割工作表窗格。
    /// </summary>
    /// <param name="splitRow">水平分割線所在的列索引（0 表示不分割）</param>
    /// <param name="splitColumn">垂直分割線所在的欄索引（0 表示不分割）</param>
    /// <exception cref="ArgumentOutOfRangeException">當列索引或欄索引小於 0 時拋出</exception>
    public void SplitPanes(int splitRow, int splitColumn) =>
        OdfTableSheetViewEngine.SplitPanes(MutationContext, splitRow, splitColumn);

    /// <summary>
    /// 以分割模式（非凍結）分割工作表視窗。
    /// </summary>
    /// <param name="splitRow">水平分割線所在的列索引（0 表示不分割）</param>
    /// <param name="splitColumn">垂直分割線所在的欄索引（0 表示不分割）</param>
    /// <exception cref="ArgumentOutOfRangeException">當列索引或欄索引小於 0 時擲出</exception>
    public void SplitWindow(int splitRow, int splitColumn) =>
        SplitPanes(splitRow, splitColumn);

    /// <summary>
    /// 取得目前工作表的凍結窗格設定。
    /// </summary>
    public OdfFrozenPanes FrozenPanes =>
        OdfTableSheetViewEngine.GetFrozenPanes(MutationContext);

    /// <summary>
    /// 取得目前工作表的分割窗格設定（非凍結模式）。
    /// </summary>
    public OdfSplitPanes ViewSplitPanes =>
        OdfTableSheetViewEngine.GetSplitPanes(MutationContext);

    /// <summary>
    /// 新增清單型資料驗證，並套用到指定範圍。
    /// </summary>
    /// <param name="range">要套用的儲存格範圍</param>
    /// <param name="name">驗證規則名稱</param>
    /// <param name="allowedValues">允許的值</param>
    public void AddValidationList(OdfCellRange range, string name, params string[] allowedValues) =>
        OdfTableSheetViewEngine.AddValidationList(MutationContext, range, name, allowedValues);

    #endregion
}
