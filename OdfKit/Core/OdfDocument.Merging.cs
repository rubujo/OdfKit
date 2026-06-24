using System;
using System.Collections.Generic;
using OdfKit.DOM;

namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    #region Document Merging API


    /// <summary>
    /// 採納來源節點至目前文件，並回傳可安全掛載於目前文件 DOM 的節點副本。
    /// </summary>
    /// <param name="node">要採納的來源節點</param>
    /// <returns>已完成採納的節點副本</returns>
    /// <remarks>
    /// 當來源節點來自另一份文件時，此方法會透過 <see cref="OdfNode.ImportNode(OdfNode, OdfPackage?, OdfPackage?)"/>
    /// 自動處理必要的媒體參照移轉。呼叫端可將回傳節點掛載到目前文件任一合法位置。
    /// </remarks>
    public virtual OdfNode AdoptNode(OdfNode node)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        return OdfNode.ImportNode(node, sourcePackage: null, destPackage: Package);
    }

    /// <summary>
    /// 採納來源文件中的節點至目前文件，並回傳可安全掛載的節點副本。
    /// </summary>
    /// <param name="sourceDocument">來源文件</param>
    /// <param name="node">要採納的來源節點</param>
    /// <returns>已完成採納的節點副本</returns>
    /// <remarks>
    /// 來源文件資訊可用於跨文件媒體參照移轉，避免嵌入圖片或物件在採納後失聯。
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

        return OdfNode.ImportNode(node, sourceDocument.Package, Package);
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
