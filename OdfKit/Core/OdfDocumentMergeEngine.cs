using System;
using System.Collections.Generic;
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

                bool conflict = dest.StyleEngine.StyleExists(name);

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
