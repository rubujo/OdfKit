using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

/// <summary>
/// 文字文件欄位與清單元素引擎（內部協作者）。
/// </summary>
internal static class TextDocumentFieldsEngine
{
    internal static OdfParagraph AddParagraph(TextDocument document, TextDocumentMutationContext context, string text)
    {
        var pNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        pNode.TextContent = text;
        if (context.TrackedChanges)
        {
            string changeId = context.RecordTrackedChange("insertion");
            var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
            startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
            var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
            endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");

            context.BodyTextRoot.AppendChild(startNode);
            context.BodyTextRoot.AppendChild(pNode);
            context.BodyTextRoot.AppendChild(endNode);
        }
        else
        {
            context.BodyTextRoot.AppendChild(pNode);
        }

        return new OdfParagraph(pNode, document);
    }

    internal static OdfHeading AddHeading(TextDocument document, TextDocumentMutationContext context, string text, int outlineLevel)
    {
        var hNode = OdfNodeFactory.CreateElement("h", OdfNamespaces.Text, "text");
        hNode.TextContent = text;
        hNode.SetAttribute("outline-level", OdfNamespaces.Text, outlineLevel.ToString(), "text");
        if (context.TrackedChanges)
        {
            string changeId = context.RecordTrackedChange("insertion");
            var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
            startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
            var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
            endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");

            context.BodyTextRoot.AppendChild(startNode);
            context.BodyTextRoot.AppendChild(hNode);
            context.BodyTextRoot.AppendChild(endNode);
        }
        else
        {
            context.BodyTextRoot.AppendChild(hNode);
        }

        return new OdfHeading(hNode, document);
    }

    internal static OdfList AddList(TextDocument document, TextDocumentMutationContext context, string? styleName)
    {
        var listNode = OdfNodeFactory.CreateElement("list", OdfNamespaces.Text, "text");
        if (styleName is not null)
            listNode.SetAttribute("style-name", OdfNamespaces.Text, styleName, "text");
        context.BodyTextRoot.AppendChild(listNode);
        return new OdfList(listNode, document);
    }

    internal static OdfList AddListWithStyle(
        TextDocument document,
        TextDocumentMutationContext context,
        string styleName,
        IReadOnlyList<OdfListLevelStyle> levels)
    {
        var officeStyles = TextDocumentDomHelper.FindOrCreateChild(context.StylesDom, "styles", OdfNamespaces.Office, "office");
        var listStyleNode = OdfNodeFactory.CreateElement("list-style", OdfNamespaces.Text, "text");
        listStyleNode.SetAttribute("name", OdfNamespaces.Style, styleName, "style");

        foreach (OdfListLevelStyle lvl in levels)
        {
            OdfNode levelNode;
            if (lvl.Type == OdfListLevelType.Bullet)
            {
                levelNode = OdfNodeFactory.CreateElement("list-level-style-bullet", OdfNamespaces.Text, "text");
                levelNode.SetAttribute("bullet-char", OdfNamespaces.Text, lvl.BulletChar ?? "•", "text");
            }
            else
            {
                levelNode = OdfNodeFactory.CreateElement("list-level-style-number", OdfNamespaces.Text, "text");
                levelNode.SetAttribute("num-format", OdfNamespaces.Fo, lvl.NumFormat, "fo");
                if (!string.IsNullOrEmpty(lvl.NumPrefix))
                    levelNode.SetAttribute("num-prefix", OdfNamespaces.Text, lvl.NumPrefix!, "text");
                if (lvl.NumSuffix is not null)
                    levelNode.SetAttribute("num-suffix", OdfNamespaces.Text, lvl.NumSuffix, "text");
            }

            levelNode.SetAttribute("level", OdfNamespaces.Text, lvl.Level.ToString(), "text");

            var propsNode = OdfNodeFactory.CreateElement("list-level-properties", OdfNamespaces.Style, "style");
            var alignNode = OdfNodeFactory.CreateElement("list-level-label-alignment", OdfNamespaces.Style, "style");
            alignNode.SetAttribute("label-followed-by", OdfNamespaces.Text, "listtab", "text");
            if (lvl.IndentLeft.Value > 0)
                alignNode.SetAttribute("margin-left", OdfNamespaces.Fo, lvl.IndentLeft.ToString(), "fo");
            if (lvl.FirstLineIndent.Value != 0)
                alignNode.SetAttribute("text-indent", OdfNamespaces.Fo, lvl.FirstLineIndent.ToString(), "fo");
            propsNode.AppendChild(alignNode);
            levelNode.AppendChild(propsNode);

            listStyleNode.AppendChild(levelNode);
        }

        officeStyles.AppendChild(listStyleNode);
        return AddList(document, context, styleName);
    }

    internal static void AddDateField(OdfParagraph paragraph) =>
        paragraph.Node.AppendChild(OdfNodeFactory.CreateElement("date", OdfNamespaces.Text, "text"));

    internal static void AddTimeField(OdfParagraph paragraph) =>
        paragraph.Node.AppendChild(OdfNodeFactory.CreateElement("time", OdfNamespaces.Text, "text"));

    internal static void AddAuthorField(OdfParagraph paragraph) =>
        paragraph.Node.AppendChild(OdfNodeFactory.CreateElement("author-name", OdfNamespaces.Text, "text"));

    internal static void AddChapterField(OdfParagraph paragraph) =>
        paragraph.Node.AppendChild(OdfNodeFactory.CreateElement("chapter", OdfNamespaces.Text, "text"));

    internal static void AddSequenceField(OdfParagraph paragraph, string name, string numFormat)
    {
        var fNode = OdfNodeFactory.CreateElement("sequence", OdfNamespaces.Text, "text");
        fNode.SetAttribute("name", OdfNamespaces.Text, name, "text");
        fNode.SetAttribute("num-format", OdfNamespaces.Style, numFormat, "style");
        paragraph.Node.AppendChild(fNode);
    }

    internal static void AddReferenceField(OdfParagraph paragraph, string refName)
    {
        var fNode = OdfNodeFactory.CreateElement("reference-ref", OdfNamespaces.Text, "text");
        fNode.SetAttribute("ref-name", OdfNamespaces.Text, refName, "text");
        paragraph.Node.AppendChild(fNode);
    }

    internal static void AddSequenceRefField(OdfParagraph paragraph, string sequenceName, string referenceFormat)
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (string.IsNullOrEmpty(sequenceName))
            throw new ArgumentException("序號欄位名稱不可為空。", nameof(sequenceName));
        var fNode = OdfNodeFactory.CreateElement("sequence-ref", OdfNamespaces.Text, "text");
        fNode.SetAttribute("ref-name", OdfNamespaces.Text, sequenceName, "text");
        fNode.SetAttribute("reference-format", OdfNamespaces.Text, referenceFormat, "text");
        paragraph.Node.AppendChild(fNode);
    }

    internal static void AddBookmarkReferenceField(OdfParagraph paragraph, string bookmarkName, string referenceFormat)
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (string.IsNullOrEmpty(bookmarkName))
            throw new ArgumentException("書籤名稱不可為空。", nameof(bookmarkName));

        var fNode = OdfNodeFactory.CreateElement("bookmark-ref", OdfNamespaces.Text, "text");
        fNode.SetAttribute("ref-name", OdfNamespaces.Text, bookmarkName, "text");
        fNode.SetAttribute("reference-format", OdfNamespaces.Text, referenceFormat, "text");
        fNode.TextContent = bookmarkName;
        paragraph.Node.AppendChild(fNode);
    }

    internal static void AddVariableSetField(OdfParagraph paragraph, string name, string value)
    {
        var fNode = OdfNodeFactory.CreateElement("variable-set", OdfNamespaces.Text, "text");
        fNode.SetAttribute("name", OdfNamespaces.Text, name, "text");
        fNode.TextContent = value;
        paragraph.Node.AppendChild(fNode);
    }

    internal static void AddVariableGetField(OdfParagraph paragraph, string name)
    {
        var fNode = OdfNodeFactory.CreateElement("variable-get", OdfNamespaces.Text, "text");
        fNode.SetAttribute("name", OdfNamespaces.Text, name, "text");
        paragraph.Node.AppendChild(fNode);
    }
}
