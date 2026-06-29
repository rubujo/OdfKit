namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents summary information for a sort rule on a database range.
/// 表示資料庫範圍上一筆排序規則的摘要資訊。
/// </summary>
/// <param name="fieldNumber">The zero-based field number. / 欄位編號，採零起始。</param>
/// <param name="ascending">Whether sorting is ascending. / 是否遞增排序。</param>
public sealed class OdfDatabaseSortRuleInfo(int fieldNumber, bool ascending)
{
    /// <summary>
    /// Gets the field number.
    /// 取得欄位編號。
    /// </summary>
    public int FieldNumber { get; } = fieldNumber;

    /// <summary>
    /// Gets whether sorting is ascending.
    /// 取得是否遞增排序。
    /// </summary>
    public bool Ascending { get; } = ascending;
}
