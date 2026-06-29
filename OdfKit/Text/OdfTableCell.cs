using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Represents a cell within a table.
/// 表示表格中的儲存格。
/// </summary>
/// <param name="node">The <see cref="OdfNode"/> associated with this cell. / 與此儲存格相關聯的 OdfNode 節點。</param>
/// <param name="doc">The owning text document. / 所屬的文字文件。</param>
public class OdfTableCell(OdfNode node, TextDocument doc)
{
    /// <summary>
    /// 取得與此儲存格相關聯的 OdfNode 節點。
    /// </summary>
    internal OdfNode Node { get; } = node;

    private readonly TextDocument _doc = doc;

    /// <summary>
    /// Gets or sets the cell's text content.
    /// 取得或設定儲存格的文字內容。
    /// </summary>
    public string TextContent
    {
        get => Node.TextContent;
        set => Node.TextContent = value;
    }

    /// <summary>
    /// Adds a paragraph to the cell.
    /// 在儲存格中新增段落。
    /// </summary>
    /// <param name="text">The paragraph text content. / 段落文字內文。</param>
    /// <returns>The newly created paragraph instance. / 新建立的段落物件執行個體。</returns>
    public OdfParagraph AddParagraph(string text)
    {
        var pNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        Node.AppendChild(pNode);
        var paragraph = new OdfParagraph(pNode, _doc);
        if (!string.IsNullOrEmpty(text))
            paragraph.AddTextRun(text);

        return paragraph;
    }
}
