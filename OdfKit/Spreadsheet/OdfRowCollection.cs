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
    /// <param name="sheet">所屬工作表。</param>
    internal OdfRowCollection(OdfTableSheet sheet)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
    }

    /// <summary>
    /// 依索引取得列。
    /// </summary>
    /// <param name="index">以 0 為基準的列索引。</param>
    /// <returns>指定列。</returns>
    public OdfSheetRow this[int index] => new(_sheet, index);
}
