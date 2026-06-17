namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示樞紐分析表一個排序欄位設定的摘要資訊。
/// </summary>
/// <param name="sourceFieldName">排序欄位名稱（<c>table:source-field-name</c>）。</param>
/// <param name="ascending">是否升冪排序。</param>
public sealed class OdfPivotTableSortFieldInfo(string sourceFieldName, bool ascending)
{
    /// <summary>
    /// 取得排序欄位名稱。
    /// </summary>
    public string SourceFieldName { get; } = sourceFieldName ?? string.Empty;

    /// <summary>
    /// 取得是否升冪排序。
    /// </summary>
    public bool Ascending { get; } = ascending;
}
