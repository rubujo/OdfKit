using System;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents a column in a worksheet.
/// 表示工作表中的一欄。
/// </summary>
public sealed class OdfSheetColumn
{
    private readonly OdfTableSheet _sheet;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfSheetColumn"/> class.
    /// 初始化 <see cref="OdfSheetColumn"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">The owning worksheet. / 所屬工作表。</param>
    /// <param name="index">The zero-based column index. / 採 0 為基準的欄索引。</param>
    internal OdfSheetColumn(OdfTableSheet sheet, int index)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        Index = index;
    }

    /// <summary>
    /// Gets the zero-based column index.
    /// 取得以 0 為基準的欄索引。
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets or sets whether this column is visible.
    /// 取得或設定此欄是否顯示。
    /// </summary>
    public bool Visible
    {
        get => _sheet.IsColumnVisible(Index);
        set => _sheet.SetColumnVisible(Index, value);
    }

    /// <summary>
    /// Sets the column width.
    /// 設定欄寬。
    /// </summary>
    /// <param name="width">The column width. / 欄寬。</param>
    public void SetWidth(OdfLength width)
    {
        _sheet.SetColumnWidth(Index, width);
    }

    /// <summary>
    /// Automatically fits the column width according to current content.
    /// 依目前內容自動調整欄寬。
    /// </summary>
    public void AutoFit()
    {
        _sheet.AutoFitColumnWidth(Index);
    }
}
