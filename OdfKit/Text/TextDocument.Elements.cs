using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Document Elements Addition API


    /// <summary>
    /// 新增一個段落至文件本文結尾。
    /// </summary>
    /// <param name="text">段落的文字內容</param>
    /// <returns>新建立的段落執行個體</returns>
    public OdfParagraph AddParagraph(string text = "")
    {
        var pNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        pNode.TextContent = text;
        if (TrackedChanges)
        {
            string changeId = RecordTrackedChange("insertion");
            var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
            startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
            var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
            endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");

            BodyTextRoot.AppendChild(startNode);
            BodyTextRoot.AppendChild(pNode);
            BodyTextRoot.AppendChild(endNode);
        }
        else
        {
            BodyTextRoot.AppendChild(pNode);
        }
        return new OdfParagraph(pNode, this);
    }

    /// <summary>
    /// 新增一個標題至文件本文結尾。
    /// </summary>
    /// <param name="text">標題的文字內容</param>
    /// <param name="outlineLevel">標題的大綱階層</param>
    /// <returns>新建立的標題執行個體</returns>
    public OdfHeading AddHeading(string text, int outlineLevel)
    {
        var hNode = OdfNodeFactory.CreateElement("h", OdfNamespaces.Text, "text");
        hNode.TextContent = text;
        hNode.SetAttribute("outline-level", OdfNamespaces.Text, outlineLevel.ToString(), "text");
        if (TrackedChanges)
        {
            string changeId = RecordTrackedChange("insertion");
            var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
            startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
            var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
            endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");

            BodyTextRoot.AppendChild(startNode);
            BodyTextRoot.AppendChild(hNode);
            BodyTextRoot.AppendChild(endNode);
        }
        else
        {
            BodyTextRoot.AppendChild(hNode);
        }
        return new OdfHeading(hNode, this);
    }

    /// <summary>
    /// 新增一個項目清單至文件本文結尾。
    /// </summary>
    /// <param name="styleName">項目清單樣式名稱</param>
    /// <returns>新建立的清單項目</returns>
    public OdfList AddList(string? styleName = null)
    {
        var listNode = OdfNodeFactory.CreateElement("list", OdfNamespaces.Text, "text");
        if (styleName is not null)
        {
            listNode.SetAttribute("style-name", OdfNamespaces.Text, styleName, "text");
        }
        BodyTextRoot.AppendChild(listNode);
        return new OdfList(listNode, this);
    }

    /// <summary>
    /// 以多層級樣式定義建立清單，樣式寫入 styles.xml 的 office:styles 區段。
    /// </summary>
    /// <param name="styleName">清單樣式名稱，必須唯一。</param>
    /// <param name="levels">各層級的樣式設定；Level 屬性需從 1 開始連續遞增。</param>
    /// <returns>新建立的清單（已套用樣式名稱）。</returns>
    public OdfList AddListWithStyle(string styleName, IReadOnlyList<OdfListLevelStyle> levels)
    {
        var officeStyles = FindOrCreateChild(StylesDom, "styles", OdfNamespaces.Office, "office");
        var listStyleNode = OdfNodeFactory.CreateElement("list-style", OdfNamespaces.Text, "text");
        listStyleNode.SetAttribute("name", OdfNamespaces.Style, styleName, "style");

        foreach (var lvl in levels)
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
        return AddList(styleName);
    }

    /// <summary>
    /// 在指定的段落中新增日期欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    internal void AddDateField(OdfParagraph paragraph)
    {
        var fNode = OdfNodeFactory.CreateElement("date", OdfNamespaces.Text, "text");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中新增時間欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    internal void AddTimeField(OdfParagraph paragraph)
    {
        var fNode = OdfNodeFactory.CreateElement("time", OdfNamespaces.Text, "text");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中新增作者名稱欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    internal void AddAuthorField(OdfParagraph paragraph)
    {
        var fNode = OdfNodeFactory.CreateElement("author-name", OdfNamespaces.Text, "text");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中新增章節欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    internal void AddChapterField(OdfParagraph paragraph)
    {
        var fNode = OdfNodeFactory.CreateElement("chapter", OdfNamespaces.Text, "text");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中新增序號欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    /// <param name="name">序號欄位的名稱</param>
    /// <param name="numFormat">序號的編號格式</param>
    internal void AddSequenceField(OdfParagraph paragraph, string name, string numFormat = "1")
    {
        var fNode = OdfNodeFactory.CreateElement("sequence", OdfNamespaces.Text, "text");
        fNode.SetAttribute("name", OdfNamespaces.Text, name, "text");
        fNode.SetAttribute("num-format", OdfNamespaces.Style, numFormat, "style");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中新增參考項目欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    /// <param name="refName">要參考的項目名稱</param>
    internal void AddReferenceField(OdfParagraph paragraph, string refName)
    {
        var fNode = OdfNodeFactory.CreateElement("reference-ref", OdfNamespaces.Text, "text");
        fNode.SetAttribute("ref-name", OdfNamespaces.Text, refName, "text");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中新增序號交互參照欄位 (<c>text:sequence-ref</c>)。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    /// <param name="sequenceName">序號欄位名稱（需與 AddSequenceField 使用的 name 相同）</param>
    /// <param name="referenceFormat">參照格式，預設為 "value"（顯示數值）</param>
    internal void AddSequenceRefField(OdfParagraph paragraph, string sequenceName, string referenceFormat = "value")
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

    /// <summary>
    /// 在指定的段落中新增書籤參照欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體。</param>
    /// <param name="bookmarkName">要參照的書籤名稱。</param>
    /// <param name="referenceFormat">參照格式，預設為 "text"。</param>
    internal void AddBookmarkReferenceField(OdfParagraph paragraph, string bookmarkName, string referenceFormat = "text")
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (string.IsNullOrEmpty(bookmarkName))
            throw new ArgumentException("書籤名稱不可為空。", nameof(bookmarkName));

        var fNode = OdfNodeFactory.CreateElement("bookmark-ref", OdfNamespaces.Text, "text");
        fNode.SetAttribute("ref-name", OdfNamespaces.Text, bookmarkName, "text");
        fNode.SetAttribute("reference-format", OdfNamespaces.Text, referenceFormat, "text");
        fNode.TextContent = bookmarkName; // 預設顯示書籤名稱
        paragraph.Node.AppendChild(fNode);
    }


    /// <summary>
    /// 在指定的段落中設定變數欄位值。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    /// <param name="name">變數的名稱</param>
    /// <param name="value">變數的值</param>
    internal void AddVariableSetField(OdfParagraph paragraph, string name, string value)
    {
        var fNode = OdfNodeFactory.CreateElement("variable-set", OdfNamespaces.Text, "text");
        fNode.SetAttribute("name", OdfNamespaces.Text, name, "text");
        fNode.TextContent = value;
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中取得變數欄位值。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    /// <param name="name">變數的名稱</param>
    internal void AddVariableGetField(OdfParagraph paragraph, string name)
    {
        var fNode = OdfNodeFactory.CreateElement("variable-get", OdfNamespaces.Text, "text");
        fNode.SetAttribute("name", OdfNamespaces.Text, name, "text");
        paragraph.Node.AppendChild(fNode);
    }

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
