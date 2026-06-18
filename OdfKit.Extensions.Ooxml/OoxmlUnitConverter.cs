using System;
using System.Globalization;

namespace OdfKit.Conversion;

/// <summary>
/// ODF 長度與 OOXML twip 單位轉換輔助。
/// </summary>
internal static class OoxmlUnitConverter
{
    private const double TwipsPerInch = 1440d;
    private const double TwipsPerPoint = 20d;
    private const double CmPerInch = 2.54d;

    /// <summary>
    /// 將 ODF 長度字串轉為 OOXML twip 整數。
    /// </summary>
    /// <param name="value">ODF 長度（例如 <c>1.5cm</c>、<c>12pt</c>）。</param>
    /// <returns>換算後的 twip；無法解析時為 <see langword="null"/>。</returns>
    internal static int? TryParseOdfLengthToTwips(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string text = value!.Trim();
        if (text.Length < 3)
        {
            return null;
        }

        string suffix = text.Substring(text.Length - 2);
        string numberText = text.Substring(0, text.Length - 2);
        if (!double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out double amount))
        {
            return null;
        }

        double twips = suffix.ToLowerInvariant() switch
        {
            "pt" => amount * TwipsPerPoint,
            "in" => amount * TwipsPerInch,
            "cm" => amount * TwipsPerInch / CmPerInch,
            "mm" => amount * TwipsPerInch / (CmPerInch * 10d),
            _ => double.NaN,
        };

        if (double.IsNaN(twips) || twips < 0)
        {
            return null;
        }

        return (int)Math.Round(twips, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 將 OOXML twip 整數轉為 ODF 公分長度字串。
    /// </summary>
    /// <param name="twips">twip 數值。</param>
    /// <returns>ODF 長度字串；無法換算時為 <see langword="null"/>。</returns>
    internal static string? TryFormatTwipsAsOdfCentimeters(int? twips)
    {
        if (twips is null or < 0)
        {
            return null;
        }

        double centimeters = twips.Value / TwipsPerInch * CmPerInch;
        return centimeters.ToString("0.###", CultureInfo.InvariantCulture) + "cm";
    }

    /// <summary>
    /// 將 ODF 行高（點或百分比）轉為 OOXML 行距設定。
    /// </summary>
    /// <param name="value">ODF <c>fo:line-height</c> 值。</param>
    /// <returns>twip 與是否為自動行距規則。</returns>
    internal static (int? Twips, bool IsAutoRule) TryParseOdfLineHeight(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, false);
        }

        string text = value!.Trim();
        if (text.EndsWith("%", StringComparison.Ordinal))
        {
            string percentText = text.Substring(0, text.Length - 1);
            if (double.TryParse(percentText, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
            {
                int scaled = (int)Math.Round(percent * 2.4d, MidpointRounding.AwayFromZero);
                return (scaled, true);
            }

            return (null, false);
        }

        return (TryParseOdfLengthToTwips(text), false);
    }

    /// <summary>
    /// 將 OOXML 行距 twip 轉為 ODF 行高點數字串。
    /// </summary>
    /// <param name="twips">行距 twip。</param>
    /// <param name="isAutoRule">是否為自動行距規則。</param>
    /// <returns>ODF 行高；無法換算時為 <see langword="null"/>。</returns>
    internal static string? TryFormatLineHeightFromTwips(int? twips, bool isAutoRule)
    {
        if (twips is null or < 0)
        {
            return null;
        }

        if (isAutoRule)
        {
            double percent = twips.Value / 2.4d;
            return percent.ToString("0.##", CultureInfo.InvariantCulture) + "%";
        }

        double points = twips.Value / TwipsPerPoint;
        return points.ToString("0.##", CultureInfo.InvariantCulture) + "pt";
    }
}
