using System;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents a row in a worksheet.
/// 表示工作表中的一列。
/// </summary>
public sealed class OdfSheetRow
{
    private readonly OdfTableSheet _sheet;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfSheetRow"/> class.
    /// 初始化 <see cref="OdfSheetRow"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">The owning worksheet. / 所屬工作表。</param>
    /// <param name="index">The zero-based row index. / 採 0 為基準的列索引。</param>
    internal OdfSheetRow(OdfTableSheet sheet, int index)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        Index = index;
    }

    /// <summary>
    /// Gets the zero-based row index.
    /// 取得以 0 為基準的列索引。
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets or sets whether this row is visible.
    /// 取得或設定此列是否顯示。
    /// </summary>
    public bool Visible
    {
        get => _sheet.IsRowVisible(Index);
        set => _sheet.SetRowVisible(Index, value);
    }

    /// <summary>
    /// Gets the cell collection for this row.
    /// 取得此列的儲存格集合。
    /// </summary>
    public OdfRowCellCollection Cells => new(_sheet, Index);

    /// <summary>
    /// Gets or sets whether optimal automatic row height is enabled for this row.
    /// 取得或設定此列是否啟用最佳自動列高 (AutoHeight)。
    /// </summary>
    public bool OptimalHeight
    {
        get => _sheet.IsRowOptimalHeight(Index);
        set => _sheet.SetRowOptimalHeight(Index, value);
    }

    /// <summary>
    /// Gets or sets the fixed height of this row. Setting it to <see langword="null"/> clears the height setting.
    /// 取得或設定此列的固定高度。若設定為 <see langword="null"/> 則清除高度設定。
    /// </summary>
    public OdfLength? Height
    {
        get => _sheet.GetRowHeight(Index);
        set => _sheet.SetRowHeight(Index, value);
    }
}
