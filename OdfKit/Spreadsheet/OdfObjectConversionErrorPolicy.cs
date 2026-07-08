namespace OdfKit.Spreadsheet;

/// <summary>
/// Defines how object reading handles cell conversion errors.
/// 定義物件讀取遇到儲存格轉換錯誤時的處理方式。
/// </summary>
public enum OdfObjectConversionErrorPolicy
{
    /// <summary>
    /// Throws when a conversion error occurs.
    /// 發生轉換錯誤時擲出例外。
    /// </summary>
    Throw,

    /// <summary>
    /// Records a warning and leaves the property default value.
    /// 記錄警告並保留屬性預設值。
    /// </summary>
    WarnAndUseDefault,

    /// <summary>
    /// Records a warning and skips the entire row.
    /// 記錄警告並略過整個資料列。
    /// </summary>
    WarnAndSkipRow
}
