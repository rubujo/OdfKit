using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Document Elements - Notes & Ruby

    /// <summary>
    /// 在指定段落中插入腳注 (text:note, note-class="footnote")。
    /// </summary>
    /// <param name="paragraph">要插入腳注的段落。</param>
    /// <param name="citation">腳注引用標記，例如 "1" 或 "*"。</param>
    /// <param name="bodyText">腳注本文內容。</param>
    internal void AddFootnote(OdfParagraph paragraph, string citation, string bodyText)
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (citation is null)
            throw new ArgumentNullException(nameof(citation));
        if (bodyText is null)
            throw new ArgumentNullException(nameof(bodyText));
        AppendNote(paragraph, "footnote", $"ftn{_footnoteCounter++}", citation, bodyText);
    }

    /// <summary>
    /// 在指定段落中插入尾注 (text:note, note-class="endnote")。
    /// </summary>
    /// <param name="paragraph">要插入尾注的段落。</param>
    /// <param name="citation">尾注引用標記，例如 "i" 或 "a"。</param>
    /// <param name="bodyText">尾注本文內容。</param>
    internal void AddEndnote(OdfParagraph paragraph, string citation, string bodyText)
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (citation is null)
            throw new ArgumentNullException(nameof(citation));
        if (bodyText is null)
            throw new ArgumentNullException(nameof(bodyText));
        AppendNote(paragraph, "endnote", $"etn{_endnoteCounter++}", citation, bodyText);
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

    /// <summary>
    /// 新增字母索引至文件本文結尾。
    /// </summary>
    /// <param name="title">索引標題</param>
    /// <returns>建立的字母索引物件</returns>
    public OdfAlphabeticalIndex AddAlphabeticalIndex(string title = "Alphabetical Index")
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

        BodyTextRoot.AppendChild(idxNode);
        SetUpdateFieldsWhenOpening(true);
        return new OdfAlphabeticalIndex(idxNode, this);
    }

    /// <summary>
    /// 新增文獻目錄至文件本文結尾。
    /// </summary>
    /// <param name="title">文獻目錄標題</param>
    /// <returns>建立的文獻目錄物件</returns>
    public OdfBibliography AddBibliography(string title = "Bibliography")
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

        BodyTextRoot.AppendChild(bibNode);
        SetUpdateFieldsWhenOpening(true);
        return new OdfBibliography(bibNode, this);
    }

    /// <summary>
    /// 取得文件中所有索引的列表。
    /// </summary>
    /// <returns>包含索引物件的列表</returns>
    public List<OdfIndex> GetIndexes()
    {
        List<OdfIndex> list = [];
        FindIndexesRecursive(BodyTextRoot, list);
        return list;
    }

    private void FindIndexesRecursive(OdfNode node, List<OdfIndex> list)
    {
        if (node.NamespaceUri == OdfNamespaces.Text)
        {
            if (node.LocalName == "table-of-content")
                list.Add(new OdfTableOfContents(node, this));
            else if (node.LocalName == "alphabetical-index")
                list.Add(new OdfAlphabeticalIndex(node, this));
            else if (node.LocalName == "bibliography")
                list.Add(new OdfBibliography(node, this));
        }
        foreach (var child in node.Children)
        {
            FindIndexesRecursive(child, list);
        }
    }

    /// <summary>
    /// 在指定的段落中新增字母索引標記。
    /// </summary>
    /// <param name="paragraph">要新增標記的段落執行個體</param>
    /// <param name="stringValue">索引字串值</param>
    /// <param name="key1">主要鍵值</param>
    /// <param name="key2">次要鍵值</param>
    /// <returns>建立的字母索引標記物件</returns>
    internal OdfAlphabeticalIndexMark AddAlphabeticalIndexMark(OdfParagraph paragraph, string stringValue, string? key1 = null, string? key2 = null)
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

    /// <summary>
    /// 在指定的段落中新增文獻標記。
    /// </summary>
    /// <param name="paragraph">要新增標記的段落執行個體</param>
    /// <param name="identifier">文獻標記識別碼</param>
    /// <param name="bibliographyType">文獻類型</param>
    /// <param name="author">文獻作者</param>
    /// <param name="title">文獻標題</param>
    /// <param name="year">出版年份</param>
    /// <returns>建立的文獻標記物件</returns>
    internal OdfBibliographyMark AddBibliographyMark(OdfParagraph paragraph, string identifier, string bibliographyType, string author, string title, string year)
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

    /// <summary>
    /// 新增表格索引至文件本文結尾。
    /// </summary>
    public void AddTableIndex()
    {
        var idxNode = OdfNodeFactory.CreateElement("table-index", OdfNamespaces.Text, "text");
        idxNode.SetAttribute("name", OdfNamespaces.Text, "Index of Tables", "text");
        var bodyNode = OdfNodeFactory.CreateElement("index-body", OdfNamespaces.Text, "text");
        idxNode.AppendChild(bodyNode);
        BodyTextRoot.AppendChild(idxNode);
        SetUpdateFieldsWhenOpening(true);
    }

    /// <summary>
    /// 在指定的段落中新增書籤。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    /// <param name="name">書籤名稱</param>
    internal void AddBookmark(OdfParagraph paragraph, string name)
    {
        var bNode = OdfNodeFactory.CreateElement("bookmark", OdfNamespaces.Text, "text");
        bNode.SetAttribute("name", OdfNamespaces.Text, name, "text");
        paragraph.Node.AppendChild(bNode);
    }

    /// <summary>
    /// 在指定的段落中新增參考標記。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    /// <param name="name">參考標記名稱</param>
    internal void AddReferenceMark(OdfParagraph paragraph, string name)
    {
        var rNode = OdfNodeFactory.CreateElement("reference-mark", OdfNamespaces.Text, "text");
        rNode.SetAttribute("name", OdfNamespaces.Text, name, "text");
        paragraph.Node.AppendChild(rNode);
    }

    /// <summary>
    /// 在指定的段落中新增超連結。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    /// <param name="url">超連結網址</param>
    /// <param name="text">連結顯示文字</param>
    internal void AddHyperlink(OdfParagraph paragraph, string url, string text)
    {
        var aNode = OdfNodeFactory.CreateElement("a", OdfNamespaces.Text, "text");
        aNode.SetAttribute("href", OdfNamespaces.XLink, url, "xlink");
        aNode.TextContent = text;
        paragraph.Node.AppendChild(aNode);
    }

    /// <summary>
    /// 在指定的段落中新增圖片。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    /// <param name="packagePath">圖片在封裝包內的路徑</param>
    /// <param name="width">圖片寬度</param>
    /// <param name="height">圖片高度</param>
    /// <param name="name">圖片名稱</param>
    /// <returns>新建立的圖片物件</returns>
    internal OdfImage AddImage(OdfParagraph paragraph, string packagePath, OdfLength width, OdfLength height, string? name = null)
    {
        var frameNode = OdfNodeFactory.CreateElement("frame", OdfNamespaces.Draw, "draw");
        if (name is not null)
        {
            frameNode.SetAttribute("name", OdfNamespaces.Draw, name, "draw");
        }
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

        return new OdfImage(frameNode, imageNode);
    }

    /// <summary>
    /// 在指定的段落中新增旁註標記（注音資訊）。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    /// <param name="baseText">基礎文字</param>
    /// <param name="rubyText">注音（旁註）文字</param>
    /// <returns>新建立的旁註標記物件</returns>
    internal OdfRuby AddRuby(OdfParagraph paragraph, string baseText, string rubyText)
    {
        var rubyNode = OdfNodeFactory.CreateElement("ruby", OdfNamespaces.Text, "text");

        var baseNode = OdfNodeFactory.CreateElement("ruby-base", OdfNamespaces.Text, "text");
        baseNode.TextContent = baseText;
        rubyNode.AppendChild(baseNode);

        var textNode = OdfNodeFactory.CreateElement("ruby-text", OdfNamespaces.Text, "text");
        textNode.TextContent = rubyText;
        rubyNode.AppendChild(textNode);

        paragraph.Node.AppendChild(rubyNode);
        return new OdfRuby(rubyNode, this);
    }


    #endregion
}
