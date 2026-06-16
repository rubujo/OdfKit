using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    #region Document Merging API


    /// <summary>
    /// 將另一份 ODF 文件附加到目前文件。
    /// </summary>
    /// <param name="otherDoc">要附加的來源文件。</param>
    /// <param name="options">合併選項。</param>
    public virtual void AppendDocument(OdfDocument otherDoc, OdfMergeOptions? options = null)
    {
        options ??= OdfMergeOptions.Default;
        if (otherDoc == null)
            throw new ArgumentNullException(nameof(otherDoc));

        var styleRenameMap = new Dictionary<string, string>(StringComparer.Ordinal);

        if (options.ImportStyles)
        {
            MergeStyles(otherDoc, options, styleRenameMap);
        }

        MergeContentNodes(otherDoc, options, styleRenameMap);
    }


    #endregion

    #region Internal Merging Helpers


    private void MergeStyleNodes(OdfNode sourceContainer, OdfNode destContainer, OdfMergeOptions options, Dictionary<string, string> renameMap)
    {
        foreach (var srcStyle in sourceContainer.Children)
        {
            if (srcStyle.NodeType == OdfNodeType.Element && !string.IsNullOrEmpty(srcStyle.GetAttribute("name", OdfNamespaces.Style)))
            {
                string name = srcStyle.GetAttribute("name", OdfNamespaces.Style)!;
                string family = srcStyle.GetAttribute("family", OdfNamespaces.Style) ?? "paragraph";

                bool conflict = StyleEngine.StyleExists(name);

                if (conflict && options.StyleConflictResolution == ConflictResolution.KeepSourceFormatting)
                {
                    string newName = GenerateUniqueStyleName(name, family);
                    renameMap[name] = newName;

                    var clonedStyle = srcStyle.CloneNode(true);
                    clonedStyle.SetAttribute("name", OdfNamespaces.Style, newName, "style");
                    destContainer.AppendChild(clonedStyle);
                }
                else if (!conflict)
                {
                    var clonedStyle = srcStyle.CloneNode(true);
                    destContainer.AppendChild(clonedStyle);
                }
            }
        }
    }

    private void MergeStyles(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
    {
        var sourceContentAuto = FindOrCreateChild(sourceDoc.ContentDom, "automatic-styles", OdfNamespaces.Office, "office");
        var destContentAuto = FindOrCreateChild(ContentDom, "automatic-styles", OdfNamespaces.Office, "office");
        MergeStyleNodes(sourceContentAuto, destContentAuto, options, renameMap);

        var sourceStylesStyles = FindOrCreateChild(sourceDoc.StylesDom, "styles", OdfNamespaces.Office, "office");
        var destStylesStyles = FindOrCreateChild(StylesDom, "styles", OdfNamespaces.Office, "office");
        MergeStyleNodes(sourceStylesStyles, destStylesStyles, options, renameMap);

        var sourceStylesAuto = FindOrCreateChild(sourceDoc.StylesDom, "automatic-styles", OdfNamespaces.Office, "office");
        var destStylesAuto = FindOrCreateChild(StylesDom, "automatic-styles", OdfNamespaces.Office, "office");
        MergeStyleNodes(sourceStylesAuto, destStylesAuto, options, renameMap);
    }

    private string GenerateUniqueStyleName(string baseName, string family = "paragraph")
    {
        int i = 1;
        string testName;
        do
        {
            testName = $"{baseName}_s{i++}";
        } while (StyleEngine.StyleExists(testName));
        return testName;
    }

    /// <summary>
    /// 尋找或建立指定子元素。
    /// </summary>
    /// <param name="parent">父節點。</param>
    /// <param name="localName">子元素區域名稱。</param>
    /// <param name="ns">子元素命名空間 URI。</param>
    /// <param name="prefix">子元素前綴。</param>
    /// <returns>符合條件的既有或新建子元素。</returns>
    protected OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
    {
        foreach (var child in parent.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
                return child;
        }
        var node = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
        parent.AppendChild(node);
        return node;
    }

    /// <summary>
    /// 將來源文件的內容節點合併到目前文件。
    /// </summary>
    /// <param name="sourceDoc">來源文件。</param>
    /// <param name="options">合併選項。</param>
    /// <param name="renameMap">樣式重新命名對照表。</param>
    protected abstract void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap);

    /// <summary>
    /// 依樣式重新命名對照表重寫節點樹中的樣式參照。
    /// </summary>
    /// <param name="node">要處理的根節點。</param>
    /// <param name="renameMap">樣式重新命名對照表。</param>
    protected void RemapStylesInNodes(OdfNode node, Dictionary<string, string> renameMap)
    {
        var styleNameAttr = new OdfAttributeName("style-name", OdfNamespaces.Text);
        if (node.Attributes.TryGetValue(styleNameAttr, out string? currentStyleName))
        {
            if (currentStyleName != null && renameMap.TryGetValue(currentStyleName, out string? newName))
            {
                node.Attributes[styleNameAttr] = newName;
            }
        }

        var drawStyleAttr = new OdfAttributeName("style-name", OdfNamespaces.Draw);
        if (node.Attributes.TryGetValue(drawStyleAttr, out string? dsName))
        {
            if (dsName != null && renameMap.TryGetValue(dsName, out string? newName))
            {
                node.Attributes[drawStyleAttr] = newName;
            }
        }

        var tableStyleAttr = new OdfAttributeName("style-name", OdfNamespaces.Table);
        if (node.Attributes.TryGetValue(tableStyleAttr, out string? tsName))
        {
            if (tsName != null && renameMap.TryGetValue(tsName, out string? newName))
            {
                node.Attributes[tableStyleAttr] = newName;
            }
        }

        foreach (var child in node.Children)
        {
            RemapStylesInNodes(child, renameMap);
        }
    }


    #endregion

}
