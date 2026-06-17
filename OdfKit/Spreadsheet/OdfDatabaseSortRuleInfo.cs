namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示資料庫範圍上一筆排序規則的摘要資訊。
/// </summary>
/// <param name="fieldNumber">欄位編號（零起始）。</param>
/// <param name="ascending">是否遞增排序。</param>
public sealed class OdfDatabaseSortRuleInfo(int fieldNumber, bool ascending)
{
    /// <summary>
    /// 取得欄位編號。
    /// </summary>
    public int FieldNumber { get; } = fieldNumber;

    /// <summary>
    /// 取得是否遞增排序。
    /// </summary>
    public bool Ascending { get; } = ascending;
}
