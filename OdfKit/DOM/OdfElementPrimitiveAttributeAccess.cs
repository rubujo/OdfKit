using System;
using System.Globalization;

namespace OdfKit.DOM;

/// <summary>
/// ODF 元素基本型別屬性解析與格式化引擎（內部協作者）。
/// </summary>
internal static class OdfElementPrimitiveAttributeAccess
{
    /// <summary>
    /// 解析 32 位元整數屬性字串。
    /// </summary>
    /// <param name="value">原始屬性字串</param>
    /// <param name="defaultValue">解析失敗時的預設值</param>
    /// <returns>解析後的整數值</returns>
    internal static int GetInt32(string? value, int defaultValue = 0)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : defaultValue;

    /// <summary>
    /// 解析可空 32 位元整數屬性字串。
    /// </summary>
    /// <param name="value">原始屬性字串</param>
    /// <returns>解析後的整數值；若格式無效則為 <see langword="null"/></returns>
    internal static int? GetNullableInt32(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : null;

    /// <summary>
    /// 將 32 位元整數格式化為 ODF 屬性字串。
    /// </summary>
    /// <param name="value">整數值</param>
    /// <returns>ODF 屬性字串</returns>
    internal static string FormatInt32(int value)
        => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// 解析布林屬性字串。
    /// </summary>
    /// <param name="value">原始屬性字串</param>
    /// <returns>解析後的布林值；若格式無效則為 <see langword="null"/></returns>
    internal static bool? GetBoolean(string? value)
    {
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "1", StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "0", StringComparison.Ordinal))
        {
            return false;
        }

        return null;
    }

    /// <summary>
    /// 將布林值格式化為 ODF 屬性字串。
    /// </summary>
    /// <param name="value">布林值</param>
    /// <returns>ODF 屬性字串</returns>
    internal static string FormatBoolean(bool value)
        => value ? "true" : "false";

    /// <summary>
    /// 解析十進位數值屬性字串。
    /// </summary>
    /// <param name="value">原始屬性字串</param>
    /// <returns>解析後的十進位數值；若格式無效則為 <see langword="null"/></returns>
    internal static decimal? GetDecimal(string? value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
            ? parsed
            : null;

    /// <summary>
    /// 將十進位數值格式化為 ODF 屬性字串。
    /// </summary>
    /// <param name="value">十進位數值</param>
    /// <returns>ODF 屬性字串</returns>
    internal static string FormatDecimal(decimal value)
        => value.ToString(CultureInfo.InvariantCulture);
}
