using System;
using System.Collections;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 提供段落新增入口。
/// </summary>
public sealed class OdfParagraphCollection : IEnumerable<OdfParagraph>
{
    private readonly TextDocument _document;

    /// <summary>
    /// 初始化 <see cref="OdfParagraphCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">所屬文字文件</param>
    public OdfParagraphCollection(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 新增段落。
    /// </summary>
    /// <param name="text">段落文字</param>
    /// <returns>新增完成的段落</returns>
    public OdfParagraph Add(string text = "")
    {
        return _document.AddParagraph(text);
    }

    /// <summary>
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
    /// 取得段落列舉器，供 LINQ 查詢使用。
    /// </summary>
    /// <returns>段落列舉器</returns>
    public IEnumerator<OdfParagraph> GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
