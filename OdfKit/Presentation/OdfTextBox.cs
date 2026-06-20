using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Presentation;

/// <summary>
/// 表示投影片中的文字方塊圖形。
/// </summary>
/// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
/// <param name="doc">所屬的文件執行個體</param>
/// <param name="slide">所屬的投影片執行個體，若不屬於簡報投影片則為 <c>null</c></param>
public class OdfTextBox(OdfNode node, OdfDocument doc, OdfSlide? slide) : OdfShape(node, doc, slide)
{
    /// <summary>
    /// 初始化 <see cref="OdfTextBox"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
    /// <param name="slide">所屬的投影片執行個體</param>
    public OdfTextBox(OdfNode node, OdfSlide slide) : this(node, slide?.Document!, slide) { }

    /// <summary>
    /// 初始化 <see cref="OdfTextBox"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
    /// <param name="doc">所屬的 ODF 文件執行個體</param>
    public OdfTextBox(OdfNode node, OdfDocument doc) : this(node, doc, null) { }

    /// <summary>
    /// 取得文字方塊中的純文字內容。多段落會以 <see cref="Environment.NewLine"/> 串接。
    /// </summary>
    public string Text => string.Join(Environment.NewLine, Paragraphs);

    /// <summary>
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
