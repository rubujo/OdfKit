using System.Collections.Generic;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents summary information for a database range in a spreadsheet.
/// 表示試算表中一個資料庫範圍的摘要資訊。
/// </summary>
/// <param name="name">The database range name. / 資料庫範圍名稱。</param>
/// <param name="targetRangeAddress">The target range address string from <c>table:target-range-address</c>. / 目標範圍位址字串（<c>table:target-range-address</c>）。</param>
/// <param name="displayFilterButtons">Whether to display auto-filter buttons. / 是否顯示自動篩選按鈕。</param>
/// <param name="filterConditions">The filter condition list. / 篩選條件清單。</param>
/// <param name="sortRules">The sort rule list. / 排序規則清單。</param>
public sealed class OdfDatabaseRangeInfo(
    string name,
    string targetRangeAddress,
    bool displayFilterButtons,
    IReadOnlyList<OdfDatabaseFilterConditionInfo> filterConditions,
    IReadOnlyList<OdfDatabaseSortRuleInfo> sortRules)
{
    /// <summary>
    /// Gets the database range name.
    /// 取得資料庫範圍名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// Gets the target range address string.
    /// 取得目標範圍位址字串。
    /// </summary>
    public string TargetRangeAddress { get; } = targetRangeAddress ?? string.Empty;

    /// <summary>
    /// Gets whether auto-filter buttons are displayed.
    /// 取得是否顯示自動篩選按鈕。
    /// </summary>
    public bool DisplayFilterButtons { get; } = displayFilterButtons;

    /// <summary>
    /// Gets the filter condition list.
    /// 取得篩選條件清單。
    /// </summary>
    public IReadOnlyList<OdfDatabaseFilterConditionInfo> FilterConditions { get; } =
        filterConditions ?? [];

    /// <summary>
    /// Gets the sort rule list.
    /// 取得排序規則清單。
    /// </summary>
    public IReadOnlyList<OdfDatabaseSortRuleInfo> SortRules { get; } = sortRules ?? [];

    /// <summary>
    /// Attempts to parse <see cref="TargetRangeAddress"/> as an <see cref="OdfCellRange"/>.
    /// 嘗試將 <see cref="TargetRangeAddress"/> 解析為 <see cref="OdfCellRange"/>。
    /// </summary>
    /// <param name="range">The cell range returned when parsing succeeds. / 解析成功時傳回的儲存格範圍。</param>
    /// <returns><see langword="true"/> if parsing succeeds. / 若解析成功則為 <see langword="true"/>。</returns>
    public bool TryGetTargetRange(out OdfCellRange range) =>
        OdfCellRange.TryParse(TargetRangeAddress, out range);
}
