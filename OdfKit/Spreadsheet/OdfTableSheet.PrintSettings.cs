namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region 列印設定

    /// <summary>
    /// Sets the print area.
    /// 設定列印範圍。
    /// </summary>
    /// <param name="range">The cell range. / 列印範圍</param>
    public void SetPrintArea(OdfCellRange range) =>
        OdfTableSheetPrintSettingsEngine.SetPrintArea(MutationContext, range);

    /// <summary>
    /// Gets the print area, or <see langword="null"/> when it is not set.
    /// 取得列印範圍，若未設定則傳回 <see langword="null"/>。
    /// </summary>
    public OdfCellRange? GetPrintArea() =>
        OdfTableSheetPrintSettingsEngine.GetPrintArea(MutationContext);

    /// <summary>
    /// Clears the print area setting.
    /// 清除列印範圍設定。
    /// </summary>
    public void ClearPrintArea() =>
        OdfTableSheetPrintSettingsEngine.ClearPrintArea(MutationContext);

    /// <summary>
    /// Sets title rows that repeat on each printed page.
    /// 設定標題列（列印時每頁重複的列）。
    /// </summary>
    /// <param name="startRow">The numeric value. / 起始列索引（0 為基準）</param>
    /// <param name="endRow">The numeric value. / 結束列索引（包含，0 為基準）</param>
    public void SetPrintTitleRows(int startRow, int endRow) =>
        OdfTableSheetPrintSettingsEngine.SetPrintTitleRows(MutationContext, startRow, endRow);

    /// <summary>
    /// Clears the title row setting.
    /// 清除標題列設定。
    /// </summary>
    public void ClearPrintTitleRows() =>
        OdfTableSheetPrintSettingsEngine.ClearPrintTitleRows(MutationContext);

    /// <summary>
    /// Sets title columns that repeat on each printed page.
    /// 設定標題欄（列印時每頁重複的欄）。
    /// </summary>
    /// <param name="startCol">The numeric value. / 起始欄索引（0 為基準）</param>
    /// <param name="endCol">The numeric value. / 結束欄索引（包含，0 為基準）</param>
    public void SetPrintTitleColumns(int startCol, int endCol) =>
        OdfTableSheetPrintSettingsEngine.SetPrintTitleColumns(MutationContext, startCol, endCol);

    /// <summary>
    /// Clears the title column setting.
    /// 清除標題欄設定。
    /// </summary>
    public void ClearPrintTitleColumns() =>
        OdfTableSheetPrintSettingsEngine.ClearPrintTitleColumns(MutationContext);

    /// <summary>
    /// Inserts a manual row page break after the specified row.
    /// 在指定列之後插入手動列分頁符。
    /// </summary>
    /// <param name="afterRow">The numeric value. / 分頁符位於此列之後（0 為基準）</param>
    public void InsertRowPageBreak(int afterRow) =>
        OdfTableSheetPrintSettingsEngine.InsertRowPageBreak(MutationContext, afterRow);

    /// <summary>
    /// Removes the manual page break for the specified row.
    /// 移除指定列的手動分頁符。
    /// </summary>
    /// <param name="afterRow">The zero-based row index after which the page break is located. / 分頁符位於此列之後（0 為基準）。</param>
    /// <returns><see langword="true"/> if the page break was removed; otherwise, <see langword="false"/>. / 若已移除分頁符則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool RemoveRowPageBreak(int afterRow) =>
        OdfTableSheetPrintSettingsEngine.RemoveRowPageBreak(MutationContext, afterRow);

    /// <summary>
    /// Inserts a manual column page break after the specified column.
    /// 在指定欄之後插入手動欄分頁符。
    /// </summary>
    /// <param name="afterCol">The numeric value. / 分頁符位於此欄之後（0 為基準）</param>
    public void InsertColumnPageBreak(int afterCol) =>
        OdfTableSheetPrintSettingsEngine.InsertColumnPageBreak(MutationContext, afterCol);

    /// <summary>
    /// Removes the manual page break for the specified column.
    /// 移除指定欄的手動分頁符。
    /// </summary>
    /// <param name="afterCol">The zero-based column index after which the page break is located. / 分頁符位於此欄之後（0 為基準）。</param>
    /// <returns><see langword="true"/> if the page break was removed; otherwise, <see langword="false"/>. / 若已移除分頁符則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool RemoveColumnPageBreak(int afterCol) =>
        OdfTableSheetPrintSettingsEngine.RemoveColumnPageBreak(MutationContext, afterCol);

    /// <summary>
    /// Sets the print scale percentage from 1 to 400, or 0 to restore automatic scaling.
    /// 設定列印縮放比例（1–400），傳入 0 代表恢復自動。
    /// </summary>
    /// <param name="percent">The numeric value. / 縮放比例（百分比）</param>
    public void SetPrintScale(int percent) =>
        OdfTableSheetPrintSettingsEngine.SetPrintScale(MutationContext, percent);

    /// <summary>
    /// Sets scaling to fit within the specified page count.
    /// 設定縮放以適合指定頁數。
    /// </summary>
    /// <param name="maxPagesWide">The numeric value. / 最大橫向頁數（0 代表不限制）</param>
    /// <param name="maxPagesTall">The numeric value. / 最大縱向頁數（0 代表不限制）</param>
    public void SetFitToPage(int maxPagesWide = 1, int maxPagesTall = 0) =>
        OdfTableSheetPrintSettingsEngine.SetFitToPage(MutationContext, maxPagesWide, maxPagesTall);

    #endregion
}
