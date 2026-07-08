namespace OdfKit.Spreadsheet;

/// <summary>
/// Defines how null property values are written to spreadsheet cells.
/// 定義 null 屬性值寫入試算表儲存格的方式。
/// </summary>
public enum OdfObjectNullValuePolicy
{
    /// <summary>
    /// Writes null values as empty cells.
    /// 將 null 值寫成空白儲存格。
    /// </summary>
    EmptyCell,

    /// <summary>
    /// Writes null values as empty strings.
    /// 將 null 值寫成空字串。
    /// </summary>
    EmptyString
}
