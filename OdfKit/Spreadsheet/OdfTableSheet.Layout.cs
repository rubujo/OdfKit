using System.Collections.Generic;
using System.Threading;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Provides the OdfTableSheet API.
/// 提供 OdfTableSheet API。
/// </summary>
public partial class OdfTableSheet
{
    #region Layout

    /// <summary>
    /// Automatically adjusts the width of the specified column based on its content.
    /// 自動調整指定欄的寬度，根據內容長度來適配。
    /// </summary>
    /// <param name="col">The zero-based column index. / 以 0 為基準的欄索引。</param>
    public void AutoFitColumnWidth(int col) =>
        OdfTableSheetLayoutEngine.AutoFitColumnWidth(MutationContext, col);

    /// <summary>
    /// Automatically calculates and applies a column width using bounded layout options.
    /// 使用具資源上限的版面選項自動計算並套用欄寬。
    /// </summary>
    /// <param name="column">The zero-based column index. / 以 0 為基準的欄索引。</param>
    /// <param name="options">The automatic layout options. / 自動版面配置選項。</param>
    /// <returns>The applied column width. / 已套用的欄寬。</returns>
    public OdfLength AutoFitColumnWidth(int column, OdfAutoFitOptions options) =>
        AutoFitColumnWidth(column, options, CancellationToken.None);

    /// <summary>
    /// Automatically calculates and applies a column width using bounded layout options.
    /// 使用具資源上限的版面選項自動計算並套用欄寬。
    /// </summary>
    /// <param name="column">The zero-based column index. / 以 0 為基準的欄索引。</param>
    /// <param name="options">The automatic layout options. / 自動版面配置選項。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The applied column width. / 已套用的欄寬。</returns>
    public OdfLength AutoFitColumnWidth(
        int column,
        OdfAutoFitOptions options,
        CancellationToken cancellationToken) =>
        OdfTableSheetLayoutEngine.AutoFitColumnWidth(
            MutationContext,
            column,
            options,
            cancellationToken);

    /// <summary>
    /// Automatically calculates and applies multiple column widths in one bounded scan.
    /// 以單次具資源上限的掃描自動計算並套用多個欄寬。
    /// </summary>
    /// <param name="columns">The zero-based column indexes. / 以 0 為基準的欄索引集合。</param>
    /// <param name="options">The automatic layout options. / 自動版面配置選項。</param>
    /// <returns>The applied widths keyed by column index. / 依欄索引排列的已套用寬度。</returns>
    public IReadOnlyDictionary<int, OdfLength> AutoFitColumnWidths(
        IEnumerable<int> columns,
        OdfAutoFitOptions options) =>
        AutoFitColumnWidths(columns, options, CancellationToken.None);

    /// <summary>
    /// Automatically calculates and applies multiple column widths in one bounded scan.
    /// 以單次具資源上限的掃描自動計算並套用多個欄寬。
    /// </summary>
    /// <param name="columns">The zero-based column indexes. / 以 0 為基準的欄索引集合。</param>
    /// <param name="options">The automatic layout options. / 自動版面配置選項。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The applied widths keyed by column index. / 依欄索引排列的已套用寬度。</returns>
    public IReadOnlyDictionary<int, OdfLength> AutoFitColumnWidths(
        IEnumerable<int> columns,
        OdfAutoFitOptions options,
        CancellationToken cancellationToken) =>
        OdfTableSheetLayoutEngine.AutoFitColumnWidths(
            MutationContext,
            columns,
            options,
            cancellationToken);

    /// <summary>
    /// Sets the width of the specified column.
    /// 設定指定欄的寬度。
    /// </summary>
    /// <param name="col">The zero-based column index. / 以 0 為基準的欄索引。</param>
    /// <param name="width">The column width. / 欄寬度。</param>
    public void SetColumnWidth(int col, OdfLength width) =>
        OdfTableSheetLayoutEngine.SetColumnWidth(MutationContext, col, width);

    /// <summary>
    /// Sets whether the specified row uses optimal automatic height.
    /// 設定指定列是否啟用最佳自動列高 (AutoHeight)。
    /// </summary>
    /// <param name="row">The zero-based row index. / 以 0 為基準的列索引。</param>
    /// <param name="useOptimal">Whether to enable it. / 是否啟用。</param>
    public void SetRowOptimalHeight(int row, bool useOptimal) =>
        OdfTableSheetLayoutEngine.SetRowOptimalHeight(MutationContext, row, useOptimal);

    /// <summary>
    /// Calculates and applies a deterministic row height from cell content and effective column widths.
    /// 依儲存格內容與有效欄寬計算並套用確定性列高。
    /// </summary>
    /// <param name="row">The zero-based row index. / 以 0 為基準的列索引。</param>
    /// <param name="options">The automatic layout options. / 自動版面配置選項。</param>
    /// <returns>The applied row height. / 已套用的列高。</returns>
    public OdfLength AutoFitRowHeight(int row, OdfAutoFitOptions options) =>
        AutoFitRowHeight(row, options, CancellationToken.None);

    /// <summary>
    /// Calculates and applies a deterministic row height from cell content and effective column widths.
    /// 依儲存格內容與有效欄寬計算並套用確定性列高。
    /// </summary>
    /// <param name="row">The zero-based row index. / 以 0 為基準的列索引。</param>
    /// <param name="options">The automatic layout options. / 自動版面配置選項。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The applied row height. / 已套用的列高。</returns>
    public OdfLength AutoFitRowHeight(
        int row,
        OdfAutoFitOptions options,
        CancellationToken cancellationToken) =>
        OdfTableSheetLayoutEngine.AutoFitRowHeight(
            MutationContext,
            row,
            options,
            cancellationToken);

    /// <summary>
    /// Calculates and applies multiple row heights with a shared bounded measurement cache.
    /// 使用共用且具上限的量測快取，計算並套用多個列高。
    /// </summary>
    /// <param name="rows">The zero-based row indexes. / 以 0 為基準的列索引集合。</param>
    /// <param name="options">The automatic layout options. / 自動版面配置選項。</param>
    /// <returns>The applied heights keyed by row index. / 依列索引排列的已套用高度。</returns>
    public IReadOnlyDictionary<int, OdfLength> AutoFitRowHeights(
        IEnumerable<int> rows,
        OdfAutoFitOptions options) =>
        AutoFitRowHeights(rows, options, CancellationToken.None);

    /// <summary>
    /// Calculates and applies multiple row heights with a shared bounded measurement cache.
    /// 使用共用且具上限的量測快取，計算並套用多個列高。
    /// </summary>
    /// <param name="rows">The zero-based row indexes. / 以 0 為基準的列索引集合。</param>
    /// <param name="options">The automatic layout options. / 自動版面配置選項。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The applied heights keyed by row index. / 依列索引排列的已套用高度。</returns>
    public IReadOnlyDictionary<int, OdfLength> AutoFitRowHeights(
        IEnumerable<int> rows,
        OdfAutoFitOptions options,
        CancellationToken cancellationToken) =>
        OdfTableSheetLayoutEngine.AutoFitRowHeights(
            MutationContext,
            rows,
            options,
            cancellationToken);

    /// <summary>
    /// Determines whether the specified row uses optimal automatic height.
    /// 判斷指定列是否啟用最佳自動列高。
    /// </summary>
    /// <param name="row">The zero-based row index. / 以 0 為基準的列索引。</param>
    /// <returns>Whether it is enabled. / 是否啟用。</returns>
    public bool IsRowOptimalHeight(int row) =>
        OdfTableSheetLayoutEngine.IsRowOptimalHeight(MutationContext, row);

    /// <summary>
    /// Sets the fixed height of the specified row.
    /// 設定指定列的固定高度。
    /// </summary>
    /// <param name="row">The zero-based row index. / 以 0 為基準的列索引。</param>
    /// <param name="height">The row height. / 列高度。</param>
    public void SetRowHeight(int row, OdfLength? height) =>
        OdfTableSheetLayoutEngine.SetRowHeight(MutationContext, row, height);

    /// <summary>
    /// Gets the fixed height of the specified row.
    /// 取得指定列的固定高度。
    /// </summary>
    /// <param name="row">The zero-based row index. / 以 0 為基準的列索引。</param>
    /// <returns>The row height; <see langword="null"/> if not set. / 列高度，若未設定則為 null。</returns>
    public OdfLength? GetRowHeight(int row) =>
        OdfTableSheetLayoutEngine.GetRowHeight(MutationContext, row);

    /// <summary>
    /// Gets the fixed width of the specified column.
    /// 取得指定欄的寬度。
    /// </summary>
    /// <param name="column">The zero-based column index. / 以 0 為基準的欄索引。</param>
    /// <returns>The column width; <see langword="null"/> if not set. / 欄寬度，若未設定則為 null。</returns>
    public OdfLength? GetColumnWidth(int column) =>
        OdfTableSheetLayoutEngine.GetColumnWidth(MutationContext, column);

    #endregion
}
