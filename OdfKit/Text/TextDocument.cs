using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Forms;
using OdfKit.Styles;

namespace OdfKit.Text;

/// <summary>
/// 表示 ODF 文字文件。
/// </summary>
public class TextDocument : OdfDocument
{
    private OdfTextBody? _body;
    private OdfDocumentMetadata? _metadata;
    private int _footnoteCounter;
    private int _endnoteCounter;

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

    /// <summary>
    /// 建立新的 ODT 文字文件。
    /// </summary>
    /// <returns>新的 <see cref="TextDocument"/> 執行個體。</returns>
    public static TextDocument Create()
    {
        return (TextDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Text);
    }

    /// <summary>
    /// 建立新的 ODT 文字文件 Fluent builder。
    /// </summary>
    /// <returns>新的 <see cref="TextDocumentBuilder"/> 執行個體。</returns>
    public static TextDocumentBuilder Builder()
    {
        return new TextDocumentBuilder(Create());
    }

    /// <summary>
    /// 從指定路徑載入 ODT 文字文件。
    /// </summary>
    /// <param name="path">ODT 文件路徑。</param>
    /// <returns>載入完成的 <see cref="TextDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODT 文字文件時擲出。</exception>
    public new static TextDocument Load(string path)
    {
        return EnsureTextDocument(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// 非同步從指定路徑載入 ODT 文字文件。
    /// </summary>
    /// <param name="path">ODT 文件路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="TextDocument"/>。</returns>
    public new static Task<TextDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Load(path), cancellationToken);
    }

    /// <summary>
    /// 從指定資料流載入 ODT 文字文件。
    /// </summary>
    /// <param name="stream">包含 ODT 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="TextDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODT 文字文件時擲出。</exception>
    public new static TextDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureTextDocument(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// 非同步從指定資料流載入 ODT 文字文件。
    /// </summary>
    /// <param name="stream">包含 ODT 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="TextDocument"/>。</returns>
    public new static Task<TextDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Load(stream, fileName), cancellationToken);
    }

    /// <summary>
    /// 取得文字文件本文的高階操作入口。
    /// </summary>
    public OdfTextBody Body => _body ??= new OdfTextBody(this);

    /// <summary>
    /// 取得文件中繼資料的高階操作入口。
    /// </summary>
    public OdfDocumentMetadata Metadata => _metadata ??= new OdfDocumentMetadata(this);

    private static TextDocument EnsureTextDocument(OdfDocument document)
    {
        if (document is TextDocument textDocument)
        {
            return textDocument;
        }

        document.Dispose();
        throw new InvalidOperationException("指定的 ODF 文件不是 ODT 文字文件。");
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
        return "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" xmlns:form=\"urn:oasis:names:tc:opendocument:xmlns:form:1.0\" office:version=\"" + OdfVersionInfo.DefaultVersionString + "\"><office:body><office:text></office:text></office:body></office:document-content>";
    }

    /// <summary>
    /// 取得預設的樣式 XML 字串。
    /// </summary>
    /// <returns>樣式 XML 字串</returns>
    protected override string GetDefaultStylesXml()
    {
        return "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"" + OdfVersionInfo.DefaultVersionString + "\"><office:styles></office:styles><office:automatic-styles></office:automatic-styles><office:master-styles><style:master-page style:name=\"Standard\" style:page-layout-name=\"Mpm1\"/></office:master-styles></office:document-styles>";
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

    #region Page Setup & Mirrored Layouts

    /// <summary>
    /// 取得預設的頁面設定。
    /// </summary>
    /// <returns>預設頁面設定物件</returns>
    public OdfPageSetup GetDefaultPageSetup()
    {
        return new OdfPageSetup(this);
    }

    /// <summary>
    /// 新增一個具名頁面樣式（master-page + page-layout），並可選擇性地配置其設定。
    /// </summary>
    /// <param name="name">主頁面樣式名稱（例如 "Landscape"）</param>
    /// <param name="configure">可選的頁面設定回呼</param>
    public OdfPageStyle AddPageStyle(string name, Action<OdfPageSetup>? configure = null)
    {
        string layoutName = $"MPL_{name}";
        var setup = new OdfPageSetup(this, name, layoutName);
        setup.EnsureNodes();
        configure?.Invoke(setup);
        return new OdfPageStyle(name);
    }

    /// <summary>
    /// 取得所有已定義的主頁面樣式名稱清單。
    /// </summary>
    public IReadOnlyList<string> GetPageStyleNames()
    {
        var masterStyles = FindOrCreateChild(StylesDom, "master-styles", OdfNamespaces.Office, "office");
        var names = new List<string>();
        foreach (var child in masterStyles.Children)
        {
            if (child.LocalName == "master-page" && child.NamespaceUri == OdfNamespaces.Style)
            {
                string? n = child.GetAttribute("name", OdfNamespaces.Style);
                if (!string.IsNullOrEmpty(n))
                    names.Add(n!);
            }
        }
        return names;
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
    /// 以強型別資料來源物件執行郵件合併，屬性名稱對應文件中的合併欄位名稱。
    /// </summary>
    /// <typeparam name="T">資料來源型別。</typeparam>
    /// <param name="dataSource">合併資料來源物件。</param>
    public void MailMerge<T>(T dataSource) where T : notnull
    {
        var engine = new OdfMailMergeEngine(this);
        engine.Execute(BodyTextRoot, dataSource);
    }

    /// <summary>
    /// 以字典資料來源執行郵件合併，Key 對應文件中的合併欄位名稱。
    /// </summary>
    /// <param name="dataSource">以欄位名稱為 Key 的資料字典。</param>
    public void MailMerge(IReadOnlyDictionary<string, object?> dataSource)
    {
        var engine = new OdfMailMergeEngine(this);
        engine.Execute(BodyTextRoot, dataSource);
    }

    /// <summary>
    /// 以強型別記錄集合執行批次郵件合併，每筆記錄產生獨立的文件副本。
    /// </summary>
    /// <typeparam name="T">記錄型別；屬性名稱對應文件中的合併欄位名稱。</typeparam>
    /// <param name="records">資料記錄集合。</param>
    /// <returns>每筆記錄對應一個已合併的 <see cref="TextDocument"/>；呼叫端負責 Dispose。</returns>
    public IReadOnlyList<TextDocument> MailMerge<T>(IEnumerable<T> records) where T : notnull
    {
        var result = new List<TextDocument>();
        foreach (T record in records)
        {
            TextDocument clone = CloneTextDocument();
            new OdfMailMergeEngine(clone).Execute(clone.BodyTextRoot, record);
            result.Add(clone);
        }
        return result;
    }

    /// <summary>
    /// 以字典記錄集合執行批次郵件合併，每筆記錄產生獨立的文件副本。
    /// </summary>
    /// <param name="records">字典記錄集合，Key 對應合併欄位名稱。</param>
    /// <returns>每筆記錄對應一個已合併的 <see cref="TextDocument"/>；呼叫端負責 Dispose。</returns>
    public IReadOnlyList<TextDocument> MailMerge(IEnumerable<IReadOnlyDictionary<string, object?>> records)
    {
        var result = new List<TextDocument>();
        foreach (var record in records)
        {
            TextDocument clone = CloneTextDocument();
            new OdfMailMergeEngine(clone).Execute(clone.BodyTextRoot, record);
            result.Add(clone);
        }
        return result;
    }

    private TextDocument CloneTextDocument()
    {
        using var ms = new MemoryStream();
        SaveToStream(ms);
        ms.Position = 0;
        return (TextDocument)OdfDocumentFactory.LoadDocument(ms);
    }

    #endregion

    #region Mathematical Formulas (MathML)

    /// <summary>
    /// 在指定的段落中新增數學公式。
    /// </summary>
    /// <param name="paragraph">要插入公式的段落</param>
    /// <param name="mathMlXmlString">MathML 結構的 XML 字串內容</param>
    internal void AddFormula(OdfParagraph paragraph, string mathMlXmlString)
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (string.IsNullOrWhiteSpace(mathMlXmlString))
            throw new ArgumentException("MathML XML content cannot be empty.", nameof(mathMlXmlString));

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
    internal void AddComment(OdfParagraph paragraph, OdfComment comment)
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (comment is null)
            throw new ArgumentNullException(nameof(comment));

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
    internal void AddPageNumberField(OdfParagraph paragraph)
    {
        var fNode = new OdfNode(OdfNodeType.Element, "page-number", OdfNamespaces.Text, "text");
        fNode.SetAttribute("select-page", OdfNamespaces.Text, "current", "text");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中新增總頁數欄位。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    internal void AddPageCountField(OdfParagraph paragraph)
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

    /// <summary>
    /// 在文件本文結尾新增一個指向外部子文件參照的區段（用於主文件）。
    /// </summary>
    /// <param name="name">區段名稱。</param>
    /// <param name="subDocumentUri">外部子文件的相對或絕對 URI/路徑（將寫入 xlink:href）。</param>
    /// <returns>新建立的區段物件。</returns>
    public OdfSection AddSubDocumentReference(string name, string subDocumentUri)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("區段名稱不可為空。", nameof(name));
        if (string.IsNullOrEmpty(subDocumentUri))
            throw new ArgumentException("子文件 URI 不可為空。", nameof(subDocumentUri));

        var sectionNode = new OdfNode(OdfNodeType.Element, "section", OdfNamespaces.Text, "text");
        sectionNode.SetAttribute("name", OdfNamespaces.Text, name, "text");

        var sourceNode = new OdfNode(OdfNodeType.Element, "section-source", OdfNamespaces.Text, "text");
        sourceNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        sourceNode.SetAttribute("href", OdfNamespaces.XLink, subDocumentUri, "xlink");
        sourceNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
        sourceNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");

        sectionNode.AppendChild(sourceNode);
        BodyTextRoot.AppendChild(sectionNode);

        return new OdfSection(sectionNode, this);
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
        return AddTrackedChange(changeType, "Author", DateTime.UtcNow, extraContent, originalStyleName, targetFamily);
    }

    /// <summary>
    /// 新增一個追蹤修訂記錄。
    /// </summary>
    /// <param name="changeType">修訂類型（"insertion"、"deletion" 或 "format-change"）。</param>
    /// <param name="creator">建立者姓名。</param>
    /// <param name="date">修訂時間。</param>
    /// <param name="extraContent">修訂的附加內容節點。</param>
    /// <param name="originalStyleName">原本的樣式名稱。</param>
    /// <param name="targetFamily">目標樣式系列名稱。</param>
    /// <returns>產生的修訂識別碼。</returns>
    public string AddTrackedChange(string changeType, string creator, DateTime date, OdfNode? extraContent = null, string? originalStyleName = null, string? targetFamily = null)
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

        var creatorNode = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc");
        creatorNode.TextContent = creator;
        changeInfo.AppendChild(creatorNode);

        var dateNode = new OdfNode(OdfNodeType.Element, "date", OdfNamespaces.Dc, "dc");
        dateNode.TextContent = date == DateTime.MinValue ? "0001-01-01T00:00:00Z" :
                               date == DateTime.MaxValue ? "9999-12-31T23:59:59.9999999Z" :
                               date.ToString("yyyy-MM-ddTHH:mm:ssZ");
        changeInfo.AppendChild(dateNode);

        tcNode.AppendChild(changedRegion);
        return changeId;
    }

    /// <summary>
    /// 接受文件中所有的追蹤修訂。
    /// </summary>
    public void AcceptAllChanges()
    {
        AcceptAllTrackedChanges();
    }

    /// <summary>
    /// 拒絕文件中所有的追蹤修訂。
    /// </summary>
    public void RejectAllChanges()
    {
        RejectAllTrackedChanges();
    }

    /// <summary>
    /// 取得文件中所有的追蹤修訂。
    /// </summary>
    /// <returns>追蹤修訂的集合。</returns>
    public IEnumerable<OdfTrackedChange> GetTrackedChanges()
    {
        var list = new List<OdfTrackedChange>();
        var tcNode = FindChild(BodyTextRoot, "tracked-changes", OdfNamespaces.Text);
        if (tcNode is null)
            return list;

        foreach (var changedRegion in tcNode.Children)
        {
            string? id = changedRegion.GetAttribute("id", OdfNamespaces.Text);
            if (string.IsNullOrEmpty(id))
                continue;

            string changeType = "";
            string creator = "";
            DateTime date = DateTime.MinValue;
            OdfNode? specNode = null;

            foreach (var child in changedRegion.Children)
            {
                if ((child.LocalName == "insertion" || child.LocalName == "deletion" || child.LocalName == "format-change") &&
                    child.NamespaceUri == OdfNamespaces.Text)
                {
                    changeType = child.LocalName;
                    specNode = child;
                    break;
                }
            }

            if (specNode is not null)
            {
                var changeInfo = FindChild(specNode, "change-info", OdfNamespaces.Office);
                if (changeInfo is not null)
                {
                    var creatorNode = FindChild(changeInfo, "creator", OdfNamespaces.Dc);
                    if (creatorNode is not null)
                        creator = creatorNode.TextContent ?? "";

                    var dateNode = FindChild(changeInfo, "date", OdfNamespaces.Dc);
                    if (dateNode is not null)
                    {
                        var textContent = dateNode.TextContent;
                        if (!string.IsNullOrEmpty(textContent))
                        {
                            if (textContent == "0001-01-01T00:00:00Z" || textContent.StartsWith("0001-01-01"))
                            {
                                date = DateTime.MinValue;
                            }
                            else if (textContent == "9999-12-31T23:59:59.9999999Z" || textContent.StartsWith("9999-12-31"))
                            {
                                date = DateTime.MaxValue;
                            }
                            else if (DateTime.TryParse(textContent, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsedDate))
                            {
                                date = parsedDate;
                            }
                        }
                    }
                }
            }

            string content = "";
            if (changeType == "deletion" && specNode is not null)
            {
                var sb = new System.Text.StringBuilder();
                ExtractTextContentIgnoringChangeInfo(specNode, sb);
                content = sb.ToString();
            }
            else if (changeType == "insertion" || changeType == "format-change")
            {
                content = ExtractTextBetweenMarkers(BodyTextRoot, id!);
            }

            list.Add(new OdfTrackedChange
            {
                RegionId = id!,
                ChangeType = changeType switch
                {
                    "deletion" => OdfChangeType.Deletion,
                    "format-change" => OdfChangeType.FormatChange,
                    _ => OdfChangeType.Insertion,
                },
                Author = creator,
                ChangedAt = date,
                Content = content,
            });
        }

        return list;
    }

    private void ExtractTextContentIgnoringChangeInfo(OdfNode node, System.Text.StringBuilder sb)
    {
        if (node.LocalName == "change-info" && node.NamespaceUri == OdfNamespaces.Office)
        {
            return;
        }
        if (node.NodeType == OdfNodeType.Text)
        {
            sb.Append(node.TextContent);
        }
        foreach (var child in node.Children)
        {
            ExtractTextContentIgnoringChangeInfo(child, sb);
        }
    }

    private string ExtractTextBetweenMarkers(OdfNode root, string changeId)
    {
        var sb = new System.Text.StringBuilder();
        bool collect = false;
        ExtractTextBetweenMarkersRecursive(root, changeId, ref collect, sb);
        return sb.ToString();
    }

    private void ExtractTextBetweenMarkersRecursive(OdfNode node, string changeId, ref bool collect, System.Text.StringBuilder sb)
    {
        if (node.LocalName == "change-start" && node.NamespaceUri == OdfNamespaces.Text && node.GetAttribute("change-id", OdfNamespaces.Text) == changeId)
        {
            collect = true;
            return;
        }
        if (node.LocalName == "change-end" && node.NamespaceUri == OdfNamespaces.Text && node.GetAttribute("change-id", OdfNamespaces.Text) == changeId)
        {
            collect = false;
            return;
        }

        if (collect && node.NodeType == OdfNodeType.Text)
        {
            sb.Append(node.TextContent);
        }

        foreach (var child in node.Children)
        {
            ExtractTextBetweenMarkersRecursive(child, changeId, ref collect, sb);
        }
    }


    /// <summary>
    /// 追蹤格式變更。
    /// </summary>
    /// <param name="node">發生變更的 ODF 節點</param>
    /// <param name="family">樣式系列名稱</param>
    public void TrackFormatChange(OdfNode node, string family)
    {
        if (!TrackedChanges)
            return;

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
        if (node.Parent is null)
            return;
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
        if (startNode is null || startNode.Parent is null)
            return affected;

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
                    if (child == startNode)
                    { collect = true; continue; }
                    if (child == endNode)
                    { collect = false; break; }
                    if (collect)
                        siblingsBetween.Add(child);
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
                    if (child == startNode)
                    { collect = true; continue; }
                    if (child == endNode)
                    { collect = false; break; }
                    if (collect)
                        affected.Add(child);
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
        if (tcNode is null)
            return;

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
        if (tcNode is null)
            return;

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
        if (tcNode is null)
            return;

        var changes = new Dictionary<string, string>(StringComparer.Ordinal);
        ExtractTrackedChangesMeta(tcNode, changes);

        if (!changes.TryGetValue(changeId, out var type))
            return;

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
        if (regionToRemove is not null)
            tcNode.RemoveChild(regionToRemove);
        if (tcNode.Children.Count == 0)
            BodyTextRoot.RemoveChild(tcNode);
    }

    /// <summary>
    /// 拒絕指定的追蹤修訂。
    /// </summary>
    /// <param name="changeId">修訂識別碼</param>
    public void RejectChange(string changeId)
    {
        var tcNode = FindChild(BodyTextRoot, "tracked-changes", OdfNamespaces.Text);
        if (tcNode is null)
            return;

        var changes = new Dictionary<string, string>(StringComparer.Ordinal);
        ExtractTrackedChangesMeta(tcNode, changes);

        if (!changes.TryGetValue(changeId, out var type))
            return;

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
        if (regionToRemove is not null)
            tcNode.RemoveChild(regionToRemove);
        if (tcNode.Children.Count == 0)
            BodyTextRoot.RemoveChild(tcNode);
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

        if (deletionContent is null)
            return;

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
            if (found is not null)
                return found;
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
            if (string.IsNullOrEmpty(id))
                continue;

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
    internal void AddHtmlFragment(OdfParagraph paragraph, string html)
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (string.IsNullOrWhiteSpace(html))
            return;

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
                    if (state.Bold.HasValue)
                        activeBold = state.Bold.Value;
                    if (state.Italic.HasValue)
                        activeItalic = state.Italic.Value;
                    if (state.Underline.HasValue)
                        activeUnderline = state.Underline.Value;
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
        if (string.IsNullOrEmpty(text))
            return text;
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
                    if (genericFamily is not null)
                        child.SetAttribute("font-family-generic", OdfNamespaces.Style, genericFamily, "style");
                    if (pitch is not null)
                        child.SetAttribute("font-pitch", OdfNamespaces.Style, pitch, "style");
                    return;
                }
            }

            var fontFace = new OdfNode(OdfNodeType.Element, "font-face", OdfNamespaces.Style, "style");
            fontFace.SetAttribute("name", OdfNamespaces.Style, name, "style");
            fontFace.SetAttribute("font-family", OdfNamespaces.Svg, fontFamily, "svg");
            if (genericFamily is not null)
                fontFace.SetAttribute("font-family-generic", OdfNamespaces.Style, genericFamily, "style");
            if (pitch is not null)
                fontFace.SetAttribute("font-pitch", OdfNamespaces.Style, pitch, "style");
            fontDecls.AppendChild(fontFace);
        }

        AddToDom(ContentDom);
        if (StylesDom is not null)
            AddToDom(StylesDom);
    }

    #endregion

    #region 表單控制項（Form Controls）

    /// <summary>
    /// 在文件中加入表單控制項（draw:frame + office:forms 定義）。
    /// </summary>
    /// <param name="type">控制項類型。</param>
    /// <param name="name">控制項名稱（唯一識別字）。</param>
    /// <param name="x">控制項左邊距。</param>
    /// <param name="y">控制項上邊距。</param>
    /// <param name="width">控制項寬度。</param>
    /// <param name="height">控制項高度。</param>
    /// <param name="label">控制項標籤文字（核取方塊、按鈕）或預設值（文字欄位）。</param>
    /// <param name="listItems">下拉式清單選項（僅 ListBox 有效）。</param>
    /// <returns>描述新控制項的 <see cref="OdfFormControl"/> 物件。</returns>
    public OdfFormControl AddFormControl(
        OdfControlType type,
        string name,
        OdfLength x,
        OdfLength y,
        OdfLength width,
        OdfLength height,
        string label = "",
        IReadOnlyList<string>? listItems = null)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("控制項名稱不可為空。", nameof(name));

        // 1. 建立/取得 <office:forms><form:form>
        OdfNode formsNode = FindOrCreateFormsNode();
        OdfNode formNode = FindOrCreateChild(formsNode, "form", OdfNamespaces.Form, "form");
        if (string.IsNullOrEmpty(formNode.GetAttribute("name", OdfNamespaces.Form)))
            formNode.SetAttribute("name", OdfNamespaces.Form, "Form1", "form");
        formNode.SetAttribute("apply-design-mode", OdfNamespaces.Form, "false", "form");

        // 2. 建立控制項 form:* 元素
        string elemName = type switch
        {
            OdfControlType.CheckBox => "checkbox",
            OdfControlType.ListBox => "listbox",
            OdfControlType.Button => "button",
            _ => "text",
        };
        OdfNode ctrlNode = new OdfNode(OdfNodeType.Element, elemName, OdfNamespaces.Form, "form");
        ctrlNode.SetAttribute("name", OdfNamespaces.Form, name, "form");
        ctrlNode.SetAttribute("id", OdfNamespaces.Form, name, "form");
        if (!string.IsNullOrEmpty(label))
            ctrlNode.SetAttribute("label", OdfNamespaces.Form, label, "form");
        if (type == OdfControlType.TextBox && !string.IsNullOrEmpty(label))
            ctrlNode.SetAttribute("value", OdfNamespaces.Form, label, "form");
        if (type == OdfControlType.CheckBox)
            ctrlNode.SetAttribute("current-state", OdfNamespaces.Form, "unchecked", "form");

        if (type == OdfControlType.ListBox && listItems is not null)
        {
            foreach (string item in listItems)
            {
                OdfNode optNode = new OdfNode(OdfNodeType.Element, "option", OdfNamespaces.Form, "form");
                optNode.SetAttribute("label", OdfNamespaces.Form, item, "form");
                ctrlNode.AppendChild(optNode);
            }
        }
        formNode.AppendChild(ctrlNode);

        // 3. 建立 draw:frame 錨點段落
        OdfNode para = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        OdfNode frame = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
        frame.SetAttribute("name", OdfNamespaces.Draw, $"ctrl-{name}", "draw");
        frame.SetAttribute("anchor-type", OdfNamespaces.Text, "paragraph", "text");
        frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        frame.SetAttribute("width", OdfNamespaces.Svg, width.ToString(), "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, height.ToString(), "svg");
        frame.SetAttribute("z-index", OdfNamespaces.Draw, "0", "draw");

        OdfNode ctrlRef = new OdfNode(OdfNodeType.Element, "control", OdfNamespaces.Draw, "draw");
        ctrlRef.SetAttribute("control", OdfNamespaces.Draw, name, "draw");
        frame.AppendChild(ctrlRef);
        para.AppendChild(frame);
        BodyTextRoot.AppendChild(para);

        return new OdfFormControl
        {
            ControlType = type,
            Name = name,
            Label = label,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            ListItems = listItems ?? [],
        };
    }

    /// <summary>
    /// 取得文件中所有表單控制項。
    /// </summary>
    /// <returns>控制項清單；若無表單則回傳空清單。</returns>
    public IReadOnlyList<OdfFormControl> GetFormControls()
    {
        var result = new List<OdfFormControl>();
        OdfNode? formsNode = FindFormsNode();
        if (formsNode is null)
            return result;

        foreach (OdfNode formNode in formsNode.Children)
        {
            if (formNode.LocalName != "form" || formNode.NamespaceUri != OdfNamespaces.Form)
                continue;

            foreach (OdfNode ctrl in formNode.Children)
            {
                if (ctrl.NamespaceUri != OdfNamespaces.Form)
                    continue;

                OdfControlType type = ctrl.LocalName switch
                {
                    "checkbox" => OdfControlType.CheckBox,
                    "listbox" => OdfControlType.ListBox,
                    "button" => OdfControlType.Button,
                    _ => OdfControlType.TextBox,
                };

                var items = new List<string>();
                foreach (OdfNode child in ctrl.Children)
                {
                    if (child.LocalName == "option" && child.NamespaceUri == OdfNamespaces.Form)
                    {
                        string? optLabel = child.GetAttribute("label", OdfNamespaces.Form);
                        if (!string.IsNullOrEmpty(optLabel))
                            items.Add(optLabel!);
                    }
                }

                result.Add(new OdfFormControl
                {
                    ControlType = type,
                    Name = ctrl.GetAttribute("name", OdfNamespaces.Form) ?? string.Empty,
                    Label = ctrl.GetAttribute("label", OdfNamespaces.Form) ?? string.Empty,
                    Value = ctrl.GetAttribute("value", OdfNamespaces.Form),
                    IsChecked = ctrl.GetAttribute("current-state", OdfNamespaces.Form) == "checked",
                    ListItems = items,
                });
            }
        }

        return result;
    }

    private OdfNode FindOrCreateFormsNode()
    {
        OdfNode? existing = FindFormsNode();
        if (existing is not null)
            return existing;

        OdfNode formsNode = new OdfNode(OdfNodeType.Element, "forms", OdfNamespaces.Office, "office");
        if (BodyTextRoot.Children.Count > 0)
            BodyTextRoot.InsertBefore(formsNode, BodyTextRoot.Children[0]);
        else
            BodyTextRoot.AppendChild(formsNode);
        return formsNode;
    }

    private OdfNode? FindFormsNode()
    {
        foreach (OdfNode child in BodyTextRoot.Children)
        {
            if (child.LocalName == "forms" && child.NamespaceUri == OdfNamespaces.Office)
                return child;
        }
        return null;
    }

    #endregion
}

/// <summary>
/// 指定頁面的使用方式。
/// </summary>
public enum OdfPageUsage
{
    /// <summary>套用至所有頁面（預設）。</summary>
    All,
    /// <summary>僅套用至左側頁面。</summary>
    Left,
    /// <summary>僅套用至右側頁面。</summary>
    Right,
    /// <summary>鏡像頁面，左右交替。</summary>
    Mirrored,
}

/// <summary>
/// 指定版面配置網格的模式。
/// </summary>
public enum OdfLayoutGridMode
{
    /// <summary>無網格。</summary>
    None,
    /// <summary>僅顯示行網格。</summary>
    Line,
    /// <summary>顯示行列網格。</summary>
    Both,
}

/// <summary>
/// 表示文字文件的頁面設定。
/// </summary>
public class OdfPageSetup
{
    private readonly TextDocument _doc;
    private readonly string _masterPageName;
    private readonly string _pageLayoutName;

    /// <summary>使用預設主頁面（Standard / Mpm1）初始化。</summary>
    public OdfPageSetup(TextDocument doc) : this(doc, "Standard", "Mpm1") { }

    internal OdfPageSetup(TextDocument doc, string masterPageName, string pageLayoutName)
    {
        _doc = doc;
        _masterPageName = masterPageName;
        _pageLayoutName = pageLayoutName;
    }

    private OdfNode ContentDom => _doc.ContentDom;
    private OdfNode StylesDom => _doc.StylesDom;

    internal void EnsureNodes()
    {
        _ = FindOrCreatePageLayoutProperties();
        _ = FindOrCreateMasterPage();
    }

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
    /// 取得或設定頁面使用方式。
    /// </summary>
    public OdfPageUsage PageUsage
    {
        get
        {
            var props = FindOrCreatePageLayoutProperties();
            return (props.GetAttribute("page-usage", OdfNamespaces.Style) ?? "all") switch
            {
                "left" => OdfPageUsage.Left,
                "right" => OdfPageUsage.Right,
                "mirrored" => OdfPageUsage.Mirrored,
                _ => OdfPageUsage.All,
            };
        }
        set
        {
            var props = FindOrCreatePageLayoutProperties();
            string str = value switch
            {
                OdfPageUsage.Left => "left",
                OdfPageUsage.Right => "right",
                OdfPageUsage.Mirrored => "mirrored",
                _ => "all",
            };
            props.SetAttribute("page-usage", OdfNamespaces.Style, str, "style");
        }
    }

    /// <summary>
    /// 取得或設定頁面的文字書寫模式。
    /// </summary>
    public OdfWritingMode WritingMode
    {
        get
        {
            var props = FindOrCreatePageLayoutProperties();
            return OdfWritingModeExtensions.FromOdfToken(props.GetAttribute("writing-mode", OdfNamespaces.Style));
        }
        set
        {
            var props = FindOrCreatePageLayoutProperties();
            props.SetAttribute("writing-mode", OdfNamespaces.Style, value.ToOdfToken(), "style");
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
    public OdfLayoutGridMode LayoutGridMode
    {
        get
        {
            return (GetPageStyleProp("layout-grid-mode") ?? "none") switch
            {
                "line" => OdfLayoutGridMode.Line,
                "both" => OdfLayoutGridMode.Both,
                _ => OdfLayoutGridMode.None,
            };
        }
        set
        {
            string str = value switch
            {
                OdfLayoutGridMode.Line => "line",
                OdfLayoutGridMode.Both => "both",
                _ => "none",
            };
            SetPageStyleProp("layout-grid-mode", str);
        }
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
            if (child.LocalName == "page-layout" && child.NamespaceUri == OdfNamespaces.Style &&
                child.GetAttribute("name", OdfNamespaces.Style) == _pageLayoutName)
                return child;
        }
        var pageLayout = new OdfNode(OdfNodeType.Element, "page-layout", OdfNamespaces.Style, "style");
        pageLayout.SetAttribute("name", OdfNamespaces.Style, _pageLayoutName, "style");
        autoStyles.AppendChild(pageLayout);
        return pageLayout;
    }

    private OdfNode FindOrCreatePageLayoutProperties()
    {
        var layoutNode = FindOrCreatePageLayoutNode();
        foreach (var child in layoutNode.Children)
        {
            if (child.LocalName == "page-layout-properties" && child.NamespaceUri == OdfNamespaces.Style)
                return child;
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
            if (child.LocalName == "master-page" && child.NamespaceUri == OdfNamespaces.Style &&
                child.GetAttribute("name", OdfNamespaces.Style) == _masterPageName)
                return child;
        }
        var masterPage = new OdfNode(OdfNodeType.Element, "master-page", OdfNamespaces.Style, "style");
        masterPage.SetAttribute("name", OdfNamespaces.Style, _masterPageName, "style");
        masterPage.SetAttribute("page-layout-name", OdfNamespaces.Style, _pageLayoutName, "style");
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
            if (target is not null)
                mp.RemoveChild(target);
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
                    if (genericFamily is not null)
                        child.SetAttribute("font-family-generic", OdfNamespaces.Style, genericFamily, "style");
                    if (pitch is not null)
                        child.SetAttribute("font-pitch", OdfNamespaces.Style, pitch, "style");
                    return;
                }
            }

            var fontFace = new OdfNode(OdfNodeType.Element, "font-face", OdfNamespaces.Style, "style");
            fontFace.SetAttribute("name", OdfNamespaces.Style, name, "style");
            fontFace.SetAttribute("font-family", OdfNamespaces.Svg, fontFamily, "svg");
            if (genericFamily is not null)
                fontFace.SetAttribute("font-family-generic", OdfNamespaces.Style, genericFamily, "style");
            if (pitch is not null)
                fontFace.SetAttribute("font-pitch", OdfNamespaces.Style, pitch, "style");
            fontDecls.AppendChild(fontFace);
        }

        AddToDom(_doc.ContentDom);
        if (_doc.StylesDom is not null)
            AddToDom(_doc.StylesDom);
    }
}

/// <summary>
/// 代表文字文件中的一個具名頁面樣式（master-page）。
/// </summary>
public sealed class OdfPageStyle
{
    /// <summary>取得主頁面樣式名稱。</summary>
    public string Name { get; }

    internal OdfPageStyle(string name) { Name = name; }
}

/// <summary>
/// 提供文字文件本文的高階操作入口。
/// </summary>
public sealed class OdfTextBody
{
    private readonly TextDocument _document;
    private OdfParagraphCollection? _paragraphs;
    private OdfHeadingCollection? _headings;
    private OdfListCollection? _lists;
    private OdfTextTableCollection? _tables;
    private OdfTextImageCollection? _images;

    /// <summary>
    /// 初始化 <see cref="OdfTextBody"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">所屬文字文件。</param>
    public OdfTextBody(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 取得段落集合。
    /// </summary>
    public OdfParagraphCollection Paragraphs => _paragraphs ??= new OdfParagraphCollection(_document);

    /// <summary>
    /// 取得標題集合。
    /// </summary>
    public OdfHeadingCollection Headings => _headings ??= new OdfHeadingCollection(_document);

    /// <summary>
    /// 取得清單集合。
    /// </summary>
    public OdfListCollection Lists => _lists ??= new OdfListCollection(_document);

    /// <summary>
    /// 取得表格集合。
    /// </summary>
    public OdfTextTableCollection Tables => _tables ??= new OdfTextTableCollection(_document);

    /// <summary>
    /// 取得圖片集合。
    /// </summary>
    public OdfTextImageCollection Images => _images ??= new OdfTextImageCollection(_document);

    /// <summary>
    /// 取得文件中的所有區段（Section）集合。
    /// </summary>
    public IReadOnlyList<OdfSection> Sections
    {
        get
        {
            var sections = new List<OdfSection>();
            var nodes = _document.BodyTextRoot.Descendants()
                .Where(n => n.NodeType == OdfNodeType.Element &&
                            n.LocalName == "section" &&
                            n.NamespaceUri == OdfNamespaces.Text);
            foreach (var node in nodes)
            {
                sections.Add(new OdfSection(node, _document));
            }
            return sections;
        }
    }
}

/// <summary>
/// 提供段落新增入口。
/// </summary>
public sealed class OdfParagraphCollection : IEnumerable<OdfParagraph>
{
    private readonly TextDocument _document;

    /// <summary>
    /// 初始化 <see cref="OdfParagraphCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">所屬文字文件。</param>
    public OdfParagraphCollection(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 新增段落。
    /// </summary>
    /// <param name="text">段落文字。</param>
    /// <returns>新增完成的段落。</returns>
    public OdfParagraph Add(string text = "")
    {
        return _document.AddParagraph(text);
    }

    /// <summary>
    /// 取得文件本文最上層段落清單。
    /// </summary>
    public IReadOnlyList<OdfParagraph> Items
    {
        get
        {
            List<OdfParagraph> paragraphs = [];
            foreach (OdfNode child in _document.BodyTextRoot.Children)
            {
                if (child.NodeType is OdfNodeType.Element &&
                    child.LocalName == "p" &&
                    child.NamespaceUri == OdfNamespaces.Text)
                {
                    paragraphs.Add(new OdfParagraph(child, _document));
                }
            }

            return paragraphs.AsReadOnly();
        }
    }

    /// <summary>
    /// 取得段落列舉器，供 LINQ 查詢使用。
    /// </summary>
    /// <returns>段落列舉器。</returns>
    public IEnumerator<OdfParagraph> GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>
/// 提供標題新增入口。
/// </summary>
public sealed class OdfHeadingCollection : IEnumerable<OdfHeading>
{
    private readonly TextDocument _document;

    /// <summary>
    /// 初始化 <see cref="OdfHeadingCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">所屬文字文件。</param>
    public OdfHeadingCollection(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 新增標題。
    /// </summary>
    /// <param name="text">標題文字。</param>
    /// <param name="outlineLevel">大綱階層。</param>
    /// <returns>新增完成的標題。</returns>
    public OdfHeading Add(string text, int outlineLevel = 1)
    {
        return _document.AddHeading(text, outlineLevel);
    }

    /// <summary>
    /// 取得文件本文最上層標題清單。
    /// </summary>
    public IReadOnlyList<OdfHeading> Items
    {
        get
        {
            List<OdfHeading> headings = [];
            foreach (OdfNode child in _document.BodyTextRoot.Children)
            {
                if (child.NodeType is OdfNodeType.Element &&
                    child.LocalName == "h" &&
                    child.NamespaceUri == OdfNamespaces.Text)
                {
                    headings.Add(new OdfHeading(child, _document));
                }
            }

            return headings.AsReadOnly();
        }
    }

    /// <summary>
    /// 取得標題列舉器，供 LINQ 查詢使用。
    /// </summary>
    /// <returns>標題列舉器。</returns>
    public IEnumerator<OdfHeading> GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>
/// 提供清單新增入口。
/// </summary>
public sealed class OdfListCollection : IEnumerable<OdfList>
{
    private readonly TextDocument _document;

    /// <summary>
    /// 初始化 <see cref="OdfListCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">所屬文字文件。</param>
    public OdfListCollection(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 新增清單。
    /// </summary>
    /// <param name="styleName">選用的清單樣式名稱。</param>
    /// <returns>新增完成的清單。</returns>
    public OdfList Add(string? styleName = null)
    {
        return _document.AddList(styleName);
    }

    /// <summary>
    /// 取得文件本文最上層清單清單。
    /// </summary>
    public IReadOnlyList<OdfList> Items
    {
        get
        {
            List<OdfList> lists = [];
            foreach (OdfNode child in _document.BodyTextRoot.Children)
            {
                if (child.NodeType is OdfNodeType.Element &&
                    child.LocalName == "list" &&
                    child.NamespaceUri == OdfNamespaces.Text)
                {
                    lists.Add(new OdfList(child, _document));
                }
            }

            return lists.AsReadOnly();
        }
    }

    /// <summary>
    /// 取得清單列舉器，供 LINQ 查詢使用。
    /// </summary>
    /// <returns>清單列舉器。</returns>
    public IEnumerator<OdfList> GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>
/// 提供表格新增入口。
/// </summary>
public sealed class OdfTextTableCollection : IEnumerable<OdfTextTableInfo>
{
    private readonly TextDocument _document;

    /// <summary>
    /// 初始化 <see cref="OdfTextTableCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">所屬文字文件。</param>
    public OdfTextTableCollection(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 新增表格。
    /// </summary>
    /// <param name="rows">列數。</param>
    /// <param name="columns">欄數。</param>
    /// <returns>新增完成的表格。</returns>
    public OdfTable Add(int rows, int columns)
    {
        return _document.AddTable(rows, columns);
    }

    /// <summary>
    /// 取得文件本文最上層文字表格摘要清單。
    /// </summary>
    public IReadOnlyList<OdfTextTableInfo> Items
    {
        get
        {
            List<OdfTextTableInfo> tables = [];
            foreach (OdfNode child in _document.BodyTextRoot.Children)
            {
                if (child.NodeType is OdfNodeType.Element &&
                    child.LocalName == "table" &&
                    child.NamespaceUri == OdfNamespaces.Table)
                {
                    tables.Add(OdfTextTableInfo.FromNode(child));
                }
            }

            return tables.AsReadOnly();
        }
    }

    /// <summary>
    /// 取得文字表格摘要列舉器，供 LINQ 查詢使用。
    /// </summary>
    /// <returns>文字表格摘要列舉器。</returns>
    public IEnumerator<OdfTextTableInfo> GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>
/// 提供圖片新增入口。
/// </summary>
public sealed class OdfTextImageCollection : IEnumerable<OdfImage>
{
    private readonly TextDocument _document;

    /// <summary>
    /// 初始化 <see cref="OdfTextImageCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">所屬文字文件。</param>
    public OdfTextImageCollection(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 新增圖片至新的段落。
    /// </summary>
    /// <param name="imageBytes">圖片二進位內容。</param>
    /// <param name="width">圖片寬度。</param>
    /// <param name="height">圖片高度。</param>
    /// <param name="name">選用的圖片名稱。</param>
    /// <returns>新增完成的圖片。</returns>
    public OdfImage Add(byte[] imageBytes, OdfLength width, OdfLength height, string? name = null)
    {
        var media = new OdfMediaManager(_document.Package);
        string path = media.AddImage(imageBytes, name);
        OdfParagraph paragraph = _document.AddParagraph();
        return _document.AddImage(paragraph, path, width, height, name);
    }

    /// <summary>
    /// 取得文件本文中的圖片清單。
    /// </summary>
    public IReadOnlyList<OdfImage> Items
    {
        get
        {
            List<OdfImage> images = [];
            CollectImages(_document.BodyTextRoot, images);
            return images.AsReadOnly();
        }
    }

    /// <summary>
    /// 取得圖片列舉器，供 LINQ 查詢使用。
    /// </summary>
    /// <returns>圖片列舉器。</returns>
    public IEnumerator<OdfImage> GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private static void CollectImages(OdfNode node, List<OdfImage> images)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "frame" &&
                child.NamespaceUri == OdfNamespaces.Draw)
            {
                OdfNode? image = FindDescendant(child, "image", OdfNamespaces.Draw);
                if (image is not null)
                {
                    images.Add(new OdfImage(child, image));
                }
            }

            CollectImages(child, images);
        }
    }

    private static OdfNode? FindDescendant(OdfNode node, string localName, string namespaceUri)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return child;
            }

            OdfNode? descendant = FindDescendant(child, localName, namespaceUri);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}

/// <summary>
/// 表示文字文件中的表格摘要。
/// </summary>
public sealed class OdfTextTableInfo
{
    private OdfTextTableInfo(string? name, int rowCount, int columnCount)
    {
        Name = name;
        RowCount = rowCount;
        ColumnCount = columnCount;
    }

    /// <summary>
    /// 取得表格名稱。
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// 取得表格列數。
    /// </summary>
    public int RowCount { get; }

    /// <summary>
    /// 取得表格最大欄數。
    /// </summary>
    public int ColumnCount { get; }

    internal static OdfTextTableInfo FromNode(OdfNode tableNode)
    {
        int rowCount = 0;
        int columnCount = 0;
        foreach (OdfNode row in tableNode.Children)
        {
            if (row.NodeType is not OdfNodeType.Element ||
                row.LocalName != "table-row" ||
                row.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }

            rowCount++;
            int cells = 0;
            foreach (OdfNode cell in row.Children)
            {
                if (cell.NodeType is OdfNodeType.Element &&
                    (cell.LocalName == "table-cell" || cell.LocalName == "covered-table-cell") &&
                    cell.NamespaceUri == OdfNamespaces.Table)
                {
                    cells++;
                }
            }

            columnCount = Math.Max(columnCount, cells);
        }

        return new OdfTextTableInfo(tableNode.GetAttribute("name", OdfNamespaces.Table), rowCount, columnCount);
    }
}

/// <summary>
/// 提供文件中繼資料的高階操作入口。
/// </summary>
public sealed class OdfDocumentMetadata
{
    private readonly OdfDocument _document;

    /// <summary>
    /// 初始化 <see cref="OdfDocumentMetadata"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">所屬文件。</param>
    public OdfDocumentMetadata(OdfDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 取得或設定標題。
    /// </summary>
    public string? Title
    {
        get => _document.Title;
        set => _document.Title = value;
    }

    /// <summary>
    /// 取得或設定作者。
    /// </summary>
    public string? Creator
    {
        get => _document.Creator;
        set => _document.Creator = value;
    }

    /// <summary>
    /// 取得或設定主旨。
    /// </summary>
    public string? Subject
    {
        get => _document.Subject;
        set => _document.Subject = value;
    }

    /// <summary>
    /// 取得或設定描述。
    /// </summary>
    public string? Description
    {
        get => _document.Description;
        set => _document.Description = value;
    }

    /// <summary>
    /// 取得或設定文件語言（BCP-47 語言標籤，例如 "zh-TW"、"en-US"）。
    /// 對應 ODF 的 <c>dc:language</c> 元素。
    /// </summary>
    public string? Language
    {
        get => _document.Language;
        set => _document.Language = value;
    }
}

/// <summary>
/// 表示文字文件中的段落。
/// </summary>
public partial class OdfParagraph
{
    internal OdfParagraph(OdfNode node, TextDocument doc)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Doc = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    /// <summary>
    /// 取得與此段落相關聯的 OdfNode 節點。
    /// </summary>
    internal OdfNode Node { get; }

    /// <summary>
    /// 取得所屬的文字文件。
    /// </summary>
    protected readonly TextDocument Doc;

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

    internal TextDocument DocProperty => Doc;
    internal OdfStyleEngine StyleEngine => Doc.StyleEngine;

    private OdfKit.Styles.OdfParagraphStyleProxy? _styleProxy;

    /// <summary>
    /// 取得此段落的高階樣式設定代理 Facade。
    /// </summary>
    public OdfKit.Styles.OdfParagraphStyleProxy Style => _styleProxy ??= new OdfKit.Styles.OdfParagraphStyleProxy(this);

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
    public OdfWritingMode WritingMode
    {
        get => OdfWritingModeExtensions.FromOdfToken(Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "writing-mode", OdfNamespaces.Style, "paragraph"));
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "writing-mode", OdfNamespaces.Style, value.ToOdfToken(), "style");
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

    /// <summary>
    /// 取得此段落的所有文字片段（Runs）。存取時會自動將直屬的文字節點分裂包裝為 span 節點。
    /// </summary>
    public System.Collections.Generic.IEnumerable<OdfTextRun> Runs
    {
        get
        {
            // 裂變同步：若存在直屬的 text 節點，則先將其包裝到一個全新的 <text:span> 中
            var children = new System.Collections.Generic.List<OdfNode>(Node.Children);
            foreach (var child in children)
            {
                if (child.NodeType == OdfNodeType.Text && !string.IsNullOrEmpty(child.TextContent))
                {
                    var spanNode = OdfNodeFactory.CreateElement("span", OdfNamespaces.Text, "text");
                    Node.InsertBefore(spanNode, child);
                    spanNode.AppendChild(child);
                }
            }

            // 回傳所有的 text:span 節點包裝
            foreach (var child in Node.Children)
            {
                if (child.NodeType == OdfNodeType.Element &&
                    child.LocalName == "span" &&
                    child.NamespaceUri == OdfNamespaces.Text)
                {
                    yield return new OdfTextRun(child, Doc);
                }
            }
        }
    }

    /// <summary>
    /// 清除此段落內的所有文字片段（移除所有的 span 節點）。
    /// </summary>
    public void ClearRuns()
    {
        var spans = new System.Collections.Generic.List<OdfNode>();
        foreach (var child in Node.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.LocalName == "span" &&
                child.NamespaceUri == OdfNamespaces.Text)
            {
                spans.Add(child);
            }
        }
        foreach (var span in spans)
        {
            Node.RemoveChild(span);
        }
    }

    /// <summary>在段落中新增日期欄位。</summary>
    public void AddDateField() => Doc.AddDateField(this);

    /// <summary>在段落中新增時間欄位。</summary>
    public void AddTimeField() => Doc.AddTimeField(this);

    /// <summary>在段落中新增作者名稱欄位。</summary>
    public void AddAuthorField() => Doc.AddAuthorField(this);

    /// <summary>在段落中新增章節欄位。</summary>
    public void AddChapterField() => Doc.AddChapterField(this);

    /// <summary>在段落中新增序號欄位。</summary>
    /// <param name="name">序號欄位名稱</param>
    /// <param name="numFormat">編號格式</param>
    public void AddSequenceField(string name, string numFormat = "1") => Doc.AddSequenceField(this, name, numFormat);

    /// <summary>在段落中新增參考項目欄位。</summary>
    /// <param name="refName">參考項目名稱</param>
    public void AddReferenceField(string refName) => Doc.AddReferenceField(this, refName);

    /// <summary>在段落中新增序號交互參照欄位。</summary>
    /// <param name="sequenceName">序號欄位名稱</param>
    /// <param name="referenceFormat">參照格式，預設為 "value"</param>
    public void AddSequenceRefField(string sequenceName, string referenceFormat = "value")
        => Doc.AddSequenceRefField(this, sequenceName, referenceFormat);

    /// <summary>在段落中新增書籤參照欄位。</summary>
    /// <param name="bookmarkName">書籤名稱</param>
    /// <param name="referenceFormat">參照格式，預設為 "text"</param>
    public void AddBookmarkReferenceField(string bookmarkName, string referenceFormat = "text") => Doc.AddBookmarkReferenceField(this, bookmarkName, referenceFormat);

    /// <summary>在段落中設定變數欄位值。</summary>
    /// <param name="name">變數名稱</param>
    /// <param name="value">變數值</param>
    public void AddVariableSetField(string name, string value) => Doc.AddVariableSetField(this, name, value);

    /// <summary>在段落中取得變數欄位值。</summary>
    /// <param name="name">變數名稱</param>
    public void AddVariableGetField(string name) => Doc.AddVariableGetField(this, name);

    /// <summary>在段落中插入腳注。</summary>
    /// <param name="citation">腳注引用標記</param>
    /// <param name="bodyText">腳注本文內容</param>
    public void AddFootnote(string citation, string bodyText) => Doc.AddFootnote(this, citation, bodyText);

    /// <summary>在段落中插入尾注。</summary>
    /// <param name="citation">尾注引用標記</param>
    /// <param name="bodyText">尾注本文內容</param>
    public void AddEndnote(string citation, string bodyText) => Doc.AddEndnote(this, citation, bodyText);

    /// <summary>在段落中新增字母索引標記。</summary>
    /// <param name="stringValue">索引字串值</param>
    /// <param name="key1">主要鍵值</param>
    /// <param name="key2">次要鍵值</param>
    public OdfAlphabeticalIndexMark AddAlphabeticalIndexMark(string stringValue, string? key1 = null, string? key2 = null)
        => Doc.AddAlphabeticalIndexMark(this, stringValue, key1, key2);

    /// <summary>在段落中新增文獻標記。</summary>
    /// <param name="identifier">文獻標記識別碼</param>
    /// <param name="bibliographyType">文獻類型</param>
    /// <param name="author">文獻作者</param>
    /// <param name="title">文獻標題</param>
    /// <param name="year">出版年份</param>
    public OdfBibliographyMark AddBibliographyMark(string identifier, string bibliographyType, string author, string title, string year)
        => Doc.AddBibliographyMark(this, identifier, bibliographyType, author, title, year);

    /// <summary>在段落中新增書籤。</summary>
    /// <param name="name">書籤名稱</param>
    public void AddBookmark(string name) => Doc.AddBookmark(this, name);

    /// <summary>在段落中新增參考標記。</summary>
    /// <param name="name">參考標記名稱</param>
    public void AddReferenceMark(string name) => Doc.AddReferenceMark(this, name);

    /// <summary>在段落中新增超連結。</summary>
    /// <param name="url">目標 URL</param>
    /// <param name="text">顯示文字</param>
    public void AddHyperlink(string url, string text) => Doc.AddHyperlink(this, url, text);

    /// <summary>在段落中新增圖片。</summary>
    /// <param name="packagePath">圖片在封裝包內的路徑</param>
    /// <param name="width">圖片寬度</param>
    /// <param name="height">圖片高度</param>
    /// <param name="name">圖片名稱</param>
    public OdfImage AddImage(string packagePath, OdfLength width, OdfLength height, string? name = null)
        => Doc.AddImage(this, packagePath, width, height, name);

    /// <summary>
    /// 在段落中新增浮動文字框。
    /// </summary>
    /// <param name="x">X 軸座標位置。</param>
    /// <param name="y">Y 軸座標位置。</param>
    /// <param name="width">文字框寬度。</param>
    /// <param name="height">文字框高度。</param>
    /// <param name="anchorType">錨定類型。</param>
    /// <param name="wrap">文字環繞方式。</param>
    /// <returns>新建立的浮動文字框。</returns>
    public OdfFloatingTextBox AddFloatingTextBox(
        OdfLength x,
        OdfLength y,
        OdfLength width,
        OdfLength height,
        OdfAnchorType anchorType = OdfAnchorType.Paragraph,
        OdfTextWrap wrap = OdfTextWrap.Parallel)
    {
        var frame = OdfNodeFactory.CreateElement("frame", OdfNamespaces.Draw, "draw");
        frame.SetAttribute("name", OdfNamespaces.Draw, "TextBox_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        frame.SetAttribute("anchor-type", OdfNamespaces.Text, ToAnchorTypeValue(anchorType), "text");
        frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        frame.SetAttribute("width", OdfNamespaces.Svg, width.ToString(), "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, height.ToString(), "svg");
        frame.SetAttribute("wrap", OdfNamespaces.Style, ToWrapValue(wrap), "style");

        var textBox = OdfNodeFactory.CreateElement("text-box", OdfNamespaces.Draw, "draw");
        frame.AppendChild(textBox);
        Node.AppendChild(frame);

        return new OdfFloatingTextBox(textBox, Doc);
    }

    private static string ToAnchorTypeValue(OdfAnchorType anchorType) => anchorType switch
    {
        OdfAnchorType.Page => "page",
        OdfAnchorType.Character => "char",
        OdfAnchorType.AsChar => "as-char",
        _ => "paragraph",
    };

    private static string ToWrapValue(OdfTextWrap wrap) => wrap switch
    {
        OdfTextWrap.None => "none",
        OdfTextWrap.Left => "left",
        OdfTextWrap.Right => "right",
        OdfTextWrap.Through => "run-through",
        _ => "parallel",
    };

    /// <summary>在段落中新增旁註標記（注音）。</summary>
    /// <param name="baseText">基礎文字</param>
    /// <param name="rubyText">注音文字</param>
    public OdfRuby AddRuby(string baseText, string rubyText) => Doc.AddRuby(this, baseText, rubyText);

    /// <summary>在段落中新增公式物件（MathML）。</summary>
    /// <param name="mathMlXmlString">MathML XML 字串</param>
    public void AddFormula(string mathMlXmlString) => Doc.AddFormula(this, mathMlXmlString);

    /// <summary>在段落中新增批注。</summary>
    /// <param name="comment">批注物件</param>
    public void AddComment(OdfComment comment) => Doc.AddComment(this, comment);

    /// <summary>在段落中解析並新增 HTML 片段。</summary>
    /// <param name="html">HTML 字串片段</param>
    public void AddHtmlFragment(string html) => Doc.AddHtmlFragment(this, html);

    /// <summary>在段落中新增頁碼欄位。</summary>
    public void AddPageNumberField() => Doc.AddPageNumberField(this);

    /// <summary>在段落中新增總頁數欄位。</summary>
    public void AddPageCountField() => Doc.AddPageCountField(this);

    /// <summary>
    /// 在此段落前插入分頁符號，並可選擇性地切換頁面樣式。
    /// </summary>
    /// <param name="masterPageName">要切換的主頁面樣式名稱；null 表示只插入分頁。</param>
    /// <param name="pageNumber">新頁碼起始值；null 表示繼續。</param>
    public void BreakPageBefore(string? masterPageName = null, int? pageNumber = null)
    {
        Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "break-before", OdfNamespaces.Fo, "page", "fo");
        if (pageNumber.HasValue)
            Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "page-number", OdfNamespaces.Style, pageNumber.Value.ToString(), "style");
        if (!string.IsNullOrEmpty(masterPageName))
            Doc.StyleEngine.GetOrCreateLocalStyle(Node, "paragraph").SetAttribute("master-page-name", OdfNamespaces.Style, masterPageName!, "style");
    }
}

/// <summary>
/// 表示浮動文字框的文字環繞方式。
/// </summary>
public enum OdfTextWrap
{
    /// <summary>
    /// 不環繞。
    /// </summary>
    None,

    /// <summary>
    /// 平行環繞。
    /// </summary>
    Parallel,

    /// <summary>
    /// 只允許左側環繞。
    /// </summary>
    Left,

    /// <summary>
    /// 只允許右側環繞。
    /// </summary>
    Right,

    /// <summary>
    /// 文字穿越物件。
    /// </summary>
    Through
}

/// <summary>
/// 表示浮動物件的錨定類型。
/// </summary>
public enum OdfAnchorType
{
    /// <summary>
    /// 錨定到頁面。
    /// </summary>
    Page,

    /// <summary>
    /// 錨定到段落。
    /// </summary>
    Paragraph,

    /// <summary>
    /// 錨定到字元。
    /// </summary>
    Character,

    /// <summary>
    /// 視為字元。
    /// </summary>
    AsChar
}

/// <summary>
/// 表示 ODT 文件中的浮動文字框。
/// </summary>
public sealed class OdfFloatingTextBox
{
    private readonly OdfNode _textBoxNode;
    private readonly TextDocument _document;

    internal OdfFloatingTextBox(OdfNode textBoxNode, TextDocument document)
    {
        _textBoxNode = textBoxNode ?? throw new ArgumentNullException(nameof(textBoxNode));
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 新增文字框段落。
    /// </summary>
    /// <param name="text">段落文字。</param>
    /// <returns>新建立的段落。</returns>
    public OdfParagraph AddParagraph(string text = "")
    {
        var paragraphNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        paragraphNode.TextContent = text;
        _textBoxNode.AppendChild(paragraphNode);
        return new OdfParagraph(paragraphNode, _document);
    }
}

/// <summary>
/// 表示文字文件中的標題。
/// </summary>
public class OdfHeading : OdfParagraph
{
    internal OdfHeading(OdfNode node, TextDocument doc) : base(node, doc) { }

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
public class OdfTextRun
{
    internal OdfTextRun(OdfNode node, TextDocument doc)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    /// <summary>
    /// 取得與此文字片段相關聯的 OdfNode 節點。
    /// </summary>
    internal OdfNode Node { get; }

    private readonly TextDocument _doc;

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
    /// 取得或設定文字片段的字色。
    /// </summary>
    public string? Color
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "color", OdfNamespaces.Fo, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "color", OdfNamespaces.Fo, value ?? string.Empty, "fo");
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

    /// <summary>
    /// 設定此文字片段是否為粗體。
    /// </summary>
    /// <param name="bold">是否粗體。</param>
    /// <returns>文字片段本身。</returns>
    public OdfTextRun WithBold(bool bold = true)
    {
        IsBold = bold;
        return this;
    }

    /// <summary>
    /// 設定此文字片段是否為斜體。
    /// </summary>
    /// <param name="italic">是否斜體。</param>
    /// <returns>文字片段本身。</returns>
    public OdfTextRun WithItalic(bool italic = true)
    {
        IsItalic = italic;
        return this;
    }

    /// <summary>
    /// 設定此文字片段的西文、東亞及複雜字型大小。
    /// </summary>
    /// <param name="size">字型大小，例如 <c>12pt</c>。</param>
    /// <returns>文字片段本身。</returns>
    public OdfTextRun WithFontSize(string size)
    {
        SetFontSize(size);
        return this;
    }

    /// <summary>
    /// 設定此文字片段的字色。
    /// </summary>
    /// <param name="hexColor">十六進位顏色字串，例如 <c>#FF0000</c>。</param>
    /// <returns>文字片段本身。</returns>
    public OdfTextRun WithColor(string hexColor)
    {
        Color = hexColor;
        return this;
    }
}

/// <summary>
/// 表示文字文件中的多欄版面配置區段。
/// </summary>
public class OdfSection
{
    internal OdfSection(OdfNode node, TextDocument doc)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    /// <summary>
    /// 取得與此區段相關聯的 OdfNode 節點。
    /// </summary>
    internal OdfNode Node { get; }

    private readonly TextDocument _doc;

    /// <summary>
    /// 取得或設定此區段的書寫模式。
    /// </summary>
    public string? WritingMode
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "writing-mode", OdfNamespaces.Style, "section");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "section", "section-properties", "writing-mode", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// 取得此區段是否受保護。
    /// </summary>
    public bool IsProtected => Node.GetAttribute("protected", OdfNamespaces.Text) == "true";

    /// <summary>
    /// 以指定密碼保護此區段。
    /// </summary>
    /// <param name="password">密碼明文。</param>
    public void Protect(string password)
    {
        OdfKit.Core.OdfProtectionHelper.ProtectNode(Node, password, "text", OdfNamespaces.Text);
    }

    /// <summary>
    /// 解除此區段的密碼保護。
    /// </summary>
    public void Unprotect()
    {
        OdfKit.Core.OdfProtectionHelper.UnprotectNode(Node, OdfNamespaces.Text);
    }

    /// <summary>
    /// 嘗試以指定密碼解除此區段的保護。
    /// </summary>
    /// <param name="password">密碼明文。</param>
    /// <returns>若解除成功則為 true，否則為 false。</returns>
    public bool TryUnprotect(string password)
    {
        if (!IsProtected)
            return true;
        if (OdfKit.Core.OdfProtectionHelper.VerifyPassword(Node, password, OdfNamespaces.Text))
        {
            Unprotect();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 驗證指定密碼是否能成功解鎖此區段。
    /// </summary>
    /// <param name="password">密碼明文。</param>
    /// <returns>若密碼正確或區段未受保護則為 true，否則為 false。</returns>
    public bool VerifyPassword(string password)
    {
        if (!IsProtected)
            return true;
        return OdfKit.Core.OdfProtectionHelper.VerifyPassword(Node, password, OdfNamespaces.Text);
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
    internal OdfNode Node { get; }

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
    /// 取得或設定表格的無障礙摘要說明（對應 ODF <c>table:summary</c> 屬性）。
    /// </summary>
    public string? Summary
    {
        get => Node.GetAttribute("summary", OdfNamespaces.Table);
        set
        {
            if (string.IsNullOrEmpty(value))
                Node.RemoveAttribute("summary", OdfNamespaces.Table);
            else
                Node.SetAttribute("summary", OdfNamespaces.Table, value!, "table");
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
                if (r == startRow && c == startCol)
                    continue;

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
/// 清單層級的類型。
/// </summary>
public enum OdfListLevelType
{
    /// <summary>編號清單</summary>
    Number,
    /// <summary>項目符號清單</summary>
    Bullet,
}

/// <summary>
/// 定義多層級清單樣式的單一層級設定。
/// </summary>
public sealed class OdfListLevelStyle
{
    /// <summary>層級（1–10）。</summary>
    public int Level { get; init; } = 1;
    /// <summary>層級類型（編號或項目符號）。</summary>
    public OdfListLevelType Type { get; init; } = OdfListLevelType.Number;
    /// <summary>項目符號字元（僅 Bullet 類型有效）。</summary>
    public string? BulletChar { get; init; }
    /// <summary>編號格式（"1"、"a"、"A"、"i"、"I"）。</summary>
    public string NumFormat { get; init; } = "1";
    /// <summary>編號前綴文字。</summary>
    public string? NumPrefix { get; init; }
    /// <summary>編號後綴文字（預設為 "."）。</summary>
    public string? NumSuffix { get; init; } = ".";
    /// <summary>左側縮排量。</summary>
    public OdfLength IndentLeft { get; init; }
    /// <summary>首行縮排量（負值表示懸掛縮排）。</summary>
    public OdfLength FirstLineIndent { get; init; }
}

/// <summary>
/// 表示文字文件中的清單。
/// </summary>
public class OdfList
{
    internal OdfList(OdfNode node, TextDocument doc)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    /// <summary>
    /// 取得與此清單相關聯的 OdfNode 節點。
    /// </summary>
    internal OdfNode Node { get; }

    private readonly TextDocument _doc;

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
    /// 在指定層級新增清單項目（1-based）。層級 1 直接加入此清單；
    /// 層級 2 以上則自動建立/沿用巢狀清單結構。
    /// </summary>
    /// <param name="text">項目文字內容。</param>
    /// <param name="level">目標層級，從 1 開始，最大值為 10。</param>
    /// <returns>新建立的清單項目。</returns>
    public OdfListItem AddItem(string text, int level = 1)
    {
        if (level < 1)
            level = 1;
        if (level > 10)
            level = 10;
        if (level == 1)
            return AddListItem(text);

        OdfNode currentList = Node;
        for (int l = 1; l < level; l++)
        {
            var lastItem = FindLastListItem(currentList);
            if (lastItem is null)
            {
                var parentItem = OdfNodeFactory.CreateElement("list-item", OdfNamespaces.Text, "text");
                currentList.AppendChild(parentItem);
                lastItem = parentItem;
            }
            OdfNode? nestedList = FindNestedList(lastItem);
            if (nestedList is null)
            {
                nestedList = OdfNodeFactory.CreateElement("list", OdfNamespaces.Text, "text");
                if (!string.IsNullOrEmpty(StyleName))
                    nestedList.SetAttribute("style-name", OdfNamespaces.Text, StyleName!, "text");
                lastItem.AppendChild(nestedList);
            }
            currentList = nestedList;
        }

        var itemNode = OdfNodeFactory.CreateElement("list-item", OdfNamespaces.Text, "text");
        currentList.AppendChild(itemNode);
        var item = new OdfListItem(itemNode, _doc);
        item.AddParagraph(text);
        return item;
    }

    private static OdfNode? FindLastListItem(OdfNode listNode)
    {
        OdfNode? last = null;
        foreach (var child in listNode.Children)
        {
            if (child.LocalName == "list-item" && child.NamespaceUri == OdfNamespaces.Text)
                last = child;
        }
        return last;
    }

    private static OdfNode? FindNestedList(OdfNode itemNode)
    {
        foreach (var child in itemNode.Children)
        {
            if (child.LocalName == "list" && child.NamespaceUri == OdfNamespaces.Text)
                return child;
        }
        return null;
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

    /// <summary>
    /// 設定清單的起始編號。
    /// </summary>
    /// <param name="value">起始編號；ODF 1.4 允許從 0 開始。</param>
    /// <returns>目前清單執行個體。</returns>
    public OdfList StartFrom(int value)
    {
        RestartNumbering(value);
        return this;
    }

    /// <summary>
    /// 取得清單項目清單。
    /// </summary>
    public IReadOnlyList<OdfListItem> Items
    {
        get
        {
            List<OdfListItem> items = [];
            foreach (OdfNode child in Node.Children)
            {
                if (child.NodeType is OdfNodeType.Element &&
                    child.LocalName == "list-item" &&
                    child.NamespaceUri == OdfNamespaces.Text)
                {
                    items.Add(new OdfListItem(child, _doc));
                }
            }

            return items.AsReadOnly();
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
    internal OdfNode Node { get; } = node;

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

    /// <summary>
    /// 取得清單項目中的段落清單。
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
    /// 將此圖片標記為裝飾性，輔助技術應略過此物件。
    /// </summary>
    /// <param name="decorative">是否標記為裝飾性。</param>
    /// <returns>目前圖片執行個體。</returns>
    public OdfImage MarkAsDecorative(bool decorative = true)
    {
        if (decorative)
        {
            FrameNode.SetAttribute("decorative", OdfNamespaces.Draw, "true", "draw");
        }
        else
        {
            FrameNode.RemoveAttribute("decorative", OdfNamespaces.Draw);
        }

        return this;
    }

    /// <summary>
    /// 取得或設定圖片的名稱。
    /// </summary>
    public string? Name
    {
        get => FrameNode.GetAttribute("name", OdfNamespaces.Draw);
        set => FrameNode.SetAttribute("name", OdfNamespaces.Draw, value ?? string.Empty, "draw");
    }

    /// <summary>
    /// 取得圖片在 ODF 封裝中的參照路徑。
    /// </summary>
    public string? ImageHref => ImageNode.GetAttribute("href", OdfNamespaces.XLink);

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

    /// <summary>
    /// 取得或設定圖片的無障礙替代文字（對應 <c>&lt;svg:desc&gt;</c>）。
    /// </summary>
    public string? AltText
    {
        get => FindSvgChildText("desc");
        set => SetSvgChildText("desc", value);
    }

    /// <summary>
    /// 取得或設定圖片的無障礙標題（對應 <c>&lt;svg:title&gt;</c>）。
    /// </summary>
    public string? AccessibilityTitle
    {
        get => FindSvgChildText("title");
        set => SetSvgChildText("title", value);
    }

    private string? FindSvgChildText(string localName)
    {
        foreach (var child in FrameNode.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == OdfNamespaces.Svg)
                return child.TextContent;
        }
        return null;
    }

    private void SetSvgChildText(string localName, string? text)
    {
        foreach (var child in FrameNode.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == OdfNamespaces.Svg)
            {
                if (string.IsNullOrEmpty(text))
                    FrameNode.RemoveChild(child);
                else
                    child.TextContent = text!;
                return;
            }
        }
        if (!string.IsNullOrEmpty(text))
        {
            var node = OdfNodeFactory.CreateElement(localName, OdfNamespaces.Svg, "svg");
            node.TextContent = text!;
            FrameNode.AppendChild(node);
        }
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
    internal OdfNode Node { get; } = node;

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
