namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中 <c>presentation:transition-type</c> 的轉場類型 token。
/// </summary>
public enum OdfPresentationTransitionType
{
    /// <summary>
    /// 手動轉場。
    /// </summary>
    Manual,

    /// <summary>
    /// 自動轉場。
    /// </summary>
    Automatic,

    /// <summary>
    /// 半自動轉場。
    /// </summary>
    SemiAutomatic
}
