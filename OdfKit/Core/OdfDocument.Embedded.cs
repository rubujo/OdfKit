using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

using OdfKit.Compliance;
namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    private readonly List<OdfDocument> _trackedEmbeddedDocuments = [];
    private bool _isFlushingTrackedEmbeddedDocuments;

    #region Embedded Documents

    /// <summary>
    /// 取得指定子路徑的嵌入式 ODF 文件。
    /// </summary>
    /// <typeparam name="T">嵌入式文件 wrapper 類型</typeparam>
    /// <param name="subPath">封裝中的子路徑</param>
    /// <returns>嵌入式文件 wrapper</returns>
    public T GetEmbeddedDocument<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(string subPath) where T : OdfDocument
    {
        if (string.IsNullOrEmpty(subPath))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDocument_SubpathCannotBeEmpty_2"), nameof(subPath));
        if (!subPath.EndsWith("/"))
            subPath += "/";

        if (OdfEmbeddedDocumentFactory.TryCreate(Package, subPath, out T document))
        {
            TrackEmbeddedDocument(document);
            return document;
        }

        T reflected = CreateEmbeddedViaReflection<T>(Package, subPath);
        TrackEmbeddedDocument(reflected);
        return reflected;
    }

    /// <summary>
    /// 建立指定子路徑的嵌入式 ODF 文件。
    /// </summary>
    /// <typeparam name="T">嵌入式文件 wrapper 類型</typeparam>
    /// <param name="subPath">封裝中的子路徑</param>
    /// <returns>建立完成的嵌入式文件 wrapper</returns>
    /// <remarks>
    /// 此方法會在建立時立即呼叫一次傳回文件的 <c>Save()</c> 以寫入最小骨架內容；
    /// 後續透過傳回 wrapper 所做的修改會由父文件 <see cref="Save(OdfSaveOptions?)"/>、
    /// <see cref="SaveToStream(System.IO.Stream, OdfSaveOptions?)"/> 與非同步儲存流程自動 flush 至封裝。
    /// 呼叫端仍可手動呼叫嵌入式文件的 <c>Save()</c> 以提早寫回。
    /// </remarks>
    public T CreateEmbeddedDocument<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(string subPath) where T : OdfDocument
    {
        if (string.IsNullOrEmpty(subPath))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDocument_SubpathCannotBeEmpty_2"), nameof(subPath));
        if (!subPath.EndsWith("/"))
            subPath += "/";

        string mimeType = OdfEmbeddedDocumentFactory.GetMimeType<T>();
        string mimePath = subPath + "mimetype";
        Package.WriteEntry(mimePath, Encoding.UTF8.GetBytes(mimeType), "");

        T doc;
        if (!OdfEmbeddedDocumentFactory.TryCreate(Package, subPath, out doc))
        {
            doc = CreateEmbeddedViaReflection<T>(Package, subPath);
        }

        doc.Save();
        TrackEmbeddedDocument(doc);
        return doc;
    }

    private void TrackEmbeddedDocument(OdfDocument document)
    {
        if (ReferenceEquals(document, this) || _trackedEmbeddedDocuments.Contains(document))
        {
            return;
        }

        _trackedEmbeddedDocuments.Add(document);
    }

    internal void TrackEmbeddedDocumentForPersistence(OdfDocument document) => TrackEmbeddedDocument(document);

    private void FlushTrackedEmbeddedDocumentsCore(OdfSaveOptions options)
    {
        if (_isFlushingTrackedEmbeddedDocuments || _trackedEmbeddedDocuments.Count == 0)
        {
            return;
        }

        _isFlushingTrackedEmbeddedDocuments = true;
        try
        {
            foreach (OdfDocument document in _trackedEmbeddedDocuments)
            {
                OdfDocumentPersistenceEngine.PrepareDomEntriesForSave(document.PersistenceCollaborators, options);
            }
        }
        finally
        {
            _isFlushingTrackedEmbeddedDocuments = false;
        }
    }

    private static T CreateEmbeddedViaReflection<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(OdfPackage package, string subPath) where T : OdfDocument
    {
        var ctor = typeof(T).GetConstructor(new[] { typeof(OdfPackage), typeof(string) });
        if (ctor is not null)
        {
            return (T)ctor.Invoke(new object[] { package, subPath });
        }

        ctor = typeof(T).GetConstructor(new[] { typeof(OdfPackage) });
        if (ctor is not null)
        {
            var doc = (T)ctor.Invoke(new object[] { package });
            doc.SubPath = subPath;
            return doc;
        }

        throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfDocument_TypeCompatibleConstructor", typeof(T).Name));
    }

    #endregion
}
