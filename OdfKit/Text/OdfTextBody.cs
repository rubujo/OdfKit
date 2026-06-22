using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 提供文字文件本文的高階操作入口。
/// </summary>
public sealed class OdfTextBody
{
    private readonly TextDocument _document;
    private OdfParagraphCollection? _paragraphs;
    private OdfHeadingCollection? _headings;
    private OdfListCollection? _lists;
    private OdfTextTableCollection? _tables;
    private OdfTextImageCollection? _images;

    /// <summary>
    /// 初始化 <see cref="OdfTextBody"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">所屬文字文件</param>
    public OdfTextBody(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 取得段落集合。
    /// </summary>
    public OdfParagraphCollection Paragraphs => _paragraphs ??= new OdfParagraphCollection(_document);

    /// <summary>
    /// 取得標題集合。
    /// </summary>
    public OdfHeadingCollection Headings => _headings ??= new OdfHeadingCollection(_document);

    /// <summary>
    /// 取得清單集合。
    /// </summary>
    public OdfListCollection Lists => _lists ??= new OdfListCollection(_document);

    /// <summary>
    /// 取得表格集合。
    /// </summary>
    public OdfTextTableCollection Tables => _tables ??= new OdfTextTableCollection(_document);

    /// <summary>
    /// 取得圖片集合。
    /// </summary>
    public OdfTextImageCollection Images => _images ??= new OdfTextImageCollection(_document);

    /// <summary>
    /// 取得文件中的所有區段（Section）集合。
    /// </summary>
    public IReadOnlyList<OdfSection> Sections
    {
        get
        {
            var sections = new List<OdfSection>();
            var nodes = _document.BodyTextRoot.Descendants()
                .Where(n => n.NodeType == OdfNodeType.Element &&
                            n.LocalName == "section" &&
                            n.NamespaceUri == OdfNamespaces.Text);
            foreach (var node in nodes)
            {
                sections.Add(new OdfSection(node, _document));
            }
            return sections;
        }
    }
}
