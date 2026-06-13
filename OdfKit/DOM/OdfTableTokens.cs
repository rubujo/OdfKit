namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中 <c>table:order</c> 的排序方向 token。
/// </summary>
public enum OdfTableOrder
{
    /// <summary>
    /// 遞增排序。
    /// </summary>
    Ascending,

    /// <summary>
    /// 遞減排序。
    /// </summary>
    Descending
}

/// <summary>
/// 表示 ODF schema 中封閉 <c>table:type</c> 屬性的表格類型 token。
/// </summary>
public enum OdfTableType
{
    /// <summary>
    /// 欄。
    /// </summary>
    Column,

    /// <summary>
    /// 列。
    /// </summary>
    Row,

    /// <summary>
    /// 表格。
    /// </summary>
    Table,

    /// <summary>
    /// 欄百分比。
    /// </summary>
    ColumnPercentage,

    /// <summary>
    /// 索引。
    /// </summary>
    Index,

    /// <summary>
    /// 成員差異。
    /// </summary>
    MemberDifference,

    /// <summary>
    /// 成員百分比。
    /// </summary>
    MemberPercentage,

    /// <summary>
    /// 成員百分比差異。
    /// </summary>
    MemberPercentageDifference,

    /// <summary>
    /// 無類型。
    /// </summary>
    None,

    /// <summary>
    /// 列百分比。
    /// </summary>
    RowPercentage,

    /// <summary>
    /// 累計總和。
    /// </summary>
    RunningTotal,

    /// <summary>
    /// 總計百分比。
    /// </summary>
    TotalPercentage
}
