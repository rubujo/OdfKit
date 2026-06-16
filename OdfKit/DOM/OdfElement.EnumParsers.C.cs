using System;
using OdfKit.DOM;

namespace OdfKit.DOM;

public partial class OdfElement
{
    #region Enum Parsers (C)

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


    #endregion
}
