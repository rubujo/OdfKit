using OdfKit.Chart;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示工作表中一個嵌入圖表的摘要資訊。
/// </summary>
/// <param name="sheetName">所在工作表名稱</param>
/// <param name="anchorAddress">圖表錨定儲存格位址字串</param>
/// <param name="objectPath">嵌入圖表子封裝路徑（例如 <c>Object 1/</c>）</param>
/// <param name="chartType">圖表類型</param>
/// <param name="title">圖表標題</param>
/// <param name="dataRangeAddress">資料範圍位址字串</param>
public sealed class OdfEmbeddedChartInfo(
    string sheetName,
    string anchorAddress,
    string objectPath,
    OdfChartType chartType,
    string? title,
    string? dataRangeAddress)
{
    /// <summary>
    /// 取得所在工作表名稱。
    /// </summary>
    public string SheetName { get; } = sheetName ?? string.Empty;

    /// <summary>
    /// 取得圖表錨定儲存格位址字串。
    /// </summary>
    public string AnchorAddress { get; } = anchorAddress ?? string.Empty;

    /// <summary>
    /// 取得嵌入圖表子封裝路徑。
    /// </summary>
    public string ObjectPath { get; } = objectPath ?? string.Empty;

    /// <summary>
    /// 取得圖表類型。
    /// </summary>
    public OdfChartType ChartType { get; } = chartType;

    /// <summary>
    /// 取得圖表標題。
    /// </summary>
    public string? Title { get; } = title;

    /// <summary>
    /// 取得資料範圍位址字串。
    /// </summary>
    public string? DataRangeAddress { get; } = dataRangeAddress;

    /// <summary>
    /// 嘗試將 <see cref="AnchorAddress"/> 解析為 <see cref="OdfCellAddress"/>。
    /// </summary>
    /// <param name="address">解析成功時傳回的儲存格位址</param>
    /// <returns>若解析成功則為 <see langword="true"/></returns>
    public bool TryGetAnchorAddress(out OdfCellAddress address) =>
        OdfCellAddress.TryParse(AnchorAddress, out address);

    /// <summary>
    /// 嘗試將 <see cref="DataRangeAddress"/> 解析為 <see cref="OdfCellRange"/>。
    /// </summary>
    /// <param name="range">解析成功時傳回的儲存格範圍</param>
    /// <returns>若解析成功則為 <see langword="true"/></returns>
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
