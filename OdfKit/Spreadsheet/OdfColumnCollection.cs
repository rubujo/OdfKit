using System;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 提供工作表欄的索引入口。
/// </summary>
public sealed class OdfColumnCollection
{
    private readonly OdfTableSheet _sheet;

    /// <summary>
    /// 初始化 <see cref="OdfColumnCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">所屬工作表</param>
    internal OdfColumnCollection(OdfTableSheet sheet)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
    }

    /// <summary>
    /// 依索引取得欄。
    /// </summary>
    /// <param name="index">以 0 為基準的欄索引</param>
    /// <returns>指定欄</returns>
    public OdfSheetColumn this[int index] => new(_sheet, index);

    /// <summary>
    /// 將指定欄範圍設為可展開/收合的群組。
    /// </summary>
    /// <param name="startColumn">起始欄索引（0 為基準）</param>
    /// <param name="endColumn">結束欄索引（包含，0 為基準）</param>
    /// <param name="collapsed">是否預設為收合狀態</param>
    public void Group(int startColumn, int endColumn, bool collapsed = false)
    {
        _sheet.GroupColumns(startColumn, endColumn, collapsed);
    }

    /// <summary>
    /// 移除包含指定欄範圍的群組，將欄移回工作表主體。
    /// </summary>
    /// <param name="startColumn">起始欄索引（0 為基準）</param>
    /// <param name="endColumn">結束欄索引（包含，0 為基準）</param>
    public void Ungroup(int startColumn, int endColumn)
    {
        _sheet.UngroupColumns(startColumn, endColumn);
    }
}
