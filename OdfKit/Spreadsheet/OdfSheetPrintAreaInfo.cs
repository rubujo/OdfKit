namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents summary information for a worksheet print area.
/// 表示工作表列印範圍的摘要資訊。
/// </summary>
/// <param name="sheetName">The sheet name. / 工作表名稱。</param>
/// <param name="rangeAddress">The print range address string from <c>table:print-ranges</c>. / 列印範圍位址字串（<c>table:print-ranges</c>）。</param>
public sealed class OdfSheetPrintAreaInfo(string sheetName, string rangeAddress)
{
    /// <summary>
    /// Gets the sheet name.
    /// 取得工作表名稱。
    /// </summary>
    public string SheetName { get; } = sheetName ?? string.Empty;

    /// <summary>
    /// Gets the print range address string.
    /// 取得列印範圍位址字串。
    /// </summary>
    public string RangeAddress { get; } = rangeAddress ?? string.Empty;

    /// <summary>
    /// Attempts to parse <see cref="RangeAddress"/> as an <see cref="OdfCellRange"/>.
    /// 嘗試將 <see cref="RangeAddress"/> 解析為 <see cref="OdfCellRange"/>。
    /// </summary>
    /// <param name="range">The cell range returned when parsing succeeds. / 解析成功時傳回的儲存格範圍。</param>
    /// <returns><see langword="true"/> if parsing succeeds. / 若解析成功則為 <see langword="true"/>。</returns>
    public bool TryGetRange(out OdfCellRange range) =>
        OdfCellRange.TryParse(RangeAddress, out range);
}
