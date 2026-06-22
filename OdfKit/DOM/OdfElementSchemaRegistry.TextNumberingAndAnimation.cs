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
    #region Schema Registry - Text Numbering & Animation

    internal static bool TryParseTextStartNumberingAt(string? value, out OdfTextStartNumberingAt startNumberingAt)
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

    internal static string FormatTextStartNumberingAt(OdfTextStartNumberingAt startNumberingAt)
    {
        return startNumberingAt switch
        {
            OdfTextStartNumberingAt.Chapter => "chapter",
            OdfTextStartNumberingAt.Document => "document",
            OdfTextStartNumberingAt.Page => "page",
            _ => throw new ArgumentOutOfRangeException(nameof(startNumberingAt), startNumberingAt, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfLiteralStarting"))
        };
    }

    internal static bool TryParseTextFootnotesPosition(string? value, out OdfTextFootnotesPosition position)
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

    internal static string FormatTextFootnotesPosition(OdfTextFootnotesPosition position)
    {
        return position switch
        {
            OdfTextFootnotesPosition.Document => "document",
            OdfTextFootnotesPosition.Page => "page",
            OdfTextFootnotesPosition.Section => "section",
            OdfTextFootnotesPosition.Text => "text",
            _ => throw new ArgumentOutOfRangeException(nameof(position), position, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfTextFootnote"))
        };
    }

    internal static bool TryParseTextCaptionSequenceFormat(string? value, out OdfTextCaptionSequenceFormat format)
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

    internal static string FormatTextCaptionSequenceFormat(OdfTextCaptionSequenceFormat format)
    {
        return format switch
        {
            OdfTextCaptionSequenceFormat.Caption => "caption",
            OdfTextCaptionSequenceFormat.CategoryAndValue => "category-and-value",
            OdfTextCaptionSequenceFormat.Text => "text",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfLiteralLabel"))
        };
    }

    internal static bool TryParseTextNumberPosition(string? value, out OdfTextNumberPosition position)
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

    internal static string FormatTextNumberPosition(OdfTextNumberPosition position)
    {
        return position switch
        {
            OdfTextNumberPosition.Inner => "inner",
            OdfTextNumberPosition.Left => "left",
            OdfTextNumberPosition.Outer => "outer",
            OdfTextNumberPosition.Right => "right",
            _ => throw new ArgumentOutOfRangeException(nameof(position), position, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfLiteralNumber"))
        };
    }

    internal static bool TryParseTextPlaceholderType(string? value, out OdfTextPlaceholderType placeholderType)
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

    internal static string FormatTextPlaceholderType(OdfTextPlaceholderType placeholderType)
    {
        return placeholderType switch
        {
            OdfTextPlaceholderType.Image => "image",
            OdfTextPlaceholderType.Object => "object",
            OdfTextPlaceholderType.Table => "table",
            OdfTextPlaceholderType.Text => "text",
            OdfTextPlaceholderType.TextBox => "text-box",
            _ => throw new ArgumentOutOfRangeException(nameof(placeholderType), placeholderType, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfLiteralPlaceholder"))
        };
    }

    internal static bool TryParseTextAnimation(string? value, out OdfTextAnimation animation)
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

    internal static string FormatTextAnimation(OdfTextAnimation animation)
    {
        return animation switch
        {
            OdfTextAnimation.Alternate => "alternate",
            OdfTextAnimation.None => "none",
            OdfTextAnimation.Scroll => "scroll",
            OdfTextAnimation.Slide => "slide",
            _ => throw new ArgumentOutOfRangeException(nameof(animation), animation, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfTextAnimation"))
        };
    }

    internal static bool TryParseTextAnimationDirection(string? value, out OdfTextAnimationDirection direction)
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

    internal static string FormatTextAnimationDirection(OdfTextAnimationDirection direction)
    {
        return direction switch
        {
            OdfTextAnimationDirection.Down => "down",
            OdfTextAnimationDirection.Left => "left",
            OdfTextAnimationDirection.Right => "right",
            OdfTextAnimationDirection.Up => "up",
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfTextAnimation_2"))
        };
    }

    #endregion
}
