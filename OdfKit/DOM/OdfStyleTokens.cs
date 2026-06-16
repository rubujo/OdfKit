namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中 <c>style:line-break</c> 的斷行 token。
/// </summary>
public enum OdfStyleLineBreak
{
    /// <summary>
    /// 一般斷行規則。
    /// </summary>
    Normal,

    /// <summary>
    /// 嚴格斷行規則。
    /// </summary>
    Strict
}

/// <summary>
/// 表示 ODF schema 中 <c>style:repeat</c> 的背景重複 token。
/// </summary>
public enum OdfStyleRepeat
{
    /// <summary>
    /// 不重複背景影像。
    /// </summary>
    NoRepeat,

    /// <summary>
    /// 重複背景影像。
    /// </summary>
    Repeat,

    /// <summary>
    /// 延展背景影像。
    /// </summary>
    Stretch
}
