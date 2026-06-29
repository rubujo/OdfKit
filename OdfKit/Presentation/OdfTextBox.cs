using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Presentation;

/// <summary>
/// Represents a text box shape in a slide.
/// 表示投影片中的文字方塊圖形。
/// </summary>
/// <param name="node">The underlying <see cref="OdfNode"/> instance. / 底層的 <see cref="OdfNode"/> 執行個體。</param>
/// <param name="doc">The owning document instance. / 所屬的文件執行個體。</param>
/// <param name="slide">The owning slide instance, or <c>null</c> when the shape does not belong to a presentation slide. / 所屬的投影片執行個體，若不屬於簡報投影片則為 <c>null</c>。</param>
public class OdfTextBox(OdfNode node, OdfDocument doc, OdfSlide? slide) : OdfShape(node, doc, slide)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OdfTextBox"/> class.
    /// 初始化 <see cref="OdfTextBox"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">The underlying <see cref="OdfNode"/> instance. / 底層的 <see cref="OdfNode"/> 執行個體。</param>
    /// <param name="slide">The owning slide instance. / 所屬的投影片執行個體。</param>
    public OdfTextBox(OdfNode node, OdfSlide slide) : this(node, slide?.Document!, slide) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfTextBox"/> class.
    /// 初始化 <see cref="OdfTextBox"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">The underlying <see cref="OdfNode"/> instance. / 底層的 <see cref="OdfNode"/> 執行個體。</param>
    /// <param name="doc">The owning ODF document instance. / 所屬的 ODF 文件執行個體。</param>
    public OdfTextBox(OdfNode node, OdfDocument doc) : this(node, doc, null) { }

    /// <summary>
    /// Gets the plain text content in the text box.
    /// 取得文字方塊中的純文字內容。
    /// </summary>
    /// <remarks>
    /// Multiple paragraphs are joined with <see cref="Environment.NewLine"/>.
    /// 多段落會以 <see cref="Environment.NewLine"/> 串接。
    /// </remarks>
    public string Text => string.Join(Environment.NewLine, Paragraphs);

    /// <summary>
    /// Gets the paragraph text list in the text box.
    /// 取得文字方塊中的段落文字清單。
    /// </summary>
    public IReadOnlyList<string> Paragraphs
    {
        get
        {
            var paragraphs = new List<string>();
            AddParagraphs(Node, paragraphs);
            return paragraphs.AsReadOnly();
        }
    }

    private static void AddParagraphs(OdfNode node, List<string> paragraphs)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "p" &&
                child.NamespaceUri == OdfNamespaces.Text)
            {
                paragraphs.Add(child.TextContent);
            }

            AddParagraphs(child, paragraphs);
        }
    }
}
