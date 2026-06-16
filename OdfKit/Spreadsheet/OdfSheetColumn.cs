using System;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示工作表中的一欄。
/// </summary>
public sealed class OdfSheetColumn
{
    private readonly OdfTableSheet _sheet;

    /// <summary>
    /// 初始化 <see cref="OdfSheetColumn"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">所屬工作表。</param>
    /// <param name="index">以 0 為基準的欄索引。</param>
    internal OdfSheetColumn(OdfTableSheet sheet, int index)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        Index = index;
    }

    /// <summary>
    /// 取得以 0 為基準的欄索引。
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// 取得或設定此欄是否顯示。
    /// </summary>
    public bool Visible
    {
        get => _sheet.IsColumnVisible(Index);
        set => _sheet.SetColumnVisible(Index, value);
    }

    /// <summary>
    /// 設定欄寬。
    /// </summary>
    /// <param name="width">欄寬。</param>
    public void SetWidth(OdfLength width)
    {
        _sheet.SetColumnWidth(Index, width);
    }

    /// <summary>
    /// 依目前內容自動調整欄寬。
    /// </summary>
    public void AutoFit()
    {
        _sheet.AutoFitColumnWidth(Index);
    }
}
