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
    /// Short overload of Add that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：Add 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfParagraph Add() => Add("");


    /// <summary>
    /// Adds a paragraph to the collection.
    /// 新增段落。
    /// </summary>
    /// <param name="text">The paragraph text. / 段落文字。</param>
    /// <returns>The newly added paragraph. / 新增完成的段落。</returns>
    public OdfParagraph Add(string text)
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
    /// Finds the first top-level paragraph that satisfies the predicate.
    /// 查找第一個符合條件的最上層段落。
    /// </summary>
    /// <param name="predicate">The paragraph predicate. / 段落條件。</param>
    /// <returns>The matching paragraph, or <see langword="null"/> when no match exists. / 符合的段落；若找不到則為 <see langword="null"/>。</returns>
    public OdfParagraph? Find(Predicate<OdfParagraph> predicate)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(predicate, nameof(predicate));

        foreach (OdfParagraph paragraph in Items)
        {
            if (predicate(paragraph))
            {
                return paragraph;
            }
        }

        return null;
    }

    /// <summary>
    /// Removes the specified top-level paragraph.
    /// 移除指定的最上層段落。
    /// </summary>
    /// <param name="paragraph">The paragraph to remove. / 要移除的段落。</param>
    /// <returns><see langword="true"/> if the paragraph was removed; otherwise, <see langword="false"/>. / 若已移除段落則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool Remove(OdfParagraph paragraph)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(paragraph, nameof(paragraph));

        return ReferenceEquals(paragraph.Node.Parent, _document.BodyTextRoot) &&
            _document.BodyTextRoot.RemoveChild(paragraph.Node);
    }

    /// <summary>
    /// Removes all top-level paragraphs while preserving other body content.
    /// 移除所有最上層段落，並保留其他本文內容。
    /// </summary>
    public void Clear()
    {
        foreach (OdfParagraph paragraph in Items)
        {
            _document.BodyTextRoot.RemoveChild(paragraph.Node);
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
