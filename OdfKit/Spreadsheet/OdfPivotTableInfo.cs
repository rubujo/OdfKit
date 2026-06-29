using System.Collections.Generic;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents summary information for a pivot table in a spreadsheet.
/// 表示試算表中一個樞紐分析表的摘要資訊。
/// </summary>
/// <param name="sheetName">The containing sheet name. / 所在工作表名稱。</param>
/// <param name="name">The pivot table name from <c>table:name</c>. / 樞紐分析表名稱（<c>table:name</c>）。</param>
/// <param name="sourceRangeAddress">The source data range address from <c>table:cell-range-address</c> under <c>table:source-cell-range</c>. / 來源資料範圍位址（<c>table:source-cell-range</c> 的 <c>table:cell-range-address</c>）。</param>
/// <param name="targetRangeAddress">The target output range start from <c>table:target-range-address</c>. / 目標輸出範圍起點（<c>table:target-range-address</c>）。</param>
/// <param name="hasColumnHeaders">Whether the source has column headers. / 來源是否含欄標題。</param>
/// <param name="hasRowHeaders">Whether the source has row headers. / 來源是否含列標題。</param>
/// <param name="fields">The field setting list. / 欄位設定清單。</param>
/// <param name="sortFields">The sort field list. / 排序欄位清單。</param>
/// <param name="filterConditions">The filter condition list. / 篩選條件清單。</param>
public sealed class OdfPivotTableInfo(
    string sheetName,
    string name,
    string sourceRangeAddress,
    string targetRangeAddress,
    bool hasColumnHeaders,
    bool hasRowHeaders,
    IReadOnlyList<OdfPivotTableFieldInfo> fields,
    IReadOnlyList<OdfPivotTableSortFieldInfo> sortFields,
    IReadOnlyList<OdfPivotTableFilterConditionInfo> filterConditions)
{
    /// <summary>
    /// Gets the containing sheet name.
    /// 取得所在工作表名稱。
    /// </summary>
    public string SheetName { get; } = sheetName ?? string.Empty;

    /// <summary>
    /// Gets the pivot table name.
    /// 取得樞紐分析表名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// Gets the source data range address string.
    /// 取得來源資料範圍位址字串。
    /// </summary>
    public string SourceRangeAddress { get; } = sourceRangeAddress ?? string.Empty;

    /// <summary>
    /// Gets the target output range start address string.
    /// 取得目標輸出範圍起點位址字串。
    /// </summary>
    public string TargetRangeAddress { get; } = targetRangeAddress ?? string.Empty;

    /// <summary>
    /// Gets whether the source has column headers.
    /// 取得來源是否含欄標題。
    /// </summary>
    public bool HasColumnHeaders { get; } = hasColumnHeaders;

    /// <summary>
    /// Gets whether the source has row headers.
    /// 取得來源是否含列標題。
    /// </summary>
    public bool HasRowHeaders { get; } = hasRowHeaders;

    /// <summary>
    /// Gets the field setting list.
    /// 取得欄位設定清單。
    /// </summary>
    public IReadOnlyList<OdfPivotTableFieldInfo> Fields { get; } = fields ?? [];

    /// <summary>
    /// Gets the sort field list.
    /// 取得排序欄位清單。
    /// </summary>
    public IReadOnlyList<OdfPivotTableSortFieldInfo> SortFields { get; } = sortFields ?? [];

    /// <summary>
    /// Gets the filter condition list.
    /// 取得篩選條件清單。
    /// </summary>
    public IReadOnlyList<OdfPivotTableFilterConditionInfo> FilterConditions { get; } = filterConditions ?? [];

    /// <summary>
    /// Attempts to parse <see cref="SourceRangeAddress"/> as an <see cref="OdfCellRange"/>.
    /// 嘗試將 <see cref="SourceRangeAddress"/> 解析為 <see cref="OdfCellRange"/>。
    /// </summary>
    /// <param name="range">The cell range returned when parsing succeeds. / 解析成功時傳回的儲存格範圍。</param>
    /// <returns><see langword="true"/> if parsing succeeds. / 若解析成功則為 <see langword="true"/>。</returns>
    public bool TryGetSourceRange(out OdfCellRange range) =>
        OdfCellRange.TryParse(SourceRangeAddress, out range);

    /// <summary>
    /// Attempts to parse <see cref="TargetRangeAddress"/> as the starting <see cref="OdfCellAddress"/>.
    /// 嘗試將 <see cref="TargetRangeAddress"/> 解析為起點 <see cref="OdfCellAddress"/>。
    /// </summary>
    /// <param name="address">The cell address returned when parsing succeeds. / 解析成功時傳回的儲存格位址。</param>
    /// <returns><see langword="true"/> if parsing succeeds. / 若解析成功則為 <see langword="true"/>。</returns>
    /// <remarks>
    /// According to the ODF 1.4 schema, <c>table:target-range-address</c> has the <c>cellRangeAddress</c> type. This method first tries to parse the value as a range and returns its start; if the string uses a single-cell address format, such as documents written by earlier versions, it falls back to parsing a single address for backward compatibility.
    /// 依 ODF 1.4 schema，<c>table:target-range-address</c> 的型別為 <c>cellRangeAddress</c>
    /// （範圍），此方法會優先嘗試以範圍格式解析並取其起點；若該字串為單一儲存格位址格式
    /// （例如舊版本寫入的文件），則回退以單一位址格式解析，以維持向下相容。
    /// </remarks>
    public bool TryGetTargetStart(out OdfCellAddress address)
    {
        if (OdfCellRange.TryParse(TargetRangeAddress, out OdfCellRange range))
        {
            address = range.StartAddress;
            return true;
        }

        return OdfCellAddress.TryParse(TargetRangeAddress, out address);
    }
}
