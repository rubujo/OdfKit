using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.DOM;

public partial class OdfElement
{
    #region Enum Parsers (D)

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
