using OdfKit.DOM;

namespace OdfKit.Compliance;

/// <summary>
/// 分析 ODF DOM 是否含有目標版本無法表示的標準語意。
/// </summary>
internal static class OdfVersionCompatibilityAnalyzer
{
    internal static OdfVersionCompatibilityReport Analyze(
        OdfVersion targetVersion,
        params (string EntryName, OdfNode Root)[] roots)
    {
        OdfSchemaSet sourceSchema = OdfSchemaRegistry.GetSchema(OdfVersion.Odf14);
        OdfSchemaSet targetSchema = OdfSchemaRegistry.GetSchema(targetVersion);
        List<OdfVersionCompatibilityIssue> issues = [];

        foreach ((string entryName, OdfNode root) in roots)
        {
            AnalyzeNode(root, entryName + "/" + root.LocalName, sourceSchema, targetSchema, targetVersion, issues);
        }

        return new OdfVersionCompatibilityReport(OdfVersion.Odf14, targetVersion, issues.AsReadOnly());
    }

    private static void AnalyzeNode(
        OdfNode node,
        string path,
        OdfSchemaSet sourceSchema,
        OdfSchemaSet targetSchema,
        OdfVersion targetVersion,
        List<OdfVersionCompatibilityIssue> issues)
    {
        if (node.NodeType == OdfNodeType.Element && !string.IsNullOrEmpty(node.NamespaceUri))
        {
            OdfQualifiedName elementName = new(node.NamespaceUri, node.LocalName);
            if (sourceSchema.Elements.ContainsKey(elementName) && !targetSchema.Elements.ContainsKey(elementName))
            {
                issues.Add(new OdfVersionCompatibilityIssue(
                    OdfVersionCompatibilityIssueKind.ElementNotSupported,
                    node.NamespaceUri,
                    node.LocalName,
                    path,
                    OdfVersion.Odf14,
                    targetVersion));
            }

            foreach (OdfAttributeName attribute in node.Attributes.Keys)
            {
                if (string.IsNullOrEmpty(attribute.NamespaceUri))
                {
                    continue;
                }

                OdfQualifiedName attributeName = new(attribute.NamespaceUri, attribute.LocalName);
                if (sourceSchema.Attributes.ContainsKey(attributeName) && !targetSchema.Attributes.ContainsKey(attributeName))
                {
                    issues.Add(new OdfVersionCompatibilityIssue(
                        OdfVersionCompatibilityIssueKind.AttributeNotSupported,
                        attribute.NamespaceUri,
                        attribute.LocalName,
                        path + "/@" + attribute.LocalName,
                        OdfVersion.Odf14,
                        targetVersion));
                }
            }
        }

        Dictionary<string, int> childIndexes = new(StringComparer.Ordinal);
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType != OdfNodeType.Element)
            {
                continue;
            }

            string key = child.NamespaceUri + "\u001f" + child.LocalName;
            childIndexes.TryGetValue(key, out int index);
            index++;
            childIndexes[key] = index;
            AnalyzeNode(
                child,
                path + "/" + child.LocalName + "[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                sourceSchema,
                targetSchema,
                targetVersion,
                issues);
        }
    }
}
