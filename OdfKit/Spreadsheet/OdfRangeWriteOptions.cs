namespace OdfKit.Spreadsheet;

/// <summary>
/// Configures high-level spreadsheet range write operations.
/// 設定高階試算表範圍寫入作業。
/// </summary>
public sealed class OdfRangeWriteOptions
{
    /// <summary>
    /// Gets or sets whether cells inside the target rectangle but outside short input rows are cleared.
    /// 取得或設定是否清除目標矩形中超出短輸入列的儲存格。
    /// </summary>
    public bool ClearTrailingCells { get; set; }

    /// <summary>
    /// Gets or sets whether existing target cell styles are preserved when writing values.
    /// 取得或設定寫入值時是否保留既有目標儲存格樣式。
    /// </summary>
    public bool PreserveStyles { get; set; } = true;

    /// <summary>
    /// Gets or sets whether formulas copied from template rows are shifted by row offset.
    /// 取得或設定是否依列位移調整從模板列複製的公式。
    /// </summary>
    public bool ShiftFormulas { get; set; } = true;

    /// <summary>
    /// Gets or sets the column used to detect the append row; <see langword="null"/> uses the whole used range.
    /// 取得或設定用來判斷附加列的欄位；<see langword="null"/> 表示使用整個已使用範圍。
    /// </summary>
    public int? AppendKeyColumn { get; set; }

    /// <summary>
    /// Gets a default options instance.
    /// 取得預設選項執行個體。
    /// </summary>
    public static OdfRangeWriteOptions Default { get; } = new();
}
