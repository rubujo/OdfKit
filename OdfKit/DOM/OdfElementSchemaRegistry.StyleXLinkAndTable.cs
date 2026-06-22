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
    #region Schema Registry - Style, XLink & Table

    internal static bool TryParseStyleLineBreak(string? value, out OdfStyleLineBreak lineBreak)
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

    internal static string FormatStyleLineBreak(OdfStyleLineBreak lineBreak)
    {
        return lineBreak switch
        {
            OdfStyleLineBreak.Normal => "normal",
            OdfStyleLineBreak.Strict => "strict",
            _ => throw new ArgumentOutOfRangeException(nameof(lineBreak), lineBreak, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfLineBreaking"))
        };
    }

    internal static bool TryParseStyleRepeat(string? value, out OdfStyleRepeat repeat)
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

    internal static string FormatStyleRepeat(OdfStyleRepeat repeat)
    {
        return repeat switch
        {
            OdfStyleRepeat.NoRepeat => "no-repeat",
            OdfStyleRepeat.Repeat => "repeat",
            OdfStyleRepeat.Stretch => "stretch",
            _ => throw new ArgumentOutOfRangeException(nameof(repeat), repeat, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfBackgroundDuplicate"))
        };
    }

    internal static bool TryParseXLinkType(string? value, out OdfXLinkType type)
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

    internal static string FormatXLinkType(OdfXLinkType type)
    {
        return type switch
        {
            OdfXLinkType.Simple => "simple",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfXlinkType"))
        };
    }

    internal static bool TryParseXLinkShow(string? value, out OdfXLinkShow show)
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

    internal static string FormatXLinkShow(OdfXLinkShow show)
    {
        return show switch
        {
            OdfXLinkShow.Embed => "embed",
            OdfXLinkShow.New => "new",
            OdfXLinkShow.None => "none",
            OdfXLinkShow.Replace => "replace",
            _ => throw new ArgumentOutOfRangeException(nameof(show), show, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfXlinkDisplay"))
        };
    }

    internal static bool TryParseXLinkActuate(string? value, out OdfXLinkActuate actuate)
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

    internal static string FormatXLinkActuate(OdfXLinkActuate actuate)
    {
        return actuate switch
        {
            OdfXLinkActuate.OnLoad => "onLoad",
            OdfXLinkActuate.OnRequest => "onRequest",
            _ => throw new ArgumentOutOfRangeException(nameof(actuate), actuate, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfXlinkTriggering"))
        };
    }

    internal static bool TryParseNumberStyle(string? value, out OdfNumberStyle style)
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

    internal static string FormatNumberStyle(OdfNumberStyle style)
    {
        return style switch
        {
            OdfNumberStyle.Short => "short",
            OdfNumberStyle.Long => "long",
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfNumberStyle"))
        };
    }

    internal static bool TryParseTableOrder(string? value, out OdfTableOrder order)
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

    internal static string FormatTableOrder(OdfTableOrder order)
    {
        return order switch
        {
            OdfTableOrder.Ascending => "ascending",
            OdfTableOrder.Descending => "descending",
            _ => throw new ArgumentOutOfRangeException(nameof(order), order, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfTableSort"))
        };
    }

    internal static bool TryParseTableType(string? value, out OdfTableType type)
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

    internal static string FormatTableType(OdfTableType type)
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
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfTableType"))
        };
    }


    #endregion
}
