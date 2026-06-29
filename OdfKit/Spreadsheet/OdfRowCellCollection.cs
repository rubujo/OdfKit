using System;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Indexes cells within a worksheet row.
/// 提供列內儲存格的索引入口。
/// </summary>
public sealed class OdfRowCellCollection
{
    private readonly OdfTableSheet _sheet;
    private readonly int _row;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfRowCellCollection"/> class.
    /// 初始化 <see cref="OdfRowCellCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">The owning worksheet. / 所屬工作表。</param>
    /// <param name="row">The zero-based row index. / 採 0 為基準的列索引。</param>
    internal OdfRowCellCollection(OdfTableSheet sheet, int row)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        _row = row;
    }

    /// <summary>
    /// Gets a cell in the row by column index.
    /// 依欄索引取得列內儲存格。
    /// </summary>
    /// <param name="column">The zero-based column index. / 採 0 為基準的欄索引。</param>
    /// <returns>The specified cell. / 指定儲存格。</returns>
    public OdfCell this[int column] => _sheet.GetCell(_row, column);
}
