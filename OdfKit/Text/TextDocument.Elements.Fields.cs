using System.Collections.Generic;
using OdfKit.Styles;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Document Elements - Fields & Variables

    /// <summary>
    /// Adds add paragraph.
    /// 新增一個段落至文件本文結尾。
    /// </summary>
    /// <param name="text">The text or value. / 段落的文字內容</param>
    /// <returns>The result. / 新建立的段落執行個體</returns>
    public OdfParagraph AddParagraph(string text = "") =>
        TextDocumentFieldsEngine.AddParagraph(this, MutationContext, text);

    /// <summary>
    /// Creates begin paragraph prebinding.
    /// 建立預先綁定至文字本文根節點的批次段落寫入器。
    /// </summary>
    /// <param name="styleName">The name or identifier. / 選用的段落樣式名稱，會套用至此寫入器新增的所有段落</param>
    /// <returns>The result. / 預先綁定的段落寫入器</returns>
    public OdfParagraphPrebindingWriter BeginParagraphPrebinding(string? styleName = null) =>
        new(MutationContext, styleName);

    /// <summary>
    /// Adds add heading.
    /// 新增一個標題至文件本文結尾。
    /// </summary>
    /// <param name="text">The text or value. / 標題的文字內容</param>
    /// <param name="outlineLevel">The numeric value. / 標題的大綱階層</param>
    /// <returns>The result. / 新建立的標題執行個體</returns>
    public OdfHeading AddHeading(string text, int outlineLevel) =>
        TextDocumentFieldsEngine.AddHeading(this, MutationContext, text, outlineLevel);

    /// <summary>
    /// Adds add list.
    /// 新增一個專案清單至文件本文結尾。
    /// </summary>
    /// <param name="styleName">The name or identifier. / 專案清單樣式名稱</param>
    /// <returns>The result. / 新建立的清單專案</returns>
    public OdfList AddList(string? styleName = null) =>
        TextDocumentFieldsEngine.AddList(this, MutationContext, styleName);

    /// <summary>
    /// Provides add list with style.
    /// 以多層級樣式定義建立清單，樣式寫入 styles.xml 的 office:styles 區段。
    /// </summary>
    /// <param name="styleName">The name or identifier. / 清單樣式名稱，必須唯一</param>
    /// <param name="levels">The numeric value. / 各層級的樣式設定；Level 屬性需從 1 開始連續遞增</param>
    /// <returns>The result. / 新建立的清單（已套用樣式名稱）</returns>
    public OdfList AddListWithStyle(string styleName, IReadOnlyList<OdfListLevelStyle> levels) =>
        TextDocumentFieldsEngine.AddListWithStyle(this, MutationContext, styleName, levels);

    /// <summary>
    /// Provides extract fields.
    /// 從文件中反向提取範本欄位值，支援跨 <c>text:span</c> 斷裂的文字標記、書籤範圍與 ODF 變數欄位。
    /// </summary>
    /// <param name="startDelimiter">The numeric value. / 欄位起始分隔符號，預設為 <c>[</c></param>
    /// <param name="endDelimiter">The numeric value. / 欄位結束分隔符號，預設為 <c>]</c></param>
    /// <returns>The result. / 依欄位名稱索引的欄位值字典</returns>
    /// <remarks>
    /// 文字標記格式為 <c>[Name]value[/Name]</c>。若同名欄位重複出現，會保留文件中第一個值。
    /// </remarks>
    public IReadOnlyDictionary<string, string> ExtractFields(string startDelimiter = "[", string endDelimiter = "]") =>
        TextDocumentFieldExtractionEngine.ExtractFieldValues(this, startDelimiter, endDelimiter);

    /// <summary>
    /// Provides extract field infos.
    /// 從文件中反向提取範本欄位詳細資料，包含欄位來源。
    /// </summary>
    /// <param name="startDelimiter">The numeric value. / 欄位起始分隔符號，預設為 <c>[</c></param>
    /// <param name="endDelimiter">The numeric value. / 欄位結束分隔符號，預設為 <c>]</c></param>
    /// <returns>The result. / 依欄位名稱索引的欄位詳細資料字典</returns>
    public IReadOnlyDictionary<string, OdfExtractedFieldInfo> ExtractFieldInfos(string startDelimiter = "[", string endDelimiter = "]") =>
        TextDocumentFieldExtractionEngine.ExtractFields(this, startDelimiter, endDelimiter);

    /// <summary>
    /// 在指定的段落中新增日期欄位。
    /// </summary>
    /// <param name="paragraph">The value to use. / 要新增欄位的段落執行個體</param>
    internal void AddDateField(OdfParagraph paragraph) =>
        TextDocumentFieldsEngine.AddDateField(paragraph);

    /// <summary>
    /// 在指定的段落中新增時間欄位。
    /// </summary>
    /// <param name="paragraph">The value to use. / 要新增欄位的段落執行個體</param>
    internal void AddTimeField(OdfParagraph paragraph) =>
        TextDocumentFieldsEngine.AddTimeField(paragraph);

    /// <summary>
    /// 在指定的段落中新增作者名稱欄位。
    /// </summary>
    /// <param name="paragraph">The value to use. / 要新增欄位的段落執行個體</param>
    internal void AddAuthorField(OdfParagraph paragraph) =>
        TextDocumentFieldsEngine.AddAuthorField(paragraph);

    /// <summary>
    /// 在指定的段落中新增章節欄位。
    /// </summary>
    /// <param name="paragraph">The value to use. / 要新增欄位的段落執行個體</param>
    internal void AddChapterField(OdfParagraph paragraph) =>
        TextDocumentFieldsEngine.AddChapterField(paragraph);

    /// <summary>
    /// 在指定的段落中新增序號欄位。
    /// </summary>
    /// <param name="paragraph">The value to use. / 要新增欄位的段落執行個體</param>
    /// <param name="name">The name or identifier. / 序號欄位的名稱</param>
    /// <param name="numFormat">The value to use. / 序號的編號格式</param>
    internal void AddSequenceField(OdfParagraph paragraph, string name, string numFormat = "1") =>
        TextDocumentFieldsEngine.AddSequenceField(paragraph, name, numFormat);

    /// <summary>
    /// 在指定的段落中新增參考專案欄位。
    /// </summary>
    /// <param name="paragraph">The value to use. / 要新增欄位的段落執行個體</param>
    /// <param name="refName">The name or identifier. / 要參考的專案名稱</param>
    internal void AddReferenceField(OdfParagraph paragraph, string refName) =>
        TextDocumentFieldsEngine.AddReferenceField(paragraph, refName);

    /// <summary>
    /// 在指定的段落中新增序號交互參照欄位 (<c>text:sequence-ref</c>)。
    /// </summary>
    /// <param name="paragraph">The value to use. / 目標段落</param>
    /// <param name="sequenceName">The name or identifier. / 序號欄位名稱（需與 AddSequenceField 使用的 name 相同）</param>
    /// <param name="referenceFormat">The value to use. / 參照格式，預設為 "value"（顯示數值）</param>
    internal void AddSequenceRefField(OdfParagraph paragraph, string sequenceName, string referenceFormat = "value") =>
        TextDocumentFieldsEngine.AddSequenceRefField(paragraph, sequenceName, referenceFormat);

    /// <summary>
    /// 在指定的段落中新增書籤參照欄位。
    /// </summary>
    /// <param name="paragraph">The value to use. / 要新增欄位的段落執行個體</param>
    /// <param name="bookmarkName">The name or identifier. / 要參照的書籤名稱</param>
    /// <param name="referenceFormat">The value to use. / 參照格式，預設為 "text"</param>
    internal void AddBookmarkReferenceField(OdfParagraph paragraph, string bookmarkName, string referenceFormat = "text") =>
        TextDocumentFieldsEngine.AddBookmarkReferenceField(paragraph, bookmarkName, referenceFormat);

    /// <summary>
    /// 在指定的段落中設定變數欄位值。
    /// </summary>
    /// <param name="paragraph">The value to use. / 要新增欄位的段落執行個體</param>
    /// <param name="name">The name or identifier. / 變數的名稱</param>
    /// <param name="value">The text or value. / 變數的值</param>
    internal void AddVariableSetField(OdfParagraph paragraph, string name, string value) =>
        TextDocumentFieldsEngine.AddVariableSetField(paragraph, name, value);

    /// <summary>
    /// 在指定的段落中取得變數欄位值。
    /// </summary>
    /// <param name="paragraph">The value to use. / 要新增欄位的段落執行個體</param>
    /// <param name="name">The name or identifier. / 變數的名稱</param>
    internal void AddVariableGetField(OdfParagraph paragraph, string name) =>
        TextDocumentFieldsEngine.AddVariableGetField(paragraph, name);

    /// <summary>
    /// 在指定的段落中新增資料庫欄位顯示欄位 (<c>text:database-display</c>)。
    /// </summary>
    /// <param name="paragraph">The value to use. / 要新增欄位的段落執行個體</param>
    /// <param name="tableName">The name or identifier. / 資料表、查詢或指令名稱</param>
    /// <param name="columnName">The name or identifier. / 要顯示的欄位名稱</param>
    /// <param name="tableType">The value to use. / 資料來源類型，可為 "table"、"query" 或 "command"</param>
    /// <param name="databaseName">The name or identifier. / 資料庫連線名稱</param>
    internal void AddDatabaseDisplayField(OdfParagraph paragraph, string tableName, string columnName, string? tableType = null, string? databaseName = null) =>
        TextDocumentFieldsEngine.AddDatabaseDisplayField(paragraph, tableName, columnName, tableType, databaseName);

    /// <summary>
    /// 在指定的段落中新增資料庫下一筆記錄欄位 (<c>text:database-next</c>)，用於合併列印或報表的逐筆換行。
    /// </summary>
    /// <param name="paragraph">The value to use. / 要新增欄位的段落執行個體</param>
    /// <param name="tableName">The name or identifier. / 資料表、查詢或指令名稱</param>
    /// <param name="tableType">The value to use. / 資料來源類型，可為 "table"、"query" 或 "command"</param>
    /// <param name="databaseName">The name or identifier. / 資料庫連線名稱</param>
    /// <param name="condition">The value to use. / 換行前的判斷條件式</param>
    internal void AddDatabaseNextField(OdfParagraph paragraph, string tableName, string? tableType = null, string? databaseName = null, string? condition = null) =>
        TextDocumentFieldsEngine.AddDatabaseNextField(paragraph, tableName, tableType, databaseName, condition);

    #endregion
}
