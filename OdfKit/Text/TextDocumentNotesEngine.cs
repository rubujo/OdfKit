using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

/// <summary>
/// 文字文件腳注、索引與內嵌元素引擎（內部協作者）。
/// </summary>
internal static class TextDocumentNotesEngine
{
    internal static void AddFootnote(TextDocumentMutationContext context, OdfParagraph paragraph, string citation, string bodyText)
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (citation is null)
            throw new ArgumentNullException(nameof(citation));
        if (bodyText is null)
            throw new ArgumentNullException(nameof(bodyText));
        AppendNote(paragraph, "footnote", context.NextFootnoteId(), citation, bodyText);
    }

    internal static void AddEndnote(TextDocumentMutationContext context, OdfParagraph paragraph, string citation, string bodyText)
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (citation is null)
            throw new ArgumentNullException(nameof(citation));
        if (bodyText is null)
            throw new ArgumentNullException(nameof(bodyText));
        AppendNote(paragraph, "endnote", context.NextEndnoteId(), citation, bodyText);
    }

    internal static OdfAlphabeticalIndex AddAlphabeticalIndex(TextDocument document, TextDocumentMutationContext context, string title)
    {
        var idxNode = OdfNodeFactory.CreateElement("alphabetical-index", OdfNamespaces.Text, "text");
        idxNode.SetAttribute("name", OdfNamespaces.Text, title, "text");

        var sourceNode = OdfNodeFactory.CreateElement("alphabetical-index-source", OdfNamespaces.Text, "text");
        idxNode.AppendChild(sourceNode);

        var bodyNode = OdfNodeFactory.CreateElement("index-body", OdfNamespaces.Text, "text");
        var titlePara = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        titlePara.TextContent = title;
        bodyNode.AppendChild(titlePara);
        idxNode.AppendChild(bodyNode);

        context.BodyTextRoot.AppendChild(idxNode);
        context.SetUpdateFieldsWhenOpening(true);
        return new OdfAlphabeticalIndex(idxNode, document);
    }

    internal static OdfBibliography AddBibliography(TextDocument document, TextDocumentMutationContext context, string title)
    {
        var bibNode = OdfNodeFactory.CreateElement("bibliography", OdfNamespaces.Text, "text");
        bibNode.SetAttribute("name", OdfNamespaces.Text, title, "text");

        var sourceNode = OdfNodeFactory.CreateElement("bibliography-source", OdfNamespaces.Text, "text");
        bibNode.AppendChild(sourceNode);

        var bodyNode = OdfNodeFactory.CreateElement("index-body", OdfNamespaces.Text, "text");
        var titlePara = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        titlePara.TextContent = title;
        bodyNode.AppendChild(titlePara);
        bibNode.AppendChild(bodyNode);

        context.BodyTextRoot.AppendChild(bibNode);
        context.SetUpdateFieldsWhenOpening(true);
        return new OdfBibliography(bibNode, document);
    }

    internal static List<OdfIndex> GetIndexes(TextDocument document, OdfNode bodyTextRoot)
    {
        List<OdfIndex> list = [];
        FindIndexesRecursive(document, bodyTextRoot, list);
        return list;
    }

    internal static OdfAlphabeticalIndexMark AddAlphabeticalIndexMark(
        OdfParagraph paragraph,
        string stringValue,
        string? key1,
        string? key2)
    {
        var markNode = OdfNodeFactory.CreateElement("alphabetical-index-mark", OdfNamespaces.Text, "text");
        markNode.SetAttribute("string-value", OdfNamespaces.Text, stringValue, "text");
        if (key1 is not null)
            markNode.SetAttribute("key1", OdfNamespaces.Text, key1, "text");
        if (key2 is not null)
            markNode.SetAttribute("key2", OdfNamespaces.Text, key2, "text");

        paragraph.Node.AppendChild(markNode);
        return new OdfAlphabeticalIndexMark(markNode);
    }

    internal static OdfBibliographyMark AddBibliographyMark(
        OdfParagraph paragraph,
        string identifier,
        string bibliographyType,
        string author,
        string title,
        string year)
    {
        var markNode = OdfNodeFactory.CreateElement("bibliography-mark", OdfNamespaces.Text, "text");
        markNode.SetAttribute("identifier", OdfNamespaces.Text, identifier, "text");
        markNode.SetAttribute("bibliography-type", OdfNamespaces.Text, bibliographyType, "text");
        markNode.SetAttribute("author", OdfNamespaces.Text, author, "text");
        markNode.SetAttribute("title", OdfNamespaces.Text, title, "text");
        markNode.SetAttribute("year", OdfNamespaces.Text, year, "text");

        paragraph.Node.AppendChild(markNode);
        return new OdfBibliographyMark(markNode);
    }

    internal static void AddTableIndex(TextDocumentMutationContext context)
    {
        var idxNode = OdfNodeFactory.CreateElement("table-index", OdfNamespaces.Text, "text");
        idxNode.SetAttribute("name", OdfNamespaces.Text, "Index of Tables", "text");
        var bodyNode = OdfNodeFactory.CreateElement("index-body", OdfNamespaces.Text, "text");
        idxNode.AppendChild(bodyNode);
        context.BodyTextRoot.AppendChild(idxNode);
        context.SetUpdateFieldsWhenOpening(true);
    }

    internal static void AddBookmark(OdfParagraph paragraph, string name)
    {
        var bNode = OdfNodeFactory.CreateElement("bookmark", OdfNamespaces.Text, "text");
        bNode.SetAttribute("name", OdfNamespaces.Text, name, "text");
        paragraph.Node.AppendChild(bNode);
    }

    internal static void AddReferenceMark(OdfParagraph paragraph, string name)
    {
        var rNode = OdfNodeFactory.CreateElement("reference-mark", OdfNamespaces.Text, "text");
        rNode.SetAttribute("name", OdfNamespaces.Text, name, "text");
        paragraph.Node.AppendChild(rNode);
    }

    internal static void AddHyperlink(OdfParagraph paragraph, string url, string text)
    {
        var aNode = OdfNodeFactory.CreateElement("a", OdfNamespaces.Text, "text");
        aNode.SetAttribute("href", OdfNamespaces.XLink, url, "xlink");
        aNode.TextContent = text;
        paragraph.Node.AppendChild(aNode);
    }

    internal static OdfImage AddImage(
        OdfParagraph paragraph,
        string packagePath,
        OdfLength width,
        OdfLength height,
        string? name)
    {
        var frameNode = OdfNodeFactory.CreateElement("frame", OdfNamespaces.Draw, "draw");
        if (name is not null)
            frameNode.SetAttribute("name", OdfNamespaces.Draw, name, "draw");
        frameNode.SetAttribute("anchor-type", OdfNamespaces.Text, "paragraph", "text");
        frameNode.SetAttribute("width", OdfNamespaces.Svg, width.ToString(), "svg");
        frameNode.SetAttribute("height", OdfNamespaces.Svg, height.ToString(), "svg");

        var imageNode = OdfNodeFactory.CreateElement("image", OdfNamespaces.Draw, "draw");
        imageNode.SetAttribute("href", OdfNamespaces.XLink, packagePath, "xlink");
        imageNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        imageNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
        imageNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");

        frameNode.AppendChild(imageNode);
        paragraph.Node.AppendChild(frameNode);

        return new OdfImage(frameNode, imageNode, paragraph.DocProperty);
    }

    internal static OdfRuby AddRuby(TextDocument document, OdfParagraph paragraph, string baseText, string rubyText)
    {
        var rubyNode = OdfNodeFactory.CreateElement("ruby", OdfNamespaces.Text, "text");

        var baseNode = OdfNodeFactory.CreateElement("ruby-base", OdfNamespaces.Text, "text");
        baseNode.TextContent = baseText;
        rubyNode.AppendChild(baseNode);

        var textNode = OdfNodeFactory.CreateElement("ruby-text", OdfNamespaces.Text, "text");
        textNode.TextContent = rubyText;
        rubyNode.AppendChild(textNode);

        paragraph.Node.AppendChild(rubyNode);
        return new OdfRuby(rubyNode, document);
    }

    private static void AppendNote(OdfParagraph paragraph, string noteClass, string id, string citation, string bodyText)
    {
        var noteNode = OdfNodeFactory.CreateElement("note", OdfNamespaces.Text, "text");
        noteNode.SetAttribute("note-class", OdfNamespaces.Text, noteClass, "text");
        noteNode.SetAttribute("id", OdfNamespaces.Text, id, "text");

        var citationNode = OdfNodeFactory.CreateElement("note-citation", OdfNamespaces.Text, "text");
        citationNode.TextContent = citation;
        noteNode.AppendChild(citationNode);

        var bodyNode = OdfNodeFactory.CreateElement("note-body", OdfNamespaces.Text, "text");
        var paraNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        paraNode.TextContent = bodyText;
        bodyNode.AppendChild(paraNode);
        noteNode.AppendChild(bodyNode);

        paragraph.Node.AppendChild(noteNode);
    }

    private static void FindIndexesRecursive(TextDocument document, OdfNode node, List<OdfIndex> list)
    {
        if (node.NamespaceUri == OdfNamespaces.Text)
        {
            if (node.LocalName == "table-of-content")
                list.Add(new OdfTableOfContents(node, document));
            else if (node.LocalName == "alphabetical-index")
                list.Add(new OdfAlphabeticalIndex(node, document));
            else if (node.LocalName == "bibliography")
                list.Add(new OdfBibliography(node, document));
        }

        foreach (OdfNode child in node.Children)
            FindIndexesRecursive(document, child, list);
    }
}
