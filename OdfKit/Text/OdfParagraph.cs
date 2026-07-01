using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

/// <summary>
/// Represents a paragraph in a text document.
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
    internal OdfNode Node { get; set; }

    /// <summary>
    /// The owning text document.
    /// 取得所屬的文字文件。
    /// </summary>
    protected readonly TextDocument Doc;

    /// <summary>
    /// Gets or sets the paragraph's text content.
    /// 取得或設定段落的文字內容。
    /// </summary>
    public string TextContent
    {
        get => Node.TextContent;
        set => Node.TextContent = value;
    }

    /// <summary>
    /// Searches this paragraph for the specified text and replaces it with new text, while preserving the original style structure of unmatched segments.
    /// 在此段落內搜尋指定文字並替換為新文字，同時保留未命中區段的原始樣式結構。
    /// </summary>
    /// <param name="search">The keyword to search for. / 要搜尋的關鍵字。</param>
    /// <param name="replacement">The replacement text. / 要替換的新文字。</param>
    public void ReplaceText(string search, string replacement)
        => TextDocumentSearchReplaceEngine.ReplaceText(this, search, replacement);

    /// <summary>
    /// Gets or sets the paragraph's style name.
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
    /// Gets the high-level style configuration proxy facade for this paragraph.
    /// 取得此段落的高階樣式設定代理 Facade。
    /// </summary>
    public OdfKit.Styles.OdfParagraphStyleProxy Style => _styleProxy ??= new OdfKit.Styles.OdfParagraphStyleProxy(this);

    /// <summary>
    /// Gets or sets the paragraph's horizontal alignment.
    /// 取得或設定段落的水平對齊方式。
    /// </summary>
    public string? HorizontalAlignment
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "text-align", OdfNamespaces.Fo, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "text-align", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// Gets or sets the paragraph's first-line indent (maps to the <c>fo:text-indent</c> attribute).
    /// 取得或設定段落首行縮排（對應 <c>fo:text-indent</c> 屬性）。
    /// </summary>
    public string? TextIndent
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "text-indent", OdfNamespaces.Fo, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "text-indent", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// Gets or sets the paragraph's writing mode.
    /// 取得或設定段落的書寫模式。
    /// </summary>
    public OdfWritingMode WritingMode
    {
        get => OdfWritingModeExtensions.FromOdfToken(Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "writing-mode", OdfNamespaces.Style, "paragraph"));
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "writing-mode", OdfNamespaces.Style, value.ToOdfToken(), "style");
    }

    /// <summary>
    /// Gets or sets the paragraph's Western font name.
    /// 取得或設定段落的西文字型名稱。
    /// </summary>
    public string? FontName
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-name", OdfNamespaces.Style, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "text-properties", "font-name", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// Gets or sets the paragraph's East Asian (CJK) font name.
    /// 取得或設定段落的東亞（中日韓）字型名稱。
    /// </summary>
    public string? FontNameAsian
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-name-asian", OdfNamespaces.Style, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "text-properties", "font-name-asian", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// Gets or sets the paragraph's complex script font name.
    /// 取得或設定段落的複雜文字字型名稱。
    /// </summary>
    public string? FontNameComplex
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-name-complex", OdfNamespaces.Style, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "text-properties", "font-name-complex", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// Gets or sets the paragraph's Western font size.
    /// 取得或設定段落的西文字型大小。
    /// </summary>
    public string? FontSize
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-size", OdfNamespaces.Fo, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "text-properties", "font-size", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// Gets or sets the paragraph's East Asian (CJK) font size.
    /// 取得或設定段落的東亞（中日韓）字型大小。
    /// </summary>
    public string? FontSizeAsian
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-size-asian", OdfNamespaces.Style, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "text-properties", "font-size-asian", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// Gets or sets the paragraph's complex script font size.
    /// 取得或設定段落的複雜文字字型大小。
    /// </summary>
    public string? FontSizeComplex
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-size-complex", OdfNamespaces.Style, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "text-properties", "font-size-complex", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// Sets the paragraph's font names.
    /// 設定段落的字型名稱。
    /// </summary>
    /// <param name="westernFont">The Western font name. / 西文字型名稱。</param>
    /// <param name="asianFont">The East Asian (CJK) font name. / 東亞（中日韓）字型名稱。</param>
    /// <param name="complexFont">The complex script font name. / 複雜文字字型名稱。</param>
    public void SetFont(string westernFont, string? asianFont = null, string? complexFont = null)
    {
        FontName = westernFont;
        FontNameAsian = asianFont ?? westernFont;
        FontNameComplex = complexFont ?? westernFont;
    }

    /// <summary>
    /// Sets the paragraph's font sizes.
    /// 設定段落的字型大小。
    /// </summary>
    /// <param name="westernSize">The Western font size. / 西文字型大小。</param>
    /// <param name="asianSize">The East Asian font size. / 東亞字型大小。</param>
    /// <param name="complexSize">The complex script font size. / 複雜文字字型大小。</param>
    public void SetFontSize(string westernSize, string? asianSize = null, string? complexSize = null)
    {
        FontSize = westernSize;
        FontSizeAsian = asianSize ?? westernSize;
        FontSizeComplex = complexSize ?? westernSize;
    }

    /// <summary>
    /// Adds a text run at the end of the paragraph.
    /// 在段落結尾新增一個文字片段。
    /// </summary>
    /// <param name="text">The text content to add. / 要新增的文字內容。</param>
    /// <returns>The created text run object. / 建立的文字片段物件。</returns>
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
    /// Adds text using the specified font fallback options.
    /// 使用指定的字型遞補選項新增文字。
    /// </summary>
    /// <param name="text">The text content to add. / 要新增的文字內容。</param>
    /// <param name="options">The font fallback options. / 字型遞補選項。</param>
    /// <returns>The collection of added text runs. / 新增的文字片段集合。</returns>
    public IReadOnlyList<OdfTextRun> AddText(string text, OdfTextFontFallbackOptions options)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.DeclareDefaultCjkFallbackFonts)
        {
            OdfCjkFontFallbackEngine.ApplyFontFallback(Doc, options);
        }

        List<(string Text, string FontName)> segments = OdfFontSegmenter.SegmentText(text, options.BaseFont);
        var runs = new List<OdfTextRun>(segments.Count);
        foreach ((string segmentText, string fontName) in segments)
        {
            OdfTextRun run = AddTextRun(segmentText);
            run.SetFont(fontName, fontName, fontName);
            runs.Add(run);
        }

        return runs;
    }

    /// <summary>
    /// Adds a soft page break in the paragraph.
    /// 在段落中新增軟分頁符號。
    /// </summary>
    public void AddSoftPageBreak()
    {
        var node = OdfNodeFactory.CreateElement("soft-page-break", OdfNamespaces.Text, "text");
        Node.AppendChild(node);
    }

    /// <summary>
    /// Adds a tab character in the paragraph.
    /// 在段落中新增定位點（Tab）字元。
    /// </summary>
    public void AddTab()
    {
        var node = OdfNodeFactory.CreateElement("tab", OdfNamespaces.Text, "text");
        Node.AppendChild(node);
    }

    /// <summary>
    /// Adds a line break in the paragraph.
    /// 在段落中新增換行符號。
    /// </summary>
    public void AddLineBreak()
    {
        var node = OdfNodeFactory.CreateElement("line-break", OdfNamespaces.Text, "text");
        Node.AppendChild(node);
    }

    /// <summary>
    /// Adds the specified number of space entries in the paragraph.
    /// 在段落中新增指定數量的空格專案。
    /// </summary>
    /// <param name="count">The number of spaces. / 空格數量。</param>
    public void AddSpace(int count = 1)
    {
        var node = OdfNodeFactory.CreateElement("s", OdfNamespaces.Text, "text");
        if (count > 1)
        {
            node.SetAttribute("c", OdfNamespaces.Text, count.ToString(CultureInfo.InvariantCulture), "text");
        }
        Node.AppendChild(node);
    }

    /// <summary>
    /// Deletes this paragraph.
    /// 刪除此段落。
    /// </summary>
    public void Delete()
    {
        Doc.DeleteNode(Node);
    }

    /// <summary>
    /// Gets all text runs of this paragraph. Accessing it automatically wraps direct child text nodes in span nodes.
    /// 取得此段落的所有文字片段（Runs）。存取時會自動將直屬的文字節點分裂包裝為 span 節點。
    /// </summary>
    public IEnumerable<OdfTextRun> Runs
    {
        get
        {
            // 裂變同步：若存在直屬的 text 節點，則先將其包裝到一個全新的 <text:span> 中
            var children = new List<OdfNode>(Node.Children);
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
    /// Removes all text runs within this paragraph (removes all span nodes).
    /// 清除此段落內的所有文字片段（移除所有的 span 節點）。
    /// </summary>
    public void ClearRuns()
    {
        var spans = new List<OdfNode>();
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
    #region Fields & References

    /// <summary>
    /// Adds a date field in the paragraph.
    /// 在段落中新增日期欄位。
    /// </summary>
    public void AddDateField() => Doc.AddDateField(this);

    /// <summary>
    /// Adds a time field in the paragraph.
    /// 在段落中新增時間欄位。
    /// </summary>
    public void AddTimeField() => Doc.AddTimeField(this);

    /// <summary>
    /// Adds an author name field in the paragraph.
    /// 在段落中新增作者名稱欄位。
    /// </summary>
    public void AddAuthorField() => Doc.AddAuthorField(this);

    /// <summary>
    /// Adds a chapter field in the paragraph.
    /// 在段落中新增章節欄位。
    /// </summary>
    public void AddChapterField() => Doc.AddChapterField(this);

    /// <summary>
    /// Adds a sequence number field in the paragraph.
    /// 在段落中新增序號欄位。
    /// </summary>
    /// <param name="name">The sequence field name. / 序號欄位名稱。</param>
    /// <param name="numFormat">The number format. / 編號格式。</param>
    public void AddSequenceField(string name, string numFormat = "1") => Doc.AddSequenceField(this, name, numFormat);

    /// <summary>
    /// Adds a reference entry field in the paragraph.
    /// 在段落中新增參考專案欄位。
    /// </summary>
    /// <param name="refName">The reference entry name. / 參考專案名稱。</param>
    public void AddReferenceField(string refName) => Doc.AddReferenceField(this, refName);

    /// <summary>
    /// Adds a sequence cross-reference field in the paragraph.
    /// 在段落中新增序號交互參照欄位。
    /// </summary>
    /// <param name="sequenceName">The sequence field name. / 序號欄位名稱。</param>
    /// <param name="referenceFormat">The reference format; defaults to "value". / 參照格式，預設為 "value"。</param>
    public void AddSequenceRefField(string sequenceName, string referenceFormat = "value")
        => Doc.AddSequenceRefField(this, sequenceName, referenceFormat);

    /// <summary>
    /// Adds a bookmark reference field in the paragraph.
    /// 在段落中新增書籤參照欄位。
    /// </summary>
    /// <param name="bookmarkName">The bookmark name. / 書籤名稱。</param>
    /// <param name="referenceFormat">The reference format; defaults to "text". / 參照格式，預設為 "text"。</param>
    public void AddBookmarkReferenceField(string bookmarkName, string referenceFormat = "text") => Doc.AddBookmarkReferenceField(this, bookmarkName, referenceFormat);

    /// <summary>
    /// Sets a variable field value in the paragraph.
    /// 在段落中設定變數欄位值。
    /// </summary>
    /// <param name="name">The variable name. / 變數名稱。</param>
    /// <param name="value">The variable value. / 變數值。</param>
    public void AddVariableSetField(string name, string value) => Doc.AddVariableSetField(this, name, value);

    /// <summary>
    /// Gets a variable field value in the paragraph.
    /// 在段落中取得變數欄位值。
    /// </summary>
    /// <param name="name">The variable name. / 變數名稱。</param>
    public void AddVariableGetField(string name) => Doc.AddVariableGetField(this, name);

    /// <summary>
    /// Adds a database field display field (<c>text:database-display</c>) in the paragraph, used for mail merge or report content bound to a table column.
    /// 在段落中新增資料庫欄位顯示欄位（<c>text:database-display</c>），用於合併列印或報表內容綁定資料表欄位。
    /// </summary>
    /// <param name="tableName">The table, query, or command name. / 資料表、查詢或指令名稱。</param>
    /// <param name="columnName">The column name to display. / 要顯示的欄位名稱。</param>
    /// <param name="tableType">The data source type, either "table", "query", or "command". / 資料來源類型，可為 "table"、"query" 或 "command"。</param>
    /// <param name="databaseName">The database connection name. / 資料庫連線名稱。</param>
    public void AddDatabaseDisplayField(string tableName, string columnName, string? tableType = null, string? databaseName = null) =>
        Doc.AddDatabaseDisplayField(this, tableName, columnName, tableType, databaseName);

    /// <summary>
    /// Adds a database next record field (<c>text:database-next</c>) in the paragraph, used for advancing records in mail merge or reports.
    /// 在段落中新增資料庫下一筆記錄欄位（<c>text:database-next</c>），用於合併列印或報表的逐筆換行。
    /// </summary>
    /// <param name="tableName">The table, query, or command name. / 資料表、查詢或指令名稱。</param>
    /// <param name="tableType">The data source type, either "table", "query", or "command". / 資料來源類型，可為 "table"、"query" 或 "command"。</param>
    /// <param name="databaseName">The database connection name. / 資料庫連線名稱。</param>
    /// <param name="condition">The condition expression evaluated before advancing. / 換行前的判斷條件式。</param>
    public void AddDatabaseNextField(string tableName, string? tableType = null, string? databaseName = null, string? condition = null) =>
        Doc.AddDatabaseNextField(this, tableName, tableType, databaseName, condition);

    /// <summary>
    /// Inserts a footnote in the paragraph.
    /// 在段落中插入腳注。
    /// </summary>
    /// <param name="citation">The footnote citation marker. / 腳注引用標記。</param>
    /// <param name="bodyText">The footnote body content. / 腳注本文內容。</param>
    public void AddFootnote(string citation, string bodyText) => Doc.AddFootnote(this, citation, bodyText);

    /// <summary>
    /// Inserts an endnote in the paragraph.
    /// 在段落中插入尾注。
    /// </summary>
    /// <param name="citation">The endnote citation marker. / 尾注引用標記。</param>
    /// <param name="bodyText">The endnote body content. / 尾注本文內容。</param>
    public void AddEndnote(string citation, string bodyText) => Doc.AddEndnote(this, citation, bodyText);

    /// <summary>
    /// Adds an alphabetical index mark in the paragraph.
    /// 在段落中新增字母索引標記。
    /// </summary>
    /// <param name="stringValue">The index string value. / 索引字串值。</param>
    /// <param name="key1">The primary key. / 主要鍵值。</param>
    /// <param name="key2">The secondary key. / 次要鍵值。</param>
    public OdfAlphabeticalIndexMark AddAlphabeticalIndexMark(string stringValue, string? key1 = null, string? key2 = null)
        => Doc.AddAlphabeticalIndexMark(this, stringValue, key1, key2);

    /// <summary>
    /// Adds a bibliography mark in the paragraph.
    /// 在段落中新增文獻標記。
    /// </summary>
    /// <param name="identifier">The bibliography mark identifier. / 文獻標記識別碼。</param>
    /// <param name="bibliographyType">The bibliography type. / 文獻類型。</param>
    /// <param name="author">The bibliography author. / 文獻作者。</param>
    /// <param name="title">The bibliography title. / 文獻標題。</param>
    /// <param name="year">The publication year. / 出版年份。</param>
    public OdfBibliographyMark AddBibliographyMark(string identifier, string bibliographyType, string author, string title, string year)
        => Doc.AddBibliographyMark(this, identifier, bibliographyType, author, title, year);

    /// <summary>
    /// Adds a bookmark in the paragraph.
    /// 在段落中新增書籤。
    /// </summary>
    /// <param name="name">The bookmark name. / 書籤名稱。</param>
    public void AddBookmark(string name) => Doc.AddBookmark(this, name);

    /// <summary>
    /// Adds a reference mark in the paragraph.
    /// 在段落中新增參考標記。
    /// </summary>
    /// <param name="name">The reference mark name. / 參考標記名稱。</param>
    public void AddReferenceMark(string name) => Doc.AddReferenceMark(this, name);

    /// <summary>
    /// Adds a hyperlink in the paragraph.
    /// 在段落中新增超連結。
    /// </summary>
    /// <param name="url">The target URL. / 目標 URL。</param>
    /// <param name="text">The display text. / 顯示文字。</param>
    public void AddHyperlink(string url, string text) => Doc.AddHyperlink(this, url, text);

    /// <summary>
    /// Appends a list builder within the paragraph.
    /// 在段落內追加一個清單建構器。
    /// </summary>
    /// <param name="styleName">The optional list style name. / 選用的清單樣式名稱。</param>
    /// <returns>The list builder. / 清單建構器。</returns>
    public OdfListBuilder AppendList(string? styleName = null)
        => new OdfListBuilder(Node, Doc, null, styleName);

    /// <summary>
    /// Gets the paragraph's inline rich-text builder, supporting chained style configuration.
    /// 取得段落的行內富文本建構器，支援鏈式樣式設定。
    /// </summary>
    /// <returns>The inline rich-text builder. / 行內富文本建構器。</returns>
    public InlineTextBuilder AppendText()
        => new InlineTextBuilder(Node, Doc);

    #endregion

}
