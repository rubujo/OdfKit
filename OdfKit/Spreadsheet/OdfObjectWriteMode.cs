namespace OdfKit.Spreadsheet;

/// <summary>
/// Defines object table write modes.
/// 定義物件資料表寫入模式。
/// </summary>
public enum OdfObjectWriteMode
{
    /// <summary>
    /// Replaces a target range from the start cell.
    /// 從起始儲存格覆寫目標範圍。
    /// </summary>
    Overwrite,

    /// <summary>
    /// Updates existing rows by key without inserting new rows.
    /// 依 key 更新既有資料列且不新增資料列。
    /// </summary>
    UpdateExisting,

    /// <summary>
    /// Updates existing rows and inserts new rows for missing keys.
    /// 更新既有資料列，並針對缺少的 key 新增資料列。
    /// </summary>
    Upsert
}
