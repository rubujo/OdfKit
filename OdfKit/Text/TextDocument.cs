using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

/// <summary>
/// 表示 ODF 文字文件。
/// </summary>
public class TextDocument : OdfDocument
{
    /// <summary>
    /// 取得或設定文字文件的本文根節點。
    /// </summary>
    public OdfNode BodyTextRoot { get; private set; } = null!;

    /// <summary>
    /// 初始化 <see cref="TextDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">OdfPackage 封裝包執行個體</param>
    public TextDocument(OdfPackage package) : base(package)
    {
        if (string.IsNullOrEmpty(package.MimeType))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.text");
        }
        InitializeTextRoot();
        StyleEngine.OnStyleChanging = TrackFormatChange;
    }

    private void InitializeTextRoot()
    {
        var body = FindOrCreateChild(ContentDom, "body", OdfNamespaces.Office, "office");
        BodyTextRoot = FindOrCreateChild(body, "text", OdfNamespaces.Office, "office");
    }

    /// <summary>
    /// 取得預設的內容 XML 字串。
    /// </summary>
    /// <returns>內容 XML 字串</returns>
    protected override string GetDefaultContentXml()
    {
        return "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"1.3\"><office:body><office:text></office:text></office:body></office:document-content>";
    }

    /// <summary>
    /// 取得預設的樣式 XML 字串。
    /// </summary>
    /// <returns>樣式 XML 字串</returns>
    protected override string GetDefaultStylesXml()
    {
        return "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"1.3\"><office:styles></office:styles><office:automatic-styles></office:automatic-styles><office:master-styles><style:master-page style:name=\"Standard\" style:page-layout-name=\"Mpm1\"/></office:master-styles></office:document-styles>";
    }

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
    /// 在指定的段落中新增日期欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    public void AddDateField(OdfParagraph paragraph)
    {
        var fNode = OdfNodeFactory.CreateElement("date", OdfNamespaces.Text, "text");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中新增時間欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    public void AddTimeField(OdfParagraph paragraph)
    {
        var fNode = OdfNodeFactory.CreateElement("time", OdfNamespaces.Text, "text");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中新增作者名稱欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    public void AddAuthorField(OdfParagraph paragraph)
    {
        var fNode = OdfNodeFactory.CreateElement("author-name", OdfNamespaces.Text, "text");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中新增章節欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    public void AddChapterField(OdfParagraph paragraph)
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
    public void AddSequenceField(OdfParagraph paragraph, string name, string numFormat = "1")
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
    public void AddReferenceField(OdfParagraph paragraph, string refName)
    {
        var fNode = OdfNodeFactory.CreateElement("reference-ref", OdfNamespaces.Text, "text");
        fNode.SetAttribute("ref-name", OdfNamespaces.Text, refName, "text");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中設定變數欄位值。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    /// <param name="name">變數的名稱</param>
    /// <param name="value">變數的值</param>
    public void AddVariableSetField(OdfParagraph paragraph, string name, string value)
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
    public void AddVariableGetField(OdfParagraph paragraph, string name)
    {
        var fNode = OdfNodeFactory.CreateElement("variable-get", OdfNamespaces.Text, "text");
        fNode.SetAttribute("name", OdfNamespaces.Text, name, "text");
        paragraph.Node.AppendChild(fNode);
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
    public OdfAlphabeticalIndexMark AddAlphabeticalIndexMark(OdfParagraph paragraph, string stringValue, string? key1 = null, string? key2 = null)
    {
        var markNode = OdfNodeFactory.CreateElement("alphabetical-index-mark", OdfNamespaces.Text, "text");
        markNode.SetAttribute("string-value", OdfNamespaces.Text, stringValue, "text");
        if (key1 is not null) markNode.SetAttribute("key1", OdfNamespaces.Text, key1, "text");
        if (key2 is not null) markNode.SetAttribute("key2", OdfNamespaces.Text, key2, "text");
        
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
    public OdfBibliographyMark AddBibliographyMark(OdfParagraph paragraph, string identifier, string bibliographyType, string author, string title, string year)
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
    /// 在指定的段落中新增註解的起始標記。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    /// <param name="name">註解名稱</param>
    public void AddCommentStart(OdfParagraph paragraph, string name)
    {
        var startNode = OdfNodeFactory.CreateElement("annotation-start", OdfNamespaces.Office, "office");
        startNode.SetAttribute("name", OdfNamespaces.Office, name, "office");
        paragraph.Node.AppendChild(startNode);
    }

    /// <summary>
    /// 在指定的段落中新增註解的結束標記。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    /// <param name="name">註解名稱</param>
    public void AddCommentEnd(OdfParagraph paragraph, string name)
    {
        var endNode = OdfNodeFactory.CreateElement("annotation-end", OdfNamespaces.Office, "office");
        endNode.SetAttribute("name", OdfNamespaces.Office, name, "office");
        paragraph.Node.AppendChild(endNode);
    }

    /// <summary>
    /// 在指定的段落中新增書籤。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    /// <param name="name">書籤名稱</param>
    public void AddBookmark(OdfParagraph paragraph, string name)
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
    public void AddReferenceMark(OdfParagraph paragraph, string name)
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
    public void AddHyperlink(OdfParagraph paragraph, string url, string text)
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
    /// <param name="widthCm">圖片寬度（公分）</param>
    /// <param name="heightCm">圖片高度（公分）</param>
    /// <param name="name">圖片名稱</param>
    /// <returns>新建立的圖片物件</returns>
    public OdfImage AddImage(OdfParagraph paragraph, string packagePath, string widthCm, string heightCm, string? name = null)
    {
        var frameNode = OdfNodeFactory.CreateElement("frame", OdfNamespaces.Draw, "draw");
        if (name is not null)
        {
            frameNode.SetAttribute("name", OdfNamespaces.Draw, name, "draw");
        }
        frameNode.SetAttribute("anchor-type", OdfNamespaces.Text, "paragraph", "text");
        frameNode.SetAttribute("width", OdfNamespaces.Svg, widthCm, "svg");
        frameNode.SetAttribute("height", OdfNamespaces.Svg, heightCm, "svg");

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
    public OdfRuby AddRuby(OdfParagraph paragraph, string baseText, string rubyText)
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

    #region Page Setup & Mirrored Layouts

    /// <summary>
    /// 取得預設的頁面設定。
    /// </summary>
    /// <returns>預設頁面設定物件</returns>
    public OdfPageSetup GetDefaultPageSetup()
    {
        return new OdfPageSetup(this);
    }

    #endregion

    #region TOC (Table of Contents)

    /// <summary>
    /// 新增目錄項目至文件本文結尾。
    /// </summary>
    /// <param name="title">目錄標題</param>
    /// <param name="outlineLevel">目錄的大綱階層上限</param>
    /// <returns>新建立的目錄物件</returns>
    public OdfTableOfContents AddTableOfContents(string title = "Table of Contents", int outlineLevel = 10)
    {
        var tocNode = OdfNodeFactory.CreateElement("table-of-content", OdfNamespaces.Text, "text");
        tocNode.SetAttribute("name", OdfNamespaces.Text, title, "text");

        var sourceNode = OdfNodeFactory.CreateElement("table-of-content-source", OdfNamespaces.Text, "text");
        sourceNode.SetAttribute("outline-level", OdfNamespaces.Text, outlineLevel.ToString(), "text");
        tocNode.AppendChild(sourceNode);

        var bodyNode = OdfNodeFactory.CreateElement("index-body", OdfNamespaces.Text, "text");
        
        var titlePara = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        titlePara.SetAttribute("style-name", OdfNamespaces.Text, "Contents_20_Heading", "text");
        titlePara.TextContent = title;
        bodyNode.AppendChild(titlePara);
        
        tocNode.AppendChild(bodyNode);
        BodyTextRoot.AppendChild(tocNode);

        SetUpdateFieldsWhenOpening(true);
        return new OdfTableOfContents(tocNode, this);
    }

    private void SetUpdateFieldsWhenOpening(bool update)
    {
        var sc = FindOrCreateSettingsNode(SettingsDom, "view-settings");
        var map = FindOrCreateMapNode(sc, "Views");
        var entry = FindOrCreateMapEntryNode(map);
        var item = FindOrCreateConfigItemNode(entry, "UpdateFieldsWhenOpening", "boolean");
        item.TextContent = update ? "true" : "false";
    }

    #endregion

    #region Search & Replace with Actions/Regex

    /// <summary>
    /// 搜尋指定文字並替換為新文字。
    /// </summary>
    /// <param name="search">要搜尋的關鍵字</param>
    /// <param name="replacement">要替換的新文字</param>
    /// <param name="styleAction">套用於替換後文字片段的樣式委派作業</param>
    public void ReplaceText(string search, string replacement, Action<OdfTextRun>? styleAction = null)
    {
        ReplaceTextRecursive(BodyTextRoot, search, replacement, styleAction);
    }

    /// <summary>
    /// 以規則運算式搜尋文字並替換為新文字。
    /// </summary>
    /// <param name="regex">代表搜尋條件的規則運算式物件</param>
    /// <param name="replacement">要替換的新文字</param>
    /// <param name="styleAction">套用於替換後文字片段的樣式委派作業</param>
    public void ReplaceText(Regex regex, string replacement, Action<OdfTextRun>? styleAction = null)
    {
        ReplaceTextRegexRecursive(BodyTextRoot, regex, replacement, styleAction);
    }

    private void ReplaceTextRecursive(OdfNode node, string search, string replacement, Action<OdfTextRun>? styleAction)
    {
        NormalizeParagraphTextNodes(node);

        if (node.NodeType == OdfNodeType.Text)
        {
            string text = node.TextContent;
            if (text.Contains(search))
            {
                if (styleAction is not null && node.Parent is not null)
                {
                    int index = text.IndexOf(search);
                    var left = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text.Substring(0, index) };
                    var mid = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text");
                    var midRun = new OdfTextRun(mid, this)
                    {
                        Text = replacement
                    };
                    styleAction(midRun);
                    
                    var right = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text.Substring(index + search.Length) };

                    var parent = node.Parent;
                    parent.InsertBefore(left, node);
                    parent.InsertBefore(mid, node);
                    parent.InsertBefore(right, node);
                    parent.RemoveChild(node);
                }
                else
                {
                    node.TextContent = text.Replace(search, replacement);
                }
            }
            return;
        }

        if (node.LocalName == "annotation" && node.NamespaceUri == OdfNamespaces.Office)
        {
            foreach (var child in node.Children)
            {
                if (child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    ReplaceTextRecursive(child, search, replacement, styleAction);
                }
            }
        }

        for (int i = 0; i < node.Children.Count; i++)
        {
            ReplaceTextRecursive(node.Children[i], search, replacement, styleAction);
        }
    }

    private void ReplaceTextRegexRecursive(OdfNode node, Regex regex, string replacement, Action<OdfTextRun>? styleAction)
    {
        NormalizeParagraphTextNodes(node);

        if (node.NodeType == OdfNodeType.Text)
        {
            string text = node.TextContent;
            if (regex.IsMatch(text))
            {
                if (styleAction is not null && node.Parent is not null)
                {
                    var match = regex.Match(text);
                    int index = match.Index;
                    
                    var left = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text.Substring(0, index) };
                    var mid = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text");
                    var midRun = new OdfTextRun(mid, this);
                    midRun.Text = regex.Replace(match.Value, replacement);
                    styleAction(midRun);
                    
                    var right = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text.Substring(index + match.Length) };

                    var parent = node.Parent;
                    parent.InsertBefore(left, node);
                    parent.InsertBefore(mid, node);
                    parent.InsertBefore(right, node);
                    parent.RemoveChild(node);
                }
                else
                {
                    node.TextContent = regex.Replace(text, replacement);
                }
            }
            return;
        }

        for (int i = 0; i < node.Children.Count; i++)
        {
            ReplaceTextRegexRecursive(node.Children[i], regex, replacement, styleAction);
        }
    }

    private void NormalizeParagraphTextNodes(OdfNode parent)
    {
        if (parent.LocalName == "p" && parent.NamespaceUri == OdfNamespaces.Text)
        {
            for (int i = parent.Children.Count - 2; i >= 0; i--)
            {
                if (parent.Children[i].NodeType == OdfNodeType.Text && parent.Children[i + 1].NodeType == OdfNodeType.Text)
                {
                    parent.Children[i].TextContent += parent.Children[i + 1].TextContent;
                    parent.RemoveChild(parent.Children[i + 1]);
                }
            }
        }
    }

    #endregion

    #region MailMerge Implementation

    /// <summary>
    /// 執行郵件合併作業。
    /// </summary>
    /// <param name="dataSource">包含資料來源屬性的物件</param>
    public void MailMerge(object dataSource)
    {
        var engine = new OdfMailMergeEngine(this);
        engine.Execute(BodyTextRoot, dataSource);
    }

    #endregion

    #region Mathematical Formulas (MathML)

    /// <summary>
    /// 在指定的段落中新增數學公式。
    /// </summary>
    /// <param name="paragraph">要插入公式的段落</param>
    /// <param name="mathMlXmlString">MathML 結構的 XML 字串內容</param>
    public void AddFormula(OdfParagraph paragraph, string mathMlXmlString)
    {
        if (paragraph is null) throw new ArgumentNullException(nameof(paragraph));
        if (string.IsNullOrWhiteSpace(mathMlXmlString)) throw new ArgumentException("MathML XML content cannot be empty.", nameof(mathMlXmlString));

        // 驗證 mathMlXmlString 是否為格式正確的 XML
        try
        {
            XElement.Parse(mathMlXmlString);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Invalid MathML XML: " + ex.Message, nameof(mathMlXmlString), ex);
        }

        string folder = $"Formula_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        string mathDocXml = $"<office:document-meta xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:math=\"http://www.w3.org/1998/Math/MathML\"><office:body><office:formula>{mathMlXmlString}</office:formula></office:body></office:document-meta>";

        Package.WriteEntry($"{folder}/content.xml", System.Text.Encoding.UTF8.GetBytes(mathDocXml), "text/xml");
        Package.WriteEntry($"{folder}/mimetype", System.Text.Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.formula"), "application/vnd.oasis.opendocument.formula");
        
        Package.SaveManifestToEntries();

        var frame = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
        frame.SetAttribute("width", OdfNamespaces.Svg, "2cm", "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, "1cm", "svg");
        frame.SetAttribute("anchor-type", OdfNamespaces.Text, "as-char", "text");

        var obj = new OdfNode(OdfNodeType.Element, "object", OdfNamespaces.Draw, "draw");
        obj.SetAttribute("href", OdfNamespaces.XLink, folder, "xlink");
        frame.AppendChild(obj);

        paragraph.Node.AppendChild(frame);
    }

    #endregion

    #region CJK Font Fallback

    /// <summary>
    /// 套用中日韓（CJK）字型遞補設定。
    /// </summary>
    public void ApplyCjkFontFallback()
    {
        // 宣告預設的中日韓字型，若不存在則新增
        AddFontFace("PMingLiU", "PMingLiU", "system-serif", "variable");
        AddFontFace("Microsoft JhengHei", "Microsoft JhengHei", "system-sans-serif", "variable");
        AddFontFace("MS Mincho", "MS Mincho", "system-serif", "variable");
        AddFontFace("MS Gothic", "MS Gothic", "system-sans-serif", "variable");
        AddFontFace("SimSun", "SimSun", "system-serif", "variable");
        AddFontFace("Microsoft YaHei", "Microsoft YaHei", "system-sans-serif", "variable");
        AddFontFace("Malgun Gothic", "Malgun Gothic", "system-sans-serif", "variable");
    }

    #endregion

    #region Comments / Annotations

    /// <summary>
    /// 在指定的段落中新增註解。
    /// </summary>
    /// <param name="paragraph">要新增註解的段落</param>
    /// <param name="comment">註解物件執行個體</param>
    public void AddComment(OdfParagraph paragraph, OdfComment comment)
    {
        if (paragraph is null) throw new ArgumentNullException(nameof(paragraph));
        if (comment is null) throw new ArgumentNullException(nameof(comment));

        var node = comment.ToXmlNode();
        if (node.LocalName == "annotation-list")
        {
            foreach (var child in new List<OdfNode>(node.Children))
            {
                paragraph.Node.AppendChild(child);
            }
        }
        else
        {
            paragraph.Node.AppendChild(node);
        }
    }

    /// <summary>
    /// 取得文件中所有註解的列表。
    /// </summary>
    /// <returns>註解物件列表</returns>
    public List<OdfComment> GetComments()
    {
        List<OdfComment> list = [];
        FindCommentsRecursive(BodyTextRoot, list);
        return list;
    }

    private void FindCommentsRecursive(OdfNode node, List<OdfComment> list)
    {
        if (node.LocalName == "annotation" && node.NamespaceUri == OdfNamespaces.Office)
        {
            // 檢查是否為最上層註解（沒有 annotation-parent）
            string? parent = node.GetAttribute("annotation-parent", OdfNamespaces.Office);
            if (string.IsNullOrEmpty(parent))
            {
                try
                {
                    list.Add(OdfComment.FromXmlNode(node));
                }
                catch (Exception ex)
                {
                    OdfKitDiagnostics.Warn($"Failed to parse comment node: {ex.Message}");
                }
            }
        }

        foreach (var child in node.Children)
        {
            FindCommentsRecursive(child, list);
        }
    }

    #endregion

    #region Dynamic Page / Field Indicators

    /// <summary>
    /// 在指定的段落中新增頁碼欄位。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    public void AddPageNumberField(OdfParagraph paragraph)
    {
        var fNode = new OdfNode(OdfNodeType.Element, "page-number", OdfNamespaces.Text, "text");
        fNode.SetAttribute("select-page", OdfNamespaces.Text, "current", "text");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中新增總頁數欄位。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    public void AddPageCountField(OdfParagraph paragraph)
    {
        var fNode = new OdfNode(OdfNodeType.Element, "page-count", OdfNamespaces.Text, "text");
        fNode.SetAttribute("num-format", OdfNamespaces.Style, "1", "style");
        paragraph.Node.AppendChild(fNode);
    }

    #endregion

    #region Multi-Column Sections Layouts

    /// <summary>
    /// 新增多欄版面配置區段至文件本文結尾。
    /// </summary>
    /// <param name="name">區段名稱</param>
    /// <param name="columnCount">欄位數量</param>
    /// <param name="gap">欄間距寬度</param>
    /// <returns>新建立的區段物件</returns>
    public OdfSection AddSection(string name, int columnCount, OdfLength gap)
    {
        var section = new OdfNode(OdfNodeType.Element, "section", OdfNamespaces.Text, "text");
        section.SetAttribute("name", OdfNamespaces.Text, name, "text");

        string styleName = StyleEngine.GetOrCreateLocalStyle(section, "section").GetAttribute("name", OdfNamespaces.Style) ?? "S1";
        StyleEngine.SetLocalStyleProperty(section, "section", "section-properties", "column-count", OdfNamespaces.Fo, columnCount.ToString(), "fo");
        StyleEngine.SetLocalStyleProperty(section, "section", "section-properties", "column-gap", OdfNamespaces.Fo, gap.ToString(), "fo");

        BodyTextRoot.AppendChild(section);
        return new OdfSection(section, this);
    }

    #endregion

    #region Tracked Changes (Accept/Reject)

    /// <summary>
    /// 取得或設定一個值，指出是否啟用修訂追蹤（追蹤修訂）。
    /// </summary>
    public bool TrackedChanges { get; set; }

    /// <summary>
    /// 記錄修訂追蹤資訊。
    /// </summary>
    /// <param name="changeType">修訂類型</param>
    /// <param name="extraContent">修訂的附加內容節點</param>
    /// <param name="originalStyleName">原本的樣式名稱</param>
    /// <param name="targetFamily">目標樣式系列名稱</param>
    /// <returns>產生的修訂識別碼</returns>
    public string RecordTrackedChange(string changeType, OdfNode? extraContent = null, string? originalStyleName = null, string? targetFamily = null)
    {
        OdfNode? tcNode = null;
        foreach (var child in BodyTextRoot.Children)
        {
            if (child.LocalName == "tracked-changes" && child.NamespaceUri == OdfNamespaces.Text)
            {
                tcNode = child;
                break;
            }
        }
        if (tcNode is null)
        {
            tcNode = new OdfNode(OdfNodeType.Element, "tracked-changes", OdfNamespaces.Text, "text");
            if (BodyTextRoot.Children.Count > 0)
                BodyTextRoot.InsertBefore(tcNode, BodyTextRoot.Children[0]);
            else
                BodyTextRoot.AppendChild(tcNode);
        }

        string changeId = "ct_" + Guid.NewGuid().ToString("N").Substring(0, 8);

        var changedRegion = new OdfNode(OdfNodeType.Element, "changed-region", OdfNamespaces.Text, "text");
        changedRegion.SetAttribute("id", OdfNamespaces.Text, changeId, "text");

        var typeNode = new OdfNode(OdfNodeType.Element, changeType, OdfNamespaces.Text, "text");
        if (changeType == "deletion" && extraContent is not null)
        {
            typeNode.AppendChild(extraContent.CloneNode(true));
        }
        else if (changeType == "format-change")
        {
            if (originalStyleName is not null)
            {
                typeNode.SetAttribute("style-name", OdfNamespaces.Text, originalStyleName, "text");
            }
            if (targetFamily is not null)
            {
                typeNode.SetAttribute("target-family", OdfNamespaces.Text, targetFamily, "text");
            }
        }
        changedRegion.AppendChild(typeNode);

        var changeInfo = new OdfNode(OdfNodeType.Element, "change-info", OdfNamespaces.Office, "office");
        typeNode.AppendChild(changeInfo);

        var creator = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc");
        creator.TextContent = "Author";
        changeInfo.AppendChild(creator);

        var date = new OdfNode(OdfNodeType.Element, "date", OdfNamespaces.Dc, "dc");
        date.TextContent = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        changeInfo.AppendChild(date);

        tcNode.AppendChild(changedRegion);
        return changeId;
    }

    /// <summary>
    /// 追蹤格式變更。
    /// </summary>
    /// <param name="node">發生變更的 ODF 節點</param>
    /// <param name="family">樣式系列名稱</param>
    public void TrackFormatChange(OdfNode node, string family)
    {
        if (!TrackedChanges) return;

        string styleAttr = "style-name";
        string styleNs = family switch
        {
            "table-cell" or "table-row" or "table-column" => OdfNamespaces.Table,
            "graphic" => OdfNamespaces.Draw,
            _ => OdfNamespaces.Text
        };
        string? originalStyleName = node.GetAttribute(styleAttr, styleNs);

        string changeId = RecordTrackedChange("format-change", null, originalStyleName, family);

        var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
        startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
        var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
        endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");

        if (node.LocalName == "p" || node.LocalName == "h")
        {
            if (node.Children.Count > 0)
            {
                node.InsertBefore(startNode, node.Children[0]);
            }
            else
            {
                node.AppendChild(startNode);
            }
            node.AppendChild(endNode);
        }
        else
        {
            var parent = node.Parent;
            if (parent is not null)
            {
                parent.InsertBefore(startNode, node);
                parent.InsertAfter(endNode, node);
            }
        }
    }

    /// <summary>
    /// 刪除指定的節點並記錄刪除修訂（若啟用修訂追蹤）。
    /// </summary>
    /// <param name="node">要刪除的 ODF 節點</param>
    public void DeleteNode(OdfNode node)
    {
        if (node.Parent is null) return;
        var parent = node.Parent;

        if (TrackedChanges)
        {
            string changeId = RecordTrackedChange("deletion", node);

            var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
            startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
            var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
            endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");

            parent.InsertBefore(startNode, node);
            parent.InsertAfter(endNode, node);
            parent.RemoveChild(node);
        }
        else
        {
            parent.RemoveChild(node);
        }
    }

    private List<OdfNode> FindAffectedNodesForFormatChange(string changeId)
    {
        List<OdfNode> affected = [];
        
        // 從修訂追蹤中尋找 targetFamily
        string? targetFamily = null;
        var tcNode = FindChild(BodyTextRoot, "tracked-changes", OdfNamespaces.Text);
        if (tcNode is not null)
        {
            foreach (var region in tcNode.Children)
            {
                if (region.GetAttribute("id", OdfNamespaces.Text) == changeId)
                {
                    foreach (var spec in region.Children)
                    {
                        if (spec.LocalName == "format-change" && spec.NamespaceUri == OdfNamespaces.Text)
                        {
                            targetFamily = spec.GetAttribute("target-family", OdfNamespaces.Text);
                            break;
                        }
                    }
                }
            }
        }

        var startNode = FindChangeNode(BodyTextRoot, "change-start", changeId);
        if (startNode is null || startNode.Parent is null) return affected;

        var parent = startNode.Parent;
        
        if (targetFamily == "paragraph")
        {
            affected.Add(parent);
            return affected;
        }

        if (parent.LocalName == "p" || parent.LocalName == "h")
        {
            var endNode = FindChangeNode(parent, "change-end", changeId);
            if (endNode is not null && endNode.Parent == parent)
            {
                List<OdfNode> siblingsBetween = [];
                bool collect = false;
                foreach (var child in parent.Children)
                {
                    if (child == startNode) { collect = true; continue; }
                    if (child == endNode) { collect = false; break; }
                    if (collect) siblingsBetween.Add(child);
                }

                if (siblingsBetween.Count > 0)
                {
                    foreach (var sibling in siblingsBetween)
                    {
                        if (sibling.LocalName == "span")
                        {
                            affected.Add(sibling);
                        }
                    }
                }
                else
                {
                    affected.Add(parent);
                }
            }
        }
        else
        {
            var endNode = FindChangeNode(BodyTextRoot, "change-end", changeId);
            if (endNode is not null && endNode.Parent == parent)
            {
                bool collect = false;
                foreach (var child in parent.Children)
                {
                    if (child == startNode) { collect = true; continue; }
                    if (child == endNode) { collect = false; break; }
                    if (collect) affected.Add(child);
                }
            }
        }

        if (affected.Count == 0)
        {
            affected.Add(parent);
        }

        return affected;
    }

    /// <summary>
    /// 接受文件中所有的追蹤修訂。
    /// </summary>
    public void AcceptAllTrackedChanges()
    {
        var tcNode = FindChild(BodyTextRoot, "tracked-changes", OdfNamespaces.Text);
        if (tcNode is null) return;

        var changes = new Dictionary<string, string>(StringComparer.Ordinal);
        ExtractTrackedChangesMeta(tcNode, changes);

        foreach (var kvp in changes)
        {
            if (kvp.Value == "deletion")
            {
                var purger = new ChangePurger(kvp.Key);
                purger.Purge(BodyTextRoot);
            }
        }

        CleanupRemainingChangeMarkers(BodyTextRoot);

        BodyTextRoot.RemoveChild(tcNode);
    }

    /// <summary>
    /// 拒絕文件中所有的追蹤修訂。
    /// </summary>
    public void RejectAllTrackedChanges()
    {
        var tcNode = FindChild(BodyTextRoot, "tracked-changes", OdfNamespaces.Text);
        if (tcNode is null) return;

        var changes = new Dictionary<string, string>(StringComparer.Ordinal);
        ExtractTrackedChangesMeta(tcNode, changes);

        foreach (var kvp in changes)
        {
            if (kvp.Value == "insertion")
            {
                var purger = new ChangePurger(kvp.Key);
                purger.Purge(BodyTextRoot);
            }
            else if (kvp.Value == "deletion")
            {
                RestoreDeletedContent(tcNode, kvp.Key);
            }
            else if (kvp.Value == "format-change")
            {
                string? originalStyleName = null;
                foreach (var changedRegion in tcNode.Children)
                {
                    if (changedRegion.GetAttribute("id", OdfNamespaces.Text) == kvp.Key)
                    {
                        foreach (var spec in changedRegion.Children)
                        {
                            if (spec.LocalName == "format-change" && spec.NamespaceUri == OdfNamespaces.Text)
                            {
                                originalStyleName = spec.GetAttribute("style-name", OdfNamespaces.Text);
                                break;
                            }
                        }
                    }
                }

                var affected = FindAffectedNodesForFormatChange(kvp.Key);
                foreach (var node in affected)
                {
                    string styleAttr = "style-name";
                    string styleNs = OdfNamespaces.Text;
                    if (node.LocalName == "table-cell" || node.LocalName == "table-row" || node.LocalName == "table-column")
                    {
                        styleNs = OdfNamespaces.Table;
                    }
                    else if (node.LocalName == "object" || node.LocalName == "frame")
                    {
                        styleNs = OdfNamespaces.Draw;
                    }

                    if (originalStyleName is not null)
                    {
                        node.SetAttribute(styleAttr, styleNs, originalStyleName);
                    }
                    else
                    {
                        node.RemoveAttribute(styleAttr, styleNs);
                    }
                }
            }
        }

        CleanupRemainingChangeMarkers(BodyTextRoot);
        BodyTextRoot.RemoveChild(tcNode);
    }

    /// <summary>
    /// 接受指定的追蹤修訂。
    /// </summary>
    /// <param name="changeId">修訂識別碼</param>
    public void AcceptChange(string changeId)
    {
        var tcNode = FindChild(BodyTextRoot, "tracked-changes", OdfNamespaces.Text);
        if (tcNode is null) return;

        var changes = new Dictionary<string, string>(StringComparer.Ordinal);
        ExtractTrackedChangesMeta(tcNode, changes);

        if (!changes.TryGetValue(changeId, out var type)) return;

        if (type == "deletion")
        {
            var purger = new ChangePurger(changeId);
            purger.Purge(BodyTextRoot);
        }

        RemoveChangeMarkersForId(BodyTextRoot, changeId);

        OdfNode? regionToRemove = null;
        foreach (var region in tcNode.Children)
        {
            if (region.GetAttribute("id", OdfNamespaces.Text) == changeId)
            {
                regionToRemove = region;
                break;
            }
        }
        if (regionToRemove is not null) tcNode.RemoveChild(regionToRemove);
        if (tcNode.Children.Count == 0) BodyTextRoot.RemoveChild(tcNode);
    }

    /// <summary>
    /// 拒絕指定的追蹤修訂。
    /// </summary>
    /// <param name="changeId">修訂識別碼</param>
    public void RejectChange(string changeId)
    {
        var tcNode = FindChild(BodyTextRoot, "tracked-changes", OdfNamespaces.Text);
        if (tcNode is null) return;

        var changes = new Dictionary<string, string>(StringComparer.Ordinal);
        ExtractTrackedChangesMeta(tcNode, changes);

        if (!changes.TryGetValue(changeId, out var type)) return;

        if (type == "insertion")
        {
            var purger = new ChangePurger(changeId);
            purger.Purge(BodyTextRoot);
        }
        else if (type == "deletion")
        {
            RestoreDeletedContent(tcNode, changeId);
        }
        else if (type == "format-change")
        {
            string? originalStyleName = null;
            foreach (var changedRegion in tcNode.Children)
            {
                if (changedRegion.GetAttribute("id", OdfNamespaces.Text) == changeId)
                {
                    foreach (var spec in changedRegion.Children)
                    {
                        if (spec.LocalName == "format-change" && spec.NamespaceUri == OdfNamespaces.Text)
                        {
                            originalStyleName = spec.GetAttribute("style-name", OdfNamespaces.Text);
                            break;
                        }
                    }
                }
            }

            var affected = FindAffectedNodesForFormatChange(changeId);
            foreach (var node in affected)
            {
                string styleAttr = "style-name";
                string styleNs = OdfNamespaces.Text;
                if (node.LocalName == "table-cell" || node.LocalName == "table-row" || node.LocalName == "table-column")
                {
                    styleNs = OdfNamespaces.Table;
                }
                else if (node.LocalName == "object" || node.LocalName == "frame")
                {
                    styleNs = OdfNamespaces.Draw;
                }

                if (originalStyleName is not null)
                {
                    node.SetAttribute(styleAttr, styleNs, originalStyleName);
                }
                else
                {
                    node.RemoveAttribute(styleAttr, styleNs);
                }
            }
        }

        RemoveChangeMarkersForId(BodyTextRoot, changeId);

        OdfNode? regionToRemove = null;
        foreach (var region in tcNode.Children)
        {
            if (region.GetAttribute("id", OdfNamespaces.Text) == changeId)
            {
                regionToRemove = region;
                break;
            }
        }
        if (regionToRemove is not null) tcNode.RemoveChild(regionToRemove);
        if (tcNode.Children.Count == 0) BodyTextRoot.RemoveChild(tcNode);
    }

    private void RestoreDeletedContent(OdfNode tcNode, string changeId)
    {
        OdfNode? deletionContent = null;
        foreach (var changedRegion in tcNode.Children)
        {
            if (changedRegion.GetAttribute("id", OdfNamespaces.Text) == changeId)
            {
                foreach (var spec in changedRegion.Children)
                {
                    if (spec.LocalName == "deletion" && spec.NamespaceUri == OdfNamespaces.Text)
                    {
                        deletionContent = spec;
                        break;
                    }
                }
            }
        }

        if (deletionContent is null) return;

        OdfNode? startNode = FindChangeNode(BodyTextRoot, "change-start", changeId);
        if (startNode is not null && startNode.Parent is not null)
        {
            var parent = startNode.Parent;
            foreach (var child in deletionContent.Children)
            {
                if (child.LocalName != "change-info")
                {
                    var imported = OdfNode.ImportNode(child, Package, Package);
                    parent.InsertBefore(imported, startNode);
                }
            }
        }
    }

    private OdfNode? FindChangeNode(OdfNode root, string localName, string changeId)
    {
        if (root.LocalName == localName && root.NamespaceUri == OdfNamespaces.Text && root.GetAttribute("change-id", OdfNamespaces.Text) == changeId)
        {
            return root;
        }
        foreach (var child in root.Children)
        {
            var found = FindChangeNode(child, localName, changeId);
            if (found is not null) return found;
        }
        return null;
    }

    private void RemoveChangeMarkersForId(OdfNode node, string changeId)
    {
        for (int i = node.Children.Count - 1; i >= 0; i--)
        {
            var child = node.Children[i];
            if ((child.LocalName == "change-start" || child.LocalName == "change-end") && 
                child.NamespaceUri == OdfNamespaces.Text && 
                child.GetAttribute("change-id", OdfNamespaces.Text) == changeId)
            {
                node.RemoveChild(child);
            }
            else
            {
                RemoveChangeMarkersForId(child, changeId);
            }
        }
    }

    private void ExtractTrackedChangesMeta(OdfNode tcNode, Dictionary<string, string> changes)
    {
        foreach (var changedRegion in tcNode.Children)
        {
            string? id = changedRegion.GetAttribute("id", OdfNamespaces.Text);
            if (string.IsNullOrEmpty(id)) continue;

            foreach (var spec in changedRegion.Children)
            {
                if (spec.LocalName == "insertion" && spec.NamespaceUri == OdfNamespaces.Text)
                {
                    changes[id!] = "insertion";
                }
                else if (spec.LocalName == "deletion" && spec.NamespaceUri == OdfNamespaces.Text)
                {
                    changes[id!] = "deletion";
                }
                else if (spec.LocalName == "format-change" && spec.NamespaceUri == OdfNamespaces.Text)
                {
                    changes[id!] = "format-change";
                }
            }
        }
    }

    private void CleanupRemainingChangeMarkers(OdfNode node)
    {
        for (int i = node.Children.Count - 1; i >= 0; i--)
        {
            var child = node.Children[i];
            if ((child.LocalName == "change-start" || child.LocalName == "change-end") && child.NamespaceUri == OdfNamespaces.Text)
            {
                node.RemoveChild(child);
            }
            else
            {
                CleanupRemainingChangeMarkers(child);
            }
        }
    }

    private class ChangePurger(string targetId)
    {
        private readonly string _targetId = targetId;
        private bool _foundStart = false;
        private bool _foundEnd = false;

        public void Purge(OdfNode node)
        {
            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                var child = node.Children[i];

                bool isEnd = (child.LocalName == "change-end" && child.NamespaceUri == OdfNamespaces.Text && child.GetAttribute("change-id", OdfNamespaces.Text) == _targetId);
                bool isStart = (child.LocalName == "change-start" && child.NamespaceUri == OdfNamespaces.Text && child.GetAttribute("change-id", OdfNamespaces.Text) == _targetId);

                if (isEnd)
                {
                    _foundEnd = true;
                    node.RemoveChild(child);
                    continue;
                }

                if (isStart)
                {
                    _foundStart = true;
                    node.RemoveChild(child);
                    continue;
                }

                bool wasEndFoundBefore = _foundEnd;
                bool wasStartFoundBefore = _foundStart;

                Purge(child);

                bool containedEnd = (!wasEndFoundBefore && _foundEnd);
                bool containedStart = (!wasStartFoundBefore && _foundStart);

                if (_foundEnd && !_foundStart && !containedEnd)
                {
                    node.RemoveChild(child);
                }
            }
        }
    }

    #endregion

    #region HTML Fragment Parsing

    private class SpanState
    {
        public bool? Bold { get; set; }
        public bool? Italic { get; set; }
        public bool? Underline { get; set; }
    }

    /// <summary>
    /// 在指定的段落中解析並新增 HTML 片段。
    /// </summary>
    /// <param name="paragraph">要加入 HTML 內容的段落</param>
    /// <param name="html">要解析的 HTML 字串片段</param>
    public void AddHtmlFragment(OdfParagraph paragraph, string html)
    {
        if (paragraph is null) throw new ArgumentNullException(nameof(paragraph));
        if (string.IsNullOrWhiteSpace(html)) return;

        // 移除所有 HTML 註解
        html = Regex.Replace(html, @"<!--[\s\S]*?-->", "");

        // 預先過濾 script 與 style 區段，包含其內的內容
        html = Regex.Replace(html, @"<(script|style)\b[^>]*>([\s\S]*?)<\/\1\s*>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<(script|style)\b[^>]*\/>", "", RegexOptions.IgnoreCase);

        // 移除延伸到輸入結尾且未閉合的 script/style 區段
        html = Regex.Replace(html, @"<(script|style)\b[^>]*>([\s\S]*)$", "", RegexOptions.IgnoreCase);

        var tokenRegex = new Regex(@"(<!--[\s\S]*?-->|</?[a-zA-Z][^>]*>|[^<]+|<)", RegexOptions.Compiled);
        var matches = tokenRegex.Matches(html);

        bool isBold = false;
        bool isItalic = false;
        bool isUnderline = false;
        string? currentHref = null;
        List<SpanState> spanStack = [];
        bool inScriptOrStyle = false;

        foreach (Match match in matches)
        {
            string text = match.Value;
            bool isTag = false;
            bool isClosing = false;
            string tagName = "";

            if (text.StartsWith("<") && !text.StartsWith("<!--"))
            {
                var tagMatch = Regex.Match(text, @"^<\s*(/?)\s*([a-zA-Z0-9]+)", RegexOptions.IgnoreCase);
                if (tagMatch.Success)
                {
                    isTag = true;
                    isClosing = tagMatch.Groups[1].Value == "/";
                    tagName = tagMatch.Groups[2].Value.ToLowerInvariant();
                }
            }

            if (isTag)
            {
                if (tagName == "script" || tagName == "style")
                {
                    bool isSelfClosing = text.EndsWith("/>");
                    if (!isSelfClosing)
                    {
                        inScriptOrStyle = !isClosing;
                    }
                    continue;
                }

                if (inScriptOrStyle)
                {
                    continue;
                }

                if (tagName == "b" || tagName == "strong")
                {
                    isBold = !isClosing;
                }
                else if (tagName == "i" || tagName == "em")
                {
                    isItalic = !isClosing;
                }
                else if (tagName == "u")
                {
                    isUnderline = !isClosing;
                }
                else if (tagName == "br")
                {
                    if (!isClosing)
                    {
                        paragraph.Node.AppendChild(new OdfNode(OdfNodeType.Element, "line-break", OdfNamespaces.Text, "text"));
                    }
                }
                else if (tagName == "a")
                {
                    if (isClosing)
                    {
                        currentHref = null;
                    }
                    else
                    {
                        var hrefMatch = Regex.Match(text, @"href\s*=\s*['""]?([^'""\s>]+)['""]?", RegexOptions.IgnoreCase);
                        if (hrefMatch.Success)
                        {
                            currentHref = hrefMatch.Groups[1].Value;
                        }
                    }
                }
                else if (tagName == "span")
                {
                    if (isClosing)
                    {
                        if (spanStack.Count > 0)
                        {
                            spanStack.RemoveAt(spanStack.Count - 1);
                        }
                    }
                    else
                    {
                        bool? styleBold = null;
                        bool? styleItalic = null;
                        bool? styleUnderline = null;

                        var styleMatch = Regex.Match(text, @"style\s*=\s*(?:""([^""]*)""|'([^']*)')", RegexOptions.IgnoreCase);
                        if (styleMatch.Success)
                        {
                            string styleStr = styleMatch.Groups[1].Success ? styleMatch.Groups[1].Value : styleMatch.Groups[2].Value;
                            
                            var boldMatch = Regex.Match(styleStr, @"font-weight\s*:\s*([^;]+)", RegexOptions.IgnoreCase);
                            if (boldMatch.Success)
                            {
                                string val = boldMatch.Groups[1].Value.Trim().ToLowerInvariant();
                                if (val == "bold" || val == "700" || val == "800" || val == "900")
                                {
                                    styleBold = true;
                                }
                                else if (val == "normal" || val == "400")
                                {
                                    styleBold = false;
                                }
                            }

                            var italicMatch = Regex.Match(styleStr, @"font-style\s*:\s*([^;]+)", RegexOptions.IgnoreCase);
                            if (italicMatch.Success)
                            {
                                string val = italicMatch.Groups[1].Value.Trim().ToLowerInvariant();
                                if (val == "italic" || val == "oblique")
                                {
                                    styleItalic = true;
                                }
                                else if (val == "normal")
                                {
                                    styleItalic = false;
                                }
                            }

                            var underlineMatch = Regex.Match(styleStr, @"text-decoration\s*:\s*([^;]+)", RegexOptions.IgnoreCase);
                            if (underlineMatch.Success)
                            {
                                string val = underlineMatch.Groups[1].Value.Trim().ToLowerInvariant();
                                if (val == "underline")
                                {
                                    styleUnderline = true;
                                }
                                else if (val == "none")
                                {
                                    styleUnderline = false;
                                }
                            }
                        }

                        spanStack.Add(new SpanState { Bold = styleBold, Italic = styleItalic, Underline = styleUnderline });
                    }
                }
            }
            else
            {
                if (inScriptOrStyle)
                {
                    continue;
                }

                string decodedText = DecodeHtmlEntities(text);

                bool activeBold = isBold;
                bool activeItalic = isItalic;
                bool activeUnderline = isUnderline;

                foreach (var state in spanStack)
                {
                    if (state.Bold.HasValue) activeBold = state.Bold.Value;
                    if (state.Italic.HasValue) activeItalic = state.Italic.Value;
                    if (state.Underline.HasValue) activeUnderline = state.Underline.Value;
                }

                if (currentHref is not null)
                {
                    var aNode = new OdfNode(OdfNodeType.Element, "a", OdfNamespaces.Text, "text");
                    aNode.SetAttribute("href", OdfNamespaces.XLink, currentHref, "xlink");
                    if (activeBold || activeItalic || activeUnderline)
                    {
                        var span = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text");
                        var run = new OdfTextRun(span, this) { Text = decodedText, IsBold = activeBold, IsItalic = activeItalic, IsUnderline = activeUnderline };
                        aNode.AppendChild(span);
                    }
                    else
                    {
                        aNode.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = decodedText });
                    }
                    paragraph.Node.AppendChild(aNode);
                }
                else
                {
                    if (activeBold || activeItalic || activeUnderline)
                    {
                        var span = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text");
                        var run = new OdfTextRun(span, this) { Text = decodedText, IsBold = activeBold, IsItalic = activeItalic, IsUnderline = activeUnderline };
                        paragraph.Node.AppendChild(span);
                    }
                    else
                    {
                        var lastChild = paragraph.Node.Children.Count > 0 ? paragraph.Node.Children[paragraph.Node.Children.Count - 1] : null;
                        if (lastChild is not null && lastChild.NodeType == OdfNodeType.Text)
                        {
                            lastChild.TextContent += decodedText;
                        }
                        else
                        {
                            paragraph.Node.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = decodedText });
                        }
                    }
                }
            }
        }
    }

    #endregion

    #region Table covered cells omissions

    /// <summary>
    /// 新增一個表格項目至文件本文結尾。
    /// </summary>
    /// <param name="rows">表格的列數</param>
    /// <param name="cols">表格的欄數</param>
    /// <returns>新建立的表格物件</returns>
    public OdfTable AddTable(int rows, int cols)
    {
        var table = new OdfNode(OdfNodeType.Element, "table", OdfNamespaces.Table, "table");
        BodyTextRoot.AppendChild(table);
        return new OdfTable(table, rows, cols, this);
    }

    #endregion

    #region Document Merging Logic Override

    /// <summary>
    /// 合併來源文件與目前文件的內容節點。
    /// </summary>
    /// <param name="sourceDoc">來源 OdfDocument 文件</param>
    /// <param name="options">合併設定選項</param>
    /// <param name="renameMap">變更樣式名稱的映射字典</param>
    protected override void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
    {
        var srcText = sourceDoc as TextDocument ?? throw new ArgumentException("Source document must be a TextDocument.");
        
        foreach (var child in srcText.BodyTextRoot.Children)
        {
            if (child.NodeType == OdfNodeType.Element)
            {
                var imported = OdfNode.ImportNode(child, srcText.Package, Package);
                RemapStylesInNodes(imported, renameMap);
                BodyTextRoot.AppendChild(imported);
            }
        }
    }

    #endregion

    #region XML Helper

    private OdfNode? FindChild(OdfNode parent, string localName, string ns)
    {
        foreach (var child in parent.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
                return child;
        }
        return null;
    }

    private static string DecodeHtmlEntities(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string decoded = System.Net.WebUtility.HtmlDecode(text);
        if (decoded.Contains("&apos;"))
        {
            decoded = decoded.Replace("&apos;", "'");
        }
        if (decoded.Contains("&APOS;"))
        {
            decoded = decoded.Replace("&APOS;", "'");
        }
        return decoded;
    }

    /// <summary>
    /// 在文件中新增字型宣告項目。
    /// </summary>
    /// <param name="name">字型代碼或別名</param>
    /// <param name="fontFamily">實際的字型名稱</param>
    /// <param name="genericFamily">泛用字型系列</param>
    /// <param name="pitch">字距模式</param>
    public void AddFontFace(string name, string fontFamily, string? genericFamily = null, string? pitch = null)
    {
        void AddToDom(OdfNode domRoot)
        {
            var fontDecls = FindOrCreateChild(domRoot, "font-face-decls", OdfNamespaces.Office, "office");
            foreach (var child in fontDecls.Children)
            {
                if (child.LocalName == "font-face" && child.NamespaceUri == OdfNamespaces.Style && child.GetAttribute("name", OdfNamespaces.Style) == name)
                {
                    child.SetAttribute("font-family", OdfNamespaces.Svg, fontFamily, "svg");
                    if (genericFamily is not null) child.SetAttribute("font-family-generic", OdfNamespaces.Style, genericFamily, "style");
                    if (pitch is not null) child.SetAttribute("font-pitch", OdfNamespaces.Style, pitch, "style");
                    return;
                }
            }

            var fontFace = new OdfNode(OdfNodeType.Element, "font-face", OdfNamespaces.Style, "style");
            fontFace.SetAttribute("name", OdfNamespaces.Style, name, "style");
            fontFace.SetAttribute("font-family", OdfNamespaces.Svg, fontFamily, "svg");
            if (genericFamily is not null) fontFace.SetAttribute("font-family-generic", OdfNamespaces.Style, genericFamily, "style");
            if (pitch is not null) fontFace.SetAttribute("font-pitch", OdfNamespaces.Style, pitch, "style");
            fontDecls.AppendChild(fontFace);
        }

        AddToDom(ContentDom);
        if (StylesDom is not null) AddToDom(StylesDom);
    }

    #endregion
}

/// <summary>
/// 表示文字文件的頁面設定。
/// </summary>
/// <param name="doc">所屬的文字文件</param>
public class OdfPageSetup(TextDocument doc)
{
    private readonly TextDocument _doc = doc;
    private OdfNode ContentDom => _doc.ContentDom;
    private OdfNode StylesDom => _doc.StylesDom;

    /// <summary>
    /// 取得或設定頁面寬度（公分）。
    /// </summary>
    public double PageWidth
    {
        get
        {
            string? val = GetPageProp("page-width");
            if (val is not null && val.EndsWith("cm") && double.TryParse(val.Substring(0, val.Length - 2), out var d))
                return d;
            return 21.0;
        }
        set => SetPageProp("page-width", $"{value}cm");
    }

    /// <summary>
    /// 取得或設定頁面高度（公分）。
    /// </summary>
    public double PageHeight
    {
        get
        {
            string? val = GetPageProp("page-height");
            if (val is not null && val.EndsWith("cm") && double.TryParse(val.Substring(0, val.Length - 2), out var d))
                return d;
            return 29.7;
        }
        set => SetPageProp("page-height", $"{value}cm");
    }

    /// <summary>
    /// 取得或設定頁面使用方式（例如 "all"、"left"、"right" 或 "mirrored"）。
    /// </summary>
    public string? PageUsage
    {
        get
        {
            var props = FindOrCreatePageLayoutProperties();
            return props.GetAttribute("page-usage", OdfNamespaces.Style);
        }
        set
        {
            var props = FindOrCreatePageLayoutProperties();
            props.SetAttribute("page-usage", OdfNamespaces.Style, value ?? "all", "style");
        }
    }

    /// <summary>
    /// 取得或設定頁面的文字書寫模式。
    /// </summary>
    public string? WritingMode
    {
        get
        {
            var props = FindOrCreatePageLayoutProperties();
            return props.GetAttribute("writing-mode", OdfNamespaces.Style);
        }
        set
        {
            var props = FindOrCreatePageLayoutProperties();
            props.SetAttribute("writing-mode", OdfNamespaces.Style, value ?? "lr-tb", "style");
        }
    }

    private string? GetPageStyleProp(string name)
    {
        var props = FindOrCreatePageLayoutProperties();
        return props.GetAttribute(name, OdfNamespaces.Style);
    }

    private void SetPageStyleProp(string name, string? val)
    {
        var props = FindOrCreatePageLayoutProperties();
        if (val is not null)
        {
            props.SetAttribute(name, OdfNamespaces.Style, val, "style");
        }
        else
        {
            props.RemoveAttribute(name, OdfNamespaces.Style);
        }
    }

    /// <summary>
    /// 取得或設定頁面版面配置網格的模式。
    /// </summary>
    public string? LayoutGridMode
    {
        get => GetPageStyleProp("layout-grid-mode");
        set => SetPageStyleProp("layout-grid-mode", value);
    }

    /// <summary>
    /// 取得或設定版面配置網格的基礎高度。
    /// </summary>
    public string? LayoutGridBaseHeight
    {
        get => GetPageStyleProp("layout-grid-base-height");
        set => SetPageStyleProp("layout-grid-base-height", value);
    }

    /// <summary>
    /// 取得或設定版面配置網格的基礎寬度。
    /// </summary>
    public string? LayoutGridBaseWidth
    {
        get => GetPageStyleProp("layout-grid-base-width");
        set => SetPageStyleProp("layout-grid-base-width", value);
    }

    /// <summary>
    /// 取得或設定版面配置網格的旁註標記（注音）高度。
    /// </summary>
    public string? LayoutGridRubyHeight
    {
        get => GetPageStyleProp("layout-grid-ruby-height");
        set => SetPageStyleProp("layout-grid-ruby-height", value);
    }

    /// <summary>
    /// 取得或設定版面配置網格的行數。
    /// </summary>
    public int? LayoutGridLines
    {
        get => int.TryParse(GetPageStyleProp("layout-grid-lines"), out var val) ? val : null;
        set => SetPageStyleProp("layout-grid-lines", value?.ToString());
    }

    /// <summary>
    /// 取得或設定版面配置網格的字數。
    /// </summary>
    public int? LayoutGridCharacters
    {
        get => int.TryParse(GetPageStyleProp("layout-grid-characters"), out var val) ? val : null;
        set => SetPageStyleProp("layout-grid-characters", value?.ToString());
    }

    /// <summary>
    /// 取得或設定一個值，指出是否顯示版面配置網格。
    /// </summary>
    public bool? LayoutGridDisplay
    {
        get => GetPageStyleProp("layout-grid-display") == "true" ? true : (GetPageStyleProp("layout-grid-display") == "false" ? false : null);
        set => SetPageStyleProp("layout-grid-display", value is null ? null : (value.Value ? "true" : "false"));
    }

    /// <summary>
    /// 取得或設定一個值，指出是否列印版面配置網格。
    /// </summary>
    public bool? LayoutGridPrint
    {
        get => GetPageStyleProp("layout-grid-print") == "true" ? true : (GetPageStyleProp("layout-grid-print") == "false" ? false : null);
        set => SetPageStyleProp("layout-grid-print", value is null ? null : (value.Value ? "true" : "false"));
    }

    /// <summary>
    /// 取得或設定頁首的文字內容。
    /// </summary>
    public string? HeaderText
    {
        get => GetHeaderFooterText("header");
        set => SetHeaderFooterText("header", value);
    }

    /// <summary>
    /// 取得或設定左頁首的文字內容。
    /// </summary>
    public string? HeaderLeftText
    {
        get => GetHeaderFooterText("header-left");
        set => SetHeaderFooterText("header-left", value);
    }

    /// <summary>
    /// 取得或設定頁尾的文字內容。
    /// </summary>
    public string? FooterText
    {
        get => GetHeaderFooterText("footer");
        set => SetHeaderFooterText("footer", value);
    }

    /// <summary>
    /// 取得或設定左頁尾的文字內容。
    /// </summary>
    public string? FooterLeftText
    {
        get => GetHeaderFooterText("footer-left");
        set => SetHeaderFooterText("footer-left", value);
    }

    private string? GetPageProp(string name)
    {
        var props = FindOrCreatePageLayoutProperties();
        return props.GetAttribute(name, OdfNamespaces.Fo);
    }

    private void SetPageProp(string name, string val)
    {
        var props = FindOrCreatePageLayoutProperties();
        props.SetAttribute(name, OdfNamespaces.Fo, val, "fo");
    }

    private OdfNode FindOrCreatePageLayoutNode()
    {
        var autoStyles = FindOrCreateChild(_doc.StylesDom, "automatic-styles", OdfNamespaces.Office, "office");
        foreach (var child in autoStyles.Children)
        {
            if (child.LocalName == "page-layout" && child.NamespaceUri == OdfNamespaces.Style)
            {
                return child;
            }
        }
        var pageLayout = new OdfNode(OdfNodeType.Element, "page-layout", OdfNamespaces.Style, "style");
        pageLayout.SetAttribute("name", OdfNamespaces.Style, "Mpm1", "style");
        autoStyles.AppendChild(pageLayout);
        return pageLayout;
    }

    private OdfNode FindOrCreatePageLayoutProperties()
    {
        var layoutNode = FindOrCreatePageLayoutNode();
        foreach (var child in layoutNode.Children)
        {
            if (child.LocalName == "page-layout-properties" && child.NamespaceUri == OdfNamespaces.Style)
            {
                return child;
            }
        }
        var props = new OdfNode(OdfNodeType.Element, "page-layout-properties", OdfNamespaces.Style, "style");
        layoutNode.AppendChild(props);
        return props;
    }

    private OdfNode FindOrCreateMasterPage()
    {
        var masterStyles = FindOrCreateChild(_doc.StylesDom, "master-styles", OdfNamespaces.Office, "office");
        foreach (var child in masterStyles.Children)
        {
            if (child.LocalName == "master-page" && child.NamespaceUri == OdfNamespaces.Style)
            {
                return child;
            }
        }
        var masterPage = new OdfNode(OdfNodeType.Element, "master-page", OdfNamespaces.Style, "style");
        masterPage.SetAttribute("name", OdfNamespaces.Style, "Standard", "style");
        masterPage.SetAttribute("page-layout-name", OdfNamespaces.Style, "Mpm1", "style");
        masterStyles.AppendChild(masterPage);
        return masterPage;
    }

    private string? GetHeaderFooterText(string localName)
    {
        var mp = FindOrCreateMasterPage();
        foreach (var child in mp.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == OdfNamespaces.Style)
            {
                foreach (var p in child.Children)
                {
                    if (p.LocalName == "p" && p.NamespaceUri == OdfNamespaces.Text)
                    {
                        return p.TextContent;
                    }
                }
            }
        }
        return null;
    }

    private void SetHeaderFooterText(string localName, string? value)
    {
        var mp = FindOrCreateMasterPage();
        OdfNode? target = null;
        foreach (var child in mp.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == OdfNamespaces.Style)
            {
                target = child;
                break;
            }
        }

        if (value is null)
        {
            if (target is not null) mp.RemoveChild(target);
        }
        else
        {
            if (target is null)
            {
                target = new OdfNode(OdfNodeType.Element, localName, OdfNamespaces.Style, "style");
                mp.AppendChild(target);
            }
            
            OdfNode? pNode = null;
            foreach (var child in target.Children)
            {
                if (child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    pNode = child;
                    break;
                }
            }
            if (pNode is null)
            {
                pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
                target.AppendChild(pNode);
            }
            pNode.TextContent = value;
        }
    }

    private OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
    {
        foreach (var child in parent.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
                return child;
        }
        var node = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
        if (localName == "font-face-decls" && parent.Children.Count > 0)
        {
            parent.InsertBefore(node, parent.Children[0]);
        }
        else
        {
            parent.AppendChild(node);
        }
        return node;
    }

    /// <summary>
    /// 在頁面設定中新增字型宣告項目。
    /// </summary>
    /// <param name="name">字型代碼或別名</param>
    /// <param name="fontFamily">實際的字型名稱</param>
    /// <param name="genericFamily">泛用字型系列</param>
    /// <param name="pitch">字距模式</param>
    public void AddFontFace(string name, string fontFamily, string? genericFamily = null, string? pitch = null)
    {
        void AddToDom(OdfNode domRoot)
        {
            var fontDecls = FindOrCreateChild(domRoot, "font-face-decls", OdfNamespaces.Office, "office");
            foreach (var child in fontDecls.Children)
            {
                if (child.LocalName == "font-face" && child.NamespaceUri == OdfNamespaces.Style && child.GetAttribute("name", OdfNamespaces.Style) == name)
                {
                    child.SetAttribute("font-family", OdfNamespaces.Svg, fontFamily, "svg");
                    if (genericFamily is not null) child.SetAttribute("font-family-generic", OdfNamespaces.Style, genericFamily, "style");
                    if (pitch is not null) child.SetAttribute("font-pitch", OdfNamespaces.Style, pitch, "style");
                    return;
                }
            }

            var fontFace = new OdfNode(OdfNodeType.Element, "font-face", OdfNamespaces.Style, "style");
            fontFace.SetAttribute("name", OdfNamespaces.Style, name, "style");
            fontFace.SetAttribute("font-family", OdfNamespaces.Svg, fontFamily, "svg");
            if (genericFamily is not null) fontFace.SetAttribute("font-family-generic", OdfNamespaces.Style, genericFamily, "style");
            if (pitch is not null) fontFace.SetAttribute("font-pitch", OdfNamespaces.Style, pitch, "style");
            fontDecls.AppendChild(fontFace);
        }

        AddToDom(_doc.ContentDom);
        if (_doc.StylesDom is not null) AddToDom(_doc.StylesDom);
    }
}

/// <summary>
/// 表示文字文件中的段落。
/// </summary>
/// <param name="node">與此段落相關聯的 OdfNode 節點</param>
/// <param name="doc">取得所屬的文字文件</param>
public class OdfParagraph(OdfNode node, TextDocument doc)
{
    /// <summary>
    /// 取得與此段落相關聯的 OdfNode 節點。
    /// </summary>
    public OdfNode Node { get; } = node;

    /// <summary>
    /// 取得所屬的文字文件。
    /// </summary>
    protected readonly TextDocument Doc = doc;

    /// <summary>
    /// 取得或設定段落的文字內容。
    /// </summary>
    public string TextContent
    {
        get => Node.TextContent;
        set => Node.TextContent = value;
    }

    /// <summary>
    /// 取得或設定段落的樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => Node.GetAttribute("style-name", OdfNamespaces.Text);
        set
        {
            if (Doc.TrackedChanges)
            {
                Doc.TrackFormatChange(Node, "paragraph");
            }
            Node.SetAttribute("style-name", OdfNamespaces.Text, value ?? string.Empty, "text");
        }
    }

    /// <summary>
    /// 取得或設定段落的水平對齊方式。
    /// </summary>
    public string? HorizontalAlignment
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "text-align", OdfNamespaces.Fo, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "text-align", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// 取得或設定段落的書寫模式。
    /// </summary>
    public string? WritingMode
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "writing-mode", OdfNamespaces.Style, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "writing-mode", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// 取得或設定段落的西文字型名稱。
    /// </summary>
    public string? FontName
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-name", OdfNamespaces.Style, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "font-name", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// 取得或設定段落的東亞（中日韓）字型名稱。
    /// </summary>
    public string? FontNameAsian
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-name-asian", OdfNamespaces.Style, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "font-name-asian", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// 取得或設定段落的複雜文字字型名稱。
    /// </summary>
    public string? FontNameComplex
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-name-complex", OdfNamespaces.Style, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "font-name-complex", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// 取得或設定段落的西文字型大小。
    /// </summary>
    public string? FontSize
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-size", OdfNamespaces.Fo, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "font-size", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// 取得或設定段落的東亞（中日韓）字型大小。
    /// </summary>
    public string? FontSizeAsian
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-size-asian", OdfNamespaces.Fo, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "font-size-asian", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// 取得或設定段落的複雜文字字型大小。
    /// </summary>
    public string? FontSizeComplex
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-size-complex", OdfNamespaces.Fo, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "font-size-complex", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// 設定段落的字型名稱。
    /// </summary>
    /// <param name="westernFont">西文字型名稱</param>
    /// <param name="asianFont">東亞（中日韓）字型名稱</param>
    /// <param name="complexFont">複雜文字字型名稱</param>
    public void SetFont(string westernFont, string? asianFont = null, string? complexFont = null)
    {
        FontName = westernFont;
        FontNameAsian = asianFont ?? westernFont;
        FontNameComplex = complexFont ?? westernFont;
    }

    /// <summary>
    /// 設定段落的字型大小。
    /// </summary>
    /// <param name="westernSize">西文字型大小</param>
    /// <param name="asianSize">東亞字型大小</param>
    /// <param name="complexSize">複雜文字字型大小</param>
    public void SetFontSize(string westernSize, string? asianSize = null, string? complexSize = null)
    {
        FontSize = westernSize;
        FontSizeAsian = asianSize ?? westernSize;
        FontSizeComplex = complexSize ?? westernSize;
    }

    /// <summary>
    /// 在段落結尾新增一個文字片段。
    /// </summary>
    /// <param name="text">要新增的文字內容</param>
    /// <returns>建立的文字片段物件</returns>
    public OdfTextRun AddTextRun(string text)
    {
        var spanNode = OdfNodeFactory.CreateElement("span", OdfNamespaces.Text, "text");
        spanNode.TextContent = text;
        if (Doc.TrackedChanges)
        {
            string changeId = Doc.RecordTrackedChange("insertion");
            var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
            startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
            var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
            endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
            
            Node.AppendChild(startNode);
            Node.AppendChild(spanNode);
            Node.AppendChild(endNode);
        }
        else
        {
            Node.AppendChild(spanNode);
        }
        return new OdfTextRun(spanNode, Doc);
    }

    /// <summary>
    /// 在段落中新增軟分頁符號。
    /// </summary>
    public void AddSoftPageBreak()
    {
        var node = OdfNodeFactory.CreateElement("soft-page-break", OdfNamespaces.Text, "text");
        Node.AppendChild(node);
    }

    /// <summary>
    /// 在段落中新增定位點（Tab）字元。
    /// </summary>
    public void AddTab()
    {
        var node = OdfNodeFactory.CreateElement("tab", OdfNamespaces.Text, "text");
        Node.AppendChild(node);
    }

    /// <summary>
    /// 在段落中新增換行符號。
    /// </summary>
    public void AddLineBreak()
    {
        var node = OdfNodeFactory.CreateElement("line-break", OdfNamespaces.Text, "text");
        Node.AppendChild(node);
    }

    /// <summary>
    /// 在段落中新增指定數量的空格項目。
    /// </summary>
    /// <param name="count">空格數量</param>
    public void AddSpace(int count = 1)
    {
        var node = OdfNodeFactory.CreateElement("s", OdfNamespaces.Text, "text");
        if (count > 1)
        {
            node.SetAttribute("c", OdfNamespaces.Text, count.ToString(), "text");
        }
        Node.AppendChild(node);
    }

    /// <summary>
    /// 刪除此段落。
    /// </summary>
    public void Delete()
    {
        Doc.DeleteNode(Node);
    }
}

/// <summary>
/// 表示文字文件中的標題。
/// </summary>
/// <param name="node">與此標題相關聯的 OdfNode 節點</param>
/// <param name="doc">所屬的文字文件</param>
public class OdfHeading(OdfNode node, TextDocument doc) : OdfParagraph(node, doc)
{
    /// <summary>
    /// 取得或設定標題的大綱階層。
    /// </summary>
    public int OutlineLevel
    {
        get => int.TryParse(Node.GetAttribute("outline-level", OdfNamespaces.Text), out var lvl) ? lvl : 1;
        set => Node.SetAttribute("outline-level", OdfNamespaces.Text, value.ToString(), "text");
    }
}

/// <summary>
/// 表示段落中的文字片段（Span）。
/// </summary>
/// <param name="node">與此文字片段相關聯的 OdfNode 節點</param>
/// <param name="doc">取得所屬的文字文件</param>
public class OdfTextRun(OdfNode node, TextDocument doc)
{
    /// <summary>
    /// 取得與此文字片段相關聯的 OdfNode 節點。
    /// </summary>
    public OdfNode Node { get; } = node;

    private readonly TextDocument _doc = doc;

    /// <summary>
    /// 取得或設定文字片段的內文。
    /// </summary>
    public string Text
    {
        get => Node.TextContent;
        set => Node.TextContent = value;
    }

    /// <summary>
    /// 取得或設定文字片段的樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => Node.GetAttribute("style-name", OdfNamespaces.Text);
        set
        {
            if (_doc.TrackedChanges)
            {
                _doc.TrackFormatChange(Node, "text");
            }
            Node.SetAttribute("style-name", OdfNamespaces.Text, value ?? string.Empty, "text");
        }
    }

    /// <summary>
    /// 取得或設定文字片段的西文字型名稱。
    /// </summary>
    public string? FontName
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-name", OdfNamespaces.Style, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-name", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// 取得或設定文字片段的西文字型大小。
    /// </summary>
    public string? FontSize
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-size", OdfNamespaces.Fo, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-size", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// 設定文字片段的字型名稱。
    /// </summary>
    /// <param name="westernFont">西文字型名稱</param>
    /// <param name="asianFont">東亞（中日韓）字型名稱</param>
    /// <param name="complexFont">複雜文字字型名稱</param>
    public void SetFont(string westernFont, string? asianFont = null, string? complexFont = null)
    {
        FontName = westernFont;
        FontNameAsian = asianFont ?? westernFont;
        FontNameComplex = complexFont ?? westernFont;
    }

    /// <summary>
    /// 設定文字片段的字型大小。
    /// </summary>
    /// <param name="westernSize">西文字型大小</param>
    /// <param name="asianSize">東亞字型大小</param>
    /// <param name="complexSize">複雜文字字型大小</param>
    public void SetFontSize(string westernSize, string? asianSize = null, string? complexSize = null)
    {
        FontSize = westernSize;
        FontSizeAsian = asianSize ?? westernSize;
        FontSizeComplex = complexSize ?? westernSize;
    }

    /// <summary>
    /// 取得或設定一個值，指出文字片段是否為粗體。
    /// </summary>
    public bool IsBold
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-weight", OdfNamespaces.Fo, "text") == "bold";
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-weight", OdfNamespaces.Fo, value ? "bold" : "normal", "fo");
    }

    /// <summary>
    /// 取得或設定一個值，指出文字片段是否為斜體。
    /// </summary>
    public bool IsItalic
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-style", OdfNamespaces.Fo, "text") == "italic";
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-style", OdfNamespaces.Fo, value ? "italic" : "normal", "fo");
    }

    /// <summary>
    /// 取得或設定一個值，指出文字片段是否加上底線。
    /// </summary>
    public bool IsUnderline
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "text-underline-style", OdfNamespaces.Style, "text") == "solid";
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "text-underline-style", OdfNamespaces.Style, value ? "solid" : "none", "style");
    }

    /// <summary>
    /// 取得或設定文字片段的東亞（中日韓）字型名稱。
    /// </summary>
    public string? FontNameAsian
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-name-asian", OdfNamespaces.Style, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-name-asian", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// 取得或設定文字片段的複雜文字字型名稱。
    /// </summary>
    public string? FontNameComplex
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-name-complex", OdfNamespaces.Style, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-name-complex", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// 取得或設定文字片段的東亞（中日韓）字型大小。
    /// </summary>
    public string? FontSizeAsian
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-size-asian", OdfNamespaces.Fo, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-size-asian", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// 取得或設定文字片段的複雜文字字型大小。
    /// </summary>
    public string? FontSizeComplex
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-size-complex", OdfNamespaces.Fo, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-size-complex", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    private string GetStyleName() => Node.GetAttribute("style-name", OdfNamespaces.Text) ?? string.Empty;

    /// <summary>
    /// 刪除此文字片段。
    /// </summary>
    public void Delete()
    {
        _doc.DeleteNode(Node);
    }
}

/// <summary>
/// 表示文字文件中的多欄版面配置區段。
/// </summary>
/// <param name="node">與此區段相關聯的 OdfNode 節點</param>
/// <param name="doc">取得所屬的文字文件</param>
public class OdfSection(OdfNode node, TextDocument doc)
{
    /// <summary>
    /// 取得與此區段相關聯的 OdfNode 節點。
    /// </summary>
    public OdfNode Node { get; } = node;

    private readonly TextDocument _doc = doc;

    /// <summary>
    /// 取得或設定此區段的書寫模式。
    /// </summary>
    public string? WritingMode
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "writing-mode", OdfNamespaces.Style, "section");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "section", "section-properties", "writing-mode", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    private string GetStyleName() => Node.GetAttribute("style-name", OdfNamespaces.Text) ?? string.Empty;
}

/// <summary>
/// 表示文字文件中的表格。
/// </summary>
public class OdfTable
{
    /// <summary>
    /// 取得與此表格相關聯的 OdfNode 節點。
    /// </summary>
    public OdfNode Node { get; }

    private readonly TextDocument _doc;
    private readonly int _rows;
    private readonly int _cols;

    /// <summary>
    /// 初始化 <see cref="OdfTable"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">表格的 OdfNode 節點</param>
    /// <param name="rows">表格列數</param>
    /// <param name="cols">表格欄數</param>
    /// <param name="doc">所屬的文字文件</param>
    public OdfTable(OdfNode node, int rows, int cols, TextDocument doc)
    {
        Node = node;
        _rows = rows;
        _cols = cols;
        _doc = doc;
        BuildGrid();
    }

    private void BuildGrid()
    {
        for (int r = 0; r < _rows; r++)
        {
            var rNode = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");
            for (int c = 0; c < _cols; c++)
            {
                var cNode = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table");
                rNode.AppendChild(cNode);
            }
            Node.AppendChild(rNode);
        }
    }

    /// <summary>
    /// 合併表格中的儲存格。
    /// </summary>
    /// <param name="startRow">起始列索引</param>
    /// <param name="startCol">起始欄索引</param>
    /// <param name="rowSpan">橫跨的列數</param>
    /// <param name="colSpan">橫跨的欄數</param>
    public void MergeCells(int startRow, int startCol, int rowSpan, int colSpan)
    {
        List<OdfNode> rows = [];
        foreach (var child in Node.Children)
        {
            if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
            {
                rows.Add(child);
            }
        }

        var targetRowNode = rows[startRow];
        List<OdfNode> cellsInTargetRow = [];
        foreach (var child in targetRowNode.Children)
        {
            if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") && child.NamespaceUri == OdfNamespaces.Table)
            {
                cellsInTargetRow.Add(child);
            }
        }
        var targetCell = cellsInTargetRow[startCol];
        targetCell.SetAttribute("number-rows-spanned", OdfNamespaces.Table, rowSpan.ToString(), "table");
        targetCell.SetAttribute("number-columns-spanned", OdfNamespaces.Table, colSpan.ToString(), "table");

        for (int r = startRow; r < startRow + rowSpan; r++)
        {
            var rowNode = rows[r];
            List<OdfNode> cellsInRow = [];
            foreach (var child in rowNode.Children)
            {
                if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") && child.NamespaceUri == OdfNamespaces.Table)
                {
                    cellsInRow.Add(child);
                }
            }

            for (int c = startCol; c < startCol + colSpan; c++)
            {
                if (r == startRow && c == startCol) continue;
                
                var cellToRemove = cellsInRow[c];
                var coveredNode = new OdfNode(OdfNodeType.Element, "covered-table-cell", OdfNamespaces.Table, "table");
                rowNode.InsertBefore(coveredNode, cellToRemove);
                rowNode.RemoveChild(cellToRemove);
            }
        }
    }

    /// <summary>
    /// 在指定儲存格中新增巢狀表格。
    /// </summary>
    /// <param name="row">儲存格列索引</param>
    /// <param name="col">儲存格欄索引</param>
    /// <param name="nestedRows">巢狀表格列數</param>
    /// <param name="nestedCols">巢狀表格欄數</param>
    /// <returns>建立的巢狀表格物件</returns>
    public OdfTable AddNestedTable(int row, int col, int nestedRows, int nestedCols)
    {
        var cellNode = GetCellNode(row, col);
        var nestedTableNode = OdfNodeFactory.CreateElement("table", OdfNamespaces.Table, "table");
        cellNode.AppendChild(nestedTableNode);
        return new OdfTable(nestedTableNode, nestedRows, nestedCols, _doc);
    }

    /// <summary>
    /// 設定指定儲存格的樣式名稱。
    /// </summary>
    /// <param name="row">儲存格列索引</param>
    /// <param name="col">儲存格欄索引</param>
    /// <param name="styleName">樣式名稱</param>
    public void SetCellStyle(int row, int col, string styleName)
    {
        var cellNode = GetCellNode(row, col);
        cellNode.SetAttribute("style-name", OdfNamespaces.Table, styleName, "table");
    }

    /// <summary>
    /// 設定指定列的重複次數。
    /// </summary>
    /// <param name="row">列索引</param>
    /// <param name="repeatCount">重複次數</param>
    public void SetRowRepeat(int row, int repeatCount)
    {
        List<OdfNode> rows = [];
        foreach (var child in Node.Children)
        {
            if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
                rows.Add(child);
        }
        var rowNode = rows[row];
        rowNode.SetAttribute("number-rows-repeated", OdfNamespaces.Table, repeatCount.ToString(), "table");
    }

    private OdfNode GetCellNode(int row, int col)
    {
        List<OdfNode> rows = [];
        foreach (var child in Node.Children)
        {
            if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
                rows.Add(child);
        }
        var rowNode = rows[row];
        List<OdfNode> cells = [];
        foreach (var child in rowNode.Children)
        {
            if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") && child.NamespaceUri == OdfNamespaces.Table)
                cells.Add(child);
        }
        return cells[col];
    }

    /// <summary>
    /// 取得指定的儲存格物件。
    /// </summary>
    /// <param name="row">列索引</param>
    /// <param name="col">欄索引</param>
    /// <returns>對應的儲存格執行個體</returns>
    public OdfTableCell GetCell(int row, int col)
    {
        var cellNode = GetCellNode(row, col);
        return new OdfTableCell(cellNode, _doc);
    }

    /// <summary>
    /// 設定指定欄的欄寬。
    /// </summary>
    /// <param name="col">欄位索引</param>
    /// <param name="width">欄寬值</param>
    public void SetColumnWidth(int col, OdfLength width)
    {
        var colNode = GetOrCreateColumnNode(col);
        _doc.StyleEngine.SetLocalStyleProperty(colNode, "table-column", "table-column-properties", "column-width", OdfNamespaces.Style, width.ToString(), "style");
    }

    private OdfNode GetOrCreateColumnNode(int col)
    {
        List<OdfNode> cols = [];
        OdfNode? firstNonCol = null;
        foreach (var child in Node.Children)
        {
            if (child.NodeType == OdfNodeType.Element)
            {
                if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
                {
                    cols.Add(child);
                }
                else if (firstNonCol is null)
                {
                    firstNonCol = child;
                }
            }
        }

        while (cols.Count <= col)
        {
            var newCol = OdfNodeFactory.CreateElement("table-column", OdfNamespaces.Table, "table");
            if (firstNonCol is not null)
            {
                Node.InsertBefore(newCol, firstNonCol);
            }
            else
            {
                Node.AppendChild(newCol);
            }
            cols.Add(newCol);
        }

        return cols[col];
    }
}

/// <summary>
/// 表示文字文件中的清單。
/// </summary>
/// <param name="node">與此清單相關聯的 OdfNode 節點</param>
/// <param name="doc">所屬的文字文件</param>
public class OdfList(OdfNode node, TextDocument doc)
{
    /// <summary>
    /// 取得與此清單相關聯的 OdfNode 節點。
    /// </summary>
    public OdfNode Node { get; } = node;

    private readonly TextDocument _doc = doc;

    /// <summary>
    /// 取得或設定此清單的樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => Node.GetAttribute("style-name", OdfNamespaces.Text);
        set => Node.SetAttribute("style-name", OdfNamespaces.Text, value ?? string.Empty, "text");
    }

    /// <summary>
    /// 取得或設定一個值，指出清單編號是否延續上一清單。
    /// </summary>
    public bool? ContinueNumbering
    {
        get => Node.GetAttribute("continue-numbering", OdfNamespaces.Text) == "true" ? true : (Node.GetAttribute("continue-numbering", OdfNamespaces.Text) == "false" ? false : null);
        set
        {
            if (value.HasValue)
                Node.SetAttribute("continue-numbering", OdfNamespaces.Text, value.Value ? "true" : "false", "text");
            else
                Node.RemoveAttribute("continue-numbering", OdfNamespaces.Text);
        }
    }

    /// <summary>
    /// 在清單中新增清單項目。
    /// </summary>
    /// <param name="text">項目預設段落文字內容</param>
    /// <returns>新建立的清單項目執行個體</returns>
    public OdfListItem AddListItem(string text = "")
    {
        var itemNode = OdfNodeFactory.CreateElement("list-item", OdfNamespaces.Text, "text");
        Node.AppendChild(itemNode);
        var item = new OdfListItem(itemNode, _doc);
        if (!string.IsNullOrEmpty(text))
        {
            item.AddParagraph(text);
        }
        return item;
    }

    /// <summary>
    /// 重新開始清單的編號。
    /// </summary>
    /// <param name="startValue">開始數值</param>
    public void RestartNumbering(int startValue = 1)
    {
        ContinueNumbering = false;
        var firstItemNode = Node.Children.FirstOrDefault(c => c.LocalName == "list-item" && c.NamespaceUri == OdfNamespaces.Text);
        if (firstItemNode is not null)
        {
            var item = new OdfListItem(firstItemNode, _doc);
            item.StartValue = startValue;
        }
    }
}

/// <summary>
/// 表示清單中的清單項目。
/// </summary>
/// <param name="node">與此清單項目相關聯的 OdfNode 節點</param>
/// <param name="doc">所屬的文字文件</param>
public class OdfListItem(OdfNode node, TextDocument doc)
{
    /// <summary>
    /// 取得與此清單項目相關聯的 OdfNode 節點。
    /// </summary>
    public OdfNode Node { get; } = node;

    private readonly TextDocument _doc = doc;

    /// <summary>
    /// 取得或設定此清單項目的起始數值。
    /// </summary>
    public int? StartValue
    {
        get => int.TryParse(Node.GetAttribute("start-value", OdfNamespaces.Text), out var val) ? val : null;
        set
        {
            if (value.HasValue)
                Node.SetAttribute("start-value", OdfNamespaces.Text, value.Value.ToString(), "text");
            else
                Node.RemoveAttribute("start-value", OdfNamespaces.Text);
        }
    }

    /// <summary>
    /// 在清單項目中新增段落。
    /// </summary>
    /// <param name="text">段落的預設內文</param>
    /// <returns>建立的段落物件</returns>
    public OdfParagraph AddParagraph(string text = "")
    {
        var pNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        pNode.TextContent = text;
        Node.AppendChild(pNode);
        return new OdfParagraph(pNode, _doc);
    }

    /// <summary>
    /// 在清單項目中新增巢狀清單。
    /// </summary>
    /// <param name="styleName">項目清單樣式名稱</param>
    /// <returns>新建立的巢狀清單執行個體</returns>
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
}

/// <summary>
/// 表示文字文件中的圖片。
/// </summary>
/// <param name="frameNode">圖片的外框節點</param>
/// <param name="imageNode">圖片的影像節點</param>
public class OdfImage(OdfNode frameNode, OdfNode imageNode)
{
    /// <summary>
    /// 取得圖片的外框節點。
    /// </summary>
    public OdfNode FrameNode { get; } = frameNode;

    /// <summary>
    /// 取得圖片的影像節點。
    /// </summary>
    public OdfNode ImageNode { get; } = imageNode;

    /// <summary>
    /// 取得或設定圖片的名稱。
    /// </summary>
    public string? Name
    {
        get => FrameNode.GetAttribute("name", OdfNamespaces.Draw);
        set => FrameNode.SetAttribute("name", OdfNamespaces.Draw, value ?? string.Empty, "draw");
    }

    /// <summary>
    /// 取得或設定圖片的錨定類型。
    /// </summary>
    public string? AnchorType
    {
        get => FrameNode.GetAttribute("anchor-type", OdfNamespaces.Text);
        set => FrameNode.SetAttribute("anchor-type", OdfNamespaces.Text, value ?? "paragraph", "text");
    }

    /// <summary>
    /// 取得或設定圖片的寬度。
    /// </summary>
    public string? Width
    {
        get => FrameNode.GetAttribute("width", OdfNamespaces.Svg);
        set => FrameNode.SetAttribute("width", OdfNamespaces.Svg, value ?? string.Empty, "svg");
    }

    /// <summary>
    /// 取得或設定圖片的高度。
    /// </summary>
    public string? Height
    {
        get => FrameNode.GetAttribute("height", OdfNamespaces.Svg);
        set => FrameNode.SetAttribute("height", OdfNamespaces.Svg, value ?? string.Empty, "svg");
    }

    /// <summary>
    /// 取得或設定圖片的文繞圖樣式。
    /// </summary>
    public string? WrapStyle
    {
        get => FrameNode.GetAttribute("wrap-style", OdfNamespaces.Style);
        set => FrameNode.SetAttribute("wrap-style", OdfNamespaces.Style, value ?? "none", "style");
    }

    /// <summary>
    /// 取得或設定圖片的裁剪邊界。
    /// </summary>
    public string? CropTop
    {
        get => ImageNode.GetAttribute("clip", OdfNamespaces.Fo);
        set => ImageNode.SetAttribute("clip", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }
}

/// <summary>
/// 表示表格中的儲存格。
/// </summary>
/// <param name="node">與此儲存格相關聯的 OdfNode 節點</param>
/// <param name="doc">所屬的文字文件</param>
public class OdfTableCell(OdfNode node, TextDocument doc)
{
    /// <summary>
    /// 取得與此儲存格相關聯的 OdfNode 節點。
    /// </summary>
    public OdfNode Node { get; } = node;

    private readonly TextDocument _doc = doc;

    /// <summary>
    /// 取得或設定儲存格的文字內容。
    /// </summary>
    public string TextContent
    {
        get => Node.TextContent;
        set => Node.TextContent = value;
    }

    /// <summary>
    /// 在儲存格中新增段落。
    /// </summary>
    /// <param name="text">段落文字內文</param>
    /// <returns>新建立的段落物件執行個體</returns>
    public OdfParagraph AddParagraph(string text)
    {
        var pNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        pNode.TextContent = text;
        Node.AppendChild(pNode);
        return new OdfParagraph(pNode, _doc);
    }
}
