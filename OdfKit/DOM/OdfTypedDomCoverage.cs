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
public static partial class OdfTypedDomCoverage
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
}
