using System.Collections.Generic;

namespace OdfKit.Spreadsheet;
/// <summary>
/// Provides the OdfTableSheet API.
/// 提供 OdfTableSheet API。
/// </summary>

public partial class OdfTableSheet
{
    #region Visibility

    /// <summary>
    /// Sets whether the specified row is visible.
    /// 設定指定列是否可見。
    /// </summary>
    /// <param name="row">The zero-based row index. / 以 0 為基準的列索引。</param>
    /// <param name="visible">Whether to show it. / 是否顯示。</param>
    public void SetRowVisible(int row, bool visible) =>
        OdfTableSheetVisibilityEngine.SetRowVisible(MutationContext, row, visible);

    /// <summary>
    /// Sets whether the specified column is visible.
    /// 設定指定欄是否可見。
    /// </summary>
    /// <param name="col">The zero-based column index. / 以 0 為基準的欄索引。</param>
    /// <param name="visible">Whether to show it. / 是否顯示。</param>
    public void SetColumnVisible(int col, bool visible) =>
        OdfTableSheetVisibilityEngine.SetColumnVisible(MutationContext, col, visible);

    /// <summary>
    /// Determines whether the specified row is visible.
    /// 判斷指定列是否可見。
    /// </summary>
    /// <param name="row">The zero-based row index. / 以 0 為基準的列索引。</param>
    /// <returns><see langword="true"/> if visible; otherwise <see langword="false"/>. / 若顯示則為 true，否則為 false。</returns>
    public bool IsRowVisible(int row) =>
        OdfTableSheetVisibilityEngine.IsRowVisible(MutationContext, row);

    /// <summary>
    /// Determines whether the specified column is visible.
    /// 判斷指定欄是否可見。
    /// </summary>
    /// <param name="col">The zero-based column index. / 以 0 為基準的欄索引。</param>
    /// <returns><see langword="true"/> if visible; otherwise <see langword="false"/>. / 若顯示則為 true，否則為 false。</returns>
    public bool IsColumnVisible(int col) =>
        OdfTableSheetVisibilityEngine.IsColumnVisible(MutationContext, col);
    /// <summary>
    /// Short overload of AddNamedRange that accepts name and range; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name 與 range；其餘可選參數使用預設值並轉呼叫最長 AddNamedRange 多載。
    /// </summary>
    public void AddNamedRange(string name, OdfCellRange range) => AddNamedRange(name, range, null);


    /// <summary>
    /// Adds a named range to this worksheet.
    /// 新增命名範圍至此工作表。
    /// </summary>
    /// <param name="name">The named range's name. / 命名範圍的名稱。</param>
    /// <param name="range">The cell range. / 儲存格範圍。</param>
    /// <param name="baseCell">The base cell address. / 基準儲存格位址。</param>
    public void AddNamedRange(string name, OdfCellRange range, OdfCellAddress? baseCell) =>
        OdfTableSheetNamedRangeEngine.AddNamedRange(MutationContext, name, range, baseCell);


    /// <summary>
    /// Gets the named ranges in this worksheet.
    /// 取得此工作表中的命名範圍清單。
    /// </summary>
    public IReadOnlyList<OdfNamedRangeInfo> NamedRanges =>
        OdfTableSheetNamedRangeEngine.GetNamedRanges(MutationContext);

    /// <summary>
    /// Finds a named range in this worksheet by its exact name.
    /// 依精確名稱尋找此工作表中的命名範圍。
    /// </summary>
    /// <param name="name">The exact name. / 精確名稱。</param>
    /// <returns>The matching range, or <see langword="null"/>. / 相符的範圍；若不存在則為 <see langword="null"/>。</returns>
    public OdfNamedRangeInfo? FindNamedRange(string name) =>
        OdfTableSheetNamedRangeEngine.FindNamedRange(MutationContext, name);

    /// <summary>
    /// Removes a named range from this worksheet.
    /// 從此工作表移除命名範圍。
    /// </summary>
    /// <param name="name">The exact name. / 精確名稱。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveNamedRange(string name) =>
        OdfTableSheetNamedRangeEngine.RemoveNamedRange(MutationContext, name);

    /// <summary>
    /// Removes all named ranges from this worksheet while preserving named expressions and unknown content.
    /// 移除此工作表中的所有命名範圍，並保留具名運算式與未知內容。
    /// </summary>
    /// <returns>The number removed. / 移除數量。</returns>
    public int ClearNamedRanges() =>
        OdfTableSheetNamedRangeEngine.ClearNamedRanges(MutationContext);
    /// <summary>
    /// Short overload of AddNamedExpression that accepts name and expression; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name 與 expression；其餘可選參數使用預設值並轉呼叫最長 AddNamedExpression 多載。
    /// </summary>
    public void AddNamedExpression(string name, string expression) => AddNamedExpression(name, expression, null);


    /// <summary>
    /// Adds a named expression to this worksheet.
    /// 新增具名運算式至此工作表。
    /// </summary>
    /// <param name="name">The named expression's name. / 具名運算式的名稱。</param>
    /// <param name="expression">The formula expression string. / 公式運算式字串。</param>
    /// <param name="baseCell">The base cell address. / 基準儲存格位址。</param>
    public void AddNamedExpression(string name, string expression, OdfCellAddress? baseCell) =>
        OdfTableSheetNamedRangeEngine.AddNamedExpression(MutationContext, name, expression, baseCell);


    /// <summary>
    /// Gets the named expressions in this worksheet.
    /// 取得此工作表中的具名運算式清單。
    /// </summary>
    public IReadOnlyList<OdfNamedExpressionInfo> NamedExpressions =>
        OdfTableSheetNamedRangeEngine.GetNamedExpressions(MutationContext);

    /// <summary>
    /// Finds a named expression in this worksheet by its exact name.
    /// 依精確名稱尋找此工作表中的具名運算式。
    /// </summary>
    /// <param name="name">The exact name. / 精確名稱。</param>
    /// <returns>The matching expression, or <see langword="null"/>. / 相符的運算式；若不存在則為 <see langword="null"/>。</returns>
    public OdfNamedExpressionInfo? FindNamedExpression(string name) =>
        OdfTableSheetNamedRangeEngine.FindNamedExpression(MutationContext, name);

    /// <summary>
    /// Removes a named expression from this worksheet.
    /// 從此工作表移除具名運算式。
    /// </summary>
    /// <param name="name">The exact name. / 精確名稱。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveNamedExpression(string name) =>
        OdfTableSheetNamedRangeEngine.RemoveNamedExpression(MutationContext, name);

    /// <summary>
    /// Removes all named expressions from this worksheet while preserving named ranges and unknown content.
    /// 移除此工作表中的所有具名運算式，並保留命名範圍與未知內容。
    /// </summary>
    /// <returns>The number removed. / 移除數量。</returns>
    public int ClearNamedExpressions() =>
        OdfTableSheetNamedRangeEngine.ClearNamedExpressions(MutationContext);

    #endregion
}
