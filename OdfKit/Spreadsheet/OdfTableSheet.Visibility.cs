using System.Collections.Generic;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region Visibility

    /// <summary>
    /// 設定指定列是否可見。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引</param>
    /// <param name="visible">是否顯示</param>
    public void SetRowVisible(int row, bool visible) =>
        OdfTableSheetVisibilityEngine.SetRowVisible(MutationContext, row, visible);

    /// <summary>
    /// 設定指定欄是否可見。
    /// </summary>
    /// <param name="col">以 0 為基準的欄索引</param>
    /// <param name="visible">是否顯示</param>
    public void SetColumnVisible(int col, bool visible) =>
        OdfTableSheetVisibilityEngine.SetColumnVisible(MutationContext, col, visible);

    /// <summary>
    /// 判斷指定列是否可見。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引</param>
    /// <returns>若顯示則為 true，否則為 false</returns>
    public bool IsRowVisible(int row) =>
        OdfTableSheetVisibilityEngine.IsRowVisible(MutationContext, row);

    /// <summary>
    /// 判斷指定欄是否可見。
    /// </summary>
    /// <param name="col">以 0 為基準的欄索引</param>
    /// <returns>若顯示則為 true，否則為 false</returns>
    public bool IsColumnVisible(int col) =>
        OdfTableSheetVisibilityEngine.IsColumnVisible(MutationContext, col);

    /// <summary>
    /// 新增命名範圍至此工作表。
    /// </summary>
    /// <param name="name">命名範圍的名稱</param>
    /// <param name="range">儲存格範圍</param>
    /// <param name="baseCell">基準儲存格位址</param>
    public void AddNamedRange(string name, OdfCellRange range, OdfCellAddress? baseCell = null) =>
        OdfTableSheetNamedRangeEngine.AddNamedRange(MutationContext, name, range, baseCell);

    /// <summary>
    /// 取得此工作表中的命名範圍清單。
    /// </summary>
    public IReadOnlyList<OdfNamedRangeInfo> NamedRanges =>
        OdfTableSheetNamedRangeEngine.GetNamedRanges(MutationContext);

    /// <summary>
    /// 新增具名運算式至此工作表。
    /// </summary>
    /// <param name="name">具名運算式的名稱</param>
    /// <param name="expression">公式運算式字串</param>
    /// <param name="baseCell">基準儲存格位址</param>
    public void AddNamedExpression(string name, string expression, OdfCellAddress? baseCell = null) =>
        OdfTableSheetNamedRangeEngine.AddNamedExpression(MutationContext, name, expression, baseCell);

    /// <summary>
    /// 取得此工作表中的具名運算式清單。
    /// </summary>
    public IReadOnlyList<OdfNamedExpressionInfo> NamedExpressions =>
        OdfTableSheetNamedRangeEngine.GetNamedExpressions(MutationContext);

    #endregion
}
