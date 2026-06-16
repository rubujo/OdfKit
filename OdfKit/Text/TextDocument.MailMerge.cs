using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class TextDocument
{
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
}
