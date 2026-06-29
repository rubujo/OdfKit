using System;
using System.Collections;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Adds paragraphs to text documents.
/// 提供段落新增入口。
/// </summary>
public sealed class OdfParagraphCollection : IEnumerable<OdfParagraph>
{
    private readonly TextDocument _document;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfParagraphCollection"/> class.
    /// 初始化 <see cref="OdfParagraphCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">The owning text document. / 所屬文字文件。</param>
    public OdfParagraphCollection(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// Adds a paragraph to the collection.
    /// 新增段落。
    /// </summary>
    /// <param name="text">The paragraph text. / 段落文字。</param>
    /// <returns>The newly added paragraph. / 新增完成的段落。</returns>
    public OdfParagraph Add(string text = "")
    {
        return _document.AddParagraph(text);
    }

    /// <summary>
    /// Gets a summary list of the top-level paragraphs in the document body.
    /// 取得文件本文最上層段落清單。
    /// </summary>
    public IReadOnlyList<OdfParagraph> Items
    {
        get
        {
            List<OdfParagraph> paragraphs = [];
            foreach (OdfNode child in _document.BodyTextRoot.Children)
            {
                if (child.NodeType is OdfNodeType.Element &&
                    child.LocalName == "p" &&
                    child.NamespaceUri == OdfNamespaces.Text)
                {
                    paragraphs.Add(new OdfParagraph(child, _document));
                }
            }

            return paragraphs.AsReadOnly();
        }
    }

    /// <summary>
    /// Gets an enumerator over the paragraphs, for use with LINQ queries.
    /// 取得段落列舉器，供 LINQ 查詢使用。
    /// </summary>
    /// <returns>The paragraph enumerator. / 段落列舉器。</returns>
    public IEnumerator<OdfParagraph> GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
