namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示樞紐分析表一個篩選條件的摘要資訊。
/// </summary>
/// <param name="sourceFieldName">篩選欄位名稱（<c>table:source-field-name</c>）。</param>
/// <param name="operator">比較運算子（<c>table:operator</c>）。</param>
/// <param name="value">篩選值（<c>table:value</c>）。</param>
public sealed class OdfPivotTableFilterConditionInfo(string sourceFieldName, string @operator, string value)
{
    /// <summary>
    /// 取得篩選欄位名稱。
    /// </summary>
    public string SourceFieldName { get; } = sourceFieldName ?? string.Empty;

    /// <summary>
    /// 取得比較運算子。
    /// </summary>
    public string Operator { get; } = @operator ?? string.Empty;

    /// <summary>
    /// 取得篩選值。
    /// </summary>
    public string Value { get; } = value ?? string.Empty;
}
