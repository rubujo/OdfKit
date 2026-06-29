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
    /// Adds a heading paragraph to the collection.
    /// 新增標題。
    /// </summary>
    /// <param name="text">The heading text. / 標題文字。</param>
    /// <param name="outlineLevel">The outline level. / 大綱階層。</param>
    /// <returns>The newly added heading. / 新增完成的標題。</returns>
    public OdfHeading Add(string text, int outlineLevel = 1)
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
