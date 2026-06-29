using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Represents odf index.
/// 表示 ODF 文件中索引的抽象基底類別。
/// </summary>
public abstract class OdfIndex
{
    /// <summary>
    /// Gets node.
    /// 取得與此索引相關聯的 OdfNode 節點。
    /// </summary>
    public OdfNode Node { get; }

    /// <summary>
    /// Provides doc.
    /// 所屬的文字文件。
    /// </summary>
    protected readonly TextDocument Doc;

    /// <summary>
    /// Provides odf index.
    /// 初始化 <see cref="OdfIndex"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">The value to use. / 相關聯的 OdfNode 節點</param>
    /// <param name="doc">The value to use. / 所屬的文字文件</param>
    protected OdfIndex(OdfNode node, TextDocument doc)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Doc = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定索引的名稱。
    /// </summary>
    public string Name
    {
        get => Node.GetAttribute("name", OdfNamespaces.Text) ?? string.Empty;
        set => Node.SetAttribute("name", OdfNamespaces.Text, value, "text");
    }

    /// <summary>
    /// Gets find child.
    /// 取得索引的來源節點。
    /// </summary>
    public OdfNode? SourceNode => FindChild(Node, GetSourceLocalName(), OdfNamespaces.Text);

    /// <summary>
    /// Gets find child.
    /// 取得索引的本文節點。
    /// </summary>
    public OdfNode? BodyNode => FindChild(Node, "index-body", OdfNamespaces.Text);

    /// <summary>
    /// Gets get source local name.
    /// 取得索引來源節點的 XML 本地名稱。
    /// </summary>
    /// <returns>The result. / XML 本地名稱</returns>
    protected abstract string GetSourceLocalName();

    /// <summary>
    /// Provides find child.
    /// 尋找符合指定 XML 本地名稱與命名空間的第一個子專案。
    /// </summary>
    /// <param name="parent">The value to use. / 父節點</param>
    /// <param name="localName">The name or identifier. / XML 本地名稱</param>
    /// <param name="ns">The value to use. / 命名空間 URI</param>
    /// <returns>The result. / 符合條件的子專案，若無則傳回 <c>null</c></returns>
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
    /// Provides find or create child.
    /// 尋找或建立符合指定 XML 本地名稱與命名空間的子專案。
    /// </summary>
    /// <param name="parent">The value to use. / 父節點</param>
    /// <param name="localName">The name or identifier. / XML 本地名稱</param>
    /// <param name="ns">The value to use. / 命名空間 URI</param>
    /// <param name="prefix">The value to use. / 命名空間前綴</param>
    /// <returns>The result. / 現有的或新建立的子節點</returns>
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
    /// Provides update.
    /// 更新索引內容。
    /// </summary>
    public abstract void Update();
}

