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
    #region Schema Registry - Table Border & Text

    internal static bool TryParseTableBorderModel(string? value, out OdfTableBorderModel borderModel)
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

    internal static string FormatTableBorderModel(OdfTableBorderModel borderModel)
    {
        return borderModel switch
        {
            OdfTableBorderModel.Collapsing => "collapsing",
            OdfTableBorderModel.Separating => "separating",
            _ => throw new ArgumentOutOfRangeException(nameof(borderModel), borderModel, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfTableBorder"))
        };
    }

    internal static bool TryParseTextLabelFollowedBy(string? value, out OdfTextLabelFollowedBy labelFollowedBy)
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

    internal static string FormatTextLabelFollowedBy(OdfTextLabelFollowedBy labelFollowedBy)
    {
        return labelFollowedBy switch
        {
            OdfTextLabelFollowedBy.ListTab => "listtab",
            OdfTextLabelFollowedBy.Nothing => "nothing",
            OdfTextLabelFollowedBy.Space => "space",
            _ => throw new ArgumentOutOfRangeException(nameof(labelFollowedBy), labelFollowedBy, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfTextList"))
        };
    }

    internal static bool TryParseTextListLevelPositionMode(string? value, out OdfTextListLevelPositionMode mode)
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

    internal static string FormatTextListLevelPositionMode(OdfTextListLevelPositionMode mode)
    {
        return mode switch
        {
            OdfTextListLevelPositionMode.LabelAlignment => "label-alignment",
            OdfTextListLevelPositionMode.LabelWidthAndPosition => "label-width-and-position",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfTextList_2"))
        };
    }

    internal static bool TryParseTextIndexScope(string? value, out OdfTextIndexScope scope)
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

    internal static string FormatTextIndexScope(OdfTextIndexScope scope)
    {
        return scope switch
        {
            OdfTextIndexScope.Chapter => "chapter",
            OdfTextIndexScope.Document => "document",
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfTextIndex"))
        };
    }

    internal static bool TryParseTextTableType(string? value, out OdfTextTableType tableType)
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

    internal static string FormatTextTableType(OdfTextTableType tableType)
    {
        return tableType switch
        {
            OdfTextTableType.Command => "command",
            OdfTextTableType.Query => "query",
            OdfTextTableType.Table => "table",
            _ => throw new ArgumentOutOfRangeException(nameof(tableType), tableType, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfTextTable"))
        };
    }

    internal static bool TryParseTextAnchorType(string? value, out OdfTextAnchorType anchorType)
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

    internal static string FormatTextAnchorType(OdfTextAnchorType anchorType)
    {
        return anchorType switch
        {
            OdfTextAnchorType.AsChar => "as-char",
            OdfTextAnchorType.Char => "char",
            OdfTextAnchorType.Frame => "frame",
            OdfTextAnchorType.Page => "page",
            OdfTextAnchorType.Paragraph => "paragraph",
            _ => throw new ArgumentOutOfRangeException(nameof(anchorType), anchorType, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfTextAnchor"))
        };
    }

    internal static bool TryParseTextNoteClass(string? value, out OdfTextNoteClass noteClass)
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

    internal static string FormatTextNoteClass(OdfTextNoteClass noteClass)
    {
        return noteClass switch
        {
            OdfTextNoteClass.Endnote => "endnote",
            OdfTextNoteClass.Footnote => "footnote",
            _ => throw new ArgumentOutOfRangeException(nameof(noteClass), noteClass, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfTextAnnotation"))
        };
    }

    internal static bool TryParseTextSelectPage(string? value, out OdfTextSelectPage selectPage)
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

    internal static string FormatTextSelectPage(OdfTextSelectPage selectPage)
    {
        return selectPage switch
        {
            OdfTextSelectPage.Current => "current",
            OdfTextSelectPage.Next => "next",
            OdfTextSelectPage.Previous => "previous",
            _ => throw new ArgumentOutOfRangeException(nameof(selectPage), selectPage, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfTextPage"))
        };
    }

    internal static bool TryParseTextReferenceFormat(string? value, out OdfTextReferenceFormat format)
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

    internal static string FormatTextReferenceFormat(OdfTextReferenceFormat format)
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
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfLiteralReference"))
        };
    }


    #endregion
}
