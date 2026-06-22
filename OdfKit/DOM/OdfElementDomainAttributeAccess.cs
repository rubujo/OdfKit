using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.DOM;

/// <summary>
/// ODF 元素領域值型別屬性解析引擎（幾何、語系、儲存格位址等，內部協作者）。
/// </summary>
internal static class OdfElementDomainAttributeAccess
{
    /// <summary>
    /// 以指定的 TryParse 委派解析可空領域值屬性字串。
    /// </summary>
    /// <typeparam name="T">領域值型別</typeparam>
    /// <param name="value">原始屬性字串</param>
    /// <param name="tryParse">TryParse 委派</param>
    /// <returns>解析後的領域值；若格式無效則為 <see langword="null"/></returns>
    internal static T? GetNullable<T>(string? value, OdfElementEnumAttributeAccess.TryParseHandler<T> tryParse)
        where T : struct
        => OdfElementEnumAttributeAccess.GetNullable(value, tryParse);

    /// <summary>
    /// 解析 0 到 100 百分比屬性字串。
    /// </summary>
    /// <param name="value">原始屬性字串</param>
    /// <returns>解析後的百分比；若格式無效則為 <see langword="null"/></returns>
    internal static OdfPercent? GetPercent(string? value)
        => OdfPercent.TryParse(value, out OdfPercent percent) ? percent : null;

    /// <summary>
    /// 解析 -100 到 100 百分比屬性字串。
    /// </summary>
    /// <param name="value">原始屬性字串</param>
    /// <returns>解析後的百分比；若格式無效則為 <see langword="null"/></returns>
    internal static OdfPercent? GetSignedPercent(string? value)
        => OdfPercent.TryParse(value, allowNegative: true, out OdfPercent percent) ? percent : null;

    /// <summary>
    /// 解析 ODF 版本屬性字串。
    /// </summary>
    /// <param name="value">原始屬性字串</param>
    /// <returns>解析後的 ODF 版本；若格式無效則為 <see langword="null"/></returns>
    internal static OdfVersion? GetVersion(string? value)
        => OdfVersionInfo.TryParseVersionString(value, out OdfVersion parsed) ? parsed : null;

    /// <summary>
    /// 將 ODF 版本格式化為屬性字串。
    /// </summary>
    /// <param name="value">ODF 版本值</param>
    /// <returns>ODF 屬性字串</returns>
    internal static string FormatVersion(OdfVersion value)
        => OdfVersionInfo.ToVersionString(value);
}
