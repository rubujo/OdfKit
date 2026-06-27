using System;
using System.Collections.Generic;
using OdfKit.DOM;

namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    #region Document Merging API


    /// <summary>
    /// 採納來源節點至目前文件，將其與原父節點脫鉤，並轉移其所有權至目前文件。
    /// </summary>
    /// <param name="node">要採納的來源節點</param>
    /// <returns>已完成採納的節點實體（與原 Parent 脫鉤，且 Document 屬性已更新）</returns>
    /// <remarks>
    /// 此方法實作了 O(1) 零拷貝節點轉移。當來源節點來自另一份文件時，
    /// 會自動處理必要的媒體參照移轉與命名空間補全。
    /// </remarks>
    public virtual OdfNode AdoptNode(OdfNode node)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        var sourceDoc = node.Document;
        var sourcePackage = sourceDoc?.Package;

        // 1. 與原 Parent 脫鉤
        node.Parent?.RemoveChild(node);

        // 2. 如果有來源套件且與目前套件不同，進行媒體遷移
        if (sourcePackage is not null && sourcePackage != Package)
        {
            OdfNode.MigrateMediaReferences(node, sourcePackage, Package);
        }

        // 3. 匯入採納節點引用的樣式，並扁平化來源 default style 屬性。
        if (sourceDoc is not null && sourceDoc != this)
        {
            ImportReferencedStylesForAdoptedNode(sourceDoc, node);
        }

        // 4. 遞迴更新 Document 與命名空間前綴
        NormalizeAdoptedNodeNamespaces(node);
        UpdateNodeDocument(node, this);

        return node;
    }

    /// <summary>
    /// 採納來源文件中的節點至目前文件，將其與原父節點脫鉤，並轉移其所有權至目前文件。
    /// </summary>
    /// <param name="sourceDocument">來源文件</param>
    /// <param name="node">要採納的來源節點</param>
    /// <returns>已完成採納的節點實體（與原 Parent 脫鉤，且 Document 屬性已更新）</returns>
    /// <remarks>
    /// 此方法實作了 O(1) 零拷貝節點轉移。來源文件資訊用於跨文件媒體參照移轉，避免嵌入圖片或物件在採納後失聯。
    /// </remarks>
    public virtual OdfNode AdoptNode(OdfDocument sourceDocument, OdfNode node)
    {
        if (sourceDocument is null)
        {
            throw new ArgumentNullException(nameof(sourceDocument));
        }

        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        // 1. 與原 Parent 脫鉤
        node.Parent?.RemoveChild(node);

        // 2. 如果在不同的套件之間遷移，則掃描並移轉其所屬的媒體檔案與樣式關聯
        if (sourceDocument.Package != Package)
        {
            OdfNode.MigrateMediaReferences(node, sourceDocument.Package, Package);
        }

        // 3. 匯入採納節點引用的樣式，並扁平化來源 default style 屬性。
        if (sourceDocument != this)
        {
            ImportReferencedStylesForAdoptedNode(sourceDocument, node);
        }

        // 4. 遞迴更新 Document 與命名空間前綴
        NormalizeAdoptedNodeNamespaces(node);
        UpdateNodeDocument(node, this);

        return node;
    }

    private static void UpdateNodeDocument(OdfNode node, OdfDocument doc)
    {
        node.Document = doc;
        foreach (var child in node.Children)
        {
            UpdateNodeDocument(child, doc);
        }
    }

    private static void NormalizeAdoptedNodeNamespaces(OdfNode node)
    {
        if (node.NodeType == OdfNodeType.Element)
        {
            string elementPrefix = OdfNamespaces.GetPrefix(node.NamespaceUri);
            if (!string.IsNullOrEmpty(elementPrefix) &&
                !string.Equals(node.Prefix, elementPrefix, StringComparison.Ordinal))
            {
                node.Prefix = elementPrefix;
                node.IsModified = true;
            }

            foreach (var attr in node.Attributes)
            {
                string attrPrefix = OdfNamespaces.GetPrefix(attr.Key.NamespaceUri);
                if (!string.IsNullOrEmpty(attrPrefix) &&
                    !string.Equals(node.GetAttributePrefix(attr.Key), attrPrefix, StringComparison.Ordinal))
                {
                    node.SetAttribute(attr.Key.LocalName, attr.Key.NamespaceUri, attr.Value, attrPrefix);
                }
            }
        }

        foreach (var child in node.Children)
        {
            NormalizeAdoptedNodeNamespaces(child);
        }
    }

    private void ImportReferencedStylesForAdoptedNode(OdfDocument sourceDocument, OdfNode node)
    {
        Dictionary<string, string> renameMap = new(StringComparer.Ordinal);
        HashSet<string> imported = new(StringComparer.Ordinal);

        ImportReferencedStylesRecursive(sourceDocument, node, renameMap, imported);
        if (renameMap.Count > 0)
        {
            RemapStylesInNodes(node, renameMap);
        }

        if (imported.Count > 0)
        {
            StyleEngine.RebuildStyleIndex();
        }
    }

    private void ImportReferencedStylesRecursive(
        OdfDocument sourceDocument,
        OdfNode node,
        Dictionary<string, string> renameMap,
        HashSet<string> imported)
    {
        TryImportStyleAttribute(sourceDocument, node, "style-name", OdfNamespaces.Text, renameMap, imported);
        TryImportStyleAttribute(sourceDocument, node, "style-name", OdfNamespaces.Draw, renameMap, imported);
        TryImportStyleAttribute(sourceDocument, node, "style-name", OdfNamespaces.Table, renameMap, imported);
        TryImportStyleAttribute(sourceDocument, node, "presentation-page-layout-name", OdfNamespaces.Presentation, renameMap, imported);

        foreach (OdfNode child in node.Children)
        {
            ImportReferencedStylesRecursive(sourceDocument, child, renameMap, imported);
        }
    }

    private void TryImportStyleAttribute(
        OdfDocument sourceDocument,
        OdfNode node,
        string localName,
        string namespaceUri,
        Dictionary<string, string> renameMap,
        HashSet<string> imported)
    {
        OdfAttributeName attributeName = new(localName, namespaceUri);
        if (node.Attributes.TryGetValue(attributeName, out string? styleName) &&
            !string.IsNullOrWhiteSpace(styleName))
        {
            ImportStyleByName(sourceDocument, styleName, renameMap, imported, []);
        }
    }

    private string? ImportStyleByName(
        OdfDocument sourceDocument,
        string styleName,
        Dictionary<string, string> renameMap,
        HashSet<string> imported,
        HashSet<string> visiting)
    {
        if (renameMap.TryGetValue(styleName, out string? mappedName))
        {
            return mappedName;
        }

        if (!visiting.Add(styleName))
        {
            return styleName;
        }

        if (!TryFindSourceStyle(sourceDocument, styleName, out OdfNode? sourceStyle, out OdfStyleImportLocation location))
        {
            visiting.Remove(styleName);
            return styleName;
        }

        OdfNode clone = sourceStyle.CloneNode(true);
        string importedName = styleName;
        if (DestinationStyleExists(importedName) || imported.Contains(importedName))
        {
            importedName = GenerateAdoptedStyleName(styleName);
            clone.SetAttribute("name", OdfNamespaces.Style, importedName, "style");
            renameMap[styleName] = importedName;
        }

        string? parentName = clone.GetAttribute("parent-style-name", OdfNamespaces.Style);
        if (parentName != null && parentName.Trim().Length > 0)
        {
            string sourceParentName = parentName;
            string? importedParentName = ImportStyleByName(sourceDocument, sourceParentName, renameMap, imported, visiting);
            if (!string.IsNullOrWhiteSpace(importedParentName) &&
                !string.Equals(sourceParentName, importedParentName, StringComparison.Ordinal))
            {
                clone.SetAttribute("parent-style-name", OdfNamespaces.Style, importedParentName!, "style");
            }
        }

        FlattenDefaultStyleProperties(clone, sourceDocument);
        AppendImportedStyle(clone, location);
        imported.Add(importedName);
        visiting.Remove(styleName);
        return importedName;
    }

    private bool TryFindSourceStyle(
        OdfDocument sourceDocument,
        string styleName,
        out OdfNode style,
        out OdfStyleImportLocation location)
    {
        OdfNode? contentAutomaticStyles = FindChildElement(sourceDocument.ContentDom, "automatic-styles", OdfNamespaces.Office);
        if (TryFindStyleByName(contentAutomaticStyles, styleName, out style))
        {
            location = OdfStyleImportLocation.ContentAutomaticStyles;
            return true;
        }

        OdfNode? commonStyles = FindChildElement(sourceDocument.StylesDom, "styles", OdfNamespaces.Office);
        if (TryFindStyleByName(commonStyles, styleName, out style))
        {
            location = OdfStyleImportLocation.CommonStyles;
            return true;
        }

        OdfNode? stylesAutomaticStyles = FindChildElement(sourceDocument.StylesDom, "automatic-styles", OdfNamespaces.Office);
        if (TryFindStyleByName(stylesAutomaticStyles, styleName, out style))
        {
            location = OdfStyleImportLocation.StylesAutomaticStyles;
            return true;
        }

        style = null!;
        location = OdfStyleImportLocation.CommonStyles;
        return false;
    }

    private static bool TryFindStyleByName(OdfNode? container, string styleName, out OdfNode style)
    {
        if (container is not null)
        {
            foreach (OdfNode child in container.Children)
            {
                if (child.NodeType == OdfNodeType.Element &&
                    child.LocalName == "style" &&
                    child.NamespaceUri == OdfNamespaces.Style &&
                    string.Equals(child.GetAttribute("name", OdfNamespaces.Style), styleName, StringComparison.Ordinal))
                {
                    style = child;
                    return true;
                }
            }
        }

        style = null!;
        return false;
    }

    private void AppendImportedStyle(OdfNode style, OdfStyleImportLocation location)
    {
        OdfNode container = location switch
        {
            OdfStyleImportLocation.ContentAutomaticStyles => FindOrCreateChild(ContentDom, "automatic-styles", OdfNamespaces.Office, "office"),
            OdfStyleImportLocation.StylesAutomaticStyles => FindOrCreateChild(StylesDom, "automatic-styles", OdfNamespaces.Office, "office"),
            _ => FindOrCreateChild(StylesDom, "styles", OdfNamespaces.Office, "office")
        };

        container.AppendChild(style);
    }

    private bool DestinationStyleExists(string styleName)
    {
        return StyleEngine.StyleExists(styleName) ||
            TryFindStyleByName(FindChildElement(ContentDom, "automatic-styles", OdfNamespaces.Office), styleName, out _) ||
            TryFindStyleByName(FindChildElement(StylesDom, "styles", OdfNamespaces.Office), styleName, out _) ||
            TryFindStyleByName(FindChildElement(StylesDom, "automatic-styles", OdfNamespaces.Office), styleName, out _);
    }

    private string GenerateAdoptedStyleName(string baseName)
    {
        int index = 1;
        string candidate;
        do
        {
            candidate = $"{baseName}_adopt{index++}";
        }
        while (DestinationStyleExists(candidate));

        return candidate;
    }

    private static void FlattenDefaultStyleProperties(OdfNode style, OdfDocument sourceDocument)
    {
        string family = style.GetAttribute("family", OdfNamespaces.Style) ?? "paragraph";
        OdfNode? defaultStyle = FindDefaultStyle(sourceDocument.StylesDom, family);
        if (defaultStyle is null)
        {
            return;
        }

        foreach (OdfNode defaultPropertyNode in defaultStyle.Children)
        {
            if (!IsStylePropertiesNode(defaultPropertyNode))
            {
                continue;
            }

            OdfNode? targetPropertyNode = FindChildElement(style, defaultPropertyNode.LocalName, defaultPropertyNode.NamespaceUri);
            if (targetPropertyNode is null)
            {
                style.AppendChild(defaultPropertyNode.CloneNode(true));
                continue;
            }

            foreach (KeyValuePair<OdfAttributeName, string> attribute in defaultPropertyNode.Attributes)
            {
                if (!targetPropertyNode.Attributes.ContainsKey(attribute.Key))
                {
                    string prefix = defaultPropertyNode.GetAttributePrefix(attribute.Key) ?? OdfNamespaces.GetPrefix(attribute.Key.NamespaceUri);
                    targetPropertyNode.SetAttribute(attribute.Key.LocalName, attribute.Key.NamespaceUri, attribute.Value, prefix);
                }
            }
        }
    }

    private static OdfNode? FindDefaultStyle(OdfNode stylesRoot, string family)
    {
        OdfNode? styles = FindChildElement(stylesRoot, "styles", OdfNamespaces.Office);
        if (styles is null)
        {
            return null;
        }

        foreach (OdfNode child in styles.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.LocalName == "default-style" &&
                child.NamespaceUri == OdfNamespaces.Style &&
                string.Equals(child.GetAttribute("family", OdfNamespaces.Style), family, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static OdfNode? FindChildElement(OdfNode parent, string localName, string namespaceUri)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return child;
            }
        }

        return null;
    }

    private static bool IsStylePropertiesNode(OdfNode node)
    {
        return node.NodeType == OdfNodeType.Element &&
            node.NamespaceUri == OdfNamespaces.Style &&
            node.LocalName.EndsWith("-properties", StringComparison.Ordinal);
    }

    /// <summary>
    /// 將另一份 ODF 文件附加到目前文件。
    /// </summary>
    /// <param name="otherDoc">要附加的來源文件</param>
    /// <param name="options">合併選項</param>
    public virtual void AppendDocument(OdfDocument otherDoc, OdfMergeOptions? options = null)
        => OdfDocumentMergeEngine.AppendDocument(MergeCollaborators, otherDoc, options ?? OdfMergeOptions.Default);


    #endregion

    #region Internal Merging Helpers


    /// <summary>
    /// 尋找或建立指定子元素。
    /// </summary>
    /// <param name="parent">父節點</param>
    /// <param name="localName">子元素區域名稱</param>
    /// <param name="ns">子元素命名空間 URI</param>
    /// <param name="prefix">子元素前綴</param>
    /// <returns>符合條件的既有或新建子元素</returns>
    protected OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
    {
        foreach (OdfNode child in parent.Children)
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
    /// <param name="sourceDoc">來源文件</param>
    /// <param name="options">合併選項</param>
    /// <param name="renameMap">樣式重新命名對照表</param>
    protected abstract void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap);

    /// <summary>
    /// 依樣式重新命名對照表重寫節點樹中的樣式參照。
    /// </summary>
    /// <param name="node">要處理的根節點</param>
    /// <param name="renameMap">樣式重新命名對照表</param>
    protected void RemapStylesInNodes(OdfNode node, Dictionary<string, string> renameMap)
        => OdfDocumentStyleRemapEngine.RemapStylesInNodes(node, renameMap);


    #endregion

}

internal enum OdfStyleImportLocation
{
    ContentAutomaticStyles,
    CommonStyles,
    StylesAutomaticStyles
}
