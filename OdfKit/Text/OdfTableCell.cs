using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Represents odf table cell.
/// 表示表格中的儲存格。
/// </summary>
/// <param name="node">The value to use. / 與此儲存格相關聯的 OdfNode 節點</param>
/// <param name="doc">The value to use. / 所屬的文字文件</param>
public class OdfTableCell(OdfNode node, TextDocument doc)
{
    /// <summary>
    /// 取得與此儲存格相關聯的 OdfNode 節點。
    /// </summary>
    internal OdfNode Node { get; } = node;

    private readonly TextDocument _doc = doc;

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定儲存格的文字內容。
    /// </summary>
    public string TextContent
    {
        get => Node.TextContent;
        set => Node.TextContent = value;
    }

    /// <summary>
    /// Provides add paragraph.
    /// 在儲存格中新增段落。
    /// </summary>
    /// <param name="text">The text or value. / 段落文字內文</param>
    /// <returns>The result. / 新建立的段落物件執行個體</returns>
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
