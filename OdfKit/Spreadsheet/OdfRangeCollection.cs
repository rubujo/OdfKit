using System;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 提供工作表儲存格範圍的索引入口。
/// </summary>
public sealed class OdfRangeCollection
{
    private readonly OdfTableSheet _sheet;

    /// <summary>
    /// 初始化 <see cref="OdfRangeCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">所屬工作表。</param>
    internal OdfRangeCollection(OdfTableSheet sheet)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
    }

    /// <summary>
    /// 依 Excel 樣式範圍字串取得範圍。
    /// </summary>
    /// <param name="address">範圍位址，例如 <c>A1:C3</c>。</param>
    /// <returns>範圍選取物件。</returns>
    public OdfCellRangeSelection this[string address] => new(_sheet, OdfCellRange.ParseExcel(address));

    /// <summary>
    /// 依列與欄索引取得範圍。
    /// </summary>
    /// <param name="startRow">起始列索引。</param>
    /// <param name="startColumn">起始欄索引。</param>
    /// <param name="endRow">結束列索引。</param>
    /// <param name="endColumn">結束欄索引。</param>
    /// <returns>範圍選取物件。</returns>
    public OdfCellRangeSelection this[int startRow, int startColumn, int endRow, int endColumn] =>
        new(_sheet, new OdfCellRange(startRow, startColumn, endRow, endColumn, _sheet.Name));
}
