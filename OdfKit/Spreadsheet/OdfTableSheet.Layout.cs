using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region Layout

    /// <summary>
    /// 自動調整指定欄的寬度，根據內容長度來適配。
    /// </summary>
    /// <param name="col">以 0 為基準的欄索引</param>
    public void AutoFitColumnWidth(int col) =>
        OdfTableSheetLayoutEngine.AutoFitColumnWidth(MutationContext, col);

    /// <summary>
    /// 設定指定欄的寬度。
    /// </summary>
    /// <param name="col">以 0 為基準的欄索引</param>
    /// <param name="width">欄寬度</param>
    public void SetColumnWidth(int col, OdfLength width) =>
        OdfTableSheetLayoutEngine.SetColumnWidth(MutationContext, col, width);

    /// <summary>
    /// 設定指定列是否啟用最佳自動列高 (AutoHeight)。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引</param>
    /// <param name="useOptimal">是否啟用</param>
    public void SetRowOptimalHeight(int row, bool useOptimal) =>
        OdfTableSheetLayoutEngine.SetRowOptimalHeight(MutationContext, row, useOptimal);

    /// <summary>
    /// 判斷指定列是否啟用最佳自動列高。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引</param>
    /// <returns>是否啟用</returns>
    public bool IsRowOptimalHeight(int row) =>
        OdfTableSheetLayoutEngine.IsRowOptimalHeight(MutationContext, row);

    /// <summary>
    /// 設定指定列的固定高度。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引</param>
    /// <param name="height">列高度</param>
    public void SetRowHeight(int row, OdfLength? height) =>
        OdfTableSheetLayoutEngine.SetRowHeight(MutationContext, row, height);

    /// <summary>
    /// 取得指定列的固定高度。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引</param>
    /// <returns>列高度，若未設定則為 null</returns>
    public OdfLength? GetRowHeight(int row) =>
        OdfTableSheetLayoutEngine.GetRowHeight(MutationContext, row);

    /// <summary>
    /// 取得指定欄的固定寬度。
    /// </summary>
    /// <param name="column">以 0 為基準的欄索引</param>
    /// <returns>欄寬度，若未設定則為 null</returns>
    public OdfLength? GetColumnWidth(int column) =>
        OdfTableSheetLayoutEngine.GetColumnWidth(MutationContext, column);

    #endregion
}
