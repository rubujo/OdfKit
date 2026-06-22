using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

using OdfKit.Compliance;
namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    #region Embedded Documents

    /// <summary>
    /// 取得指定子路徑的嵌入式 ODF 文件。
    /// </summary>
    /// <typeparam name="T">嵌入式文件 wrapper 類型</typeparam>
    /// <param name="subPath">封裝中的子路徑</param>
    /// <returns>嵌入式文件 wrapper</returns>
    public T GetEmbeddedDocument<T>(string subPath) where T : OdfDocument
    {
        if (string.IsNullOrEmpty(subPath))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDocument_SubpathCannotBeEmpty_2"), nameof(subPath));
        if (!subPath.EndsWith("/"))
            subPath += "/";

        if (OdfEmbeddedDocumentFactory.TryCreate(Package, subPath, out T document))
        {
            return document;
        }

        return CreateEmbeddedViaReflection<T>(Package, subPath);
    }

    /// <summary>
    /// 建立指定子路徑的嵌入式 ODF 文件。
    /// </summary>
    /// <typeparam name="T">嵌入式文件 wrapper 類型</typeparam>
    /// <param name="subPath">封裝中的子路徑</param>
    /// <returns>建立完成的嵌入式文件 wrapper</returns>
    /// <remarks>
    /// 此方法會在建立時立即呼叫一次傳回文件的 <c>Save()</c> 以寫入最小骨架內容；
    /// 若呼叫端在取得傳回值後繼續修改其內容（例如設定公式或新增資料），
    /// 必須自行再次呼叫該嵌入式文件的 <c>Save()</c>，否則後續修改不會寫回封裝
    /// （外層文件的 <see cref="Save(OdfSaveOptions?)"/> 僅會持久化外層自身的
    /// <c>content.xml</c> 等專案，不會連動儲存內嵌子文件）。
    /// </remarks>
    public T CreateEmbeddedDocument<T>(string subPath) where T : OdfDocument
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
        return doc;
    }

#if !NETSTANDARD2_0
    [RequiresUnreferencedCode("未註冊的嵌入式文件類型仍以反射建立；請呼叫 OdfEmbeddedDocumentFactory 註冊。")]
    [RequiresDynamicCode("未註冊的嵌入式文件類型仍以反射建立；請呼叫 OdfEmbeddedDocumentFactory 註冊。")]
#endif
    private static T CreateEmbeddedViaReflection<T>(OdfPackage package, string subPath) where T : OdfDocument
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
