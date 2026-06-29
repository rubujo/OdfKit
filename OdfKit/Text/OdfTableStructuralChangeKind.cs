namespace OdfKit.Text;

/// <summary>
/// Represents the kind of an ODT table structural change.
/// 表示 ODT 表格結構修訂的種類。
/// </summary>
public enum OdfTableStructuralChangeKind
{
    /// <summary>
    /// A row, column, or table insertion.
    /// 插入列／欄或表格。
    /// </summary>
    Insertion,

    /// <summary>
    /// A row, column, or table deletion.
    /// 刪除列／欄或表格。
    /// </summary>
    Deletion,
}
