using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Represents the abstract base class for indexes in an ODF document.
/// 表示 ODF 文件中索引的抽象基底類別。
/// </summary>
public abstract class OdfIndex
{
    /// <summary>
    /// Gets the OdfNode associated with this index.
    /// 取得與此索引相關聯的 OdfNode 節點。
    /// </summary>
    public OdfNode Node { get; }

    /// <summary>
    /// The owning text document.
    /// 所屬的文字文件。
    /// </summary>
    protected readonly TextDocument Doc;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfIndex"/> class.
    /// 初始化 <see cref="OdfIndex"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">The associated OdfNode. / 相關聯的 OdfNode 節點。</param>
    /// <param name="doc">The owning text document. / 所屬的文字文件。</param>
    protected OdfIndex(OdfNode node, TextDocument doc)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Doc = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    /// <summary>
    /// Gets or sets the name of the index.
    /// 取得或設定索引的名稱。
    /// </summary>
    public string Name
    {
        get => Node.GetAttribute("name", OdfNamespaces.Text) ?? string.Empty;
        set => Node.SetAttribute("name", OdfNamespaces.Text, value, "text");
    }

    /// <summary>
    /// Gets the index's source node.
    /// 取得索引的來源節點。
    /// </summary>
    public OdfNode? SourceNode => FindChild(Node, GetSourceLocalName(), OdfNamespaces.Text);

    /// <summary>
    /// Gets the index's body node.
    /// 取得索引的本文節點。
    /// </summary>
    public OdfNode? BodyNode => FindChild(Node, "index-body", OdfNamespaces.Text);

    /// <summary>
    /// Gets the XML local name of the index source node.
    /// 取得索引來源節點的 XML 本地名稱。
    /// </summary>
    /// <returns>The XML local name. / XML 本地名稱。</returns>
    protected abstract string GetSourceLocalName();

    /// <summary>
    /// Finds the first child matching the specified XML local name and namespace.
    /// 尋找符合指定 XML 本地名稱與命名空間的第一個子專案。
    /// </summary>
    /// <param name="parent">The parent node. / 父節點。</param>
    /// <param name="localName">The XML local name. / XML 本地名稱。</param>
    /// <param name="ns">The namespace URI. / 命名空間 URI。</param>
    /// <returns>The matching child, or <c>null</c> if none exists. / 符合條件的子專案，若無則傳回 <c>null</c>。</returns>
    protected OdfNode? FindChild(OdfNode parent, string localName, string ns)
    {
        foreach (var child in parent.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
                return child;
        }
        return null;
    }

    /// <summary>
    /// Finds or creates a child matching the specified XML local name and namespace.
    /// 尋找或建立符合指定 XML 本地名稱與命名空間的子專案。
    /// </summary>
    /// <param name="parent">The parent node. / 父節點。</param>
    /// <param name="localName">The XML local name. / XML 本地名稱。</param>
    /// <param name="ns">The namespace URI. / 命名空間 URI。</param>
    /// <param name="prefix">The namespace prefix. / 命名空間前綴。</param>
    /// <returns>The existing or newly created child node. / 現有的或新建立的子節點。</returns>
    protected OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
    {
        var existing = FindChild(parent, localName, ns);
        if (existing is not null)
            return existing;

        var child = OdfNodeFactory.CreateElement(localName, ns, prefix);
        parent.AppendChild(child);
        return child;
    }

    /// <summary>
    /// Updates the index content.
    /// 更新索引內容。
    /// </summary>
    public abstract void Update();
}

