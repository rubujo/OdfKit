namespace OdfKit.Spreadsheet;

/// <summary>
/// Configures resource limits and stream ownership for <see cref="OdsStreamReader"/>.
/// 設定 <see cref="OdsStreamReader"/> 的資源限制與資料流所有權。
/// </summary>
public sealed class OdsStreamReaderOptions
{
    /// <summary>
    /// Gets or sets the maximum XML character count. A value of zero disables this limit.
    /// 取得或設定 XML 字元數上限；設為 0 代表停用此限制。
    /// </summary>
    public long MaxXmlCharactersInDocument { get; set; } = 64L * 1024L * 1024L;

    /// <summary>
    /// Gets or sets the maximum number of rows returned from one worksheet.
    /// 取得或設定單一工作表可回傳的資料列數上限。
    /// </summary>
    public int MaxRows { get; set; } = 1_048_576;

    /// <summary>
    /// Gets or sets the maximum number of columns in one row.
    /// 取得或設定單一資料列的資料行數上限。
    /// </summary>
    public int MaxColumns { get; set; } = 16_384;

    /// <summary>
    /// Gets or sets the maximum value accepted for a repeated-row declaration.
    /// 取得或設定重複資料列宣告可接受的最大值。
    /// </summary>
    public int MaxRepeatedRows { get; set; } = 1_048_576;

    /// <summary>
    /// Gets or sets the maximum value accepted for a repeated-column declaration.
    /// 取得或設定重複資料行宣告可接受的最大值。
    /// </summary>
    public int MaxRepeatedColumns { get; set; } = 16_384;

    /// <summary>
    /// Gets or sets the maximum extracted text length for one cell.
    /// 取得或設定單一儲存格可擷取的文字長度上限。
    /// </summary>
    public int MaxCellTextCharacters { get; set; } = 16 * 1024 * 1024;

    /// <summary>
    /// Gets or sets a value indicating whether the input stream remains open after disposal.
    /// 取得或設定處置讀取器後是否保持輸入資料流開啟。
    /// </summary>
    public bool LeaveOpen { get; set; }
}
