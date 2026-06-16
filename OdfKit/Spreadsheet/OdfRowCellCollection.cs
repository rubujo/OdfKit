using System;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 提供列內儲存格的索引入口。
/// </summary>
public sealed class OdfRowCellCollection
{
    private readonly OdfTableSheet _sheet;
    private readonly int _row;

    /// <summary>
    /// 初始化 <see cref="OdfRowCellCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">所屬工作表。</param>
    /// <param name="row">以 0 為基準的列索引。</param>
    internal OdfRowCellCollection(OdfTableSheet sheet, int row)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        _row = row;
    }

    /// <summary>
    /// 依欄索引取得列內儲存格。
    /// </summary>
    /// <param name="column">以 0 為基準的欄索引。</param>
    /// <returns>指定儲存格。</returns>
    public OdfCell this[int column] => _sheet.GetCell(_row, column);
}
