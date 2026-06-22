namespace OdfKit.Spreadsheet;

/// <summary>
/// ODS 試算表追蹤修訂的變更種類。
/// </summary>
public enum OdfSpreadsheetChangeKind
{
    /// <summary>
    /// 儲存格內容變更
    /// </summary>
    CellContentChange,

    /// <summary>
    /// 結構插入（列／欄／工作表等）
    /// </summary>
    Insertion,

    /// <summary>
    /// 結構刪除
    /// </summary>
    Deletion,

    /// <summary>
    /// 結構移動
    /// </summary>
    Movement,
}
