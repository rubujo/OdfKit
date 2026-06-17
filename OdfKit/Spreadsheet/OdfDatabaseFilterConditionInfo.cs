namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示資料庫範圍上一筆篩選條件的摘要資訊。
/// </summary>
/// <param name="fieldNumber">欄位編號（零起始）。</param>
/// <param name="operator">篩選運算子原文（<c>table:operator</c>）。</param>
/// <param name="value">篩選值原文。</param>
public sealed class OdfDatabaseFilterConditionInfo(int fieldNumber, string @operator, string value)
{
    /// <summary>
    /// 取得欄位編號。
    /// </summary>
    public int FieldNumber { get; } = fieldNumber;

    /// <summary>
    /// 取得篩選運算子原文。
    /// </summary>
    public string Operator { get; } = @operator ?? string.Empty;

    /// <summary>
    /// 取得篩選值原文。
    /// </summary>
    public string Value { get; } = value ?? string.Empty;
}
