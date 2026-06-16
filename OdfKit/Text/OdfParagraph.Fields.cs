using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

public partial class OdfParagraph
{
    #region Fields & References

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

    #endregion
}
