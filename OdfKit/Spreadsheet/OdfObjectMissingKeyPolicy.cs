namespace OdfKit.Spreadsheet;

/// <summary>
/// Defines how object update operations handle missing object keys.
/// 定義物件更新操作遇到缺少物件 key 時的處理方式。
/// </summary>
public enum OdfObjectMissingKeyPolicy
{
    /// <summary>
    /// Throws when a key is missing or empty.
    /// key 缺少或為空時擲出例外。
    /// </summary>
    Throw,

    /// <summary>
    /// Records a warning and skips the row.
    /// 記錄警告並略過資料列。
    /// </summary>
    WarnAndSkip
}
