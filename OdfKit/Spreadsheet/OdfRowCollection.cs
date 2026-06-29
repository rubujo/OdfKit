using System;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Provides indexing entry points for worksheet rows.
/// 提供工作表列的索引入口。
/// </summary>
public sealed class OdfRowCollection
{
    private readonly OdfTableSheet _sheet;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfRowCollection"/> class.
    /// 初始化 <see cref="OdfRowCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">The owning worksheet. / 所屬工作表。</param>
    internal OdfRowCollection(OdfTableSheet sheet)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
    }

    /// <summary>
    /// Gets a row by index.
    /// 依索引取得列。
    /// </summary>
    /// <param name="index">The zero-based row index. / 採 0 為基準的列索引。</param>
    /// <returns>The specified row. / 指定列。</returns>
    public OdfSheetRow this[int index] => new(_sheet, index);

    /// <summary>
    /// Groups the specified row range so it can be expanded or collapsed.
    /// 將指定列範圍設為可展開/收合的群組。
    /// </summary>
    /// <param name="startRow">The zero-based start row index. / 採 0 為基準的起始列索引。</param>
    /// <param name="endRow">The zero-based inclusive end row index. / 採 0 為基準且包含在內的結束列索引。</param>
    /// <param name="collapsed">Whether the group is collapsed by default. / 是否預設為收合狀態。</param>
    public void Group(int startRow, int endRow, bool collapsed = false)
    {
        _sheet.GroupRows(startRow, endRow, collapsed);
    }

    /// <summary>
    /// Removes the group containing the specified row range and moves the rows back to the worksheet body.
    /// 移除包含指定列範圍的群組，將列移回工作表主體。
    /// </summary>
    /// <param name="startRow">The zero-based start row index. / 採 0 為基準的起始列索引。</param>
    /// <param name="endRow">The zero-based inclusive end row index. / 採 0 為基準且包含在內的結束列索引。</param>
    public void Ungroup(int startRow, int endRow)
    {
        _sheet.UngroupRows(startRow, endRow);
    }
}
