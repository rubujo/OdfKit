namespace OdfKit.Spreadsheet;

/// <summary>
/// Defines how object reading handles missing spreadsheet columns.
/// 定義物件讀取遇到缺少試算表欄位時的處理方式。
/// </summary>
public enum OdfObjectMissingColumnPolicy
{
    /// <summary>
    /// Ignores missing columns and leaves the property unchanged.
    /// 忽略缺少的欄位並保留屬性預設值。
    /// </summary>
    Ignore,

    /// <summary>
    /// Records missing columns in the binding report when one is supplied.
    /// 在提供繫結報告時記錄缺少的欄位。
    /// </summary>
    Warn
}
