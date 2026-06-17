namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示 LibreOffice calcext 走勢圖群組內的一筆走勢圖。
/// </summary>
/// <param name="dataRangeRef">資料來源範圍位址（<c>calcext:dataRangeRef</c>）。</param>
/// <param name="hostCellRef">顯示走勢圖的儲存格位址（<c>calcext:hostCellRef</c>）。</param>
public sealed class OdfSparklineInfo(string dataRangeRef, string hostCellRef)
{
    /// <summary>
    /// 取得資料來源範圍位址。
    /// </summary>
    public string DataRangeRef { get; } = dataRangeRef ?? string.Empty;

    /// <summary>
    /// 取得顯示走勢圖的儲存格位址。
    /// </summary>
    public string HostCellRef { get; } = hostCellRef ?? string.Empty;

    /// <summary>
    /// 嘗試將 <see cref="DataRangeRef"/> 解析為 <see cref="OdfCellRange"/>。
    /// </summary>
    /// <param name="range">解析成功時傳回的儲存格範圍。</param>
    /// <returns>若解析成功則為 true，否則為 false。</returns>
    public bool TryGetDataRange(out OdfCellRange range) =>
        OdfCellRange.TryParse(DataRangeRef, out range);

    /// <summary>
    /// 嘗試將 <see cref="HostCellRef"/> 解析為 <see cref="OdfCellAddress"/>。
    /// </summary>
    /// <param name="address">解析成功時傳回的儲存格位址。</param>
    /// <returns>若解析成功則為 true，否則為 false。</returns>
    public bool TryGetHostCell(out OdfCellAddress address) =>
        OdfCellAddress.TryParse(HostCellRef, out address);
}
