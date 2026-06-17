using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件腳注與尾注讀取引擎（內部協作者）。
/// </summary>
internal static class TextDocumentFootnoteReadEngine
{
    internal static IReadOnlyList<OdfFootnoteInfo> GetFootnotes(OdfNode bodyTextRoot) =>
        CollectNotes(bodyTextRoot, "footnote");

    internal static IReadOnlyList<OdfFootnoteInfo> GetEndnotes(OdfNode bodyTextRoot) =>
        CollectNotes(bodyTextRoot, "endnote");

    private static List<OdfFootnoteInfo> CollectNotes(OdfNode node, string noteClass)
    {
        List<OdfFootnoteInfo> notes = [];
        ScanNotes(node, noteClass, notes);
        return notes;
    }

    private static void ScanNotes(OdfNode node, string noteClass, List<OdfFootnoteInfo> notes)
    {
        if (node.NodeType is OdfNodeType.Element &&
            node.LocalName is "note" &&
            node.NamespaceUri == OdfNamespaces.Text &&
            node.GetAttribute("note-class", OdfNamespaces.Text) == noteClass)
        {
            string? id = node.GetAttribute("id", OdfNamespaces.Text);
            if (!string.IsNullOrEmpty(id))
            {
                notes.Add(new OdfFootnoteInfo(
                    id!,
                    ReadNoteChildText(node, "note-citation"),
                    ReadNoteChildText(node, "note-body")));
            }
        }

        foreach (OdfNode child in node.Children)
            ScanNotes(child, noteClass, notes);
    }

    private static string ReadNoteChildText(OdfNode noteNode, string localName)
    {
        foreach (OdfNode child in noteNode.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName != localName ||
                child.NamespaceUri != OdfNamespaces.Text)
                continue;

            foreach (OdfNode paragraph in child.Children)
            {
                if (paragraph.NodeType is OdfNodeType.Element &&
                    paragraph.LocalName is "p" &&
                    paragraph.NamespaceUri == OdfNamespaces.Text)
                    return paragraph.TextContent ?? string.Empty;
            }

            return child.TextContent ?? string.Empty;
        }

        return string.Empty;
    }
}
