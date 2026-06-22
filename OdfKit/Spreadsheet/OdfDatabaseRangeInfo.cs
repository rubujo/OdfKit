using System.Collections.Generic;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示試算表中一個資料庫範圍的摘要資訊。
/// </summary>
/// <param name="name">資料庫範圍名稱</param>
/// <param name="targetRangeAddress">目標範圍位址字串（<c>table:target-range-address</c>）</param>
/// <param name="filterConditions">篩選條件清單</param>
/// <param name="sortRules">排序規則清單</param>
public sealed class OdfDatabaseRangeInfo(
    string name,
    string targetRangeAddress,
    IReadOnlyList<OdfDatabaseFilterConditionInfo> filterConditions,
    IReadOnlyList<OdfDatabaseSortRuleInfo> sortRules)
{
    /// <summary>
    /// 取得資料庫範圍名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// 取得目標範圍位址字串。
    /// </summary>
    public string TargetRangeAddress { get; } = targetRangeAddress ?? string.Empty;

    /// <summary>
    /// 取得篩選條件清單。
    /// </summary>
    public IReadOnlyList<OdfDatabaseFilterConditionInfo> FilterConditions { get; } =
        filterConditions ?? [];

    /// <summary>
    /// 取得排序規則清單。
    /// </summary>
    public IReadOnlyList<OdfDatabaseSortRuleInfo> SortRules { get; } = sortRules ?? [];

    /// <summary>
    /// 嘗試將 <see cref="TargetRangeAddress"/> 解析為 <see cref="OdfCellRange"/>。
    /// </summary>
    /// <param name="range">解析成功時傳回的儲存格範圍</param>
    /// <returns>若解析成功則為 <see langword="true"/></returns>
    public bool TryGetTargetRange(out OdfCellRange range) =>
        OdfCellRange.TryParse(TargetRangeAddress, out range);
}
