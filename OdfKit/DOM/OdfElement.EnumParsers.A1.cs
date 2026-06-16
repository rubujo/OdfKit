using System;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.DOM;

public partial class OdfElement
{
    #region Enum Parsers (A1)

    private static bool TryParseLineStyle(string? value, out OdfLineStyle lineStyle)
    {
        switch (value)
        {
            case "none":
                lineStyle = OdfLineStyle.None;
                return true;
            case "solid":
                lineStyle = OdfLineStyle.Solid;
                return true;
            case "dotted":
                lineStyle = OdfLineStyle.Dotted;
                return true;
            case "dash":
                lineStyle = OdfLineStyle.Dash;
                return true;
            case "long-dash":
                lineStyle = OdfLineStyle.LongDash;
                return true;
            case "dot-dash":
                lineStyle = OdfLineStyle.DotDash;
                return true;
            case "dot-dot-dash":
                lineStyle = OdfLineStyle.DotDotDash;
                return true;
            case "wave":
                lineStyle = OdfLineStyle.Wave;
                return true;
            default:
                lineStyle = default;
                return false;
        }
    }

    private static string FormatLineStyle(OdfLineStyle lineStyle)
    {
        return lineStyle switch
        {
            OdfLineStyle.None => "none",
            OdfLineStyle.Solid => "solid",
            OdfLineStyle.Dotted => "dotted",
            OdfLineStyle.Dash => "dash",
            OdfLineStyle.LongDash => "long-dash",
            OdfLineStyle.DotDash => "dot-dash",
            OdfLineStyle.DotDotDash => "dot-dot-dash",
            OdfLineStyle.Wave => "wave",
            _ => throw new ArgumentOutOfRangeException(nameof(lineStyle), lineStyle, "未知的 ODF 線條樣式。")
        };
    }

    private static bool TryParseLineType(string? value, out OdfLineType lineType)
    {
        switch (value)
        {
            case "none":
                lineType = OdfLineType.None;
                return true;
            case "single":
                lineType = OdfLineType.Single;
                return true;
            case "double":
                lineType = OdfLineType.Double;
                return true;
            default:
                lineType = default;
                return false;
        }
    }

    private static string FormatLineType(OdfLineType lineType)
    {
        return lineType switch
        {
            OdfLineType.None => "none",
            OdfLineType.Single => "single",
            OdfLineType.Double => "double",
            _ => throw new ArgumentOutOfRangeException(nameof(lineType), lineType, "未知的 ODF 線條類型。")
        };
    }

    private static bool TryParseLineMode(string? value, out OdfLineMode lineMode)
    {
        switch (value)
        {
            case "continuous":
                lineMode = OdfLineMode.Continuous;
                return true;
            case "skip-white-space":
                lineMode = OdfLineMode.SkipWhiteSpace;
                return true;
            default:
                lineMode = default;
                return false;
        }
    }

    private static string FormatLineMode(OdfLineMode lineMode)
    {
        return lineMode switch
        {
            OdfLineMode.Continuous => "continuous",
            OdfLineMode.SkipWhiteSpace => "skip-white-space",
            _ => throw new ArgumentOutOfRangeException(nameof(lineMode), lineMode, "未知的 ODF 線條模式。")
        };
    }

    private static bool TryParseFontStyle(string? value, out OdfFontStyle fontStyle)
    {
        switch (value)
        {
            case "normal":
                fontStyle = OdfFontStyle.Normal;
                return true;
            case "italic":
                fontStyle = OdfFontStyle.Italic;
                return true;
            case "oblique":
                fontStyle = OdfFontStyle.Oblique;
                return true;
            default:
                fontStyle = default;
                return false;
        }
    }

    private static string FormatFontStyle(OdfFontStyle fontStyle)
    {
        return fontStyle switch
        {
            OdfFontStyle.Normal => "normal",
            OdfFontStyle.Italic => "italic",
            OdfFontStyle.Oblique => "oblique",
            _ => throw new ArgumentOutOfRangeException(nameof(fontStyle), fontStyle, "未知的 ODF 字型樣式。")
        };
    }

    private static bool TryParseFontVariant(string? value, out OdfFontVariant fontVariant)
    {
        switch (value)
        {
            case "normal":
                fontVariant = OdfFontVariant.Normal;
                return true;
            case "small-caps":
                fontVariant = OdfFontVariant.SmallCaps;
                return true;
            default:
                fontVariant = default;
                return false;
        }
    }

    private static string FormatFontVariant(OdfFontVariant fontVariant)
    {
        return fontVariant switch
        {
            OdfFontVariant.Normal => "normal",
            OdfFontVariant.SmallCaps => "small-caps",
            _ => throw new ArgumentOutOfRangeException(nameof(fontVariant), fontVariant, "未知的 ODF 字型變體。")
        };
    }

    private static bool TryParseFontWeight(string? value, out OdfFontWeight fontWeight)
    {
        switch (value)
        {
            case "normal":
                fontWeight = OdfFontWeight.Normal;
                return true;
            case "bold":
                fontWeight = OdfFontWeight.Bold;
                return true;
            case "100":
                fontWeight = OdfFontWeight.Weight100;
                return true;
            case "200":
                fontWeight = OdfFontWeight.Weight200;
                return true;
            case "300":
                fontWeight = OdfFontWeight.Weight300;
                return true;
            case "400":
                fontWeight = OdfFontWeight.Weight400;
                return true;
            case "500":
                fontWeight = OdfFontWeight.Weight500;
                return true;
            case "600":
                fontWeight = OdfFontWeight.Weight600;
                return true;
            case "700":
                fontWeight = OdfFontWeight.Weight700;
                return true;
            case "800":
                fontWeight = OdfFontWeight.Weight800;
                return true;
            case "900":
                fontWeight = OdfFontWeight.Weight900;
                return true;
            default:
                fontWeight = default;
                return false;
        }
    }

    private static string FormatFontWeight(OdfFontWeight fontWeight)
    {
        return fontWeight switch
        {
            OdfFontWeight.Normal => "normal",
            OdfFontWeight.Bold => "bold",
            OdfFontWeight.Weight100 => "100",
            OdfFontWeight.Weight200 => "200",
            OdfFontWeight.Weight300 => "300",
            OdfFontWeight.Weight400 => "400",
            OdfFontWeight.Weight500 => "500",
            OdfFontWeight.Weight600 => "600",
            OdfFontWeight.Weight700 => "700",
            OdfFontWeight.Weight800 => "800",
            OdfFontWeight.Weight900 => "900",
            _ => throw new ArgumentOutOfRangeException(nameof(fontWeight), fontWeight, "未知的 ODF 字型粗細。")
        };
    }

    private static bool TryParseFontFamilyGeneric(string? value, out OdfFontFamilyGeneric family)
    {
        switch (value)
        {
            case "roman":
                family = OdfFontFamilyGeneric.Roman;
                return true;
            case "swiss":
                family = OdfFontFamilyGeneric.Swiss;
                return true;
            case "modern":
                family = OdfFontFamilyGeneric.Modern;
                return true;
            case "decorative":
                family = OdfFontFamilyGeneric.Decorative;
                return true;
            case "script":
                family = OdfFontFamilyGeneric.Script;
                return true;
            case "system":
                family = OdfFontFamilyGeneric.System;
                return true;
            default:
                family = default;
                return false;
        }
    }

    private static string FormatFontFamilyGeneric(OdfFontFamilyGeneric family)
    {
        return family switch
        {
            OdfFontFamilyGeneric.Roman => "roman",
            OdfFontFamilyGeneric.Swiss => "swiss",
            OdfFontFamilyGeneric.Modern => "modern",
            OdfFontFamilyGeneric.Decorative => "decorative",
            OdfFontFamilyGeneric.Script => "script",
            OdfFontFamilyGeneric.System => "system",
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "未知的 ODF 通用字型家族。")
        };
    }

    private static bool TryParseFontPitch(string? value, out OdfFontPitch pitch)
    {
        switch (value)
        {
            case "fixed":
                pitch = OdfFontPitch.Fixed;
                return true;
            case "variable":
                pitch = OdfFontPitch.Variable;
                return true;
            default:
                pitch = default;
                return false;
        }
    }

    private static string FormatFontPitch(OdfFontPitch pitch)
    {
        return pitch switch
        {
            OdfFontPitch.Fixed => "fixed",
            OdfFontPitch.Variable => "variable",
            _ => throw new ArgumentOutOfRangeException(nameof(pitch), pitch, "未知的 ODF 字型間距。")
        };
    }

    private static bool TryParseFontRelief(string? value, out OdfFontRelief relief)
    {
        switch (value)
        {
            case "none":
                relief = OdfFontRelief.None;
                return true;
            case "embossed":
                relief = OdfFontRelief.Embossed;
                return true;
            case "engraved":
                relief = OdfFontRelief.Engraved;
                return true;
            default:
                relief = default;
                return false;
        }
    }

    private static string FormatFontRelief(OdfFontRelief relief)
    {
        return relief switch
        {
            OdfFontRelief.None => "none",
            OdfFontRelief.Embossed => "embossed",
            OdfFontRelief.Engraved => "engraved",
            _ => throw new ArgumentOutOfRangeException(nameof(relief), relief, "未知的 ODF 字型浮雕。")
        };
    }

    private static bool TryParseFontStretch(string? value, out OdfFontStretch stretch)
    {
        switch (value)
        {
            case "normal":
                stretch = OdfFontStretch.Normal;
                return true;
            case "ultra-condensed":
                stretch = OdfFontStretch.UltraCondensed;
                return true;
            case "extra-condensed":
                stretch = OdfFontStretch.ExtraCondensed;
                return true;
            case "condensed":
                stretch = OdfFontStretch.Condensed;
                return true;
            case "semi-condensed":
                stretch = OdfFontStretch.SemiCondensed;
                return true;
            case "semi-expanded":
                stretch = OdfFontStretch.SemiExpanded;
                return true;
            case "expanded":
                stretch = OdfFontStretch.Expanded;
                return true;
            case "extra-expanded":
                stretch = OdfFontStretch.ExtraExpanded;
                return true;
            case "ultra-expanded":
                stretch = OdfFontStretch.UltraExpanded;
                return true;
            default:
                stretch = default;
                return false;
        }
    }

    private static string FormatFontStretch(OdfFontStretch stretch)
    {
        return stretch switch
        {
            OdfFontStretch.Normal => "normal",
            OdfFontStretch.UltraCondensed => "ultra-condensed",
            OdfFontStretch.ExtraCondensed => "extra-condensed",
            OdfFontStretch.Condensed => "condensed",
            OdfFontStretch.SemiCondensed => "semi-condensed",
            OdfFontStretch.SemiExpanded => "semi-expanded",
            OdfFontStretch.Expanded => "expanded",
            OdfFontStretch.ExtraExpanded => "extra-expanded",
            OdfFontStretch.UltraExpanded => "ultra-expanded",
            _ => throw new ArgumentOutOfRangeException(nameof(stretch), stretch, "未知的 ODF 字型伸縮。")
        };
    }

    #endregion
}
