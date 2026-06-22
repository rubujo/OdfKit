namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示工作表列印範圍的摘要資訊。
/// </summary>
/// <param name="sheetName">工作表名稱</param>
/// <param name="rangeAddress">列印範圍位址字串（<c>table:print-ranges</c>）</param>
public sealed class OdfSheetPrintAreaInfo(string sheetName, string rangeAddress)
{
    /// <summary>
    /// 取得工作表名稱。
    /// </summary>
    public string SheetName { get; } = sheetName ?? string.Empty;

    /// <summary>
    /// 取得列印範圍位址字串。
    /// </summary>
    public string RangeAddress { get; } = rangeAddress ?? string.Empty;

    /// <summary>
    /// 嘗試將 <see cref="RangeAddress"/> 解析為 <see cref="OdfCellRange"/>。
    /// </summary>
    /// <param name="range">解析成功時傳回的儲存格範圍</param>
    /// <returns>若解析成功則為 <see langword="true"/></returns>
    public bool TryGetRange(out OdfCellRange range) =>
        OdfCellRange.TryParse(RangeAddress, out range);
}
