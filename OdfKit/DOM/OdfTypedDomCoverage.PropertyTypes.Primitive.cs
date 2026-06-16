using System;
using OdfKit.Styles;

namespace OdfKit.DOM;

public static partial class OdfTypedDomCoverage
{
    #region Primitive Property Types

    private static string? TryResolvePrimitivePropertyTypeName(Type resolvedType)
    {
        if (resolvedType == typeof(string))
        {
            return "string";
        }

        if (resolvedType == typeof(int))
        {
            return "int";
        }

        if (resolvedType == typeof(bool))
        {
            return "bool";
        }

        if (resolvedType == typeof(decimal))
        {
            return "decimal";
        }

        if (resolvedType == typeof(DateTime))
        {
            return "dateTime";
        }

        if (resolvedType == typeof(OdfTime))
        {
            return "time";
        }

        if (resolvedType == typeof(OdfLength))
        {
            return "length";
        }

        if (resolvedType == typeof(OdfBorderWidths))
        {
            return "borderWidths";
        }

        if (resolvedType == typeof(OdfDuration))
        {
            return "duration";
        }

        if (resolvedType == typeof(OdfAngle))
        {
            return "angle";
        }

        if (resolvedType == typeof(OdfStyleName))
        {
            return "styleName";
        }

        if (resolvedType == typeof(OdfStyleNameList))
        {
            return "styleNameList";
        }

        if (resolvedType == typeof(OdfColor))
        {
            return "color";
        }

        if (resolvedType == typeof(OdfIriReference))
        {
            return "iriReference";
        }

        if (resolvedType == typeof(OdfPercent))
        {
            return "percent";
        }

        if (resolvedType == typeof(OdfCellAddressReference))
        {
            return "cellAddress";
        }

        if (resolvedType == typeof(OdfCellRangeAddress))
        {
            return "cellRangeAddress";
        }

        if (resolvedType == typeof(OdfCellRangeAddressList))
        {
            return "cellRangeAddressList";
        }

        if (resolvedType == typeof(OdfVector3D))
        {
            return "vector3D";
        }

        if (resolvedType == typeof(OdfPoint3D))
        {
            return "point3D";
        }

        if (resolvedType == typeof(OdfPointList))
        {
            return "pointList";
        }

        if (resolvedType == typeof(OdfLanguageCode))
        {
            return "languageCode";
        }

        if (resolvedType == typeof(OdfCountryCode))
        {
            return "countryCode";
        }

        if (resolvedType == typeof(OdfScriptCode))
        {
            return "scriptCode";
        }

        if (resolvedType == typeof(OdfLanguageTag))
        {
            return "languageTag";
        }

        if (resolvedType == typeof(OdfNamespacedToken))
        {
            return "namespacedToken";
        }

        if (resolvedType == typeof(OdfCharacter))
        {
            return "character";
        }

        if (resolvedType == typeof(OdfTextEncoding))
        {
            return "textEncoding";
        }

        if (resolvedType == typeof(OdfTargetFrameName))
        {
            return "targetFrameName";
        }

        if (resolvedType == typeof(OdfXLinkType))
        {
            return "xLinkType";
        }

        if (resolvedType == typeof(OdfXLinkShow))
        {
            return "xLinkShow";
        }

        if (resolvedType == typeof(OdfXLinkActuate))
        {
            return "xLinkActuate";
        }

        if (resolvedType == typeof(OdfNumberStyle))
        {
            return "numberStyle";
        }

        if (resolvedType == typeof(OdfNumberCalendar))
        {
            return "numberCalendar";
        }

        if (resolvedType == typeof(OdfTableOrder))
        {
            return "tableOrder";
        }

        if (resolvedType == typeof(OdfTableType))
        {
            return "tableType";
        }

        if (resolvedType == typeof(OdfPresentationEffect))
        {
            return "presentationEffect";
        }

        if (resolvedType == typeof(OdfPresentationSpeed))
        {
            return "presentationSpeed";
        }

        if (resolvedType == typeof(OdfPresentationAction))
        {
            return "presentationAction";
        }

        if (resolvedType == typeof(OdfPresentationTransitionType))
        {
            return "presentationTransitionType";
        }

        if (resolvedType == typeof(OdfPresentationTransitionStyle))
        {
            return "presentationTransitionStyle";
        }

        if (resolvedType == typeof(OdfFoTextTransform))
        {
            return "foTextTransform";
        }

        if (resolvedType == typeof(OdfFoTextAlign))
        {
            return "foTextAlign";
        }

        if (resolvedType == typeof(OdfStyleTextRotationScale))
        {
            return "styleTextRotationScale";
        }

        if (resolvedType == typeof(OdfStyleTextCombine))
        {
            return "styleTextCombine";
        }
        return null;
    }

    #endregion
}
