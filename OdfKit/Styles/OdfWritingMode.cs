namespace OdfKit.Styles;

/// <summary>
/// 表示 ODF 書寫方向。
/// </summary>
public enum OdfWritingMode
{
    /// <summary>
    /// 由左至右、由上至下。
    /// </summary>
    LrTb,

    /// <summary>
    /// 由右至左、由上至下。
    /// </summary>
    RlTb,

    /// <summary>
    /// 由上至下、由右至左。
    /// </summary>
    TbRl,

    /// <summary>
    /// 由上至下、由左至右。
    /// </summary>
    TbLr,

    /// <summary>
    /// 使用頁面書寫方向。
    /// </summary>
    Page
}

internal static class OdfWritingModeExtensions
{
    internal static string ToOdfToken(this OdfWritingMode mode) => mode switch
    {
        OdfWritingMode.RlTb => "rl-tb",
        OdfWritingMode.TbRl => "tb-rl",
        OdfWritingMode.TbLr => "tb-lr",
        OdfWritingMode.Page => "page",
        _ => "lr-tb",
    };

    internal static OdfWritingMode FromOdfToken(string? token) => token switch
    {
        "rl-tb" => OdfWritingMode.RlTb,
        "tb-rl" => OdfWritingMode.TbRl,
        "tb-lr" => OdfWritingMode.TbLr,
        "page" => OdfWritingMode.Page,
        _ => OdfWritingMode.LrTb,
    };
}
