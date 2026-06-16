namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region 列欄群組

    /// <summary>
    /// 將指定列範圍設為可展開/收合的群組。
    /// </summary>
    /// <param name="startRow">起始列索引（0 為基準）</param>
    /// <param name="endRow">結束列索引（包含，0 為基準）</param>
    /// <param name="collapsed">是否預設為收合狀態</param>
    public void GroupRows(int startRow, int endRow, bool collapsed = false) =>
        OdfTableSheetRowColumnGroupEngine.GroupRows(MutationContext, startRow, endRow, collapsed);

    /// <summary>
    /// 移除包含指定列範圍的群組，將列移回工作表主體。
    /// </summary>
    /// <param name="startRow">起始列索引（0 為基準）</param>
    /// <param name="endRow">結束列索引（包含，0 為基準）</param>
    public void UngroupRows(int startRow, int endRow) =>
        OdfTableSheetRowColumnGroupEngine.UngroupRows(MutationContext, startRow, endRow);

    /// <summary>
    /// 將指定欄範圍設為可展開/收合的群組。
    /// </summary>
    /// <param name="startCol">起始欄索引（0 為基準）</param>
    /// <param name="endCol">結束欄索引（包含，0 為基準）</param>
    /// <param name="collapsed">是否預設為收合狀態</param>
    public void GroupColumns(int startCol, int endCol, bool collapsed = false) =>
        OdfTableSheetRowColumnGroupEngine.GroupColumns(MutationContext, startCol, endCol, collapsed);

    /// <summary>
    /// 移除包含指定欄範圍的群組，將欄移回工作表主體。
    /// </summary>
    /// <param name="startCol">起始欄索引（0 為基準）</param>
    /// <param name="endCol">結束欄索引（包含，0 為基準）</param>
    public void UngroupColumns(int startCol, int endCol) =>
        OdfTableSheetRowColumnGroupEngine.UngroupColumns(MutationContext, startCol, endCol);

    #endregion
}
