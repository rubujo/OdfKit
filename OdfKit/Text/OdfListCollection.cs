using System;
using System.Collections;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Adds text lists to text documents.
/// 提供清單新增入口。
/// </summary>
public sealed class OdfListCollection : IEnumerable<OdfList>
{
    private readonly TextDocument _document;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfListCollection"/> class.
    /// 初始化 <see cref="OdfListCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">The owning text document. / 所屬文字文件。</param>
    public OdfListCollection(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }
    /// <summary>
    /// Short overload of Add that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：Add 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfList Add() => Add(null);


    /// <summary>
    /// Adds a text list to the collection.
    /// 新增清單。
    /// </summary>
    /// <param name="styleName">The optional list style name. / 選用的清單樣式名稱。</param>
    /// <returns>The newly added list. / 新增完成的清單。</returns>
    public OdfList Add(string? styleName)
    {
        return _document.AddList(styleName);
    }


    /// <summary>
    /// Gets a summary list of the top-level lists in the document body.
    /// 取得文件本文最上層清單清單。
    /// </summary>
    public IReadOnlyList<OdfList> Items
    {
        get
        {
            List<OdfList> lists = [];
            foreach (OdfNode child in _document.BodyTextRoot.Children)
            {
                if (child.NodeType is OdfNodeType.Element &&
                    child.LocalName == "list" &&
                    child.NamespaceUri == OdfNamespaces.Text)
                {
                    lists.Add(new OdfList(child, _document));
                }
            }

            return lists.AsReadOnly();
        }
    }

    /// <summary>
    /// Finds the first top-level list that satisfies the predicate.
    /// 查找第一個符合條件的最上層清單。
    /// </summary>
    /// <param name="predicate">The list predicate. / 清單條件。</param>
    /// <returns>The matching list, or <see langword="null"/> when no match exists. / 符合的清單；若找不到則為 <see langword="null"/>。</returns>
    public OdfList? Find(Predicate<OdfList> predicate)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(predicate, nameof(predicate));

        foreach (OdfList list in Items)
        {
            if (predicate(list))
            {
                return list;
            }
        }

        return null;
    }

    /// <summary>
    /// Removes the specified top-level list.
    /// 移除指定的最上層清單。
    /// </summary>
    /// <param name="list">The list to remove. / 要移除的清單。</param>
    /// <returns><see langword="true"/> if the list was removed; otherwise, <see langword="false"/>. / 若已移除清單則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool Remove(OdfList list)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(list, nameof(list));

        return ReferenceEquals(list.Node.Parent, _document.BodyTextRoot) &&
            _document.BodyTextRoot.RemoveChild(list.Node);
    }

    /// <summary>
    /// Removes all top-level lists while preserving other body content.
    /// 移除所有最上層清單，並保留其他本文內容。
    /// </summary>
    public void Clear()
    {
        foreach (OdfList list in Items)
        {
            _document.BodyTextRoot.RemoveChild(list.Node);
        }
    }

    /// <summary>
    /// Gets an enumerator over the lists, for use with LINQ queries.
    /// 取得清單列舉器，供 LINQ 查詢使用。
    /// </summary>
    /// <returns>The list enumerator. / 清單列舉器。</returns>
    public IEnumerator<OdfList> GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
