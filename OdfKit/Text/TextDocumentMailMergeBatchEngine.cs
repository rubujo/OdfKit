using System;
using System.Collections.Generic;
using System.IO;
using OdfKit.Core;

namespace OdfKit.Text;

/// <summary>
/// 文字文件批次郵件合併引擎（內部協作者）。
/// </summary>
internal static class TextDocumentMailMergeBatchEngine
{
    /// <summary>
    /// 以強型別記錄集合執行批次郵件合併。
    /// </summary>
    internal static IReadOnlyList<TextDocument> MailMerge<T>(TextDocument template, IEnumerable<T> records)
        where T : notnull
    {
        var result = new List<TextDocument>();
        foreach (T record in records)
        {
            TextDocument clone = Clone(template);
            new OdfMailMergeEngine(clone).Execute(clone.BodyTextRoot, record);
            result.Add(clone);
        }

        return result;
    }

    /// <summary>
    /// 以字典記錄集合執行批次郵件合併。
    /// </summary>
    internal static IReadOnlyList<TextDocument> MailMerge(
        TextDocument template,
        IEnumerable<IReadOnlyDictionary<string, object?>> records)
    {
        var result = new List<TextDocument>();
        foreach (IReadOnlyDictionary<string, object?> record in records)
        {
            TextDocument clone = Clone(template);
            new OdfMailMergeEngine(clone).Execute(clone.BodyTextRoot, record);
            result.Add(clone);
        }

        return result;
    }

    private static TextDocument Clone(TextDocument document)
    {
        using var ms = new MemoryStream();
        document.SaveToStream(ms);
        ms.Position = 0;
        return (TextDocument)OdfDocumentFactory.LoadDocument(ms);
    }
}
