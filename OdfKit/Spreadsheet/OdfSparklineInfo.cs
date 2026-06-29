namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents a sparkline in a LibreOffice calcext sparkline group.
/// 表示 LibreOffice calcext 走勢圖群組內的一筆走勢圖。
/// </summary>
/// <param name="dataRangeRef">The data source range address from <c>calcext:dataRangeRef</c>. / 資料來源範圍位址（<c>calcext:dataRangeRef</c>）。</param>
/// <param name="hostCellRef">The cell address that displays the sparkline from <c>calcext:hostCellRef</c>. / 顯示走勢圖的儲存格位址（<c>calcext:hostCellRef</c>）。</param>
public sealed class OdfSparklineInfo(string dataRangeRef, string hostCellRef)
{
    /// <summary>
    /// Gets the data source range address.
    /// 取得資料來源範圍位址。
    /// </summary>
    public string DataRangeRef { get; } = dataRangeRef ?? string.Empty;

    /// <summary>
    /// Gets the cell address that displays the sparkline.
    /// 取得顯示走勢圖的儲存格位址。
    /// </summary>
    public string HostCellRef { get; } = hostCellRef ?? string.Empty;

    /// <summary>
    /// Attempts to parse <see cref="DataRangeRef"/> as an <see cref="OdfCellRange"/>.
    /// 嘗試將 <see cref="DataRangeRef"/> 解析為 <see cref="OdfCellRange"/>。
    /// </summary>
    /// <param name="range">The cell range returned when parsing succeeds. / 解析成功時傳回的儲存格範圍。</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>. / 若解析成功則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool TryGetDataRange(out OdfCellRange range) =>
        OdfCellRange.TryParse(DataRangeRef, out range);

    /// <summary>
    /// Attempts to parse <see cref="HostCellRef"/> as an <see cref="OdfCellAddress"/>.
    /// 嘗試將 <see cref="HostCellRef"/> 解析為 <see cref="OdfCellAddress"/>。
    /// </summary>
    /// <param name="address">The cell address returned when parsing succeeds. / 解析成功時傳回的儲存格位址。</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>. / 若解析成功則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool TryGetHostCell(out OdfCellAddress address) =>
        OdfCellAddress.TryParse(HostCellRef, out address);
}
