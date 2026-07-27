namespace OdfKit.DOM;

/// <summary>
/// Defines values for OdfLineType.
/// 表示 ODF schema 中名為 <c>lineType</c> 的線條類型 token。
/// </summary>
public enum OdfLineType
{
    /// <summary>
    /// 無線條。
    /// </summary>
    None,

    /// <summary>
    /// 單線。
    /// </summary>
#pragma warning disable CA1720 // ODF lexical token or compatibility API name.
    Single,
#pragma warning restore CA1720

    /// <summary>
    /// 雙線。
    /// </summary>
#pragma warning disable CA1720 // ODF lexical token or compatibility API name.
    Double
#pragma warning restore CA1720
}
