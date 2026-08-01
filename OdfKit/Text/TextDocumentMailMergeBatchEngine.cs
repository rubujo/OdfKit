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
        try
        {
            foreach (T record in records)
            {
                TextDocument clone = Clone(template);
                try
                {
                    new OdfMailMergeEngine(clone).Execute(clone.BodyTextRoot, record);
                    result.Add(clone);
                }
                catch
                {
                    DisposeWithoutThrowing(clone);
                    throw;
                }
            }

            return result;
        }
        catch
        {
            DisposeWithoutThrowing(result);
            throw;
        }
    }

    /// <summary>
    /// 以字典記錄集合執行批次郵件合併。
    /// </summary>
    internal static IReadOnlyList<TextDocument> MailMerge(
        TextDocument template,
        IEnumerable<IReadOnlyDictionary<string, object?>> records)
    {
        var result = new List<TextDocument>();
        try
        {
            foreach (IReadOnlyDictionary<string, object?> record in records)
            {
                TextDocument clone = Clone(template);
                try
                {
                    new OdfMailMergeEngine(clone).Execute(clone.BodyTextRoot, record);
                    result.Add(clone);
                }
                catch
                {
                    DisposeWithoutThrowing(clone);
                    throw;
                }
            }

            return result;
        }
        catch
        {
            DisposeWithoutThrowing(result);
            throw;
        }
    }

    private static TextDocument Clone(TextDocument document)
    {
        using var ms = new MemoryStream();
        document.SaveToStream(ms);
        ms.Position = 0;
        return (TextDocument)OdfDocumentFactory.LoadDocument(ms);
    }

    private static void DisposeWithoutThrowing(IEnumerable<TextDocument> documents)
    {
        foreach (TextDocument document in documents)
        {
            DisposeWithoutThrowing(document);
        }
    }

    private static void DisposeWithoutThrowing(TextDocument document)
    {
        try
        {
            document.Dispose();
        }
        catch
        {
            // Preserve the primary mail-merge failure while making a best effort to release every clone.
        }
    }
}
