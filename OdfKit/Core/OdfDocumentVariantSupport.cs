using System;
using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// 文件變體（範本、主控、Flat XML）載入驗證的內部協助方法。
/// </summary>
internal static class OdfDocumentVariantSupport
{
    /// <summary>
    /// 驗證文件執行個體符合預期的變體種類與型別。
    /// </summary>
    /// <typeparam name="TDocument">預期的文件型別</typeparam>
    /// <param name="document">已載入的文件</param>
    /// <param name="expectedKind">預期的 ODF 文件種類</param>
    /// <param name="errorMessage">驗證失敗時的錯誤訊息</param>
    /// <returns>轉型後的文件執行個體</returns>
    internal static TDocument EnsureKind<TDocument>(
        OdfDocument document,
        OdfDocumentKind expectedKind,
        string errorMessage)
        where TDocument : OdfDocument
    {
        if (document is TDocument typed && document.DocumentKind == expectedKind)
        {
            return typed;
        }

        document.Dispose();
        throw new InvalidOperationException(errorMessage);
    }
}
