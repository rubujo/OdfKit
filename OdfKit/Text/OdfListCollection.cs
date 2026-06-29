using System;
using System.Collections;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Provides APIs for odf list collection.
/// 提供清單新增入口。
/// </summary>
public sealed class OdfListCollection : IEnumerable<OdfList>
{
    private readonly TextDocument _document;

    /// <summary>
    /// Provides odf list collection.
    /// 初始化 <see cref="OdfListCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">The value to use. / 所屬文字文件</param>
    public OdfListCollection(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// Adds add.
    /// 新增清單。
    /// </summary>
    /// <param name="styleName">The name or identifier. / 選用的清單樣式名稱</param>
    /// <returns>The result. / 新增完成的清單</returns>
    public OdfList Add(string? styleName = null)
    {
        return _document.AddList(styleName);
    }

    /// <summary>
    /// Gets this member.
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
    /// Gets get enumerator.
    /// 取得清單列舉器，供 LINQ 查詢使用。
    /// </summary>
    /// <returns>The result. / 清單列舉器</returns>
    public IEnumerator<OdfList> GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
