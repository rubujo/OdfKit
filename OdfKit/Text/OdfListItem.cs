using System;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Represents odf list item.
/// 表示清單中的清單專案。
/// </summary>
/// <param name="node">The value to use. / 與此清單專案相關聯的 OdfNode 節點</param>
/// <param name="doc">The value to use. / 所屬的文字文件</param>
public class OdfListItem(OdfNode node, TextDocument doc)
{
    /// <summary>
    /// 取得與此清單專案相關聯的 OdfNode 節點。
    /// </summary>
    internal OdfNode Node { get; } = node;

    private readonly TextDocument _doc = doc;

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定此清單專案的起始數值。
    /// </summary>
    public int? StartValue
    {
        get => int.TryParse(Node.GetAttribute("start-value", OdfNamespaces.Text), NumberStyles.Integer, CultureInfo.InvariantCulture, out var val) ? val : null;
        set
        {
            if (value.HasValue)
                Node.SetAttribute("start-value", OdfNamespaces.Text, value.Value.ToString(CultureInfo.InvariantCulture), "text");
            else
                Node.RemoveAttribute("start-value", OdfNamespaces.Text);
        }
    }

    /// <summary>
    /// Provides add paragraph.
    /// 在清單專案中新增段落。
    /// </summary>
    /// <param name="text">The text or value. / 段落的預設內文</param>
    /// <returns>The result. / 建立的段落物件</returns>
    public OdfParagraph AddParagraph(string text = "")
    {
        var pNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        pNode.TextContent = text;
        Node.AppendChild(pNode);
        return new OdfParagraph(pNode, _doc);
    }

    /// <summary>
    /// Provides add nested list.
    /// 在清單專案中新增巢狀清單。
    /// </summary>
    /// <param name="styleName">The name or identifier. / 專案清單樣式名稱</param>
    /// <returns>The result. / 新建立的巢狀清單執行個體</returns>
    public OdfList AddNestedList(string? styleName = null)
    {
        var listNode = OdfNodeFactory.CreateElement("list", OdfNamespaces.Text, "text");
        if (styleName is not null)
        {
            listNode.SetAttribute("style-name", OdfNamespaces.Text, styleName, "text");
        }
        Node.AppendChild(listNode);
        return new OdfList(listNode, _doc);
    }

    /// <summary>
    /// Gets this member.
    /// 取得清單專案中的段落清單。
    /// </summary>
    public IReadOnlyList<OdfParagraph> Paragraphs
    {
        get
        {
            List<OdfParagraph> paragraphs = [];
            foreach (OdfNode child in Node.Children)
            {
                if (child.NodeType is OdfNodeType.Element &&
                    child.LocalName == "p" &&
                    child.NamespaceUri == OdfNamespaces.Text)
                {
                    paragraphs.Add(new OdfParagraph(child, _doc));
                }
            }

            return paragraphs.AsReadOnly();
        }
    }
}
