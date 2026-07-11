using System.Collections.Generic;
using OdfKit.Styles;

namespace OdfKit.Text;
/// <summary>
/// Provides the TextDocument API.
/// 提供 TextDocument API。
/// </summary>

public partial class TextDocument
{
    #region Document Elements - Notes & Ruby

    /// <summary>
    /// 在指定段落中插入腳注 (text:note, note-class="footnote")。
    /// </summary>
    /// <param name="paragraph">要插入腳注的段落</param>
    /// <param name="citation">腳注引用標記，例如 "1" 或 "*"</param>
    /// <param name="bodyText">腳注本文內容</param>
    internal void AddFootnote(OdfParagraph paragraph, string citation, string bodyText) =>
        TextDocumentNotesEngine.AddFootnote(MutationContext, paragraph, citation, bodyText);

    /// <summary>
    /// 在指定段落中插入尾注 (text:note, note-class="endnote")。
    /// </summary>
    /// <param name="paragraph">要插入尾注的段落</param>
    /// <param name="citation">尾注引用標記，例如 "i" 或 "a"</param>
    /// <param name="bodyText">尾注本文內容</param>
    internal void AddEndnote(OdfParagraph paragraph, string citation, string bodyText) =>
        TextDocumentNotesEngine.AddEndnote(MutationContext, paragraph, citation, bodyText);
    /// <summary>
    /// Short overload of AddAlphabeticalIndex that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：AddAlphabeticalIndex 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfAlphabeticalIndex AddAlphabeticalIndex() => AddAlphabeticalIndex("Alphabetical Index");


    /// <summary>
    /// Adds an alphabetical index to the end of the document body.
    /// 新增字母索引至文件本文結尾。
    /// </summary>
    /// <param name="title">The index title. / 索引標題。</param>
    /// <returns>The created alphabetical index object. / 建立的字母索引物件。</returns>
    public OdfAlphabeticalIndex AddAlphabeticalIndex(string title) =>
        TextDocumentNotesEngine.AddAlphabeticalIndex(this, MutationContext, title);

    /// <summary>
    /// Short overload of AddBibliography that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：AddBibliography 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfBibliography AddBibliography() => AddBibliography("Bibliography");


    /// <summary>
    /// Adds a bibliography to the end of the document body.
    /// 新增文獻目錄至文件本文結尾。
    /// </summary>
    /// <param name="title">The bibliography title. / 文獻目錄標題。</param>
    /// <returns>The created bibliography object. / 建立的文獻目錄物件。</returns>
    public OdfBibliography AddBibliography(string title) =>
        TextDocumentNotesEngine.AddBibliography(this, MutationContext, title);


    /// <summary>
    /// Gets the list of all indexes in the document.
    /// 取得文件中所有索引的列表。
    /// </summary>
    /// <returns>The list of index objects. / 包含索引物件的列表。</returns>
    public List<OdfIndex> GetIndexes() =>
        TextDocumentNotesEngine.GetIndexes(this, BodyTextRoot);

    /// <summary>
    /// Finds the first index with the exact name.
    /// 尋找第一個具有精確名稱的索引。
    /// </summary>
    /// <param name="name">The exact index name. / 索引的精確名稱。</param>
    /// <returns>The matching index, or <see langword="null"/>. / 相符的索引；若不存在則為 <see langword="null"/>。</returns>
    public OdfIndex? FindIndex(string name)
    {
        foreach (OdfIndex index in GetIndexes())
        {
            if (string.Equals(index.Name, name, System.StringComparison.Ordinal))
                return index;
        }

        return null;
    }

    /// <summary>
    /// Removes the specified index from this document.
    /// 從此文件移除指定索引。
    /// </summary>
    /// <param name="index">The index to remove. / 要移除的索引。</param>
    /// <returns><see langword="true"/> if the index was removed; otherwise <see langword="false"/>. / 若已移除索引則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveIndex(OdfIndex index)
    {
        if (index is null)
            throw new System.ArgumentNullException(nameof(index));
        OdfKit.DOM.OdfNode? parent = index.Node.Parent;
        return parent is not null && parent.RemoveChild(index.Node);
    }

    /// <summary>
    /// Removes all indexes and returns the number removed.
    /// 移除所有索引，並傳回移除數量。
    /// </summary>
    /// <returns>The number of removed indexes. / 已移除的索引數量。</returns>
    public int ClearIndexes()
    {
        List<OdfIndex> indexes = GetIndexes();
        int removed = 0;
        foreach (OdfIndex index in indexes)
        {
            if (RemoveIndex(index))
                removed++;
        }

        return removed;
    }

    /// <summary>
    /// Regenerates the content of all indexes in the document.
    /// 重新產生文件中所有索引的內容。
    /// </summary>
    public void UpdateIndexes()
    {
        foreach (OdfIndex index in GetIndexes())
            index.Update();
    }

    /// <summary>
    /// Gets a summary list of all indexes in the document.
    /// 取得文件中所有索引的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfIndexInfo> GetIndexInfos() =>
        TextDocumentIndexReadEngine.GetIndexInfos(BodyTextRoot);

    /// <summary>
    /// Gets a summary list of all index marks in the document.
    /// 取得文件中所有索引標記的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfDocumentIndexMarkInfo> GetIndexMarks() =>
        TextDocumentIndexReadEngine.GetIndexMarks(BodyTextRoot);

    /// <summary>
    /// 在指定的段落中新增字母索引標記。
    /// </summary>
    /// <param name="paragraph">要新增標記的段落執行個體</param>
    /// <param name="stringValue">索引字串值</param>
    /// <param name="key1">主要鍵值</param>
    /// <param name="key2">次要鍵值</param>
    /// <returns>建立的字母索引標記物件</returns>
    internal OdfAlphabeticalIndexMark AddAlphabeticalIndexMark(OdfParagraph paragraph, string stringValue, string? key1 = null, string? key2 = null) =>
        TextDocumentNotesEngine.AddAlphabeticalIndexMark(paragraph, stringValue, key1, key2);

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
    internal OdfBibliographyMark AddBibliographyMark(
        OdfParagraph paragraph,
        string identifier,
        string bibliographyType,
        string author,
        string title,
        string year) =>
        TextDocumentNotesEngine.AddBibliographyMark(paragraph, identifier, bibliographyType, author, title, year);

    /// <summary>
    /// Adds a table index to the end of the document body.
    /// 新增表格索引至文件本文結尾。
    /// </summary>
    public void AddTableIndex() =>
        TextDocumentNotesEngine.AddTableIndex(MutationContext);

    /// <summary>
    /// Gets a summary list of all bookmarks in the document.
    /// 取得文件中所有書籤的摘要清單。
    /// </summary>
    /// <returns>The bookmark list, in document tree depth-first order. / 依文件樹深度優先順序排列的書籤清單。</returns>
    public IReadOnlyList<OdfBookmarkInfo> GetBookmarks() =>
        TextDocumentBookmarkReadEngine.GetBookmarks(BodyTextRoot);

    /// <summary>
    /// Finds the first bookmark with the exact name.
    /// 尋找第一個具有精確名稱的書籤。
    /// </summary>
    /// <param name="name">The exact bookmark name. / 書籤的精確名稱。</param>
    /// <returns>The matching bookmark summary, or <see langword="null"/>. / 相符的書籤摘要；若不存在則為 <see langword="null"/>。</returns>
    public OdfBookmarkInfo? FindBookmark(string name)
    {
        foreach (OdfBookmarkInfo bookmark in GetBookmarks())
        {
            if (string.Equals(bookmark.Name, name, System.StringComparison.Ordinal))
                return bookmark;
        }
        return null;
    }

    /// <summary>
    /// Renames bookmark markers and every bookmark reference field that targets them.
    /// 重新命名書籤標記，以及所有以其為目標的書籤參照欄位。
    /// </summary>
    /// <param name="currentName">The current exact name. / 目前的精確名稱。</param>
    /// <param name="newName">The replacement name. / 取代用名稱。</param>
    /// <returns>The number of changed markers and references; zero when the current bookmark does not exist, the replacement name is blank, or the replacement name already exists. / 已變更的標記與參照數量；目前書籤不存在、取代名稱為空白，或取代名稱已存在時為零。</returns>
    public int RenameBookmark(string currentName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || FindBookmark(newName) is not null)
            return 0;
        return RenameNamedTextNodes(BodyTextRoot, currentName, newName,
            ["bookmark", "bookmark-start", "bookmark-end"], ["bookmark-ref"]);
    }

    /// <summary>
    /// Removes bookmark markers and every bookmark reference field that targets them.
    /// 移除書籤標記，以及所有以其為目標的書籤參照欄位。
    /// </summary>
    /// <param name="name">The exact bookmark name. / 書籤的精確名稱。</param>
    /// <returns>The number of removed markers and references. / 已移除的標記與參照數量。</returns>
    public int RemoveBookmark(string name) =>
        RemoveNamedTextNodes(BodyTextRoot, name,
            ["bookmark", "bookmark-start", "bookmark-end"], ["bookmark-ref"]);

    /// <summary>
    /// Gets a summary list of all hyperlinks in the document.
    /// 取得文件中所有超連結的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfHyperlinkInfo> GetHyperlinks() =>
        TextDocumentHyperlinkReadEngine.GetHyperlinks(BodyTextRoot);

    /// <summary>
    /// Gets a summary list of all reference marks in the document.
    /// 取得文件中所有參考標記的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfReferenceMarkInfo> GetReferenceMarks() =>
        TextDocumentReferenceMarkReadEngine.GetReferenceMarks(BodyTextRoot);

    /// <summary>
    /// Finds the first reference mark with the exact name.
    /// 尋找第一個具有精確名稱的參考標記。
    /// </summary>
    /// <param name="name">The exact reference-mark name. / 參考標記的精確名稱。</param>
    /// <returns>The matching mark summary, or <see langword="null"/>. / 相符的標記摘要；若不存在則為 <see langword="null"/>。</returns>
    public OdfReferenceMarkInfo? FindReferenceMark(string name)
    {
        foreach (OdfReferenceMarkInfo mark in GetReferenceMarks())
        {
            if (string.Equals(mark.Name, name, System.StringComparison.Ordinal))
                return mark;
        }
        return null;
    }

    /// <summary>
    /// Renames reference-mark nodes and every reference field that targets them.
    /// 重新命名參考標記節點，以及所有以其為目標的參照欄位。
    /// </summary>
    /// <param name="currentName">The current exact name. / 目前的精確名稱。</param>
    /// <param name="newName">The replacement name. / 取代用名稱。</param>
    /// <returns>The number of changed markers and references. / 已變更的標記與參照數量。</returns>
    public int RenameReferenceMark(string currentName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || FindReferenceMark(newName) is not null)
            return 0;
        return RenameNamedTextNodes(BodyTextRoot, currentName, newName,
            ["reference-mark", "reference-mark-start", "reference-mark-end"], ["reference-ref"]);
    }

    /// <summary>
    /// Removes reference-mark nodes and every reference field that targets them.
    /// 移除參考標記節點，以及所有以其為目標的參照欄位。
    /// </summary>
    /// <param name="name">The exact reference-mark name. / 參考標記的精確名稱。</param>
    /// <returns>The number of removed markers and references. / 已移除的標記與參照數量。</returns>
    public int RemoveReferenceMark(string name) =>
        RemoveNamedTextNodes(BodyTextRoot, name,
            ["reference-mark", "reference-mark-start", "reference-mark-end"], ["reference-ref"]);

    private static int RenameNamedTextNodes(
        OdfKit.DOM.OdfNode root,
        string currentName,
        string newName,
        IReadOnlyList<string> markerNames,
        IReadOnlyList<string> referenceNames)
    {
        int changed = 0;
        bool marker = ContainsName(markerNames, root.LocalName) &&
            string.Equals(root.GetAttribute("name", OdfKit.Core.OdfNamespaces.Text), currentName, System.StringComparison.Ordinal);
        bool reference = ContainsName(referenceNames, root.LocalName) &&
            string.Equals(root.GetAttribute("ref-name", OdfKit.Core.OdfNamespaces.Text), currentName, System.StringComparison.Ordinal);
        if (root.NamespaceUri == OdfKit.Core.OdfNamespaces.Text && (marker || reference))
        {
            root.SetAttribute(marker ? "name" : "ref-name", OdfKit.Core.OdfNamespaces.Text, newName, "text");
            changed++;
        }
        foreach (OdfKit.DOM.OdfNode child in root.Children)
            changed += RenameNamedTextNodes(child, currentName, newName, markerNames, referenceNames);
        return changed;
    }

    private static int RemoveNamedTextNodes(
        OdfKit.DOM.OdfNode root,
        string name,
        IReadOnlyList<string> markerNames,
        IReadOnlyList<string> referenceNames)
    {
        List<OdfKit.DOM.OdfNode> removals = [];
        int removed = 0;
        foreach (OdfKit.DOM.OdfNode child in root.Children)
        {
            bool marker = child.NamespaceUri == OdfKit.Core.OdfNamespaces.Text &&
                ContainsName(markerNames, child.LocalName) &&
                string.Equals(child.GetAttribute("name", OdfKit.Core.OdfNamespaces.Text), name, System.StringComparison.Ordinal);
            bool reference = child.NamespaceUri == OdfKit.Core.OdfNamespaces.Text &&
                ContainsName(referenceNames, child.LocalName) &&
                string.Equals(child.GetAttribute("ref-name", OdfKit.Core.OdfNamespaces.Text), name, System.StringComparison.Ordinal);
            if (marker || reference)
                removals.Add(child);
            else
                removed += RemoveNamedTextNodes(child, name, markerNames, referenceNames);
        }
        foreach (OdfKit.DOM.OdfNode removal in removals)
            root.RemoveChild(removal);
        return removed + removals.Count;
    }

    private static bool ContainsName(IReadOnlyList<string> names, string value)
    {
        foreach (string name in names)
        {
            if (name == value)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a summary list of all footnotes in the document.
    /// 取得文件中所有腳注的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfFootnoteInfo> GetFootnotes() =>
        TextDocumentFootnoteReadEngine.GetFootnotes(BodyTextRoot);

    /// <summary>
    /// Gets a summary list of all endnotes in the document.
    /// 取得文件中所有尾注的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfFootnoteInfo> GetEndnotes() =>
        TextDocumentFootnoteReadEngine.GetEndnotes(BodyTextRoot);

    /// <summary>
    /// Finds a footnote by its exact identifier.
    /// 依精確識別碼尋找腳注。
    /// </summary>
    /// <param name="id">The exact note identifier. / 注腳的精確識別碼。</param>
    /// <returns>The matching footnote, or <see langword="null"/>. / 相符的腳注；若不存在則為 <see langword="null"/>。</returns>
    public OdfFootnoteInfo? FindFootnote(string id) => FindNote(GetFootnotes(), id);

    /// <summary>
    /// Finds an endnote by its exact identifier.
    /// 依精確識別碼尋找尾注。
    /// </summary>
    /// <param name="id">The exact note identifier. / 注腳的精確識別碼。</param>
    /// <returns>The matching endnote, or <see langword="null"/>. / 相符的尾注；若不存在則為 <see langword="null"/>。</returns>
    public OdfFootnoteInfo? FindEndnote(string id) => FindNote(GetEndnotes(), id);

    /// <summary>
    /// Updates a footnote citation and body while preserving unknown note content.
    /// 更新腳注引用標記與本文，並保留未知注腳內容。
    /// </summary>
    /// <param name="id">The exact note identifier. / 注腳的精確識別碼。</param>
    /// <param name="citation">The replacement citation. / 取代用引用標記。</param>
    /// <param name="bodyText">The replacement body text. / 取代用本文。</param>
    /// <returns><see langword="true"/> if updated; otherwise <see langword="false"/>. / 若已更新則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool UpdateFootnote(string id, string citation, string bodyText) =>
        UpdateNote(id, "footnote", citation, bodyText);

    /// <summary>
    /// Updates an endnote citation and body while preserving unknown note content.
    /// 更新尾注引用標記與本文，並保留未知注腳內容。
    /// </summary>
    /// <param name="id">The exact note identifier. / 注腳的精確識別碼。</param>
    /// <param name="citation">The replacement citation. / 取代用引用標記。</param>
    /// <param name="bodyText">The replacement body text. / 取代用本文。</param>
    /// <returns><see langword="true"/> if updated; otherwise <see langword="false"/>. / 若已更新則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool UpdateEndnote(string id, string citation, string bodyText) =>
        UpdateNote(id, "endnote", citation, bodyText);

    /// <summary>
    /// Removes a footnote by identifier.
    /// 依識別碼移除腳注。
    /// </summary>
    /// <param name="id">The exact note identifier. / 注腳的精確識別碼。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveFootnote(string id) => RemoveNote(id, "footnote");

    /// <summary>
    /// Removes an endnote by identifier.
    /// 依識別碼移除尾注。
    /// </summary>
    /// <param name="id">The exact note identifier. / 注腳的精確識別碼。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveEndnote(string id) => RemoveNote(id, "endnote");

    /// <summary>
    /// Removes all footnotes.
    /// 移除所有腳注。
    /// </summary>
    /// <returns>The number removed. / 移除數量。</returns>
    public int ClearFootnotes() => ClearNotes(BodyTextRoot, "footnote");

    /// <summary>
    /// Removes all endnotes.
    /// 移除所有尾注。
    /// </summary>
    /// <returns>The number removed. / 移除數量。</returns>
    public int ClearEndnotes() => ClearNotes(BodyTextRoot, "endnote");

    private static OdfFootnoteInfo? FindNote(IReadOnlyList<OdfFootnoteInfo> notes, string id)
    {
        foreach (OdfFootnoteInfo note in notes)
        {
            if (string.Equals(note.Id, id, System.StringComparison.Ordinal))
                return note;
        }
        return null;
    }

    private bool UpdateNote(string id, string noteClass, string citation, string bodyText)
    {
        if (citation is null)
            throw new System.ArgumentNullException(nameof(citation));
        if (bodyText is null)
            throw new System.ArgumentNullException(nameof(bodyText));
        OdfKit.DOM.OdfNode? note = FindNoteNode(BodyTextRoot, id, noteClass);
        if (note is null)
            return false;
        SetNoteChildText(note, "note-citation", citation);
        SetNoteChildText(note, "note-body", bodyText);
        return true;
    }

    private bool RemoveNote(string id, string noteClass)
    {
        OdfKit.DOM.OdfNode? note = FindNoteNode(BodyTextRoot, id, noteClass);
        if (note?.Parent is null)
            return false;
        note.Parent.RemoveChild(note);
        return true;
    }

    private static OdfKit.DOM.OdfNode? FindNoteNode(OdfKit.DOM.OdfNode root, string id, string noteClass)
    {
        if (root.LocalName == "note" && root.NamespaceUri == OdfKit.Core.OdfNamespaces.Text &&
            root.GetAttribute("note-class", OdfKit.Core.OdfNamespaces.Text) == noteClass &&
            string.Equals(root.GetAttribute("id", OdfKit.Core.OdfNamespaces.Text), id, System.StringComparison.Ordinal))
            return root;
        foreach (OdfKit.DOM.OdfNode child in root.Children)
        {
            OdfKit.DOM.OdfNode? found = FindNoteNode(child, id, noteClass);
            if (found is not null)
                return found;
        }
        return null;
    }

    private static void SetNoteChildText(OdfKit.DOM.OdfNode note, string localName, string text)
    {
        foreach (OdfKit.DOM.OdfNode child in note.Children)
        {
            if (child.LocalName != localName || child.NamespaceUri != OdfKit.Core.OdfNamespaces.Text)
                continue;
            foreach (OdfKit.DOM.OdfNode paragraph in child.Children)
            {
                if (paragraph.LocalName == "p" && paragraph.NamespaceUri == OdfKit.Core.OdfNamespaces.Text)
                {
                    paragraph.TextContent = text;
                    return;
                }
            }
            child.TextContent = text;
            return;
        }
    }

    private static int ClearNotes(OdfKit.DOM.OdfNode root, string noteClass)
    {
        List<OdfKit.DOM.OdfNode> removals = [];
        int count = 0;
        foreach (OdfKit.DOM.OdfNode child in root.Children)
        {
            if (child.LocalName == "note" && child.NamespaceUri == OdfKit.Core.OdfNamespaces.Text &&
                child.GetAttribute("note-class", OdfKit.Core.OdfNamespaces.Text) == noteClass)
                removals.Add(child);
            else
                count += ClearNotes(child, noteClass);
        }
        foreach (OdfKit.DOM.OdfNode removal in removals)
            root.RemoveChild(removal);
        return count + removals.Count;
    }

    /// <summary>
    /// 在指定的段落中新增書籤。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    /// <param name="name">書籤名稱</param>
    internal void AddBookmark(OdfParagraph paragraph, string name) =>
        TextDocumentNotesEngine.AddBookmark(paragraph, name);

    /// <summary>
    /// 在指定的段落中新增參考標記。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    /// <param name="name">參考標記名稱</param>
    internal void AddReferenceMark(OdfParagraph paragraph, string name) =>
        TextDocumentNotesEngine.AddReferenceMark(paragraph, name);

    /// <summary>
    /// 在指定的段落中新增超連結。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    /// <param name="url">超連結網址</param>
    /// <param name="text">連結顯示文字</param>
    internal void AddHyperlink(OdfParagraph paragraph, string url, string text) =>
        TextDocumentNotesEngine.AddHyperlink(paragraph, url, text);

    /// <summary>
    /// 在指定的段落中新增圖片。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    /// <param name="packagePath">圖片在封裝包內的路徑</param>
    /// <param name="width">圖片寬度</param>
    /// <param name="height">圖片高度</param>
    /// <param name="name">圖片名稱</param>
    /// <returns>新建立的圖片物件</returns>
    internal OdfImage AddImage(OdfParagraph paragraph, string packagePath, OdfLength width, OdfLength height, string? name = null) =>
        TextDocumentNotesEngine.AddImage(paragraph, packagePath, width, height, name);

    /// <summary>
    /// 在指定的段落中新增旁註標記（注音資訊）。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    /// <param name="baseText">基礎文字</param>
    /// <param name="rubyText">注音（旁註）文字</param>
    /// <returns>新建立的旁註標記物件</returns>
    internal OdfRuby AddRuby(OdfParagraph paragraph, string baseText, string rubyText) =>
        TextDocumentNotesEngine.AddRuby(this, paragraph, baseText, rubyText);

    #endregion
}
