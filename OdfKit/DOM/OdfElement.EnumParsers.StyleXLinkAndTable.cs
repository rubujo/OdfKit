using System;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.DOM;

public partial class OdfElement
{
    #region Enum Parsers - Style, XLink & Table

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


    #endregion
}
