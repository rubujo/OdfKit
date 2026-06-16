namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中 <c>style:direction</c> 的方向 token。
/// </summary>
public enum OdfStyleDirection
{
    /// <summary>
    /// 由左至右方向。
    /// </summary>
    LeftToRight,

    /// <summary>
    /// 由上至下方向。
    /// </summary>
    TopToBottom
}

/// <summary>
/// 表示 ODF schema 中 <c>form:orientation</c> 的表單方向 token。
/// </summary>
public enum OdfFormOrientation
{
    /// <summary>
    /// 水平方向。
    /// </summary>
    Horizontal,

    /// <summary>
    /// 垂直方向。
    /// </summary>
    Vertical
}

/// <summary>
/// 表示 ODF schema 中 <c>table:direction</c> 的表格方向 token。
/// </summary>
public enum OdfTableDirection
{
    /// <summary>
    /// 從另一個表格取得資料。
    /// </summary>
    FromAnotherTable,

    /// <summary>
    /// 將資料送到另一個表格。
    /// </summary>
    ToAnotherTable,

    /// <summary>
    /// 從同一個表格取得資料。
    /// </summary>
    FromSameTable
}

/// <summary>
/// 表示 ODF schema 中 <c>table:orientation</c> 的表格方向 token。
/// </summary>
public enum OdfTableOrientation
{
    /// <summary>
    /// 列方向。
    /// </summary>
    Row,

    /// <summary>
    /// 欄方向。
    /// </summary>
    Column,

    /// <summary>
    /// 資料方向。
    /// </summary>
    Data,

    /// <summary>
    /// 隱藏方向。
    /// </summary>
    Hidden,

    /// <summary>
    /// 頁面方向。
    /// </summary>
    Page
}
