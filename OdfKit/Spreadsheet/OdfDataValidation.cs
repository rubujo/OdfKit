namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示 ODS 資料驗證條件的列舉。
/// </summary>
public enum OdfValidationCondition
{
    /// <summary>
    /// 整數值介於指定範圍。
    /// </summary>
    IntegerBetween,

    /// <summary>
    /// 十進位數值介於指定範圍。
    /// </summary>
    DecimalBetween,

    /// <summary>
    /// 文字長度介於指定範圍。
    /// </summary>
    TextLengthBetween
}

/// <summary>
/// 表示 ODS 資料驗證警告樣式的列舉。
/// </summary>
public enum OdfValidationAlertStyle
{
    /// <summary>
    /// 停止。
    /// </summary>
    Stop,

    /// <summary>
    /// 警告。
    /// </summary>
    Warning,

    /// <summary>
    /// 資訊。
    /// </summary>
    Information
}

/// <summary>
/// 定義 ODS 資料驗證的設定資訊。
/// </summary>
public sealed class OdfDataValidation
{
    /// <summary>
    /// 取得或設定套用此驗證的儲存格範圍。
    /// </summary>
    public OdfCellRange ApplyTo { get; init; }

    /// <summary>
    /// 取得或設定驗證的條件類型。
    /// </summary>
    public OdfValidationCondition Condition { get; init; }

    /// <summary>
    /// 取得或設定第一個公式參數（例如下限值）。
    /// </summary>
    public string Formula1 { get; init; } = string.Empty;

    /// <summary>
    /// 取得或設定第二個公式參數（例如上限值）。
    /// </summary>
    public string Formula2 { get; init; } = string.Empty;

    /// <summary>
    /// 取得或設定輸入錯誤時顯示的訊息內容。
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// 取得或設定輸入錯誤時顯示的標題。
    /// </summary>
    public string ErrorTitle { get; init; } = string.Empty;

    /// <summary>
    /// 取得或設定輸入錯誤時的警告樣式等級。
    /// </summary>
    public OdfValidationAlertStyle AlertStyle { get; init; } = OdfValidationAlertStyle.Stop;
}
