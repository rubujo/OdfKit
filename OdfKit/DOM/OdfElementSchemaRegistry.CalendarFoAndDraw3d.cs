using System.Text;
using System;
using OdfKit.DOM;
using OdfKit.Styles;

using OdfKit.Compliance;
namespace OdfKit.DOM;

/// <summary>
/// ODF 元素 schema 枚舉 token 靜態註冊表（部分檔案）。
/// </summary>
internal static partial class OdfElementSchemaRegistry
{
    #region Schema Registry - Calendar, FO & Draw 3D

    internal static bool TryParseNumberCalendar(string? value, out OdfNumberCalendar calendar)
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

    internal static string FormatNumberCalendar(OdfNumberCalendar calendar)
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
            _ => throw new ArgumentOutOfRangeException(nameof(calendar), calendar, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfNumericCalendar"))
        };
    }

    internal static bool TryParseEnumToken<TEnum>(string? value, out TEnum result)
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

    internal static string FormatEnumToken<TEnum>(TEnum value, string exceptionMessage)
        where TEnum : struct, Enum
    {
        if (Enum.IsDefined(typeof(TEnum), value))
        {
            return ToOdfToken(value.ToString());
        }

        throw new ArgumentOutOfRangeException(nameof(value), value, exceptionMessage);
    }

    internal static string ToOdfToken(string name)
    {
        var builder = new StringBuilder(name.Length + 4);
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

    internal static bool TryParseFoKeepTogether(string? value, out OdfFoKeepTogether keepTogether)
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

    internal static string FormatFoKeepTogether(OdfFoKeepTogether keepTogether)
    {
        return keepTogether switch
        {
            OdfFoKeepTogether.Auto => "auto",
            OdfFoKeepTogether.Always => "always",
            _ => throw new ArgumentOutOfRangeException(nameof(keepTogether), keepTogether, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfFoPaging"))
        };
    }

    internal static bool TryParseFoWrapOption(string? value, out OdfFoWrapOption wrapOption)
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

    internal static string FormatFoWrapOption(OdfFoWrapOption wrapOption)
    {
        return wrapOption switch
        {
            OdfFoWrapOption.Wrap => "wrap",
            OdfFoWrapOption.NoWrap => "no-wrap",
            _ => throw new ArgumentOutOfRangeException(nameof(wrapOption), wrapOption, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfFoNewline"))
        };
    }

    internal static bool TryParseDr3dProjection(string? value, out OdfDr3dProjection projection)
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

    internal static string FormatDr3dProjection(OdfDr3dProjection projection)
    {
        return projection switch
        {
            OdfDr3dProjection.Parallel => "parallel",
            OdfDr3dProjection.Perspective => "perspective",
            _ => throw new ArgumentOutOfRangeException(nameof(projection), projection, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdf3dProjection"))
        };
    }

    internal static bool TryParseDr3dShadeMode(string? value, out OdfDr3dShadeMode shadeMode)
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

    internal static string FormatDr3dShadeMode(OdfDr3dShadeMode shadeMode)
    {
        return shadeMode switch
        {
            OdfDr3dShadeMode.Draft => "draft",
            OdfDr3dShadeMode.Flat => "flat",
            OdfDr3dShadeMode.Gouraud => "gouraud",
            OdfDr3dShadeMode.Phong => "phong",
            _ => throw new ArgumentOutOfRangeException(nameof(shadeMode), shadeMode, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdf3dShading"))
        };
    }

    internal static bool TryParseSvgFillRule(string? value, out OdfSvgFillRule fillRule)
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

    internal static string FormatSvgFillRule(OdfSvgFillRule fillRule)
    {
        return fillRule switch
        {
            OdfSvgFillRule.EvenOdd => "evenodd",
            OdfSvgFillRule.Nonzero => "nonzero",
            _ => throw new ArgumentOutOfRangeException(nameof(fillRule), fillRule, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfSvgPadding"))
        };
    }

    #endregion
}
