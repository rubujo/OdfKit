namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents summary information for a pivot table filter condition.
/// 表示樞紐分析表一個篩選條件的摘要資訊。
/// </summary>
/// <param name="sourceFieldName">The filter field name from <c>table:source-field-name</c>. / 篩選欄位名稱（<c>table:source-field-name</c>）。</param>
/// <param name="operator">The comparison operator from <c>table:operator</c>. / 比較運算子（<c>table:operator</c>）。</param>
/// <param name="value">The filter value from <c>table:value</c>. / 篩選值（<c>table:value</c>）。</param>
public sealed class OdfPivotTableFilterConditionInfo(string sourceFieldName, string @operator, string value)
{
    /// <summary>
    /// Gets the filter field name.
    /// 取得篩選欄位名稱。
    /// </summary>
    public string SourceFieldName { get; } = sourceFieldName ?? string.Empty;

    /// <summary>
    /// Gets the comparison operator.
    /// 取得比較運算子。
    /// </summary>
    public string Operator { get; } = @operator ?? string.Empty;

    /// <summary>
    /// Gets the filter value.
    /// 取得篩選值。
    /// </summary>
    public string Value { get; } = value ?? string.Empty;
}
