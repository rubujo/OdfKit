using OdfKit.Chart;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents summary information for an embedded chart in a worksheet.
/// 表示工作表中一個嵌入圖表的摘要資訊。
/// </summary>
/// <param name="sheetName">The containing sheet name. / 所在工作表名稱。</param>
/// <param name="anchorAddress">The chart anchor cell address string. / 圖表錨定儲存格位址字串。</param>
/// <param name="objectPath">The embedded chart subpackage path, such as <c>Object 1/</c>. / 嵌入圖表子封裝路徑，例如 <c>Object 1/</c>。</param>
/// <param name="chartType">The chart type. / 圖表類型。</param>
/// <param name="title">The chart title. / 圖表標題。</param>
/// <param name="dataRangeAddress">The data range address string. / 資料範圍位址字串。</param>
public sealed class OdfEmbeddedChartInfo(
    string sheetName,
    string anchorAddress,
    string objectPath,
    OdfChartType chartType,
    string? title,
    string? dataRangeAddress)
{
    /// <summary>
    /// Gets the containing sheet name.
    /// 取得所在工作表名稱。
    /// </summary>
    public string SheetName { get; } = sheetName ?? string.Empty;

    /// <summary>
    /// Gets the chart anchor cell address string.
    /// 取得圖表錨定儲存格位址字串。
    /// </summary>
    public string AnchorAddress { get; } = anchorAddress ?? string.Empty;

    /// <summary>
    /// Gets the embedded chart subpackage path.
    /// 取得嵌入圖表子封裝路徑。
    /// </summary>
    public string ObjectPath { get; } = objectPath ?? string.Empty;

    /// <summary>
    /// Gets the chart type.
    /// 取得圖表類型。
    /// </summary>
    public OdfChartType ChartType { get; } = chartType;

    /// <summary>
    /// Gets the chart title.
    /// 取得圖表標題。
    /// </summary>
    public string? Title { get; } = title;

    /// <summary>
    /// Gets the data range address string.
    /// 取得資料範圍位址字串。
    /// </summary>
    public string? DataRangeAddress { get; } = dataRangeAddress;

    /// <summary>
    /// Attempts to parse <see cref="AnchorAddress"/> as an <see cref="OdfCellAddress"/>.
    /// 嘗試將 <see cref="AnchorAddress"/> 解析為 <see cref="OdfCellAddress"/>。
    /// </summary>
    /// <param name="address">The cell address returned when parsing succeeds. / 解析成功時傳回的儲存格位址。</param>
    /// <returns><see langword="true"/> if parsing succeeds. / 若解析成功則為 <see langword="true"/>。</returns>
    public bool TryGetAnchorAddress(out OdfCellAddress address) =>
        OdfCellAddress.TryParse(AnchorAddress, out address);

    /// <summary>
    /// Attempts to parse <see cref="DataRangeAddress"/> as an <see cref="OdfCellRange"/>.
    /// 嘗試將 <see cref="DataRangeAddress"/> 解析為 <see cref="OdfCellRange"/>。
    /// </summary>
    /// <param name="range">The cell range returned when parsing succeeds. / 解析成功時傳回的儲存格範圍。</param>
    /// <returns><see langword="true"/> if parsing succeeds. / 若解析成功則為 <see langword="true"/>。</returns>
    public bool TryGetDataRange(out OdfCellRange range)
    {
        if (string.IsNullOrEmpty(DataRangeAddress))
        {
            range = default;
            return false;
        }

        return OdfCellRange.TryParse(DataRangeAddress!, out range);
    }
}
