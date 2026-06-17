namespace OdfKit.Text;

/// <summary>
/// 表示 ODT 表格結構修訂的種類。
/// </summary>
public enum OdfTableStructuralChangeKind
{
    /// <summary>插入列／欄或表格。</summary>
    Insertion,

    /// <summary>刪除列／欄或表格。</summary>
    Deletion,
}
