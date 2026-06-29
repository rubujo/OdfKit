using System.Collections.Generic;
using OdfKit.Styles;

namespace OdfKit.Text;

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
    /// Adds an alphabetical index to the end of the document body.
    /// 新增字母索引至文件本文結尾。
    /// </summary>
    /// <param name="title">The index title. / 索引標題。</param>
    /// <returns>The created alphabetical index object. / 建立的字母索引物件。</returns>
    public OdfAlphabeticalIndex AddAlphabeticalIndex(string title = "Alphabetical Index") =>
        TextDocumentNotesEngine.AddAlphabeticalIndex(this, MutationContext, title);

    /// <summary>
    /// Adds a bibliography to the end of the document body.
    /// 新增文獻目錄至文件本文結尾。
    /// </summary>
    /// <param name="title">The bibliography title. / 文獻目錄標題。</param>
    /// <returns>The created bibliography object. / 建立的文獻目錄物件。</returns>
    public OdfBibliography AddBibliography(string title = "Bibliography") =>
        TextDocumentNotesEngine.AddBibliography(this, MutationContext, title);

    /// <summary>
    /// Gets the list of all indexes in the document.
    /// 取得文件中所有索引的列表。
    /// </summary>
    /// <returns>The list of index objects. / 包含索引物件的列表。</returns>
    public List<OdfIndex> GetIndexes() =>
        TextDocumentNotesEngine.GetIndexes(this, BodyTextRoot);

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
