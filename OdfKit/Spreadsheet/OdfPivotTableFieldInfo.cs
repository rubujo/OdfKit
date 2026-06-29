namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents summary information for a pivot table field setting.
/// 表示樞紐分析表一個欄位設定的摘要資訊。
/// </summary>
/// <param name="sourceFieldName">The source field name from <c>table:source-field-name</c>. / 來源欄位名稱（<c>table:source-field-name</c>）。</param>
/// <param name="orientation">The field orientation from <c>table:orientation</c>, such as <c>row</c>, <c>column</c>, <c>data</c>, or <c>page</c>. / 欄位方向（<c>table:orientation</c>，例如 <c>row</c>、<c>column</c>、<c>data</c>、<c>page</c>）。</param>
/// <param name="function">The data field aggregate function from <c>table:function</c>, or <see langword="null"/> for non-data fields. / 資料欄位彙總函式（<c>table:function</c>），非資料欄位時為 <see langword="null"/>。</param>
/// <param name="formula">The calculated field formula from <c>table:formula</c>, or <see langword="null"/> for non-calculated fields. / 計算欄位公式（<c>table:formula</c>），非計算欄位時為 <see langword="null"/>。</param>
public sealed class OdfPivotTableFieldInfo(
    string sourceFieldName,
    string orientation,
    string? function,
    string? formula)
{
    /// <summary>
    /// Gets the source field name.
    /// 取得來源欄位名稱。
    /// </summary>
    public string SourceFieldName { get; } = sourceFieldName ?? string.Empty;

    /// <summary>
    /// Gets the field orientation.
    /// 取得欄位方向。
    /// </summary>
    public string Orientation { get; } = orientation ?? string.Empty;

    /// <summary>
    /// Gets the data field aggregate function.
    /// 取得資料欄位彙總函式。
    /// </summary>
    public string? Function { get; } = function;

    /// <summary>
    /// Gets the calculated field formula.
    /// 取得計算欄位公式。
    /// </summary>
    public string? Formula { get; } = formula;
}
