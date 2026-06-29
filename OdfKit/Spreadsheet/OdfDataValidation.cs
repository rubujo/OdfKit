namespace OdfKit.Spreadsheet;

/// <summary>
/// Specifies ODS data validation conditions.
/// 表示 ODS 資料驗證條件的列舉。
/// </summary>
public enum OdfValidationCondition
{
    /// <summary>
    /// The integer value is between the specified bounds.
    /// 整數值介於指定範圍。
    /// </summary>
    IntegerBetween,

    /// <summary>
    /// The decimal value is between the specified bounds.
    /// 十進位數值介於指定範圍。
    /// </summary>
    DecimalBetween,

    /// <summary>
    /// The text length is between the specified bounds.
    /// 文字長度介於指定範圍。
    /// </summary>
    TextLengthBetween
}

/// <summary>
/// Specifies ODS data validation alert styles.
/// 表示 ODS 資料驗證警告樣式的列舉。
/// </summary>
public enum OdfValidationAlertStyle
{
    /// <summary>
    /// Stop.
    /// 停止。
    /// </summary>
    Stop,

    /// <summary>
    /// Warning.
    /// 警告。
    /// </summary>
    Warning,

    /// <summary>
    /// Information.
    /// 資訊。
    /// </summary>
    Information
}

/// <summary>
/// Defines settings for ODS data validation.
/// 定義 ODS 資料驗證的設定資訊。
/// </summary>
public sealed class OdfDataValidation
{
    /// <summary>
    /// Gets or sets the cell range to which this validation applies.
    /// 取得或設定套用此驗證的儲存格範圍。
    /// </summary>
    public OdfCellRange ApplyTo { get; init; }

    /// <summary>
    /// Gets or sets the validation condition type.
    /// 取得或設定驗證的條件類型。
    /// </summary>
    public OdfValidationCondition Condition { get; init; }

    /// <summary>
    /// Gets or sets the first formula argument, such as the lower bound.
    /// 取得或設定第一個公式參數（例如下限值）。
    /// </summary>
    public string Formula1 { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the second formula argument, such as the upper bound.
    /// 取得或設定第二個公式參數（例如上限值）。
    /// </summary>
    public string Formula2 { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the message content displayed on input error.
    /// 取得或設定輸入錯誤時顯示的訊息內容。
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the title displayed on input error.
    /// 取得或設定輸入錯誤時顯示的標題。
    /// </summary>
    public string ErrorTitle { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the alert style level used on input error.
    /// 取得或設定輸入錯誤時的警告樣式等級。
    /// </summary>
    public OdfValidationAlertStyle AlertStyle { get; init; } = OdfValidationAlertStyle.Stop;
}
