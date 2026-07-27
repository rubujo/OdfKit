namespace OdfKit.Spreadsheet;

/// <summary>
/// Identifies the semantic type of an ODS cell.
/// 識別 ODS 儲存格的語意類型。
/// </summary>
public enum OdsCellValueKind
{
    /// <summary>
    /// An empty cell. / 空白儲存格。
    /// </summary>
    Empty,
    /// <summary>
    /// A text value. / 文字值。
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Naming",
        "CA1720:Identifier contains type name",
        Justification = "String is the normative ODF office:value-type token.")]
    String,
    /// <summary>
    /// A numeric value. / 數值。
    /// </summary>
    Number,
    /// <summary>
    /// A percentage value. / 百分比值。
    /// </summary>
    Percentage,
    /// <summary>
    /// A currency value. / 貨幣值。
    /// </summary>
    Currency,
    /// <summary>
    /// A Boolean value. / 布林值。
    /// </summary>
    Boolean,
    /// <summary>
    /// A date or date-time value. / 日期或日期時間值。
    /// </summary>
    Date,
    /// <summary>
    /// A duration or time value. / 期間或時間值。
    /// </summary>
    Time,
    /// <summary>
    /// An unrecognized ODF value type. / 無法辨識的 ODF 值類型。
    /// </summary>
    Unknown
}

/// <summary>
/// Preserves the semantic value and source metadata of an ODS cell.
/// 保留 ODS 儲存格的語意值與來源中繼資料。
/// </summary>
public sealed class OdsCellValue
{
    internal OdsCellValue(OdsCellValueKind kind, object? value, string? formula, string? currency,
        string? displayText, string? rawValueType)
    {
        Kind = kind;
        Value = value;
        Formula = formula;
        Currency = currency;
        DisplayText = displayText;
        RawValueType = rawValueType;
    }

    /// <summary>
    /// Gets the semantic value kind.
    /// 取得語意值類型。
    /// </summary>
    public OdsCellValueKind Kind { get; }

    /// <summary>
    /// Gets the semantic or cached formula value.
    /// 取得語意值或公式快取值。
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// Gets the original formula expression.
    /// 取得原始公式運算式。
    /// </summary>
    public string? Formula { get; }

    /// <summary>
    /// Gets the ISO currency code when available.
    /// 取得可用的 ISO 貨幣代碼。
    /// </summary>
    public string? Currency { get; }

    /// <summary>
    /// Gets the extracted display text.
    /// 取得擷取後的顯示文字。
    /// </summary>
    public string? DisplayText { get; }

    /// <summary>
    /// Gets the original ODF value-type token.
    /// 取得原始 ODF value-type 詞彙。
    /// </summary>
    public string? RawValueType { get; }
}
