using System;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 提供工作表列的索引入口。
/// </summary>
public sealed class OdfRowCollection
{
    private readonly OdfTableSheet _sheet;

    /// <summary>
    /// 初始化 <see cref="OdfRowCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">所屬工作表</param>
    internal OdfRowCollection(OdfTableSheet sheet)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
    }

    /// <summary>
    /// 依索引取得列。
    /// </summary>
    /// <param name="index">以 0 為基準的列索引</param>
    /// <returns>指定列</returns>
    public OdfSheetRow this[int index] => new(_sheet, index);

    /// <summary>
    /// 將指定列範圍設為可展開/收合的群組。
    /// </summary>
    /// <param name="startRow">起始列索引（0 為基準）</param>
    /// <param name="endRow">結束列索引（包含，0 為基準）</param>
    /// <param name="collapsed">是否預設為收合狀態</param>
    public void Group(int startRow, int endRow, bool collapsed = false)
    {
        _sheet.GroupRows(startRow, endRow, collapsed);
    }

    /// <summary>
    /// 移除包含指定列範圍的群組，將列移回工作表主體。
    /// </summary>
    /// <param name="startRow">起始列索引（0 為基準）</param>
    /// <param name="endRow">結束列索引（包含，0 為基準）</param>
    public void Ungroup(int startRow, int endRow)
    {
        _sheet.UngroupRows(startRow, endRow);
    }
}
