using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Styles;

namespace OdfKit.DOM;

/// <summary>
/// 產生 typed DOM 與 ODF schema 之間的覆蓋報告。
/// </summary>
public static class OdfTypedDomCoverage
{
    /// <summary>
    /// 依指定 schema 建立 machine-readable typed DOM 覆蓋報告。
    /// </summary>
    /// <param name="schema">要檢查的 schema；若為 <see langword="null"/>，則使用最新 schema。</param>
    /// <returns>typed DOM 覆蓋報告。</returns>
    public static OdfTypedDomCoverageReport Build(OdfSchemaSet? schema = null)
    {
        OdfSchemaSet resolvedSchema = schema ?? OdfSchemaRegistry.Latest;
        List<OdfTypedDomElementCoverage> elements = [];
        IReadOnlyList<OdfTypedDomChildElementRelationCoverage> childElementRelations =
            BuildChildElementRelations(resolvedSchema);
        Dictionary<string, int> wrapperPropertyTypeCounts = new(StringComparer.Ordinal);
        foreach (OdfElementDefinition definition in resolvedSchema.Elements.Values
            .OrderBy(element => element.Name.NamespaceUri, StringComparer.Ordinal)
            .ThenBy(element => element.Name.LocalName, StringComparer.Ordinal))
        {
            OdfNode node = OdfNodeFactory.CreateElement(
                definition.Name.LocalName,
                definition.Name.NamespaceUri,
                OdfNamespaces.GetPrefix(definition.Name.NamespaceUri));
            Type wrapperType = node.GetType();
            bool hasTypedWrapper = wrapperType != typeof(OdfElement);
            elements.Add(new OdfTypedDomElementCoverage(
                definition.Name.NamespaceUri,
                definition.Name.LocalName,
                definition.Role.ToString(),
                definition.DocumentKind.ToString(),
                wrapperType.FullName ?? wrapperType.Name,
                hasTypedWrapper,
                CountDeclaredPublicProperties(wrapperType)));
            foreach (PropertyInfo property in GetDeclaredPublicProperties(wrapperType))
            {
                string propertyType = GetPropertyTypeName(property.PropertyType);
                wrapperPropertyTypeCounts[propertyType] = wrapperPropertyTypeCounts.TryGetValue(propertyType, out int count)
                    ? count + 1
                    : 1;
            }
        }

        Dictionary<string, int> attributeValueTypeCounts = resolvedSchema.Attributes.Values
            .GroupBy(attribute => string.IsNullOrWhiteSpace(attribute.ValueType) ? "string" : attribute.ValueType, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        return new OdfTypedDomCoverageReport(
            FormatVersion(resolvedSchema.Version),
            resolvedSchema.SourceUrl.ToString(),
            resolvedSchema.SourceDate,
            elements,
            childElementRelations,
            resolvedSchema.Attributes.Count,
            attributeValueTypeCounts,
            wrapperPropertyTypeCounts
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }

    private static IReadOnlyList<OdfTypedDomChildElementRelationCoverage> BuildChildElementRelations(OdfSchemaSet schema)
    {
        var relations = new Dictionary<string, OdfTypedDomChildElementRelationCoverage>(StringComparer.Ordinal);
        foreach (OdfSchemaPatternDefinition pattern in schema.Patterns.Values)
        {
            foreach (OdfSchemaPatternNode root in pattern.Roots)
            {
                CollectParentElementRelations(root, schema, relations, []);
            }
        }

        return relations.Values
            .OrderBy(relation => relation.ParentNamespaceUri, StringComparer.Ordinal)
            .ThenBy(relation => relation.ParentLocalName, StringComparer.Ordinal)
            .ThenBy(relation => relation.ChildNamespaceUri, StringComparer.Ordinal)
            .ThenBy(relation => relation.ChildLocalName, StringComparer.Ordinal)
            .ToArray();
    }

    private static void CollectParentElementRelations(
        OdfSchemaPatternNode node,
        OdfSchemaSet schema,
        Dictionary<string, OdfTypedDomChildElementRelationCoverage> relations,
        HashSet<string> visitedRefs)
    {
        if (node.Kind == OdfSchemaPatternNodeKind.Element)
        {
            CollectDirectChildElementRelations(node, schema, relations, []);
            return;
        }

        if (node.Kind == OdfSchemaPatternNodeKind.Ref)
        {
            foreach (OdfSchemaPatternNode root in ResolvePatternRoots(node.ReferenceName, schema, visitedRefs))
            {
                CollectParentElementRelations(root, schema, relations, visitedRefs);
            }

            return;
        }

        foreach (OdfSchemaPatternNode child in node.Children)
        {
            CollectParentElementRelations(child, schema, relations, visitedRefs);
        }
    }

    private static void CollectDirectChildElementRelations(
        OdfSchemaPatternNode parent,
        OdfSchemaSet schema,
        Dictionary<string, OdfTypedDomChildElementRelationCoverage> relations,
        HashSet<string> visitedRefs)
    {
        foreach (OdfSchemaPatternNode child in parent.Children)
        {
            CollectDirectChildElementRelations(parent, child, schema, relations, visitedRefs);
        }
    }

    private static void CollectDirectChildElementRelations(
        OdfSchemaPatternNode parent,
        OdfSchemaPatternNode node,
        OdfSchemaSet schema,
        Dictionary<string, OdfTypedDomChildElementRelationCoverage> relations,
        HashSet<string> visitedRefs)
    {
        if (node.Kind == OdfSchemaPatternNodeKind.Attribute)
        {
            return;
        }

        if (node.Kind == OdfSchemaPatternNodeKind.Element)
        {
            if (!string.IsNullOrWhiteSpace(parent.NamespaceUri) &&
                !string.IsNullOrWhiteSpace(parent.LocalName) &&
                !string.IsNullOrWhiteSpace(node.NamespaceUri) &&
                !string.IsNullOrWhiteSpace(node.LocalName))
            {
                string key = string.Join(
                    "\u001f",
                    parent.NamespaceUri,
                    parent.LocalName,
                    node.NamespaceUri,
                    node.LocalName);
                relations[key] = new OdfTypedDomChildElementRelationCoverage(
                    parent.NamespaceUri,
                    parent.LocalName,
                    node.NamespaceUri,
                    node.LocalName,
                    node.Occurrence);
            }

            return;
        }

        if (node.Kind == OdfSchemaPatternNodeKind.Ref)
        {
            foreach (OdfSchemaPatternNode root in ResolvePatternRoots(node.ReferenceName, schema, visitedRefs))
            {
                CollectDirectChildElementRelations(parent, root, schema, relations, visitedRefs);
            }

            return;
        }

        foreach (OdfSchemaPatternNode child in node.Children)
        {
            CollectDirectChildElementRelations(parent, child, schema, relations, visitedRefs);
        }
    }

    private static IEnumerable<OdfSchemaPatternNode> ResolvePatternRoots(
        string referenceName,
        OdfSchemaSet schema,
        HashSet<string> visitedRefs)
    {
        if (string.IsNullOrWhiteSpace(referenceName) || !visitedRefs.Add(referenceName))
        {
            yield break;
        }

        OdfSchemaPatternDefinition? pattern = schema.FindPattern(referenceName);
        if (pattern is not null)
        {
            foreach (OdfSchemaPatternNode root in pattern.Roots)
            {
                yield return root;
            }
        }

        visitedRefs.Remove(referenceName);
    }

    private static int CountDeclaredPublicProperties(Type type)
    {
        return GetDeclaredPublicProperties(type).Length;
    }

    private static PropertyInfo[] GetDeclaredPublicProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
    }

    private static string GetPropertyTypeName(Type type)
    {
        Type? nullableType = Nullable.GetUnderlyingType(type);
        Type resolvedType = nullableType ?? type;
        if (resolvedType.IsGenericType &&
            resolvedType.GetGenericTypeDefinition() == typeof(IEnumerable<>) &&
            typeof(OdfElement).IsAssignableFrom(resolvedType.GetGenericArguments()[0]))
        {
            return "childElementCollection";
        }

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

        return resolvedType.FullName ?? resolvedType.Name;
    }

    private static string FormatVersion(OdfVersion version)
    {
        return version switch
        {
            OdfVersion.Odf10 => "1.0",
            OdfVersion.Odf11 => "1.1",
            OdfVersion.Odf12 => "1.2",
            OdfVersion.Odf13 => "1.3",
            OdfVersion.Odf14 => "1.4",
            _ => version.ToString()
        };
    }
}

