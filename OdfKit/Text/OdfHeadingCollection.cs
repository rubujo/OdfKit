using System;
using System.Collections;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Adds heading paragraphs to text documents.
/// 提供標題新增入口。
/// </summary>
public sealed class OdfHeadingCollection : IEnumerable<OdfHeading>
{
    private readonly TextDocument _document;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfHeadingCollection"/> class.
    /// 初始化 <see cref="OdfHeadingCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">The owning text document. / 所屬文字文件。</param>
    public OdfHeadingCollection(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }
    /// <summary>
    /// Short overload of Add that accepts text; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 text；其餘可選參數使用預設值並轉呼叫最長 Add 多載。
    /// </summary>
    public OdfHeading Add(string text) => Add(text, 1);


    /// <summary>
    /// Adds a heading paragraph to the collection.
    /// 新增標題。
    /// </summary>
    /// <param name="text">The heading text. / 標題文字。</param>
    /// <param name="outlineLevel">The outline level. / 大綱階層。</param>
    /// <returns>The newly added heading. / 新增完成的標題。</returns>
    public OdfHeading Add(string text, int outlineLevel)
    {
        return _document.AddHeading(text, outlineLevel);
    }


    /// <summary>
    /// Gets a summary list of the top-level headings in the document body.
    /// 取得文件本文最上層標題清單。
    /// </summary>
    public IReadOnlyList<OdfHeading> Items
    {
        get
        {
            List<OdfHeading> headings = [];
            foreach (OdfNode child in _document.BodyTextRoot.Children)
            {
                if (child.NodeType is OdfNodeType.Element &&
                    child.LocalName == "h" &&
                    child.NamespaceUri == OdfNamespaces.Text)
                {
                    headings.Add(new OdfHeading(child, _document));
                }
            }

            return headings.AsReadOnly();
        }
    }

    /// <summary>
    /// Finds the first top-level heading that satisfies the predicate.
    /// 查找第一個符合條件的最上層標題。
    /// </summary>
    /// <param name="predicate">The heading predicate. / 標題條件。</param>
    /// <returns>The matching heading, or <see langword="null"/> when no match exists. / 符合的標題；若找不到則為 <see langword="null"/>。</returns>
    public OdfHeading? Find(Predicate<OdfHeading> predicate)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(predicate, nameof(predicate));

        foreach (OdfHeading heading in Items)
        {
            if (predicate(heading))
            {
                return heading;
            }
        }

        return null;
    }

    /// <summary>
    /// Removes the specified top-level heading.
    /// 移除指定的最上層標題。
    /// </summary>
    /// <param name="heading">The heading to remove. / 要移除的標題。</param>
    /// <returns><see langword="true"/> if the heading was removed; otherwise, <see langword="false"/>. / 若已移除標題則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool Remove(OdfHeading heading)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(heading, nameof(heading));

        return ReferenceEquals(heading.Node.Parent, _document.BodyTextRoot) &&
            _document.BodyTextRoot.RemoveChild(heading.Node);
    }

    /// <summary>
    /// Removes all top-level headings while preserving other body content.
    /// 移除所有最上層標題，並保留其他本文內容。
    /// </summary>
    public void Clear()
    {
        foreach (OdfHeading heading in Items)
        {
            _document.BodyTextRoot.RemoveChild(heading.Node);
        }
    }

    /// <summary>
    /// Gets an enumerator over the headings, for use with LINQ queries.
    /// 取得標題列舉器，供 LINQ 查詢使用。
    /// </summary>
    /// <returns>The heading enumerator. / 標題列舉器。</returns>
    public IEnumerator<OdfHeading> GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
