using System;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Indexes worksheet cells by row, column, or address.
/// 提供工作表儲存格的索引入口。
/// </summary>
public sealed class OdfCellCollection
{
    private readonly OdfTableSheet _sheet;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfCellCollection"/> class.
    /// 初始化 <see cref="OdfCellCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">The owning worksheet. / 所屬工作表。</param>
    internal OdfCellCollection(OdfTableSheet sheet)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
    }

    /// <summary>
    /// Gets a cell by A1-style address.
    /// 依 A1 樣式位址取得儲存格。
    /// </summary>
    /// <param name="address">The cell address, such as <c>A1</c>. / 儲存格位址，例如 <c>A1</c>。</param>
    /// <returns>The cell at the specified position. / 指定位置的儲存格。</returns>
    public OdfCell this[string address] => _sheet.GetCell(address);

    /// <summary>
    /// Gets a cell by row and column index.
    /// 依列與欄索引取得儲存格。
    /// </summary>
    /// <param name="row">The zero-based row index. / 採 0 為基準的列索引。</param>
    /// <param name="column">The zero-based column index. / 採 0 為基準的欄索引。</param>
    /// <returns>The cell at the specified position. / 指定位置的儲存格。</returns>
    public OdfCell this[int row, int column] => _sheet.GetCell(row, column);
}
