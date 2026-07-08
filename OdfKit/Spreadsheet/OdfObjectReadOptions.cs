using System.Globalization;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Configures spreadsheet-to-object binding.
/// 設定試算表讀回物件的繫結行為。
/// </summary>
public class OdfObjectReadOptions
{
    /// <summary>
    /// Gets or sets the zero-based header row offset within the supplied range.
    /// 取得或設定指定範圍內 0 基準的標題列位移。
    /// </summary>
    public int HeaderRow { get; set; }

    /// <summary>
    /// Gets or sets the zero-based first data row offset within the supplied range.
    /// 取得或設定指定範圍內 0 基準的第一個資料列位移。
    /// </summary>
    public int DataStartRow { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether reading stops at the first empty data row.
    /// 取得或設定是否在第一個空白資料列停止讀取。
    /// </summary>
    public bool StopAtFirstEmptyRow { get; set; } = true;

    /// <summary>
    /// Gets or sets the culture used for string conversions.
    /// 取得或設定字串轉換使用的文化特性。
    /// </summary>
    public CultureInfo? CultureInfo { get; set; }

    /// <summary>
    /// Gets or sets how missing columns are handled.
    /// 取得或設定缺少欄位時的處理方式。
    /// </summary>
    public OdfObjectMissingColumnPolicy MissingColumnPolicy { get; set; } = OdfObjectMissingColumnPolicy.Ignore;

    /// <summary>
    /// Gets or sets how unknown spreadsheet columns are handled.
    /// 取得或設定未知試算表欄位的處理方式。
    /// </summary>
    public OdfObjectUnknownColumnPolicy UnknownColumnPolicy { get; set; }

    /// <summary>
    /// Gets or sets how duplicate header cells are handled.
    /// 取得或設定重複標題儲存格的處理方式。
    /// </summary>
    public OdfObjectDuplicateHeaderPolicy DuplicateHeaderPolicy { get; set; }

    /// <summary>
    /// Gets or sets how conversion errors are handled.
    /// 取得或設定轉換錯誤的處理方式。
    /// </summary>
    public OdfObjectConversionErrorPolicy ConversionErrorPolicy { get; set; }

    /// <summary>
    /// Gets or sets the explicit property-to-column map.
    /// 取得或設定明確的屬性與欄位對應。
    /// </summary>
    public OdfObjectColumnMap? ColumnMap { get; set; }

    /// <summary>
    /// Gets or sets the optional binding report populated while reading.
    /// 取得或設定讀取時填入的選用繫結報告。
    /// </summary>
    public OdfObjectBindingReport? Report { get; set; }
}
