namespace OdfKit.Spreadsheet;

/// <summary>
/// Defines how object binding handles duplicate spreadsheet headers.
/// 定義物件繫結遇到重複試算表標題時的處理方式。
/// </summary>
public enum OdfObjectDuplicateHeaderPolicy
{
    /// <summary>
    /// Throws when duplicate headers are found.
    /// 找到重複標題時擲出例外。
    /// </summary>
    Throw,

    /// <summary>
    /// Records duplicate headers as warnings and uses the first matching column.
    /// 將重複標題記錄為警告並使用第一個相符欄位。
    /// </summary>
    WarnAndUseFirst
}
