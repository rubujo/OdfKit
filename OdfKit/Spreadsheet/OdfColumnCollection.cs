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
}
