using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.DOM;

/// <summary>
/// Provides the OdfTypedDomCoverage API.
/// 產生 typed DOM 與 ODF schema 之間的覆蓋報告。
/// </summary>
public static class OdfTypedDomCoverage
{
    /// <summary>
    /// Builds the machine-readable typed DOM coverage report for the specified schema.
    /// 依指定 schema 建立 machine-readable typed DOM 覆蓋報告。
    /// </summary>
    /// <param name="schema">要檢查的 schema；若為 <see langword="null"/>，則使用最新 schema</param>
    /// <returns>typed DOM 覆蓋報告</returns>
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
            bool hasTypedWrapper = OdfGeneratedDomCoverageMetadata.TryGet(
                definition.Name.NamespaceUri,
                definition.Name.LocalName,
                out OdfGeneratedDomCoverageEntry wrapperMetadata);
            string wrapperTypeName = hasTypedWrapper
                ? wrapperMetadata.WrapperTypeName
                : typeof(OdfElement).FullName ?? nameof(OdfElement);
            string[] propertyTypeNames = hasTypedWrapper
                ? wrapperMetadata.PropertyTypeNames
                : Array.Empty<string>();
            elements.Add(new OdfTypedDomElementCoverage(
                definition.Name.NamespaceUri,
                definition.Name.LocalName,
                definition.Role.ToString(),
                definition.DocumentKind.ToString(),
                wrapperTypeName,
                hasTypedWrapper,
                propertyTypeNames.Length));
            foreach (string propertyType in propertyTypeNames)
            {
                wrapperPropertyTypeCounts[propertyType] = wrapperPropertyTypeCounts.TryGetValue(propertyType, out int count)
                    ? count + 1
                    : 1;
            }
        }

        Dictionary<string, int> attributeValueTypeCounts = resolvedSchema.Attributes.Values
            .GroupBy(attribute => string.IsNullOrWhiteSpace(attribute.ValueType) ? "string" : attribute.ValueType, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        Dictionary<string, int> orderedWrapperPropertyTypeCounts = wrapperPropertyTypeCounts
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        IReadOnlyList<OdfTypedDomAttributeDatatypeCoverage> attributeDatatypeCoverage =
            BuildAttributeDatatypeCoverage(attributeValueTypeCounts, orderedWrapperPropertyTypeCounts);

        return new OdfTypedDomCoverageReport(
            FormatVersion(resolvedSchema.Version),
            resolvedSchema.SourceUrl.ToString(),
            resolvedSchema.SourceDate,
            elements,
            childElementRelations,
            attributeDatatypeCoverage,
            resolvedSchema.Attributes.Count,
            attributeValueTypeCounts,
            orderedWrapperPropertyTypeCounts);
    }

    private static OdfTypedDomAttributeDatatypeCoverage[] BuildAttributeDatatypeCoverage(
        IReadOnlyDictionary<string, int> attributeValueTypeCounts,
        Dictionary<string, int> wrapperPropertyTypeCounts)
    {
        return attributeValueTypeCounts
            .Select(pair =>
            {
                string wrapperPropertyType = MapSchemaValueTypeToWrapperPropertyType(pair.Key);
                int wrapperPropertyCount = wrapperPropertyTypeCounts.TryGetValue(wrapperPropertyType, out int count)
                    ? count
                    : 0;
                bool hasTypedHelper = wrapperPropertyType != "string" && wrapperPropertyCount > 0;
                string status = GetAttributeDatatypeCoverageStatus(wrapperPropertyType, wrapperPropertyCount);
                return new OdfTypedDomAttributeDatatypeCoverage(
                    pair.Key,
                    pair.Value,
                    wrapperPropertyType,
                    wrapperPropertyCount,
                    hasTypedHelper,
                    status);
            })
            .OrderBy(coverage => coverage.SchemaValueType, StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetAttributeDatatypeCoverageStatus(string wrapperPropertyType, int wrapperPropertyCount)
    {
        if (wrapperPropertyType == "string")
        {
            return "string-preserve";
        }

        return wrapperPropertyCount > 0
            ? "typed-helper-present"
            : "candidate-for-typed-helper";
    }

    private static string MapSchemaValueTypeToWrapperPropertyType(string schemaValueType)
    {
        string normalized = schemaValueType.Replace("-", string.Empty).ToLowerInvariant();
        return normalized switch
        {
            "boolean" => "bool",
            "byte" or "short" or "int" or "integer" or "long" or "nonnegativeinteger" or "positiveinteger" or "nonpositiveinteger" or "negativeinteger" => "int",
            "decimal" or "double" or "float" => "decimal",
            "date" or "datetime" => "dateTime",
            "time" => "time",
            "duration" => "duration",
            "ncname" or "id" or "idref" => "xmlName",
            "anyuri" => "iriReference",
            _ => "string"
        };
    }

    private static OdfTypedDomChildElementRelationCoverage[] BuildChildElementRelations(OdfSchemaSet schema)
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
