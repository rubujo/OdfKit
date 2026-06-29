using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Provides APIs for odf text body.
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
    /// Provides odf text body.
    /// 初始化 <see cref="OdfTextBody"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">The value to use. / 所屬文字文件</param>
    public OdfTextBody(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// Gets odf paragraph collection.
    /// 取得段落集合。
    /// </summary>
    public OdfParagraphCollection Paragraphs => _paragraphs ??= new OdfParagraphCollection(_document);

    /// <summary>
    /// Gets odf heading collection.
    /// 取得標題集合。
    /// </summary>
    public OdfHeadingCollection Headings => _headings ??= new OdfHeadingCollection(_document);

    /// <summary>
    /// Gets odf list collection.
    /// 取得清單集合。
    /// </summary>
    public OdfListCollection Lists => _lists ??= new OdfListCollection(_document);

    /// <summary>
    /// Gets odf text table collection.
    /// 取得表格集合。
    /// </summary>
    public OdfTextTableCollection Tables => _tables ??= new OdfTextTableCollection(_document);

    /// <summary>
    /// Gets odf text image collection.
    /// 取得圖片集合。
    /// </summary>
    public OdfTextImageCollection Images => _images ??= new OdfTextImageCollection(_document);

    /// <summary>
    /// Gets this member.
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
