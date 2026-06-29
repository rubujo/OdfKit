namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents summary information for a filter condition on a database range.
/// 表示資料庫範圍上一筆篩選條件的摘要資訊。
/// </summary>
/// <param name="fieldNumber">The zero-based field number. / 欄位編號，採零起始。</param>
/// <param name="operator">The original filter operator from <c>table:operator</c>. / 篩選運算子原文（<c>table:operator</c>）。</param>
/// <param name="value">The original filter value. / 篩選值原文。</param>
public sealed class OdfDatabaseFilterConditionInfo(int fieldNumber, string @operator, string value)
{
    /// <summary>
    /// Gets the field number.
    /// 取得欄位編號。
    /// </summary>
    public int FieldNumber { get; } = fieldNumber;

    /// <summary>
    /// Gets the original filter operator.
    /// 取得篩選運算子原文。
    /// </summary>
    public string Operator { get; } = @operator ?? string.Empty;

    /// <summary>
    /// Gets the original filter value.
    /// 取得篩選值原文。
    /// </summary>
    public string Value { get; } = value ?? string.Empty;
}
