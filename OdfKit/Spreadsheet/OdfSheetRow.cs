using System;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示工作表中的一列。
/// </summary>
public sealed class OdfSheetRow
{
    private readonly OdfTableSheet _sheet;

    /// <summary>
    /// 初始化 <see cref="OdfSheetRow"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">所屬工作表。</param>
    /// <param name="index">以 0 為基準的列索引。</param>
    internal OdfSheetRow(OdfTableSheet sheet, int index)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        Index = index;
    }

    /// <summary>
    /// 取得以 0 為基準的列索引。
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// 取得或設定此列是否顯示。
    /// </summary>
    public bool Visible
    {
        get => _sheet.IsRowVisible(Index);
        set => _sheet.SetRowVisible(Index, value);
    }

    /// <summary>
    /// 取得此列的儲存格集合。
    /// </summary>
    public OdfRowCellCollection Cells => new(_sheet, Index);

    /// <summary>
    /// 取得或設定此列是否啟用最佳自動列高 (AutoHeight)。
    /// </summary>
    public bool OptimalHeight
    {
        get => _sheet.IsRowOptimalHeight(Index);
        set => _sheet.SetRowOptimalHeight(Index, value);
    }

    /// <summary>
    /// 取得或設定此列的固定高度。若設定為 null 則清除高度設定。
    /// </summary>
    public OdfLength? Height
    {
        get => _sheet.GetRowHeight(Index);
        set => _sheet.SetRowHeight(Index, value);
    }
}
