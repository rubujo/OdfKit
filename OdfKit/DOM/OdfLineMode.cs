namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>lineMode</c> 的線條模式 token。
/// </summary>
public enum OdfLineMode
{
    /// <summary>
    /// 連續繪製線條。
    /// </summary>
    Continuous,

    /// <summary>
    /// 略過空白字元繪製線條。
    /// </summary>
    SkipWhiteSpace
}
