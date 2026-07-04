namespace OdfKit.Spreadsheet;

/// <summary>
/// Defines defensive upper bounds for repeated spreadsheet rows and columns.
/// 試算表重複列與欄的防禦性上限常數。
/// </summary>
public static class OdfSpreadsheetLimits
{
    /// <summary>
    /// The maximum repeated row or column count allowed during CSV export.
    /// CSV 匯出時允許的最大重複列或欄次數。
    /// </summary>
    public const int CsvMaxRepeat = 10_000;

    /// <summary>
    /// The maximum repeated row or column count allowed during formula evaluation.
    /// 公式評估時允許的最大重複列或欄次數。
    /// </summary>
    public const int FormulaMaxRepeat = 10_000;

    /// <summary>
    /// The maximum repeated row or column count allowed while building an embedded chart's local data cache.
    /// 建立嵌入圖表本地資料快取時允許的最大重複列或欄次數。
    /// </summary>
    public const int ChartMaxRepeat = 10_000;

    /// <summary>
    /// The maximum total cell count allowed when materializing a chart series data range for rendering.
    /// 圖表渲染時，將資料範圍具體化為陣列所允許的最大儲存格總數。
    /// </summary>
    public const int ChartRenderMaxCells = 1_000_000;

    /// <summary>
    /// The maximum number of days allowed for date-span iteration in date/time formula functions
    /// (e.g. WORKDAY, NETWORKDAYS), matching the widest date range supported by the spreadsheet engine.
    /// 日期時間公式函式（如 WORKDAY、NETWORKDAYS）逐日迭代所允許的最大天數，與試算表引擎支援的最大日期範圍一致。
    /// </summary>
    public const int FormulaMaxDateSpanDays = 3_000_000;

    /// <summary>
    /// The maximum row index allowed by the ODF spreadsheet grid (matches Excel/Calc's 1,048,576-row limit).
    /// ODF 試算表格線允許的最大列索引（與 Excel/Calc 的 1,048,576 列上限一致）。
    /// </summary>
    public const int MaxRowIndex = 1_048_576 - 1;

    /// <summary>
    /// The maximum column index allowed by the ODF spreadsheet grid (matches Excel/Calc's 16,384-column limit).
    /// ODF 試算表格線允許的最大欄索引（與 Excel/Calc 的 16,384 欄上限一致）。
    /// </summary>
    public const int MaxColumnIndex = 16_384 - 1;
}
