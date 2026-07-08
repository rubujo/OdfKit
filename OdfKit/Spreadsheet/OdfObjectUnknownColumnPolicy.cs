namespace OdfKit.Spreadsheet;

/// <summary>
/// Defines how object binding validation handles spreadsheet columns that are not mapped to object properties.
/// 定義物件繫結驗證遇到未對應至物件屬性的試算表欄位時的處理方式。
/// </summary>
public enum OdfObjectUnknownColumnPolicy
{
    /// <summary>
    /// Ignores unknown spreadsheet columns.
    /// 忽略未知的試算表欄位。
    /// </summary>
    Ignore,

    /// <summary>
    /// Records unknown spreadsheet columns as warnings.
    /// 將未知的試算表欄位記錄為警告。
    /// </summary>
    Warn
}
