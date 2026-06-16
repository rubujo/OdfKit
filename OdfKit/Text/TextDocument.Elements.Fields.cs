using System.Collections.Generic;
using OdfKit.Styles;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Document Elements - Fields & Variables

    /// <summary>
    /// 新增一個段落至文件本文結尾。
    /// </summary>
    /// <param name="text">段落的文字內容</param>
    /// <returns>新建立的段落執行個體</returns>
    public OdfParagraph AddParagraph(string text = "") =>
        TextDocumentFieldsEngine.AddParagraph(this, MutationContext, text);

    /// <summary>
    /// 新增一個標題至文件本文結尾。
    /// </summary>
    /// <param name="text">標題的文字內容</param>
    /// <param name="outlineLevel">標題的大綱階層</param>
    /// <returns>新建立的標題執行個體</returns>
    public OdfHeading AddHeading(string text, int outlineLevel) =>
        TextDocumentFieldsEngine.AddHeading(this, MutationContext, text, outlineLevel);

    /// <summary>
    /// 新增一個項目清單至文件本文結尾。
    /// </summary>
    /// <param name="styleName">項目清單樣式名稱</param>
    /// <returns>新建立的清單項目</returns>
    public OdfList AddList(string? styleName = null) =>
        TextDocumentFieldsEngine.AddList(this, MutationContext, styleName);

    /// <summary>
    /// 以多層級樣式定義建立清單，樣式寫入 styles.xml 的 office:styles 區段。
    /// </summary>
    /// <param name="styleName">清單樣式名稱，必須唯一。</param>
    /// <param name="levels">各層級的樣式設定；Level 屬性需從 1 開始連續遞增。</param>
    /// <returns>新建立的清單（已套用樣式名稱）。</returns>
    public OdfList AddListWithStyle(string styleName, IReadOnlyList<OdfListLevelStyle> levels) =>
        TextDocumentFieldsEngine.AddListWithStyle(this, MutationContext, styleName, levels);

    /// <summary>
    /// 在指定的段落中新增日期欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    internal void AddDateField(OdfParagraph paragraph) =>
        TextDocumentFieldsEngine.AddDateField(paragraph);

    /// <summary>
    /// 在指定的段落中新增時間欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    internal void AddTimeField(OdfParagraph paragraph) =>
        TextDocumentFieldsEngine.AddTimeField(paragraph);

    /// <summary>
    /// 在指定的段落中新增作者名稱欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    internal void AddAuthorField(OdfParagraph paragraph) =>
        TextDocumentFieldsEngine.AddAuthorField(paragraph);

    /// <summary>
    /// 在指定的段落中新增章節欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    internal void AddChapterField(OdfParagraph paragraph) =>
        TextDocumentFieldsEngine.AddChapterField(paragraph);

    /// <summary>
    /// 在指定的段落中新增序號欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    /// <param name="name">序號欄位的名稱</param>
    /// <param name="numFormat">序號的編號格式</param>
    internal void AddSequenceField(OdfParagraph paragraph, string name, string numFormat = "1") =>
        TextDocumentFieldsEngine.AddSequenceField(paragraph, name, numFormat);

    /// <summary>
    /// 在指定的段落中新增參考項目欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    /// <param name="refName">要參考的項目名稱</param>
    internal void AddReferenceField(OdfParagraph paragraph, string refName) =>
        TextDocumentFieldsEngine.AddReferenceField(paragraph, refName);

    /// <summary>
    /// 在指定的段落中新增序號交互參照欄位 (<c>text:sequence-ref</c>)。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    /// <param name="sequenceName">序號欄位名稱（需與 AddSequenceField 使用的 name 相同）</param>
    /// <param name="referenceFormat">參照格式，預設為 "value"（顯示數值）</param>
    internal void AddSequenceRefField(OdfParagraph paragraph, string sequenceName, string referenceFormat = "value") =>
        TextDocumentFieldsEngine.AddSequenceRefField(paragraph, sequenceName, referenceFormat);

    /// <summary>
    /// 在指定的段落中新增書籤參照欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體。</param>
    /// <param name="bookmarkName">要參照的書籤名稱。</param>
    /// <param name="referenceFormat">參照格式，預設為 "text"。</param>
    internal void AddBookmarkReferenceField(OdfParagraph paragraph, string bookmarkName, string referenceFormat = "text") =>
        TextDocumentFieldsEngine.AddBookmarkReferenceField(paragraph, bookmarkName, referenceFormat);

    /// <summary>
    /// 在指定的段落中設定變數欄位值。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    /// <param name="name">變數的名稱</param>
    /// <param name="value">變數的值</param>
    internal void AddVariableSetField(OdfParagraph paragraph, string name, string value) =>
        TextDocumentFieldsEngine.AddVariableSetField(paragraph, name, value);

    /// <summary>
    /// 在指定的段落中取得變數欄位值。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    /// <param name="name">變數的名稱</param>
    internal void AddVariableGetField(OdfParagraph paragraph, string name) =>
        TextDocumentFieldsEngine.AddVariableGetField(paragraph, name);

    #endregion
}
