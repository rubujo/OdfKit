using System;
using System.Collections.Generic;
using System.Text;
using OdfKit.DOM;

namespace OdfKit.Core;

/// <summary>
/// ODF 文件合併引擎（內部協作者）。
/// </summary>
internal static class OdfDocumentMergeEngine
{
    /// <summary>
    /// 將來源文件附加至目標文件。
    /// </summary>
    internal static void AppendDocument(
        OdfDocument.OdfDocumentMergeCollaborators dest,
        OdfDocument sourceDoc,
        OdfMergeOptions options)
    {
        if (sourceDoc == null)
            throw new ArgumentNullException(nameof(sourceDoc));

        var styleRenameMap = new Dictionary<string, string>(StringComparer.Ordinal);

        if (options.ImportStyles)
        {
            MergeStyles(dest, sourceDoc, options, styleRenameMap);
        }

        dest.MergeContentNodes(sourceDoc, options, styleRenameMap);
    }

    /// <summary>
    /// 僅合併樣式（不含內容節點），供需要自行控制內容節點合併順序的呼叫端
    /// （例如 <see cref="OdfKit.Text.TextMasterDocument.MergeSubDocuments"/>）使用。
    /// </summary>
    internal static void MergeStyles(
        OdfDocument.OdfDocumentMergeCollaborators dest,
        OdfDocument sourceDoc,
        OdfMergeOptions options,
        Dictionary<string, string> renameMap)
    {
        OdfNode sourceContentAuto = dest.FindOrCreateChild(sourceDoc.ContentDom, "automatic-styles", OdfNamespaces.Office, "office");
        OdfNode destContentAuto = dest.FindOrCreateChild(dest.ContentDom, "automatic-styles", OdfNamespaces.Office, "office");
        MergeStyleNodes(dest, sourceContentAuto, destContentAuto, options, renameMap);

        OdfNode sourceStylesStyles = dest.FindOrCreateChild(sourceDoc.StylesDom, "styles", OdfNamespaces.Office, "office");
        OdfNode destStylesStyles = dest.FindOrCreateChild(dest.StylesDom, "styles", OdfNamespaces.Office, "office");
        MergeStyleNodes(dest, sourceStylesStyles, destStylesStyles, options, renameMap);

        OdfNode sourceStylesAuto = dest.FindOrCreateChild(sourceDoc.StylesDom, "automatic-styles", OdfNamespaces.Office, "office");
        OdfNode destStylesAuto = dest.FindOrCreateChild(dest.StylesDom, "automatic-styles", OdfNamespaces.Office, "office");
        MergeStyleNodes(dest, sourceStylesAuto, destStylesAuto, options, renameMap);

        // 新增的樣式節點透過原始 DOM 操作寫入，未經過樣式引擎的一般建立路徑，
        // 必須重建索引快取，否則後續（例如連續合併多份來源文件時）的衝突偵測會讀到過期快取。
        dest.StyleEngine.RebuildStyleIndex();
    }

    private static void MergeStyleNodes(
        OdfDocument.OdfDocumentMergeCollaborators dest,
        OdfNode sourceContainer,
        OdfNode destContainer,
        OdfMergeOptions options,
        Dictionary<string, string> renameMap)
    {
        foreach (OdfNode srcStyle in sourceContainer.Children)
        {
            if (srcStyle.NodeType == OdfNodeType.Element && !string.IsNullOrEmpty(srcStyle.GetAttribute("name", OdfNamespaces.Style)))
            {
                string name = srcStyle.GetAttribute("name", OdfNamespaces.Style)!;
                string family = srcStyle.GetAttribute("family", OdfNamespaces.Style) ?? "paragraph";

                OdfNode? existingStyle = FindStyleByName(destContainer, name);
                bool conflict = existingStyle is not null || dest.StyleEngine.StyleExists(name);

                if (conflict &&
                    options.StyleConflictResolution != ConflictResolution.KeepSourceFormatting &&
                    existingStyle is not null &&
                    AreSemanticallyEquivalentStyles(srcStyle, existingStyle))
                {
                    continue;
                }

                if (conflict && options.StyleConflictResolution == ConflictResolution.KeepSourceFormatting)
                {
                    string newName = GenerateUniqueStyleName(dest, name, family);
                    renameMap[name] = newName;

                    OdfNode clonedStyle = srcStyle.CloneNode(true);
                    clonedStyle.SetAttribute("name", OdfNamespaces.Style, newName, "style");
                    destContainer.AppendChild(clonedStyle);
                }
                else if (!conflict)
                {
                    OdfNode clonedStyle = srcStyle.CloneNode(true);
                    destContainer.AppendChild(clonedStyle);
                }
            }
        }
    }

    private static OdfNode? FindStyleByName(OdfNode container, string styleName)
    {
        foreach (OdfNode child in container.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.LocalName == "style" &&
                child.NamespaceUri == OdfNamespaces.Style &&
                string.Equals(child.GetAttribute("name", OdfNamespaces.Style), styleName, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static bool AreSemanticallyEquivalentStyles(OdfNode sourceStyle, OdfNode destinationStyle)
        => string.Equals(CreateSemanticStyleSignature(sourceStyle), CreateSemanticStyleSignature(destinationStyle), StringComparison.Ordinal);

    private static string CreateSemanticStyleSignature(OdfNode node)
    {
        StringBuilder builder = new();
        AppendSemanticNodeSignature(node, builder);
        return builder.ToString();
    }

    private static void AppendSemanticNodeSignature(OdfNode node, StringBuilder builder)
    {
        builder.Append(node.NodeType).Append('|');
        builder.Append(node.NamespaceUri).Append('|');
        builder.Append(node.LocalName).Append('|');

        List<OdfAttributeName> attributes = [.. node.Attributes.Keys];
        attributes.Sort((left, right) =>
        {
            int namespaceCompare = string.CompareOrdinal(left.NamespaceUri, right.NamespaceUri);
            return namespaceCompare != 0 ? namespaceCompare : string.CompareOrdinal(left.LocalName, right.LocalName);
        });

        foreach (OdfAttributeName attribute in attributes)
        {
            if (attribute.LocalName == "name" && attribute.NamespaceUri == OdfNamespaces.Style)
                continue;

            builder.Append('@')
                .Append(attribute.NamespaceUri)
                .Append(':')
                .Append(attribute.LocalName)
                .Append('=')
                .Append(node.Attributes[attribute])
                .Append(';');
        }

        List<string> childSignatures = [];
        foreach (OdfNode child in node.Children)
        {
            StringBuilder childBuilder = new();
            AppendSemanticNodeSignature(child, childBuilder);
            childSignatures.Add(childBuilder.ToString());
        }

        childSignatures.Sort(StringComparer.Ordinal);
        foreach (string childSignature in childSignatures)
        {
            builder.Append('[').Append(childSignature).Append(']');
        }
    }

    private static string GenerateUniqueStyleName(OdfDocument.OdfDocumentMergeCollaborators dest, string baseName, string family = "paragraph")
    {
        int i = 1;
        string testName;
        do
        {
            testName = $"{baseName}_s{i++}";
        }
        while (dest.StyleEngine.StyleExists(testName));

        return testName;
    }
}
