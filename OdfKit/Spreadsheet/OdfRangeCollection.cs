using System;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Indexes worksheet cell ranges by address.
/// 提供工作表儲存格範圍的索引入口。
/// </summary>
public sealed class OdfRangeCollection
{
    private readonly OdfTableSheet _sheet;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfRangeCollection"/> class.
    /// 初始化 <see cref="OdfRangeCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">The owning worksheet. / 所屬工作表。</param>
    internal OdfRangeCollection(OdfTableSheet sheet)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
    }

    /// <summary>
    /// Gets a range by Excel-style range string.
    /// 依 Excel 樣式範圍字串取得範圍。
    /// </summary>
    /// <param name="address">The range address, such as <c>A1:C3</c>. / 範圍位址，例如 <c>A1:C3</c>。</param>
    /// <returns>The range selection object. / 範圍選取物件。</returns>
    public OdfCellRangeSelection this[string address] => new(_sheet, OdfCellRange.ParseExcel(address));

    /// <summary>
    /// Gets a range by row and column indexes.
    /// 依列與欄索引取得範圍。
    /// </summary>
    /// <param name="startRow">The start row index. / 起始列索引。</param>
    /// <param name="startColumn">The start column index. / 起始欄索引。</param>
    /// <param name="endRow">The end row index. / 結束列索引。</param>
    /// <param name="endColumn">The end column index. / 結束欄索引。</param>
    /// <returns>The range selection object. / 範圍選取物件。</returns>
    public OdfCellRangeSelection this[int startRow, int startColumn, int endRow, int endColumn] =>
        new(_sheet, new OdfCellRange(startRow, startColumn, endRow, endColumn, _sheet.Name));
}
