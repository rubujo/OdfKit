using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.DOM;

public partial class OdfElement
{
    #region Enum Parsers

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

    private static bool TryParseStyleLineBreak(string? value, out OdfStyleLineBreak lineBreak)
    {
        switch (value)
        {
            case "normal":
                lineBreak = OdfStyleLineBreak.Normal;
                return true;
            case "strict":
                lineBreak = OdfStyleLineBreak.Strict;
                return true;
            default:
                lineBreak = default;
                return false;
        }
    }

    private static string FormatStyleLineBreak(OdfStyleLineBreak lineBreak)
    {
        return lineBreak switch
        {
            OdfStyleLineBreak.Normal => "normal",
            OdfStyleLineBreak.Strict => "strict",
            _ => throw new ArgumentOutOfRangeException(nameof(lineBreak), lineBreak, "未知的 ODF 斷行規則。")
        };
    }

    private static bool TryParseStyleRepeat(string? value, out OdfStyleRepeat repeat)
    {
        switch (value)
        {
            case "no-repeat":
                repeat = OdfStyleRepeat.NoRepeat;
                return true;
            case "repeat":
                repeat = OdfStyleRepeat.Repeat;
                return true;
            case "stretch":
                repeat = OdfStyleRepeat.Stretch;
                return true;
            default:
                repeat = default;
                return false;
        }
    }

    private static string FormatStyleRepeat(OdfStyleRepeat repeat)
    {
        return repeat switch
        {
            OdfStyleRepeat.NoRepeat => "no-repeat",
            OdfStyleRepeat.Repeat => "repeat",
            OdfStyleRepeat.Stretch => "stretch",
            _ => throw new ArgumentOutOfRangeException(nameof(repeat), repeat, "未知的 ODF 背景重複。")
        };
    }

    private static bool TryParseXLinkType(string? value, out OdfXLinkType type)
    {
        switch (value)
        {
            case "simple":
                type = OdfXLinkType.Simple;
                return true;
            default:
                type = default;
                return false;
        }
    }

    private static string FormatXLinkType(OdfXLinkType type)
    {
        return type switch
        {
            OdfXLinkType.Simple => "simple",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "未知的 ODF XLink 類型。")
        };
    }

    private static bool TryParseXLinkShow(string? value, out OdfXLinkShow show)
    {
        switch (value)
        {
            case "embed":
                show = OdfXLinkShow.Embed;
                return true;
            case "new":
                show = OdfXLinkShow.New;
                return true;
            case "none":
                show = OdfXLinkShow.None;
                return true;
            case "replace":
                show = OdfXLinkShow.Replace;
                return true;
            default:
                show = default;
                return false;
        }
    }

    private static string FormatXLinkShow(OdfXLinkShow show)
    {
        return show switch
        {
            OdfXLinkShow.Embed => "embed",
            OdfXLinkShow.New => "new",
            OdfXLinkShow.None => "none",
            OdfXLinkShow.Replace => "replace",
            _ => throw new ArgumentOutOfRangeException(nameof(show), show, "未知的 ODF XLink 顯示行為。")
        };
    }

    private static bool TryParseXLinkActuate(string? value, out OdfXLinkActuate actuate)
    {
        switch (value)
        {
            case "onLoad":
                actuate = OdfXLinkActuate.OnLoad;
                return true;
            case "onRequest":
                actuate = OdfXLinkActuate.OnRequest;
                return true;
            default:
                actuate = default;
                return false;
        }
    }

    private static string FormatXLinkActuate(OdfXLinkActuate actuate)
    {
        return actuate switch
        {
            OdfXLinkActuate.OnLoad => "onLoad",
            OdfXLinkActuate.OnRequest => "onRequest",
            _ => throw new ArgumentOutOfRangeException(nameof(actuate), actuate, "未知的 ODF XLink 觸發行為。")
        };
    }

    private static bool TryParseNumberStyle(string? value, out OdfNumberStyle style)
    {
        switch (value)
        {
            case "short":
                style = OdfNumberStyle.Short;
                return true;
            case "long":
                style = OdfNumberStyle.Long;
                return true;
            default:
                style = default;
                return false;
        }
    }

    private static string FormatNumberStyle(OdfNumberStyle style)
    {
        return style switch
        {
            OdfNumberStyle.Short => "short",
            OdfNumberStyle.Long => "long",
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, "未知的 ODF 數字樣式長短。")
        };
    }

    private static bool TryParseTableOrder(string? value, out OdfTableOrder order)
    {
        switch (value)
        {
            case "ascending":
                order = OdfTableOrder.Ascending;
                return true;
            case "descending":
                order = OdfTableOrder.Descending;
                return true;
            default:
                order = default;
                return false;
        }
    }

    private static string FormatTableOrder(OdfTableOrder order)
    {
        return order switch
        {
            OdfTableOrder.Ascending => "ascending",
            OdfTableOrder.Descending => "descending",
            _ => throw new ArgumentOutOfRangeException(nameof(order), order, "未知的 ODF 表格排序方向。")
        };
    }

    private static bool TryParseTableType(string? value, out OdfTableType type)
    {
        switch (value)
        {
            case "column":
                type = OdfTableType.Column;
                return true;
            case "row":
                type = OdfTableType.Row;
                return true;
            case "table":
                type = OdfTableType.Table;
                return true;
            case "column-percentage":
                type = OdfTableType.ColumnPercentage;
                return true;
            case "index":
                type = OdfTableType.Index;
                return true;
            case "member-difference":
                type = OdfTableType.MemberDifference;
                return true;
            case "member-percentage":
                type = OdfTableType.MemberPercentage;
                return true;
            case "member-percentage-difference":
                type = OdfTableType.MemberPercentageDifference;
                return true;
            case "none":
                type = OdfTableType.None;
                return true;
            case "row-percentage":
                type = OdfTableType.RowPercentage;
                return true;
            case "running-total":
                type = OdfTableType.RunningTotal;
                return true;
            case "total-percentage":
                type = OdfTableType.TotalPercentage;
                return true;
            default:
                type = default;
                return false;
        }
    }

    private static string FormatTableType(OdfTableType type)
    {
        return type switch
        {
            OdfTableType.Column => "column",
            OdfTableType.Row => "row",
            OdfTableType.Table => "table",
            OdfTableType.ColumnPercentage => "column-percentage",
            OdfTableType.Index => "index",
            OdfTableType.MemberDifference => "member-difference",
            OdfTableType.MemberPercentage => "member-percentage",
            OdfTableType.MemberPercentageDifference => "member-percentage-difference",
            OdfTableType.None => "none",
            OdfTableType.RowPercentage => "row-percentage",
            OdfTableType.RunningTotal => "running-total",
            OdfTableType.TotalPercentage => "total-percentage",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "未知的 ODF 表格類型。")
        };
    }

    private static bool TryParsePresentationEffect(string? value, out OdfPresentationEffect effect)
    {
        switch (value)
        {
            case "none":
                effect = OdfPresentationEffect.None;
                return true;
            case "fade":
                effect = OdfPresentationEffect.Fade;
                return true;
            case "move":
                effect = OdfPresentationEffect.Move;
                return true;
            case "stripes":
                effect = OdfPresentationEffect.Stripes;
                return true;
            case "open":
                effect = OdfPresentationEffect.Open;
                return true;
            case "close":
                effect = OdfPresentationEffect.Close;
                return true;
            case "dissolve":
                effect = OdfPresentationEffect.Dissolve;
                return true;
            case "wavyline":
                effect = OdfPresentationEffect.Wavyline;
                return true;
            case "random":
                effect = OdfPresentationEffect.Random;
                return true;
            case "lines":
                effect = OdfPresentationEffect.Lines;
                return true;
            case "laser":
                effect = OdfPresentationEffect.Laser;
                return true;
            case "appear":
                effect = OdfPresentationEffect.Appear;
                return true;
            case "hide":
                effect = OdfPresentationEffect.Hide;
                return true;
            case "move-short":
                effect = OdfPresentationEffect.MoveShort;
                return true;
            case "checkerboard":
                effect = OdfPresentationEffect.Checkerboard;
                return true;
            case "rotate":
                effect = OdfPresentationEffect.Rotate;
                return true;
            case "stretch":
                effect = OdfPresentationEffect.Stretch;
                return true;
            default:
                effect = default;
                return false;
        }
    }

    private static string FormatPresentationEffect(OdfPresentationEffect effect)
    {
        return effect switch
        {
            OdfPresentationEffect.None => "none",
            OdfPresentationEffect.Fade => "fade",
            OdfPresentationEffect.Move => "move",
            OdfPresentationEffect.Stripes => "stripes",
            OdfPresentationEffect.Open => "open",
            OdfPresentationEffect.Close => "close",
            OdfPresentationEffect.Dissolve => "dissolve",
            OdfPresentationEffect.Wavyline => "wavyline",
            OdfPresentationEffect.Random => "random",
            OdfPresentationEffect.Lines => "lines",
            OdfPresentationEffect.Laser => "laser",
            OdfPresentationEffect.Appear => "appear",
            OdfPresentationEffect.Hide => "hide",
            OdfPresentationEffect.MoveShort => "move-short",
            OdfPresentationEffect.Checkerboard => "checkerboard",
            OdfPresentationEffect.Rotate => "rotate",
            OdfPresentationEffect.Stretch => "stretch",
            _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, "未知的 ODF 簡報效果。")
        };
    }

    private static bool TryParsePresentationSpeed(string? value, out OdfPresentationSpeed speed)
    {
        switch (value)
        {
            case "slow":
                speed = OdfPresentationSpeed.Slow;
                return true;
            case "medium":
                speed = OdfPresentationSpeed.Medium;
                return true;
            case "fast":
                speed = OdfPresentationSpeed.Fast;
                return true;
            default:
                speed = default;
                return false;
        }
    }

    private static string FormatPresentationSpeed(OdfPresentationSpeed speed)
    {
        return speed switch
        {
            OdfPresentationSpeed.Slow => "slow",
            OdfPresentationSpeed.Medium => "medium",
            OdfPresentationSpeed.Fast => "fast",
            _ => throw new ArgumentOutOfRangeException(nameof(speed), speed, "未知的 ODF 簡報速度。")
        };
    }

    private static bool TryParsePresentationAction(string? value, out OdfPresentationAction action)
    {
        switch (value)
        {
            case "none":
                action = OdfPresentationAction.None;
                return true;
            case "previous-page":
                action = OdfPresentationAction.PreviousPage;
                return true;
            case "next-page":
                action = OdfPresentationAction.NextPage;
                return true;
            case "first-page":
                action = OdfPresentationAction.FirstPage;
                return true;
            case "last-page":
                action = OdfPresentationAction.LastPage;
                return true;
            case "hide":
                action = OdfPresentationAction.Hide;
                return true;
            case "stop":
                action = OdfPresentationAction.Stop;
                return true;
            case "execute":
                action = OdfPresentationAction.Execute;
                return true;
            case "show":
                action = OdfPresentationAction.Show;
                return true;
            case "verb":
                action = OdfPresentationAction.Verb;
                return true;
            case "fade-out":
                action = OdfPresentationAction.FadeOut;
                return true;
            case "sound":
                action = OdfPresentationAction.Sound;
                return true;
            case "last-visited-page":
                action = OdfPresentationAction.LastVisitedPage;
                return true;
            default:
                action = default;
                return false;
        }
    }

    private static string FormatPresentationAction(OdfPresentationAction action)
    {
        return action switch
        {
            OdfPresentationAction.None => "none",
            OdfPresentationAction.PreviousPage => "previous-page",
            OdfPresentationAction.NextPage => "next-page",
            OdfPresentationAction.FirstPage => "first-page",
            OdfPresentationAction.LastPage => "last-page",
            OdfPresentationAction.Hide => "hide",
            OdfPresentationAction.Stop => "stop",
            OdfPresentationAction.Execute => "execute",
            OdfPresentationAction.Show => "show",
            OdfPresentationAction.Verb => "verb",
            OdfPresentationAction.FadeOut => "fade-out",
            OdfPresentationAction.Sound => "sound",
            OdfPresentationAction.LastVisitedPage => "last-visited-page",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "未知的 ODF 簡報動作。")
        };
    }

    private static bool TryParsePresentationTransitionType(string? value, out OdfPresentationTransitionType transitionType)
    {
        switch (value)
        {
            case "manual":
                transitionType = OdfPresentationTransitionType.Manual;
                return true;
            case "automatic":
                transitionType = OdfPresentationTransitionType.Automatic;
                return true;
            case "semi-automatic":
                transitionType = OdfPresentationTransitionType.SemiAutomatic;
                return true;
            default:
                transitionType = default;
                return false;
        }
    }

    private static string FormatPresentationTransitionType(OdfPresentationTransitionType transitionType)
    {
        return transitionType switch
        {
            OdfPresentationTransitionType.Manual => "manual",
            OdfPresentationTransitionType.Automatic => "automatic",
            OdfPresentationTransitionType.SemiAutomatic => "semi-automatic",
            _ => throw new ArgumentOutOfRangeException(nameof(transitionType), transitionType, "未知的 ODF 簡報轉場類型。")
        };
    }

    private static bool TryParsePresentationTransitionStyle(string? value, out OdfPresentationTransitionStyle transitionStyle)
    {
        switch (value)
        {
            case "none":
                transitionStyle = OdfPresentationTransitionStyle.None;
                return true;
            case "fade-from-left":
                transitionStyle = OdfPresentationTransitionStyle.FadeFromLeft;
                return true;
            case "fade-from-top":
                transitionStyle = OdfPresentationTransitionStyle.FadeFromTop;
                return true;
            case "fade-from-right":
                transitionStyle = OdfPresentationTransitionStyle.FadeFromRight;
                return true;
            case "fade-from-bottom":
                transitionStyle = OdfPresentationTransitionStyle.FadeFromBottom;
                return true;
            case "fade-from-upperleft":
                transitionStyle = OdfPresentationTransitionStyle.FadeFromUpperLeft;
                return true;
            case "fade-from-upperright":
                transitionStyle = OdfPresentationTransitionStyle.FadeFromUpperRight;
                return true;
            case "fade-from-lowerleft":
                transitionStyle = OdfPresentationTransitionStyle.FadeFromLowerLeft;
                return true;
            case "fade-from-lowerright":
                transitionStyle = OdfPresentationTransitionStyle.FadeFromLowerRight;
                return true;
            case "move-from-left":
                transitionStyle = OdfPresentationTransitionStyle.MoveFromLeft;
                return true;
            case "move-from-top":
                transitionStyle = OdfPresentationTransitionStyle.MoveFromTop;
                return true;
            case "move-from-right":
                transitionStyle = OdfPresentationTransitionStyle.MoveFromRight;
                return true;
            case "move-from-bottom":
                transitionStyle = OdfPresentationTransitionStyle.MoveFromBottom;
                return true;
            case "move-from-upperleft":
                transitionStyle = OdfPresentationTransitionStyle.MoveFromUpperLeft;
                return true;
            case "move-from-upperright":
                transitionStyle = OdfPresentationTransitionStyle.MoveFromUpperRight;
                return true;
            case "move-from-lowerleft":
                transitionStyle = OdfPresentationTransitionStyle.MoveFromLowerLeft;
                return true;
            case "move-from-lowerright":
                transitionStyle = OdfPresentationTransitionStyle.MoveFromLowerRight;
                return true;
            case "uncover-to-left":
                transitionStyle = OdfPresentationTransitionStyle.UncoverToLeft;
                return true;
            case "uncover-to-top":
                transitionStyle = OdfPresentationTransitionStyle.UncoverToTop;
                return true;
            case "uncover-to-right":
                transitionStyle = OdfPresentationTransitionStyle.UncoverToRight;
                return true;
            case "uncover-to-bottom":
                transitionStyle = OdfPresentationTransitionStyle.UncoverToBottom;
                return true;
            case "uncover-to-upperleft":
                transitionStyle = OdfPresentationTransitionStyle.UncoverToUpperLeft;
                return true;
            case "uncover-to-upperright":
                transitionStyle = OdfPresentationTransitionStyle.UncoverToUpperRight;
                return true;
            case "uncover-to-lowerleft":
                transitionStyle = OdfPresentationTransitionStyle.UncoverToLowerLeft;
                return true;
            case "uncover-to-lowerright":
                transitionStyle = OdfPresentationTransitionStyle.UncoverToLowerRight;
                return true;
            case "fade-to-center":
                transitionStyle = OdfPresentationTransitionStyle.FadeToCenter;
                return true;
            case "fade-from-center":
                transitionStyle = OdfPresentationTransitionStyle.FadeFromCenter;
                return true;
            case "vertical-stripes":
                transitionStyle = OdfPresentationTransitionStyle.VerticalStripes;
                return true;
            case "horizontal-stripes":
                transitionStyle = OdfPresentationTransitionStyle.HorizontalStripes;
                return true;
            case "clockwise":
                transitionStyle = OdfPresentationTransitionStyle.Clockwise;
                return true;
            case "counterclockwise":
                transitionStyle = OdfPresentationTransitionStyle.Counterclockwise;
                return true;
            case "open-vertical":
                transitionStyle = OdfPresentationTransitionStyle.OpenVertical;
                return true;
            case "open-horizontal":
                transitionStyle = OdfPresentationTransitionStyle.OpenHorizontal;
                return true;
            case "close-vertical":
                transitionStyle = OdfPresentationTransitionStyle.CloseVertical;
                return true;
            case "close-horizontal":
                transitionStyle = OdfPresentationTransitionStyle.CloseHorizontal;
                return true;
            case "wavyline-from-left":
                transitionStyle = OdfPresentationTransitionStyle.WavylineFromLeft;
                return true;
            case "wavyline-from-top":
                transitionStyle = OdfPresentationTransitionStyle.WavylineFromTop;
                return true;
            case "wavyline-from-right":
                transitionStyle = OdfPresentationTransitionStyle.WavylineFromRight;
                return true;
            case "wavyline-from-bottom":
                transitionStyle = OdfPresentationTransitionStyle.WavylineFromBottom;
                return true;
            case "spiralin-left":
                transitionStyle = OdfPresentationTransitionStyle.SpiralinLeft;
                return true;
            case "spiralin-right":
                transitionStyle = OdfPresentationTransitionStyle.SpiralinRight;
                return true;
            case "spiralout-left":
                transitionStyle = OdfPresentationTransitionStyle.SpiraloutLeft;
                return true;
            case "spiralout-right":
                transitionStyle = OdfPresentationTransitionStyle.SpiraloutRight;
                return true;
            case "roll-from-top":
                transitionStyle = OdfPresentationTransitionStyle.RollFromTop;
                return true;
            case "roll-from-left":
                transitionStyle = OdfPresentationTransitionStyle.RollFromLeft;
                return true;
            case "roll-from-right":
                transitionStyle = OdfPresentationTransitionStyle.RollFromRight;
                return true;
            case "roll-from-bottom":
                transitionStyle = OdfPresentationTransitionStyle.RollFromBottom;
                return true;
            case "stretch-from-left":
                transitionStyle = OdfPresentationTransitionStyle.StretchFromLeft;
                return true;
            case "stretch-from-top":
                transitionStyle = OdfPresentationTransitionStyle.StretchFromTop;
                return true;
            case "stretch-from-right":
                transitionStyle = OdfPresentationTransitionStyle.StretchFromRight;
                return true;
            case "stretch-from-bottom":
                transitionStyle = OdfPresentationTransitionStyle.StretchFromBottom;
                return true;
            case "vertical-lines":
                transitionStyle = OdfPresentationTransitionStyle.VerticalLines;
                return true;
            case "horizontal-lines":
                transitionStyle = OdfPresentationTransitionStyle.HorizontalLines;
                return true;
            case "dissolve":
                transitionStyle = OdfPresentationTransitionStyle.Dissolve;
                return true;
            case "random":
                transitionStyle = OdfPresentationTransitionStyle.Random;
                return true;
            case "vertical-checkerboard":
                transitionStyle = OdfPresentationTransitionStyle.VerticalCheckerboard;
                return true;
            case "horizontal-checkerboard":
                transitionStyle = OdfPresentationTransitionStyle.HorizontalCheckerboard;
                return true;
            case "interlocking-horizontal-left":
                transitionStyle = OdfPresentationTransitionStyle.InterlockingHorizontalLeft;
                return true;
            case "interlocking-horizontal-right":
                transitionStyle = OdfPresentationTransitionStyle.InterlockingHorizontalRight;
                return true;
            case "interlocking-vertical-top":
                transitionStyle = OdfPresentationTransitionStyle.InterlockingVerticalTop;
                return true;
            case "interlocking-vertical-bottom":
                transitionStyle = OdfPresentationTransitionStyle.InterlockingVerticalBottom;
                return true;
            case "fly-away":
                transitionStyle = OdfPresentationTransitionStyle.FlyAway;
                return true;
            case "open":
                transitionStyle = OdfPresentationTransitionStyle.Open;
                return true;
            case "close":
                transitionStyle = OdfPresentationTransitionStyle.Close;
                return true;
            case "melt":
                transitionStyle = OdfPresentationTransitionStyle.Melt;
                return true;
            default:
                transitionStyle = default;
                return false;
        }
    }

    private static string FormatPresentationTransitionStyle(OdfPresentationTransitionStyle transitionStyle)
    {
        return transitionStyle switch
        {
            OdfPresentationTransitionStyle.None => "none",
            OdfPresentationTransitionStyle.FadeFromLeft => "fade-from-left",
            OdfPresentationTransitionStyle.FadeFromTop => "fade-from-top",
            OdfPresentationTransitionStyle.FadeFromRight => "fade-from-right",
            OdfPresentationTransitionStyle.FadeFromBottom => "fade-from-bottom",
            OdfPresentationTransitionStyle.FadeFromUpperLeft => "fade-from-upperleft",
            OdfPresentationTransitionStyle.FadeFromUpperRight => "fade-from-upperright",
            OdfPresentationTransitionStyle.FadeFromLowerLeft => "fade-from-lowerleft",
            OdfPresentationTransitionStyle.FadeFromLowerRight => "fade-from-lowerright",
            OdfPresentationTransitionStyle.MoveFromLeft => "move-from-left",
            OdfPresentationTransitionStyle.MoveFromTop => "move-from-top",
            OdfPresentationTransitionStyle.MoveFromRight => "move-from-right",
            OdfPresentationTransitionStyle.MoveFromBottom => "move-from-bottom",
            OdfPresentationTransitionStyle.MoveFromUpperLeft => "move-from-upperleft",
            OdfPresentationTransitionStyle.MoveFromUpperRight => "move-from-upperright",
            OdfPresentationTransitionStyle.MoveFromLowerLeft => "move-from-lowerleft",
            OdfPresentationTransitionStyle.MoveFromLowerRight => "move-from-lowerright",
            OdfPresentationTransitionStyle.UncoverToLeft => "uncover-to-left",
            OdfPresentationTransitionStyle.UncoverToTop => "uncover-to-top",
            OdfPresentationTransitionStyle.UncoverToRight => "uncover-to-right",
            OdfPresentationTransitionStyle.UncoverToBottom => "uncover-to-bottom",
            OdfPresentationTransitionStyle.UncoverToUpperLeft => "uncover-to-upperleft",
            OdfPresentationTransitionStyle.UncoverToUpperRight => "uncover-to-upperright",
            OdfPresentationTransitionStyle.UncoverToLowerLeft => "uncover-to-lowerleft",
            OdfPresentationTransitionStyle.UncoverToLowerRight => "uncover-to-lowerright",
            OdfPresentationTransitionStyle.FadeToCenter => "fade-to-center",
            OdfPresentationTransitionStyle.FadeFromCenter => "fade-from-center",
            OdfPresentationTransitionStyle.VerticalStripes => "vertical-stripes",
            OdfPresentationTransitionStyle.HorizontalStripes => "horizontal-stripes",
            OdfPresentationTransitionStyle.Clockwise => "clockwise",
            OdfPresentationTransitionStyle.Counterclockwise => "counterclockwise",
            OdfPresentationTransitionStyle.OpenVertical => "open-vertical",
            OdfPresentationTransitionStyle.OpenHorizontal => "open-horizontal",
            OdfPresentationTransitionStyle.CloseVertical => "close-vertical",
            OdfPresentationTransitionStyle.CloseHorizontal => "close-horizontal",
            OdfPresentationTransitionStyle.WavylineFromLeft => "wavyline-from-left",
            OdfPresentationTransitionStyle.WavylineFromTop => "wavyline-from-top",
            OdfPresentationTransitionStyle.WavylineFromRight => "wavyline-from-right",
            OdfPresentationTransitionStyle.WavylineFromBottom => "wavyline-from-bottom",
            OdfPresentationTransitionStyle.SpiralinLeft => "spiralin-left",
            OdfPresentationTransitionStyle.SpiralinRight => "spiralin-right",
            OdfPresentationTransitionStyle.SpiraloutLeft => "spiralout-left",
            OdfPresentationTransitionStyle.SpiraloutRight => "spiralout-right",
            OdfPresentationTransitionStyle.RollFromTop => "roll-from-top",
            OdfPresentationTransitionStyle.RollFromLeft => "roll-from-left",
            OdfPresentationTransitionStyle.RollFromRight => "roll-from-right",
            OdfPresentationTransitionStyle.RollFromBottom => "roll-from-bottom",
            OdfPresentationTransitionStyle.StretchFromLeft => "stretch-from-left",
            OdfPresentationTransitionStyle.StretchFromTop => "stretch-from-top",
            OdfPresentationTransitionStyle.StretchFromRight => "stretch-from-right",
            OdfPresentationTransitionStyle.StretchFromBottom => "stretch-from-bottom",
            OdfPresentationTransitionStyle.VerticalLines => "vertical-lines",
            OdfPresentationTransitionStyle.HorizontalLines => "horizontal-lines",
            OdfPresentationTransitionStyle.Dissolve => "dissolve",
            OdfPresentationTransitionStyle.Random => "random",
            OdfPresentationTransitionStyle.VerticalCheckerboard => "vertical-checkerboard",
            OdfPresentationTransitionStyle.HorizontalCheckerboard => "horizontal-checkerboard",
            OdfPresentationTransitionStyle.InterlockingHorizontalLeft => "interlocking-horizontal-left",
            OdfPresentationTransitionStyle.InterlockingHorizontalRight => "interlocking-horizontal-right",
            OdfPresentationTransitionStyle.InterlockingVerticalTop => "interlocking-vertical-top",
            OdfPresentationTransitionStyle.InterlockingVerticalBottom => "interlocking-vertical-bottom",
            OdfPresentationTransitionStyle.FlyAway => "fly-away",
            OdfPresentationTransitionStyle.Open => "open",
            OdfPresentationTransitionStyle.Close => "close",
            OdfPresentationTransitionStyle.Melt => "melt",
            _ => throw new ArgumentOutOfRangeException(nameof(transitionStyle), transitionStyle, "未知的 ODF 簡報轉場樣式。")
        };
    }

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

    private static bool TryParseTableBorderModel(string? value, out OdfTableBorderModel borderModel)
    {
        switch (value)
        {
            case "collapsing":
                borderModel = OdfTableBorderModel.Collapsing;
                return true;
            case "separating":
                borderModel = OdfTableBorderModel.Separating;
                return true;
            default:
                borderModel = default;
                return false;
        }
    }

    private static string FormatTableBorderModel(OdfTableBorderModel borderModel)
    {
        return borderModel switch
        {
            OdfTableBorderModel.Collapsing => "collapsing",
            OdfTableBorderModel.Separating => "separating",
            _ => throw new ArgumentOutOfRangeException(nameof(borderModel), borderModel, "未知的 ODF 表格邊框模型。")
        };
    }

    private static bool TryParseTextLabelFollowedBy(string? value, out OdfTextLabelFollowedBy labelFollowedBy)
    {
        switch (value)
        {
            case "listtab":
                labelFollowedBy = OdfTextLabelFollowedBy.ListTab;
                return true;
            case "nothing":
                labelFollowedBy = OdfTextLabelFollowedBy.Nothing;
                return true;
            case "space":
                labelFollowedBy = OdfTextLabelFollowedBy.Space;
                return true;
            default:
                labelFollowedBy = default;
                return false;
        }
    }

    private static string FormatTextLabelFollowedBy(OdfTextLabelFollowedBy labelFollowedBy)
    {
        return labelFollowedBy switch
        {
            OdfTextLabelFollowedBy.ListTab => "listtab",
            OdfTextLabelFollowedBy.Nothing => "nothing",
            OdfTextLabelFollowedBy.Space => "space",
            _ => throw new ArgumentOutOfRangeException(nameof(labelFollowedBy), labelFollowedBy, "未知的 ODF 文字清單標籤後接設定。")
        };
    }

    private static bool TryParseTextListLevelPositionMode(string? value, out OdfTextListLevelPositionMode mode)
    {
        switch (value)
        {
            case "label-alignment":
                mode = OdfTextListLevelPositionMode.LabelAlignment;
                return true;
            case "label-width-and-position":
                mode = OdfTextListLevelPositionMode.LabelWidthAndPosition;
                return true;
            default:
                mode = default;
                return false;
        }
    }

    private static string FormatTextListLevelPositionMode(OdfTextListLevelPositionMode mode)
    {
        return mode switch
        {
            OdfTextListLevelPositionMode.LabelAlignment => "label-alignment",
            OdfTextListLevelPositionMode.LabelWidthAndPosition => "label-width-and-position",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "未知的 ODF 文字清單層級定位模式。")
        };
    }

    private static bool TryParseTextIndexScope(string? value, out OdfTextIndexScope scope)
    {
        switch (value)
        {
            case "chapter":
                scope = OdfTextIndexScope.Chapter;
                return true;
            case "document":
                scope = OdfTextIndexScope.Document;
                return true;
            default:
                scope = default;
                return false;
        }
    }

    private static string FormatTextIndexScope(OdfTextIndexScope scope)
    {
        return scope switch
        {
            OdfTextIndexScope.Chapter => "chapter",
            OdfTextIndexScope.Document => "document",
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "未知的 ODF 文字索引範圍。")
        };
    }

    private static bool TryParseTextTableType(string? value, out OdfTextTableType tableType)
    {
        switch (value)
        {
            case "command":
                tableType = OdfTextTableType.Command;
                return true;
            case "query":
                tableType = OdfTextTableType.Query;
                return true;
            case "table":
                tableType = OdfTextTableType.Table;
                return true;
            default:
                tableType = default;
                return false;
        }
    }

    private static string FormatTextTableType(OdfTextTableType tableType)
    {
        return tableType switch
        {
            OdfTextTableType.Command => "command",
            OdfTextTableType.Query => "query",
            OdfTextTableType.Table => "table",
            _ => throw new ArgumentOutOfRangeException(nameof(tableType), tableType, "未知的 ODF 文字資料表來源類型。")
        };
    }

    private static bool TryParseTextAnchorType(string? value, out OdfTextAnchorType anchorType)
    {
        switch (value)
        {
            case "as-char":
                anchorType = OdfTextAnchorType.AsChar;
                return true;
            case "char":
                anchorType = OdfTextAnchorType.Char;
                return true;
            case "frame":
                anchorType = OdfTextAnchorType.Frame;
                return true;
            case "page":
                anchorType = OdfTextAnchorType.Page;
                return true;
            case "paragraph":
                anchorType = OdfTextAnchorType.Paragraph;
                return true;
            default:
                anchorType = default;
                return false;
        }
    }

    private static string FormatTextAnchorType(OdfTextAnchorType anchorType)
    {
        return anchorType switch
        {
            OdfTextAnchorType.AsChar => "as-char",
            OdfTextAnchorType.Char => "char",
            OdfTextAnchorType.Frame => "frame",
            OdfTextAnchorType.Page => "page",
            OdfTextAnchorType.Paragraph => "paragraph",
            _ => throw new ArgumentOutOfRangeException(nameof(anchorType), anchorType, "未知的 ODF 文字錨定類型。")
        };
    }

    private static bool TryParseTextNoteClass(string? value, out OdfTextNoteClass noteClass)
    {
        switch (value)
        {
            case "endnote":
                noteClass = OdfTextNoteClass.Endnote;
                return true;
            case "footnote":
                noteClass = OdfTextNoteClass.Footnote;
                return true;
            default:
                noteClass = default;
                return false;
        }
    }

    private static string FormatTextNoteClass(OdfTextNoteClass noteClass)
    {
        return noteClass switch
        {
            OdfTextNoteClass.Endnote => "endnote",
            OdfTextNoteClass.Footnote => "footnote",
            _ => throw new ArgumentOutOfRangeException(nameof(noteClass), noteClass, "未知的 ODF 文字註解類別。")
        };
    }

    private static bool TryParseTextSelectPage(string? value, out OdfTextSelectPage selectPage)
    {
        switch (value)
        {
            case "current":
                selectPage = OdfTextSelectPage.Current;
                return true;
            case "next":
                selectPage = OdfTextSelectPage.Next;
                return true;
            case "previous":
                selectPage = OdfTextSelectPage.Previous;
                return true;
            default:
                selectPage = default;
                return false;
        }
    }

    private static string FormatTextSelectPage(OdfTextSelectPage selectPage)
    {
        return selectPage switch
        {
            OdfTextSelectPage.Current => "current",
            OdfTextSelectPage.Next => "next",
            OdfTextSelectPage.Previous => "previous",
            _ => throw new ArgumentOutOfRangeException(nameof(selectPage), selectPage, "未知的 ODF 文字頁面選取。")
        };
    }

    private static bool TryParseTextReferenceFormat(string? value, out OdfTextReferenceFormat format)
    {
        switch (value)
        {
            case "caption":
                format = OdfTextReferenceFormat.Caption;
                return true;
            case "category-and-value":
                format = OdfTextReferenceFormat.CategoryAndValue;
                return true;
            case "chapter":
                format = OdfTextReferenceFormat.Chapter;
                return true;
            case "direction":
                format = OdfTextReferenceFormat.Direction;
                return true;
            case "number":
                format = OdfTextReferenceFormat.Number;
                return true;
            case "number-all-superior":
                format = OdfTextReferenceFormat.NumberAllSuperior;
                return true;
            case "number-no-superior":
                format = OdfTextReferenceFormat.NumberNoSuperior;
                return true;
            case "page":
                format = OdfTextReferenceFormat.Page;
                return true;
            case "text":
                format = OdfTextReferenceFormat.Text;
                return true;
            case "value":
                format = OdfTextReferenceFormat.Value;
                return true;
            default:
                format = default;
                return false;
        }
    }

    private static string FormatTextReferenceFormat(OdfTextReferenceFormat format)
    {
        return format switch
        {
            OdfTextReferenceFormat.Caption => "caption",
            OdfTextReferenceFormat.CategoryAndValue => "category-and-value",
            OdfTextReferenceFormat.Chapter => "chapter",
            OdfTextReferenceFormat.Direction => "direction",
            OdfTextReferenceFormat.Number => "number",
            OdfTextReferenceFormat.NumberAllSuperior => "number-all-superior",
            OdfTextReferenceFormat.NumberNoSuperior => "number-no-superior",
            OdfTextReferenceFormat.Page => "page",
            OdfTextReferenceFormat.Text => "text",
            OdfTextReferenceFormat.Value => "value",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "未知的 ODF 文字參照格式。")
        };
    }

    private static bool TryParseTextStartNumberingAt(string? value, out OdfTextStartNumberingAt startNumberingAt)
    {
        switch (value)
        {
            case "chapter":
                startNumberingAt = OdfTextStartNumberingAt.Chapter;
                return true;
            case "document":
                startNumberingAt = OdfTextStartNumberingAt.Document;
                return true;
            case "page":
                startNumberingAt = OdfTextStartNumberingAt.Page;
                return true;
            default:
                startNumberingAt = default;
                return false;
        }
    }

    private static string FormatTextStartNumberingAt(OdfTextStartNumberingAt startNumberingAt)
    {
        return startNumberingAt switch
        {
            OdfTextStartNumberingAt.Chapter => "chapter",
            OdfTextStartNumberingAt.Document => "document",
            OdfTextStartNumberingAt.Page => "page",
            _ => throw new ArgumentOutOfRangeException(nameof(startNumberingAt), startNumberingAt, "未知的 ODF 文字起始編號範圍。")
        };
    }

    private static bool TryParseTextFootnotesPosition(string? value, out OdfTextFootnotesPosition position)
    {
        switch (value)
        {
            case "document":
                position = OdfTextFootnotesPosition.Document;
                return true;
            case "page":
                position = OdfTextFootnotesPosition.Page;
                return true;
            case "section":
                position = OdfTextFootnotesPosition.Section;
                return true;
            case "text":
                position = OdfTextFootnotesPosition.Text;
                return true;
            default:
                position = default;
                return false;
        }
    }

    private static string FormatTextFootnotesPosition(OdfTextFootnotesPosition position)
    {
        return position switch
        {
            OdfTextFootnotesPosition.Document => "document",
            OdfTextFootnotesPosition.Page => "page",
            OdfTextFootnotesPosition.Section => "section",
            OdfTextFootnotesPosition.Text => "text",
            _ => throw new ArgumentOutOfRangeException(nameof(position), position, "未知的 ODF 文字註腳位置。")
        };
    }

    private static bool TryParseTextCaptionSequenceFormat(string? value, out OdfTextCaptionSequenceFormat format)
    {
        switch (value)
        {
            case "caption":
                format = OdfTextCaptionSequenceFormat.Caption;
                return true;
            case "category-and-value":
                format = OdfTextCaptionSequenceFormat.CategoryAndValue;
                return true;
            case "text":
                format = OdfTextCaptionSequenceFormat.Text;
                return true;
            default:
                format = default;
                return false;
        }
    }

    private static string FormatTextCaptionSequenceFormat(OdfTextCaptionSequenceFormat format)
    {
        return format switch
        {
            OdfTextCaptionSequenceFormat.Caption => "caption",
            OdfTextCaptionSequenceFormat.CategoryAndValue => "category-and-value",
            OdfTextCaptionSequenceFormat.Text => "text",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "未知的 ODF 文字標號序列格式。")
        };
    }

    private static bool TryParseTextNumberPosition(string? value, out OdfTextNumberPosition position)
    {
        switch (value)
        {
            case "inner":
                position = OdfTextNumberPosition.Inner;
                return true;
            case "left":
                position = OdfTextNumberPosition.Left;
                return true;
            case "outer":
                position = OdfTextNumberPosition.Outer;
                return true;
            case "right":
                position = OdfTextNumberPosition.Right;
                return true;
            default:
                position = default;
                return false;
        }
    }

    private static string FormatTextNumberPosition(OdfTextNumberPosition position)
    {
        return position switch
        {
            OdfTextNumberPosition.Inner => "inner",
            OdfTextNumberPosition.Left => "left",
            OdfTextNumberPosition.Outer => "outer",
            OdfTextNumberPosition.Right => "right",
            _ => throw new ArgumentOutOfRangeException(nameof(position), position, "未知的 ODF 文字編號位置。")
        };
    }

    private static bool TryParseTextPlaceholderType(string? value, out OdfTextPlaceholderType placeholderType)
    {
        switch (value)
        {
            case "image":
                placeholderType = OdfTextPlaceholderType.Image;
                return true;
            case "object":
                placeholderType = OdfTextPlaceholderType.Object;
                return true;
            case "table":
                placeholderType = OdfTextPlaceholderType.Table;
                return true;
            case "text":
                placeholderType = OdfTextPlaceholderType.Text;
                return true;
            case "text-box":
                placeholderType = OdfTextPlaceholderType.TextBox;
                return true;
            default:
                placeholderType = default;
                return false;
        }
    }

    private static string FormatTextPlaceholderType(OdfTextPlaceholderType placeholderType)
    {
        return placeholderType switch
        {
            OdfTextPlaceholderType.Image => "image",
            OdfTextPlaceholderType.Object => "object",
            OdfTextPlaceholderType.Table => "table",
            OdfTextPlaceholderType.Text => "text",
            OdfTextPlaceholderType.TextBox => "text-box",
            _ => throw new ArgumentOutOfRangeException(nameof(placeholderType), placeholderType, "未知的 ODF 文字預留位置類型。")
        };
    }

    private static bool TryParseTextAnimation(string? value, out OdfTextAnimation animation)
    {
        switch (value)
        {
            case "alternate":
                animation = OdfTextAnimation.Alternate;
                return true;
            case "none":
                animation = OdfTextAnimation.None;
                return true;
            case "scroll":
                animation = OdfTextAnimation.Scroll;
                return true;
            case "slide":
                animation = OdfTextAnimation.Slide;
                return true;
            default:
                animation = default;
                return false;
        }
    }

    private static string FormatTextAnimation(OdfTextAnimation animation)
    {
        return animation switch
        {
            OdfTextAnimation.Alternate => "alternate",
            OdfTextAnimation.None => "none",
            OdfTextAnimation.Scroll => "scroll",
            OdfTextAnimation.Slide => "slide",
            _ => throw new ArgumentOutOfRangeException(nameof(animation), animation, "未知的 ODF 文字動畫。")
        };
    }

    private static bool TryParseTextAnimationDirection(string? value, out OdfTextAnimationDirection direction)
    {
        switch (value)
        {
            case "down":
                direction = OdfTextAnimationDirection.Down;
                return true;
            case "left":
                direction = OdfTextAnimationDirection.Left;
                return true;
            case "right":
                direction = OdfTextAnimationDirection.Right;
                return true;
            case "up":
                direction = OdfTextAnimationDirection.Up;
                return true;
            default:
                direction = default;
                return false;
        }
    }

    private static string FormatTextAnimationDirection(OdfTextAnimationDirection direction)
    {
        return direction switch
        {
            OdfTextAnimationDirection.Down => "down",
            OdfTextAnimationDirection.Left => "left",
            OdfTextAnimationDirection.Right => "right",
            OdfTextAnimationDirection.Up => "up",
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "未知的 ODF 文字動畫方向。")
        };
    }

    private static bool TryParseTextKind(string? value, out OdfTextKind kind)
    {
        switch (value)
        {
            case "gap":
                kind = OdfTextKind.Gap;
                return true;
            case "unit":
                kind = OdfTextKind.Unit;
                return true;
            case "value":
                kind = OdfTextKind.Value;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static string FormatTextKind(OdfTextKind kind)
    {
        return kind switch
        {
            OdfTextKind.Gap => "gap",
            OdfTextKind.Unit => "unit",
            OdfTextKind.Value => "value",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知的 ODF 文字索引項目種類。")
        };
    }

    private static bool TryParseStyleDirection(string? value, out OdfStyleDirection direction)
    {
        switch (value)
        {
            case "ltr":
                direction = OdfStyleDirection.LeftToRight;
                return true;
            case "ttb":
                direction = OdfStyleDirection.TopToBottom;
                return true;
            default:
                direction = default;
                return false;
        }
    }

    private static string FormatStyleDirection(OdfStyleDirection direction)
    {
        return direction switch
        {
            OdfStyleDirection.LeftToRight => "ltr",
            OdfStyleDirection.TopToBottom => "ttb",
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "未知的 ODF 樣式方向。")
        };
    }

    private static bool TryParseFormOrientation(string? value, out OdfFormOrientation orientation)
    {
        switch (value)
        {
            case "horizontal":
                orientation = OdfFormOrientation.Horizontal;
                return true;
            case "vertical":
                orientation = OdfFormOrientation.Vertical;
                return true;
            default:
                orientation = default;
                return false;
        }
    }

    private static string FormatFormOrientation(OdfFormOrientation orientation)
    {
        return orientation switch
        {
            OdfFormOrientation.Horizontal => "horizontal",
            OdfFormOrientation.Vertical => "vertical",
            _ => throw new ArgumentOutOfRangeException(nameof(orientation), orientation, "未知的 ODF 表單方向。")
        };
    }

    private static bool TryParseTableDirection(string? value, out OdfTableDirection direction)
    {
        switch (value)
        {
            case "from-another-table":
                direction = OdfTableDirection.FromAnotherTable;
                return true;
            case "to-another-table":
                direction = OdfTableDirection.ToAnotherTable;
                return true;
            case "from-same-table":
                direction = OdfTableDirection.FromSameTable;
                return true;
            default:
                direction = default;
                return false;
        }
    }

    private static string FormatTableDirection(OdfTableDirection direction)
    {
        return direction switch
        {
            OdfTableDirection.FromAnotherTable => "from-another-table",
            OdfTableDirection.ToAnotherTable => "to-another-table",
            OdfTableDirection.FromSameTable => "from-same-table",
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "未知的 ODF 表格方向。")
        };
    }

    private static bool TryParseTableOrientation(string? value, out OdfTableOrientation orientation)
    {
        switch (value)
        {
            case "row":
                orientation = OdfTableOrientation.Row;
                return true;
            case "column":
                orientation = OdfTableOrientation.Column;
                return true;
            case "data":
                orientation = OdfTableOrientation.Data;
                return true;
            case "hidden":
                orientation = OdfTableOrientation.Hidden;
                return true;
            case "page":
                orientation = OdfTableOrientation.Page;
                return true;
            default:
                orientation = default;
                return false;
        }
    }

    private static string FormatTableOrientation(OdfTableOrientation orientation)
    {
        return orientation switch
        {
            OdfTableOrientation.Row => "row",
            OdfTableOrientation.Column => "column",
            OdfTableOrientation.Data => "data",
            OdfTableOrientation.Hidden => "hidden",
            OdfTableOrientation.Page => "page",
            _ => throw new ArgumentOutOfRangeException(nameof(orientation), orientation, "未知的 ODF 表格方位。")
        };
    }

    private static bool TryParseStyleFamily(string? value, out OdfStyleFamily family)
    {
        switch (value)
        {
            case "text":
                family = OdfStyleFamily.Text;
                return true;
            case "paragraph":
                family = OdfStyleFamily.Paragraph;
                return true;
            case "section":
                family = OdfStyleFamily.Section;
                return true;
            case "ruby":
                family = OdfStyleFamily.Ruby;
                return true;
            case "table":
                family = OdfStyleFamily.Table;
                return true;
            case "table-column":
                family = OdfStyleFamily.TableColumn;
                return true;
            case "table-row":
                family = OdfStyleFamily.TableRow;
                return true;
            case "table-cell":
                family = OdfStyleFamily.TableCell;
                return true;
            case "graphic":
                family = OdfStyleFamily.Graphic;
                return true;
            case "presentation":
                family = OdfStyleFamily.Presentation;
                return true;
            case "drawing-page":
                family = OdfStyleFamily.DrawingPage;
                return true;
            case "chart":
                family = OdfStyleFamily.Chart;
                return true;
            default:
                family = default;
                return false;
        }
    }

    private static string FormatStyleFamily(OdfStyleFamily family)
    {
        return family switch
        {
            OdfStyleFamily.Text => "text",
            OdfStyleFamily.Paragraph => "paragraph",
            OdfStyleFamily.Section => "section",
            OdfStyleFamily.Ruby => "ruby",
            OdfStyleFamily.Table => "table",
            OdfStyleFamily.TableColumn => "table-column",
            OdfStyleFamily.TableRow => "table-row",
            OdfStyleFamily.TableCell => "table-cell",
            OdfStyleFamily.Graphic => "graphic",
            OdfStyleFamily.Presentation => "presentation",
            OdfStyleFamily.DrawingPage => "drawing-page",
            OdfStyleFamily.Chart => "chart",
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "未知的 ODF 樣式家族。")
        };
    }

    /// <summary>
    /// 複製目前元素，傳回新的類型元素執行個體。
    /// </summary>
    /// <param name="deep">是否進行深層複製 (遞迴複製子節點)</param>
    /// <returns>複製的新元素</returns>
    public override OdfNode CloneNode(bool deep)
    {
        var clone = OdfNodeFactory.CreateElement(LocalName, NamespaceUri, Prefix);
        foreach (var attr in Attributes)
        {
            clone.Attributes[attr.Key] = attr.Value;
        }
        if (deep)
        {
            foreach (var child in Children)
            {
                clone.AppendChild(child.CloneNode(true));
            }
        }
        return clone;
    }

    #endregion
}
