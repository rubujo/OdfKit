using System;
using System.Globalization;
using OdfKit.Styles;

namespace OdfKit.DOM;

/// <summary>
/// ODF 元素複合型別屬性解析與格式化引擎（內部協作者）。
/// </summary>
internal static class OdfElementComplexAttributeAccess
{
    // xsd:dateTime 基底格式（不含時區資訊），依序嘗試不含與含次秒精度（最多 7 位）兩種寫法。
    private static readonly string[] DateTimeBaseFormats =
    [
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
    ];

    /// <summary>
    /// 解析日期時間屬性字串。
    /// </summary>
    /// <param name="value">原始屬性字串</param>
    /// <returns>解析後的日期時間；若格式無效則為 <see langword="null"/></returns>
    internal static DateTime? GetDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string text = value!;

        if (text.EndsWith("Z", StringComparison.Ordinal))
        {
            foreach (string baseFormat in DateTimeBaseFormats)
            {
                if (DateTime.TryParseExact(text, baseFormat + "Z", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsedUtc))
                {
                    return parsedUtc;
                }
            }

            return null;
        }

        if (HasNumericTimeZoneOffset(text))
        {
            foreach (string baseFormat in DateTimeBaseFormats)
            {
                if (DateTime.TryParseExact(text, baseFormat + "zzz", CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal, out DateTime parsedOffset))
                {
                    return parsedOffset;
                }
            }

            return null;
        }

        foreach (string baseFormat in DateTimeBaseFormats)
        {
            if (DateTime.TryParseExact(text, baseFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    // 判斷字串結尾是否為 xsd:dateTime 允許的數值時區偏移（如 "+08:00"／"-05:30"）。
    private static bool HasNumericTimeZoneOffset(string text) =>
        text.Length >= 6 &&
        (text[text.Length - 6] == '+' || text[text.Length - 6] == '-') &&
        text[text.Length - 3] == ':';

    /// <summary>
    /// 將日期時間格式化為 ODF 屬性字串。
    /// </summary>
    /// <param name="value">日期時間值</param>
    /// <returns>ODF 屬性字串</returns>
    internal static string FormatDateTime(DateTime value)
        => value.Kind == DateTimeKind.Utc
            ? value.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
            : value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>
    /// 解析時間屬性字串。
    /// </summary>
    /// <param name="value">原始屬性字串</param>
    /// <returns>解析後的時間；若格式無效則為 <see langword="null"/></returns>
    internal static OdfTime? GetTime(string? value)
        => OdfTime.TryParse(value, out OdfTime parsed) ? parsed : null;

    /// <summary>
    /// 將時間格式化為 ODF 屬性字串。
    /// </summary>
    /// <param name="value">時間值</param>
    /// <returns>ODF 屬性字串</returns>
    internal static string FormatTime(OdfTime value)
        => value.ToString();

    /// <summary>
    /// 解析長度屬性字串。
    /// </summary>
    /// <param name="value">原始屬性字串</param>
    /// <returns>解析後的長度；若格式無效則為 <see langword="null"/></returns>
    internal static OdfLength? GetLength(string? value)
        => OdfLength.TryParse(value, out OdfLength parsed) ? (OdfLength?)parsed : null;

    /// <summary>
    /// 將長度格式化為 ODF 屬性字串。
    /// </summary>
    /// <param name="value">長度值</param>
    /// <returns>ODF 屬性字串</returns>
    internal static string FormatLength(OdfLength value)
        => value.ToString();

    /// <summary>
    /// 解析三段邊框線寬屬性字串。
    /// </summary>
    /// <param name="value">原始屬性字串</param>
    /// <returns>解析後的三段邊框線寬；若格式無效則為 <see langword="null"/></returns>
    internal static OdfBorderWidths? GetBorderWidths(string? value)
        => OdfBorderWidths.TryParse(value, out OdfBorderWidths borderWidths) ? borderWidths : null;

    /// <summary>
    /// 將三段邊框線寬格式化為 ODF 屬性字串。
    /// </summary>
    /// <param name="value">三段邊框線寬值</param>
    /// <returns>ODF 屬性字串</returns>
    internal static string FormatBorderWidths(OdfBorderWidths value)
        => value.Value;

    /// <summary>
    /// 解析 duration 屬性字串。
    /// </summary>
    /// <param name="value">原始屬性字串</param>
    /// <returns>解析後的 duration；若格式無效則為 <see langword="null"/></returns>
    internal static OdfDuration? GetDuration(string? value)
        => OdfDuration.TryParse(value, out OdfDuration parsed) ? parsed : null;

    /// <summary>
    /// 將 duration 格式化為 ODF 屬性字串。
    /// </summary>
    /// <param name="value">duration 值</param>
    /// <returns>ODF 屬性字串</returns>
    internal static string FormatDuration(OdfDuration value)
        => value.Value;

    /// <summary>
    /// 解析角度屬性字串。
    /// </summary>
    /// <param name="value">原始屬性字串</param>
    /// <returns>解析後的角度；若格式無效則為 <see langword="null"/></returns>
    internal static OdfAngle? GetAngle(string? value)
        => OdfAngle.TryParse(value, out OdfAngle parsed) ? parsed : null;

    /// <summary>
    /// 將角度格式化為 ODF 屬性字串。
    /// </summary>
    /// <param name="value">角度值</param>
    /// <returns>ODF 屬性字串</returns>
    internal static string FormatAngle(OdfAngle value)
        => value.Value;

    /// <summary>
    /// 解析樣式名稱屬性字串。
    /// </summary>
    /// <param name="value">原始屬性字串</param>
    /// <returns>解析後的樣式名稱；若格式無效則為 <see langword="null"/></returns>
    internal static OdfStyleName? GetStyleName(string? value)
        => OdfStyleName.TryParse(value, out OdfStyleName styleName) ? styleName : null;

    /// <summary>
    /// 將樣式名稱格式化為 ODF 屬性字串。
    /// </summary>
    /// <param name="value">樣式名稱值</param>
    /// <returns>ODF 屬性字串</returns>
    internal static string FormatStyleName(OdfStyleName value)
        => value.Value;

    /// <summary>
    /// 解析樣式名稱參照清單屬性字串。
    /// </summary>
    /// <param name="value">原始屬性字串</param>
    /// <returns>解析後的樣式名稱參照清單；若格式無效則為 <see langword="null"/></returns>
    internal static OdfStyleNameList? GetStyleNameList(string? value)
        => OdfStyleNameList.TryParse(value, out OdfStyleNameList styleNameList) ? styleNameList : null;

    /// <summary>
    /// 將樣式名稱參照清單格式化為 ODF 屬性字串。
    /// </summary>
    /// <param name="value">樣式名稱參照清單值</param>
    /// <returns>ODF 屬性字串</returns>
    internal static string FormatStyleNameList(OdfStyleNameList value)
        => value.Value;

    /// <summary>
    /// 解析色彩屬性字串。
    /// </summary>
    /// <param name="value">原始屬性字串</param>
    /// <returns>解析後的色彩；若格式無效則為 <see langword="null"/></returns>
    internal static OdfColor? GetColor(string? value)
        => OdfColor.TryParse(value, out OdfColor color) ? color : (OdfColor?)null;

    /// <summary>
    /// 將色彩格式化為 ODF 屬性字串。
    /// </summary>
    /// <param name="value">色彩值</param>
    /// <returns>ODF 屬性字串</returns>
    internal static string FormatColor(OdfColor value)
        => value.Value;
}
