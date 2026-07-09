namespace OdfKit.Spreadsheet;

/// <summary>
/// Configures row start options for <see cref="OdsStreamWriter.WriteStartRow(OdsRowWriteOptions)"/>.
/// 設定 <see cref="OdsStreamWriter.WriteStartRow(OdsRowWriteOptions)"/> 的資料列開始選項。
/// </summary>
/// <remarks>
/// Prefer this options object over multi-optional parameter lists for new call sites.
/// 新呼叫端請優先使用此 options 物件，避免多個尾端可選參數。
/// </remarks>
public sealed class OdsRowWriteOptions
{
    /// <summary>
    /// Gets the default row write options.
    /// 取得預設資料列寫入選項。
    /// </summary>
    public static OdsRowWriteOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets the row height in points; <see langword="null"/> omits an explicit height.
    /// 取得或設定列高（點）；<see langword="null"/> 表示不寫入明確高度。
    /// </summary>
    public double? Height { get; set; }

    /// <summary>
    /// Gets or sets the table row style name.
    /// 取得或設定資料列表格樣式名稱。
    /// </summary>
    public string? StyleName { get; set; }

    /// <summary>
    /// Gets or sets whether optimal row height is requested.
    /// 取得或設定是否要求最佳列高。
    /// </summary>
    public bool UseOptimalHeight { get; set; }
}
