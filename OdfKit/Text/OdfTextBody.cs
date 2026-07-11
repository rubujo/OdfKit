using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Exposes high-level accessors for the body of a text document.
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
    /// Initializes a new instance of the <see cref="OdfTextBody"/> class.
    /// 初始化 <see cref="OdfTextBody"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">The owning text document. / 所屬文字文件。</param>
    public OdfTextBody(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// Gets the paragraph collection.
    /// 取得段落集合。
    /// </summary>
    public OdfParagraphCollection Paragraphs => _paragraphs ??= new OdfParagraphCollection(_document);

    /// <summary>
    /// Gets the heading collection.
    /// 取得標題集合。
    /// </summary>
    public OdfHeadingCollection Headings => _headings ??= new OdfHeadingCollection(_document);

    /// <summary>
    /// Gets the list collection.
    /// 取得清單集合。
    /// </summary>
    public OdfListCollection Lists => _lists ??= new OdfListCollection(_document);

    /// <summary>
    /// Gets the table collection.
    /// 取得表格集合。
    /// </summary>
    public OdfTextTableCollection Tables => _tables ??= new OdfTextTableCollection(_document);

    /// <summary>
    /// Gets the image collection.
    /// 取得圖片集合。
    /// </summary>
    public OdfTextImageCollection Images => _images ??= new OdfTextImageCollection(_document);

    /// <summary>
    /// Gets the collection of all sections in the document.
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

    /// <summary>
    /// Finds the first section with the exact name.
    /// 尋找第一個具有精確名稱的區段。
    /// </summary>
    /// <param name="name">The exact section name. / 區段的精確名稱。</param>
    /// <returns>The matching section, or <see langword="null"/>. / 相符的區段；若不存在則為 <see langword="null"/>。</returns>
    public OdfSection? FindSection(string name)
    {
        foreach (OdfSection section in Sections)
        {
            if (string.Equals(section.Name, name, StringComparison.Ordinal))
                return section;
        }

        return null;
    }

    /// <summary>
    /// Removes the specified section from this document.
    /// 從此文件移除指定區段。
    /// </summary>
    /// <param name="section">The section to remove. / 要移除的區段。</param>
    /// <returns><see langword="true"/> if the section was removed; otherwise <see langword="false"/>. / 若已移除區段則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveSection(OdfSection section)
    {
        if (section is null)
            throw new ArgumentNullException(nameof(section));
        OdfNode? parent = section.Node.Parent;
        return parent is not null && parent.RemoveChild(section.Node);
    }

    /// <summary>
    /// Removes all sections from the document and returns the number removed.
    /// 移除文件中的所有區段，並傳回移除數量。
    /// </summary>
    /// <returns>The number of removed sections. / 已移除的區段數量。</returns>
    public int ClearSections()
    {
        IReadOnlyList<OdfSection> sections = Sections;
        int removed = 0;
        foreach (OdfSection section in sections)
        {
            if (RemoveSection(section))
                removed++;
        }

        return removed;
    }
}
