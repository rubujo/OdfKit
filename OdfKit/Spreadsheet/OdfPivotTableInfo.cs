using System.Collections.Generic;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示試算表中一個樞紐分析表的摘要資訊。
/// </summary>
/// <param name="sheetName">所在工作表名稱。</param>
/// <param name="name">樞紐分析表名稱（<c>table:name</c>）。</param>
/// <param name="sourceRangeAddress">來源資料範圍位址（<c>table:source-cell-range</c> 的 <c>table:cell-range-address</c>）。</param>
/// <param name="targetRangeAddress">目標輸出範圍起點（<c>table:target-range-address</c>）。</param>
/// <param name="hasColumnHeaders">來源是否含欄標題。</param>
/// <param name="hasRowHeaders">來源是否含列標題。</param>
/// <param name="fields">欄位設定清單。</param>
/// <param name="sortFields">排序欄位清單。</param>
/// <param name="filterConditions">篩選條件清單。</param>
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
    /// 取得所在工作表名稱。
    /// </summary>
    public string SheetName { get; } = sheetName ?? string.Empty;

    /// <summary>
    /// 取得樞紐分析表名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// 取得來源資料範圍位址字串。
    /// </summary>
    public string SourceRangeAddress { get; } = sourceRangeAddress ?? string.Empty;

    /// <summary>
    /// 取得目標輸出範圍起點位址字串。
    /// </summary>
    public string TargetRangeAddress { get; } = targetRangeAddress ?? string.Empty;

    /// <summary>
    /// 取得來源是否含欄標題。
    /// </summary>
    public bool HasColumnHeaders { get; } = hasColumnHeaders;

    /// <summary>
    /// 取得來源是否含列標題。
    /// </summary>
    public bool HasRowHeaders { get; } = hasRowHeaders;

    /// <summary>
    /// 取得欄位設定清單。
    /// </summary>
    public IReadOnlyList<OdfPivotTableFieldInfo> Fields { get; } = fields ?? [];

    /// <summary>
    /// 取得排序欄位清單。
    /// </summary>
    public IReadOnlyList<OdfPivotTableSortFieldInfo> SortFields { get; } = sortFields ?? [];

    /// <summary>
    /// 取得篩選條件清單。
    /// </summary>
    public IReadOnlyList<OdfPivotTableFilterConditionInfo> FilterConditions { get; } = filterConditions ?? [];

    /// <summary>
    /// 嘗試將 <see cref="SourceRangeAddress"/> 解析為 <see cref="OdfCellRange"/>。
    /// </summary>
    /// <param name="range">解析成功時傳回的儲存格範圍。</param>
    /// <returns>若解析成功則為 <see langword="true"/>。</returns>
    public bool TryGetSourceRange(out OdfCellRange range) =>
        OdfCellRange.TryParse(SourceRangeAddress, out range);

    /// <summary>
    /// 嘗試將 <see cref="TargetRangeAddress"/> 解析為起點 <see cref="OdfCellAddress"/>。
    /// </summary>
    /// <param name="address">解析成功時傳回的儲存格位址。</param>
    /// <returns>若解析成功則為 <see langword="true"/>。</returns>
    /// <remarks>
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
