using System;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Indexes worksheet columns by position.
/// 提供工作表欄的索引入口。
/// </summary>
public sealed class OdfColumnCollection
{
    private readonly OdfTableSheet _sheet;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfColumnCollection"/> class.
    /// 初始化 <see cref="OdfColumnCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">The owning worksheet. / 所屬工作表。</param>
    internal OdfColumnCollection(OdfTableSheet sheet)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
    }

    /// <summary>
    /// Gets a column by index.
    /// 依索引取得欄。
    /// </summary>
    /// <param name="index">The zero-based column index. / 採 0 為基準的欄索引。</param>
    /// <returns>The specified column. / 指定欄。</returns>
    public OdfSheetColumn this[int index] => new(_sheet, index);

    /// <summary>
    /// Groups the specified column range so it can be expanded or collapsed.
    /// 將指定欄範圍設為可展開/收合的群組。
    /// </summary>
    /// <param name="startColumn">The zero-based start column index. / 採 0 為基準的起始欄索引。</param>
    /// <param name="endColumn">The zero-based inclusive end column index. / 採 0 為基準且包含在內的結束欄索引。</param>
    /// <param name="collapsed">Whether the group is collapsed by default. / 是否預設為收合狀態。</param>
    public void Group(int startColumn, int endColumn, bool collapsed = false)
    {
        _sheet.GroupColumns(startColumn, endColumn, collapsed);
    }

    /// <summary>
    /// Removes the group containing the specified column range and moves the columns back to the worksheet body.
    /// 移除包含指定欄範圍的群組，將欄移回工作表主體。
    /// </summary>
    /// <param name="startColumn">The zero-based start column index. / 採 0 為基準的起始欄索引。</param>
    /// <param name="endColumn">The zero-based inclusive end column index. / 採 0 為基準且包含在內的結束欄索引。</param>
    public void Ungroup(int startColumn, int endColumn)
    {
        _sheet.UngroupColumns(startColumn, endColumn);
    }
}
