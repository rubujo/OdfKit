namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region 列印設定

    /// <summary>
    /// 設定列印範圍
    /// </summary>
    /// <param name="range">列印範圍</param>
    public void SetPrintArea(OdfCellRange range) =>
        OdfTableSheetPrintSettingsEngine.SetPrintArea(MutationContext, range);

    /// <summary>
    /// 取得列印範圍，若未設定則傳回 null
    /// </summary>
    public OdfCellRange? GetPrintArea() =>
        OdfTableSheetPrintSettingsEngine.GetPrintArea(MutationContext);

    /// <summary>
    /// 清除列印範圍設定
    /// </summary>
    public void ClearPrintArea() =>
        OdfTableSheetPrintSettingsEngine.ClearPrintArea(MutationContext);

    /// <summary>
    /// 設定標題列（列印時每頁重複的列）
    /// </summary>
    /// <param name="startRow">起始列索引（0 為基準）</param>
    /// <param name="endRow">結束列索引（包含，0 為基準）</param>
    public void SetPrintTitleRows(int startRow, int endRow) =>
        OdfTableSheetPrintSettingsEngine.SetPrintTitleRows(MutationContext, startRow, endRow);

    /// <summary>
    /// 清除標題列設定
    /// </summary>
    public void ClearPrintTitleRows() =>
        OdfTableSheetPrintSettingsEngine.ClearPrintTitleRows(MutationContext);

    /// <summary>
    /// 設定標題欄（列印時每頁重複的欄）
    /// </summary>
    /// <param name="startCol">起始欄索引（0 為基準）</param>
    /// <param name="endCol">結束欄索引（包含，0 為基準）</param>
    public void SetPrintTitleColumns(int startCol, int endCol) =>
        OdfTableSheetPrintSettingsEngine.SetPrintTitleColumns(MutationContext, startCol, endCol);

    /// <summary>
    /// 清除標題欄設定
    /// </summary>
    public void ClearPrintTitleColumns() =>
        OdfTableSheetPrintSettingsEngine.ClearPrintTitleColumns(MutationContext);

    /// <summary>
    /// 在指定列之後插入手動列分頁符
    /// </summary>
    /// <param name="afterRow">分頁符位於此列之後（0 為基準）</param>
    public void InsertRowPageBreak(int afterRow) =>
        OdfTableSheetPrintSettingsEngine.InsertRowPageBreak(MutationContext, afterRow);

    /// <summary>
    /// 移除指定列的手動分頁符
    /// </summary>
    /// <param name="afterRow">分頁符位於此列之後（0 為基準）</param>
    public void RemoveRowPageBreak(int afterRow) =>
        OdfTableSheetPrintSettingsEngine.RemoveRowPageBreak(MutationContext, afterRow);

    /// <summary>
    /// 在指定欄之後插入手動欄分頁符
    /// </summary>
    /// <param name="afterCol">分頁符位於此欄之後（0 為基準）</param>
    public void InsertColumnPageBreak(int afterCol) =>
        OdfTableSheetPrintSettingsEngine.InsertColumnPageBreak(MutationContext, afterCol);

    /// <summary>
    /// 移除指定欄的手動分頁符
    /// </summary>
    /// <param name="afterCol">分頁符位於此欄之後（0 為基準）</param>
    public void RemoveColumnPageBreak(int afterCol) =>
        OdfTableSheetPrintSettingsEngine.RemoveColumnPageBreak(MutationContext, afterCol);

    /// <summary>
    /// 設定列印縮放比例（1–400），傳入 0 代表恢復自動
    /// </summary>
    /// <param name="percent">縮放比例（百分比）</param>
    public void SetPrintScale(int percent) =>
        OdfTableSheetPrintSettingsEngine.SetPrintScale(MutationContext, percent);

    /// <summary>
    /// 設定縮放以適合指定頁數
    /// </summary>
    /// <param name="maxPagesWide">最大橫向頁數（0 代表不限制）</param>
    /// <param name="maxPagesTall">最大縱向頁數（0 代表不限制）</param>
    public void SetFitToPage(int maxPagesWide = 1, int maxPagesTall = 0) =>
        OdfTableSheetPrintSettingsEngine.SetFitToPage(MutationContext, maxPagesWide, maxPagesTall);

    #endregion
}
