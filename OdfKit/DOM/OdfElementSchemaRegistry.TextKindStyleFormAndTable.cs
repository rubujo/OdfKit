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
    #region Schema Registry - Text Kind, Style, Form & Table

    internal static bool TryParseTextKind(string? value, out OdfTextKind kind)
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

    internal static string FormatTextKind(OdfTextKind kind)
    {
        return kind switch
        {
            OdfTextKind.Gap => "gap",
            OdfTextKind.Unit => "unit",
            OdfTextKind.Value => "value",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfTextIndex_2"))
        };
    }

    internal static bool TryParseStyleDirection(string? value, out OdfStyleDirection direction)
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

    internal static string FormatStyleDirection(OdfStyleDirection direction)
    {
        return direction switch
        {
            OdfStyleDirection.LeftToRight => "ltr",
            OdfStyleDirection.TopToBottom => "ttb",
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfStyleDirection"))
        };
    }

    internal static bool TryParseFormOrientation(string? value, out OdfFormOrientation orientation)
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

    internal static string FormatFormOrientation(OdfFormOrientation orientation)
    {
        return orientation switch
        {
            OdfFormOrientation.Horizontal => "horizontal",
            OdfFormOrientation.Vertical => "vertical",
            _ => throw new ArgumentOutOfRangeException(nameof(orientation), orientation, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfFormOrientation"))
        };
    }

    internal static bool TryParseTableDirection(string? value, out OdfTableDirection direction)
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

    internal static string FormatTableDirection(OdfTableDirection direction)
    {
        return direction switch
        {
            OdfTableDirection.FromAnotherTable => "from-another-table",
            OdfTableDirection.ToAnotherTable => "to-another-table",
            OdfTableDirection.FromSameTable => "from-same-table",
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfTableOrientation"))
        };
    }

    internal static bool TryParseTableOrientation(string? value, out OdfTableOrientation orientation)
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

    internal static string FormatTableOrientation(OdfTableOrientation orientation)
    {
        return orientation switch
        {
            OdfTableOrientation.Row => "row",
            OdfTableOrientation.Column => "column",
            OdfTableOrientation.Data => "data",
            OdfTableOrientation.Hidden => "hidden",
            OdfTableOrientation.Page => "page",
            _ => throw new ArgumentOutOfRangeException(nameof(orientation), orientation, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfTableOrientation_2"))
        };
    }

    internal static bool TryParseStyleFamily(string? value, out OdfStyleFamily family)
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

    internal static string FormatStyleFamily(OdfStyleFamily family)
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
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfStyleFamily"))
        };
    }

    #endregion
}
