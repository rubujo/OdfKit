using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Writes batches of paragraphs into a pre-bound text body root node.
/// 提供預先綁定至文字本文根節點的批次段落寫入器。
/// </summary>
/// <remarks>
/// 此寫入器適合大量建立單純段落；它直接追加 <c>text:p</c> 節點，不建立
/// <see cref="OdfParagraph"/> facade，避免高頻段落輸出時產生額外 wrapper 物件。
/// </remarks>
public sealed class OdfParagraphPrebindingWriter
{
    private readonly TextDocumentMutationContext _context;
    private readonly string? _styleName;

    internal OdfParagraphPrebindingWriter(TextDocumentMutationContext context, string? styleName)
    {
        _context = context;
        _styleName = string.IsNullOrWhiteSpace(styleName) ? null : styleName;
    }

    /// <summary>
    /// Gets the number of paragraphs appended through this writer.
    /// 取得已透過此寫入器追加的段落數量。
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Appends a paragraph and returns the current writer, for chained calls.
    /// 追加一個段落並傳回目前寫入器，以便鏈式呼叫。
    /// </summary>
    /// <param name="text">The paragraph text content. / 段落文字內容。</param>
    /// <returns>The current writer. / 目前寫入器。</returns>
    public OdfParagraphPrebindingWriter Add(string text)
    {
        var paragraphNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        paragraphNode.TextContent = text ?? string.Empty;
        if (_styleName is not null)
        {
            paragraphNode.SetAttribute("style-name", OdfNamespaces.Text, _styleName, "text");
        }

        if (_context.TrackedChanges)
        {
            string changeId = _context.RecordTrackedChange("insertion");
            var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
            startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
            var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
            endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");

            _context.BodyTextRoot.AppendChild(startNode);
            _context.BodyTextRoot.AppendChild(paragraphNode);
            _context.BodyTextRoot.AppendChild(endNode);
        }
        else
        {
            _context.BodyTextRoot.AppendChild(paragraphNode);
        }

        Count++;
        return this;
    }
}
