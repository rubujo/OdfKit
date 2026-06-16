using System;
using OdfKit.Compliance;
using OdfKit.Styles;

namespace OdfKit.DOM;

public static partial class OdfTypedDomCoverage
{
    #region Extended Property Types

    private static string? TryResolveExtendedPropertyTypeName(Type resolvedType)
    {
        if (resolvedType == typeof(OdfDrawFill))
        {
            return "drawFill";
        }

        if (resolvedType == typeof(OdfSmilFill))
        {
            return "smilFill";
        }

        if (resolvedType == typeof(OdfDrawFillImageRefPoint))
        {
            return "drawFillImageRefPoint";
        }

        if (resolvedType == typeof(OdfDrawColorMode))
        {
            return "drawColorMode";
        }

        if (resolvedType == typeof(OdfStyleVerticalAlign))
        {
            return "styleVerticalAlign";
        }

        if (resolvedType == typeof(OdfStyleVerticalPos))
        {
            return "styleVerticalPos";
        }

        if (resolvedType == typeof(OdfStyleVerticalRel))
        {
            return "styleVerticalRel";
        }

        if (resolvedType == typeof(OdfStyleHorizontalPos))
        {
            return "styleHorizontalPos";
        }

        if (resolvedType == typeof(OdfStyleHorizontalRel))
        {
            return "styleHorizontalRel";
        }

        if (resolvedType == typeof(OdfStyleWrap))
        {
            return "styleWrap";
        }

        if (resolvedType == typeof(OdfStyleRunThrough))
        {
            return "styleRunThrough";
        }

        if (resolvedType == typeof(OdfStyleWrapContourMode))
        {
            return "styleWrapContourMode";
        }

        if (resolvedType == typeof(OdfStyleWritingMode))
        {
            return "styleWritingMode";
        }

        if (resolvedType == typeof(OdfTableDisplayMemberMode))
        {
            return "tableDisplayMemberMode";
        }

        if (resolvedType == typeof(OdfTableLayoutMode))
        {
            return "tableLayoutMode";
        }

        if (resolvedType == typeof(OdfTableMemberType))
        {
            return "tableMemberType";
        }

        if (resolvedType == typeof(OdfTableGroupedBy))
        {
            return "tableGroupedBy";
        }

        if (resolvedType == typeof(OdfTableSortMode))
        {
            return "tableSortMode";
        }

        if (resolvedType == typeof(OdfTableConditionSource))
        {
            return "tableConditionSource";
        }

        if (resolvedType == typeof(OdfTableFunction))
        {
            return "tableFunction";
        }

        if (resolvedType == typeof(OdfDatabaseRule))
        {
            return "databaseRule";
        }

        if (resolvedType == typeof(OdfDatabaseIsNullable))
        {
            return "databaseIsNullable";
        }

        if (resolvedType == typeof(OdfDatabaseDataSourceSettingType))
        {
            return "databaseDataSourceSettingType";
        }

        if (resolvedType == typeof(OdfAnimationColorInterpolation))
        {
            return "animationColorInterpolation";
        }

        if (resolvedType == typeof(OdfAnimationColorInterpolationDirection))
        {
            return "animationColorInterpolationDirection";
        }

        if (resolvedType == typeof(OdfDrawNoHref))
        {
            return "drawNoHref";
        }

        if (resolvedType == typeof(OdfPresentationPresetClass))
        {
            return "presentationPresetClass";
        }

        if (resolvedType == typeof(OdfNumberTransliterationStyle))
        {
            return "numberTransliterationStyle";
        }

        if (resolvedType == typeof(OdfStyleScriptType))
        {
            return "styleScriptType";
        }

        if (resolvedType == typeof(OdfStyleTextEmphasize))
        {
            return "styleTextEmphasize";
        }

        if (resolvedType == typeof(OdfDrawStrokeLineJoin))
        {
            return "drawStrokeLineJoin";
        }

        if (resolvedType == typeof(OdfSvgStrokeLineCap))
        {
            return "svgStrokeLineCap";
        }

        if (resolvedType == typeof(OdfFoKeepTogether))
        {
            return "foKeepTogether";
        }

        if (resolvedType == typeof(OdfFoWrapOption))
        {
            return "foWrapOption";
        }

        if (resolvedType == typeof(OdfDr3dProjection))
        {
            return "dr3dProjection";
        }

        if (resolvedType == typeof(OdfDr3dShadeMode))
        {
            return "dr3dShadeMode";
        }

        if (resolvedType == typeof(OdfSvgFillRule))
        {
            return "svgFillRule";
        }

        if (resolvedType == typeof(OdfTableBorderModel))
        {
            return "tableBorderModel";
        }

        if (resolvedType == typeof(OdfTextLabelFollowedBy))
        {
            return "textLabelFollowedBy";
        }

        if (resolvedType == typeof(OdfTextListLevelPositionMode))
        {
            return "textListLevelPositionMode";
        }

        if (resolvedType == typeof(OdfTextIndexScope))
        {
            return "textIndexScope";
        }

        if (resolvedType == typeof(OdfTextTableType))
        {
            return "textTableType";
        }

        if (resolvedType == typeof(OdfTextAnchorType))
        {
            return "textAnchorType";
        }

        if (resolvedType == typeof(OdfTextNoteClass))
        {
            return "textNoteClass";
        }

        if (resolvedType == typeof(OdfTextSelectPage))
        {
            return "textSelectPage";
        }

        if (resolvedType == typeof(OdfTextReferenceFormat))
        {
            return "textReferenceFormat";
        }

        if (resolvedType == typeof(OdfTextStartNumberingAt))
        {
            return "textStartNumberingAt";
        }

        if (resolvedType == typeof(OdfTextFootnotesPosition))
        {
            return "textFootnotesPosition";
        }

        if (resolvedType == typeof(OdfTextCaptionSequenceFormat))
        {
            return "textCaptionSequenceFormat";
        }

        if (resolvedType == typeof(OdfTextNumberPosition))
        {
            return "textNumberPosition";
        }

        if (resolvedType == typeof(OdfTextPlaceholderType))
        {
            return "textPlaceholderType";
        }

        if (resolvedType == typeof(OdfTextAnimation))
        {
            return "textAnimation";
        }

        if (resolvedType == typeof(OdfTextAnimationDirection))
        {
            return "textAnimationDirection";
        }

        if (resolvedType == typeof(OdfTextKind))
        {
            return "textKind";
        }

        if (resolvedType == typeof(OdfLineStyle))
        {
            return "lineStyle";
        }

        if (resolvedType == typeof(OdfLineType))
        {
            return "lineType";
        }

        if (resolvedType == typeof(OdfLineWidth))
        {
            return "lineWidth";
        }

        if (resolvedType == typeof(OdfLineMode))
        {
            return "lineMode";
        }

        if (resolvedType == typeof(OdfFontStyle))
        {
            return "fontStyle";
        }

        if (resolvedType == typeof(OdfFontVariant))
        {
            return "fontVariant";
        }

        if (resolvedType == typeof(OdfFontWeight))
        {
            return "fontWeight";
        }

        if (resolvedType == typeof(OdfFontFamilyGeneric))
        {
            return "fontFamilyGeneric";
        }

        if (resolvedType == typeof(OdfFontPitch))
        {
            return "fontPitch";
        }

        if (resolvedType == typeof(OdfFontRelief))
        {
            return "fontRelief";
        }

        if (resolvedType == typeof(OdfFontStretch))
        {
            return "fontStretch";
        }

        if (resolvedType == typeof(OdfStyleLineBreak))
        {
            return "styleLineBreak";
        }

        if (resolvedType == typeof(OdfStyleRepeat))
        {
            return "styleRepeat";
        }

        if (resolvedType == typeof(OdfStyleDirection))
        {
            return "styleDirection";
        }

        if (resolvedType == typeof(OdfFormOrientation))
        {
            return "formOrientation";
        }

        if (resolvedType == typeof(OdfTableDirection))
        {
            return "tableDirection";
        }

        if (resolvedType == typeof(OdfTableOrientation))
        {
            return "tableOrientation";
        }

        if (resolvedType == typeof(OdfXmlName))
        {
            return "xmlName";
        }

        if (resolvedType == typeof(OdfStyleFamily))
        {
            return "styleFamily";
        }

        if (resolvedType == typeof(OdfVersion))
        {
            return "odfVersion";
        }

        if (resolvedType == typeof(OdfMediaType))
        {
            return "mediaType";
        }
        return null;
    }

    #endregion
}
