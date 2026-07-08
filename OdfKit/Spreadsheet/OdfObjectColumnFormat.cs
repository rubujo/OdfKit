using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Configures practical formatting for one object-bound spreadsheet column.
/// 設定單一物件繫結試算表欄位的實務格式。
/// </summary>
public sealed class OdfObjectColumnFormat
{
    /// <summary>
    /// Gets or sets the style name applied to data cells.
    /// 取得或設定套用至資料儲存格的樣式名稱。
    /// </summary>
    public string? StyleName { get; set; }

    /// <summary>
    /// Gets or sets the style name applied to the header cell.
    /// 取得或設定套用至標題儲存格的樣式名稱。
    /// </summary>
    public string? HeaderStyleName { get; set; }

    /// <summary>
    /// Gets or sets the .NET number format registered as an ODF number style.
    /// 取得或設定要註冊為 ODF 數字樣式的 .NET 數字格式。
    /// </summary>
    public string? NumberFormat { get; set; }

    /// <summary>
    /// Gets or sets the column width.
    /// 取得或設定欄寬。
    /// </summary>
    public OdfLength? Width { get; set; }

    /// <summary>
    /// Gets or sets whether the column width is auto-fitted after writing.
    /// 取得或設定寫入後是否自動調整欄寬。
    /// </summary>
    public bool AutoFit { get; set; }
}
