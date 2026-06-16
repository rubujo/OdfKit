using System;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.DOM;

public partial class OdfElement
{
    #region Enum Parsers - Calendar, FO & Draw 3D

    private static bool TryParseNumberCalendar(string? value, out OdfNumberCalendar calendar)
    {
        switch (value)
        {
            case "buddhist":
                calendar = OdfNumberCalendar.Buddhist;
                return true;
            case "gengou":
                calendar = OdfNumberCalendar.Gengou;
                return true;
            case "gregorian":
                calendar = OdfNumberCalendar.Gregorian;
                return true;
            case "hanja":
                calendar = OdfNumberCalendar.Hanja;
                return true;
            case "hanja_yoil":
                calendar = OdfNumberCalendar.HanjaYoil;
                return true;
            case "hijri":
                calendar = OdfNumberCalendar.Hijri;
                return true;
            case "jewish":
                calendar = OdfNumberCalendar.Jewish;
                return true;
            case "ROC":
                calendar = OdfNumberCalendar.Roc;
                return true;
            default:
                calendar = default;
                return false;
        }
    }

    private static string FormatNumberCalendar(OdfNumberCalendar calendar)
    {
        return calendar switch
        {
            OdfNumberCalendar.Buddhist => "buddhist",
            OdfNumberCalendar.Gengou => "gengou",
            OdfNumberCalendar.Gregorian => "gregorian",
            OdfNumberCalendar.Hanja => "hanja",
            OdfNumberCalendar.HanjaYoil => "hanja_yoil",
            OdfNumberCalendar.Hijri => "hijri",
            OdfNumberCalendar.Jewish => "jewish",
            OdfNumberCalendar.Roc => "ROC",
            _ => throw new ArgumentOutOfRangeException(nameof(calendar), calendar, "未知的 ODF 數字曆法。")
        };
    }

    private static bool TryParseEnumToken<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            foreach (TEnum item in Enum.GetValues(typeof(TEnum)))
            {
                if (string.Equals(value, ToOdfToken(item.ToString()), StringComparison.Ordinal))
                {
                    result = item;
                    return true;
                }
            }
        }

        result = default;
        return false;
    }

    private static string FormatEnumToken<TEnum>(TEnum value, string exceptionMessage)
        where TEnum : struct, Enum
    {
        if (Enum.IsDefined(typeof(TEnum), value))
        {
            return ToOdfToken(value.ToString());
        }

        throw new ArgumentOutOfRangeException(nameof(value), value, exceptionMessage);
    }

    private static string ToOdfToken(string name)
    {
        var builder = new System.Text.StringBuilder(name.Length + 4);
        for (int index = 0; index < name.Length; index++)
        {
            char current = name[index];
            if (index > 0 && char.IsUpper(current))
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }

    private static bool TryParseFoKeepTogether(string? value, out OdfFoKeepTogether keepTogether)
    {
        switch (value)
        {
            case "auto":
                keepTogether = OdfFoKeepTogether.Auto;
                return true;
            case "always":
                keepTogether = OdfFoKeepTogether.Always;
                return true;
            default:
                keepTogether = default;
                return false;
        }
    }

    private static string FormatFoKeepTogether(OdfFoKeepTogether keepTogether)
    {
        return keepTogether switch
        {
            OdfFoKeepTogether.Auto => "auto",
            OdfFoKeepTogether.Always => "always",
            _ => throw new ArgumentOutOfRangeException(nameof(keepTogether), keepTogether, "未知的 ODF FO 分頁保持設定。")
        };
    }

    private static bool TryParseFoWrapOption(string? value, out OdfFoWrapOption wrapOption)
    {
        switch (value)
        {
            case "wrap":
                wrapOption = OdfFoWrapOption.Wrap;
                return true;
            case "no-wrap":
                wrapOption = OdfFoWrapOption.NoWrap;
                return true;
            default:
                wrapOption = default;
                return false;
        }
    }

    private static string FormatFoWrapOption(OdfFoWrapOption wrapOption)
    {
        return wrapOption switch
        {
            OdfFoWrapOption.Wrap => "wrap",
            OdfFoWrapOption.NoWrap => "no-wrap",
            _ => throw new ArgumentOutOfRangeException(nameof(wrapOption), wrapOption, "未知的 ODF FO 換行選項。")
        };
    }

    private static bool TryParseDr3dProjection(string? value, out OdfDr3dProjection projection)
    {
        switch (value)
        {
            case "parallel":
                projection = OdfDr3dProjection.Parallel;
                return true;
            case "perspective":
                projection = OdfDr3dProjection.Perspective;
                return true;
            default:
                projection = default;
                return false;
        }
    }

    private static string FormatDr3dProjection(OdfDr3dProjection projection)
    {
        return projection switch
        {
            OdfDr3dProjection.Parallel => "parallel",
            OdfDr3dProjection.Perspective => "perspective",
            _ => throw new ArgumentOutOfRangeException(nameof(projection), projection, "未知的 ODF 3D 投影。")
        };
    }

    private static bool TryParseDr3dShadeMode(string? value, out OdfDr3dShadeMode shadeMode)
    {
        switch (value)
        {
            case "draft":
                shadeMode = OdfDr3dShadeMode.Draft;
                return true;
            case "flat":
                shadeMode = OdfDr3dShadeMode.Flat;
                return true;
            case "gouraud":
                shadeMode = OdfDr3dShadeMode.Gouraud;
                return true;
            case "phong":
                shadeMode = OdfDr3dShadeMode.Phong;
                return true;
            default:
                shadeMode = default;
                return false;
        }
    }

    private static string FormatDr3dShadeMode(OdfDr3dShadeMode shadeMode)
    {
        return shadeMode switch
        {
            OdfDr3dShadeMode.Draft => "draft",
            OdfDr3dShadeMode.Flat => "flat",
            OdfDr3dShadeMode.Gouraud => "gouraud",
            OdfDr3dShadeMode.Phong => "phong",
            _ => throw new ArgumentOutOfRangeException(nameof(shadeMode), shadeMode, "未知的 ODF 3D 著色模式。")
        };
    }

    private static bool TryParseSvgFillRule(string? value, out OdfSvgFillRule fillRule)
    {
        switch (value)
        {
            case "evenodd":
                fillRule = OdfSvgFillRule.EvenOdd;
                return true;
            case "nonzero":
                fillRule = OdfSvgFillRule.Nonzero;
                return true;
            default:
                fillRule = default;
                return false;
        }
    }

    private static string FormatSvgFillRule(OdfSvgFillRule fillRule)
    {
        return fillRule switch
        {
            OdfSvgFillRule.EvenOdd => "evenodd",
            OdfSvgFillRule.Nonzero => "nonzero",
            _ => throw new ArgumentOutOfRangeException(nameof(fillRule), fillRule, "未知的 ODF SVG 填滿規則。")
        };
    }

    #endregion
}
