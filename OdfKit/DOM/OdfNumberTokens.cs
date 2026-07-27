namespace OdfKit.DOM;

/// <summary>
/// Defines values for OdfNumberStyle.
/// 表示 ODF schema 中 <c>number:style</c> 的數字樣式長短 token。
/// </summary>
public enum OdfNumberStyle
{
    /// <summary>
    /// 短格式。
    /// </summary>
#pragma warning disable CA1720 // ODF lexical token or compatibility API name.
    Short,
#pragma warning restore CA1720

    /// <summary>
    /// 長格式。
    /// </summary>
#pragma warning disable CA1720 // ODF lexical token or compatibility API name.
    Long
#pragma warning restore CA1720
}
