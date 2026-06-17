namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示樞紐分析表一個欄位設定的摘要資訊。
/// </summary>
/// <param name="sourceFieldName">來源欄位名稱（<c>table:source-field-name</c>）。</param>
/// <param name="orientation">欄位方向（<c>table:orientation</c>，例如 row、column、data、page）。</param>
/// <param name="function">資料欄位彙總函式（<c>table:function</c>），非資料欄位時為 <see langword="null"/>。</param>
/// <param name="formula">計算欄位公式（<c>table:formula</c>），非計算欄位時為 <see langword="null"/>。</param>
public sealed class OdfPivotTableFieldInfo(
    string sourceFieldName,
    string orientation,
    string? function,
    string? formula)
{
    /// <summary>
    /// 取得來源欄位名稱。
    /// </summary>
    public string SourceFieldName { get; } = sourceFieldName ?? string.Empty;

    /// <summary>
    /// 取得欄位方向。
    /// </summary>
    public string Orientation { get; } = orientation ?? string.Empty;

    /// <summary>
    /// 取得資料欄位彙總函式。
    /// </summary>
    public string? Function { get; } = function;

    /// <summary>
    /// 取得計算欄位公式。
    /// </summary>
    public string? Formula { get; } = formula;
}
