using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

    /// <summary>
    /// 從檔案路徑載入並驗證文件型別與種類。
    /// </summary>
    /// <typeparam name="TDocument">預期的文件型別</typeparam>
    /// <param name="path">文件路徑</param>
    /// <param name="expectedKind">預期的 ODF 文件種類</param>
    /// <param name="errorMessageKey">驗證失敗時的在地化訊息鍵值</param>
    /// <returns>轉型後的文件執行個體</returns>
    internal static TDocument Load<TDocument>(
        string path,
        OdfDocumentKind expectedKind,
        string errorMessageKey)
        where TDocument : OdfDocument =>
        EnsureKind<TDocument>(
            OdfDocumentFactory.LoadDocument(path),
            expectedKind,
            OdfLocalizer.GetMessage(errorMessageKey));

    /// <summary>
    /// 非同步從檔案路徑載入並驗證文件型別與種類。
    /// </summary>
    /// <typeparam name="TDocument">預期的文件型別</typeparam>
    /// <param name="path">文件路徑</param>
    /// <param name="expectedKind">預期的 ODF 文件種類</param>
    /// <param name="errorMessageKey">驗證失敗時的在地化訊息鍵值</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為轉型後的文件執行個體</returns>
    internal static async Task<TDocument> LoadAsync<TDocument>(
        string path,
        OdfDocumentKind expectedKind,
        string errorMessageKey,
        CancellationToken cancellationToken = default)
        where TDocument : OdfDocument =>
        EnsureKind<TDocument>(
            await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false),
            expectedKind,
            OdfLocalizer.GetMessage(errorMessageKey));

    /// <summary>
    /// 從資料流載入並驗證文件型別與種類。
    /// </summary>
    /// <typeparam name="TDocument">預期的文件型別</typeparam>
    /// <param name="stream">來源資料流</param>
    /// <param name="expectedKind">預期的 ODF 文件種類</param>
    /// <param name="errorMessageKey">驗證失敗時的在地化訊息鍵值</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <returns>轉型後的文件執行個體</returns>
    internal static TDocument Load<TDocument>(
        Stream stream,
        OdfDocumentKind expectedKind,
        string errorMessageKey,
        string? fileName = null)
        where TDocument : OdfDocument =>
        EnsureKind<TDocument>(
            OdfDocumentFactory.LoadDocument(stream, fileName),
            expectedKind,
            OdfLocalizer.GetMessage(errorMessageKey));

    /// <summary>
    /// 非同步從資料流載入並驗證文件型別與種類。
    /// </summary>
    /// <typeparam name="TDocument">預期的文件型別</typeparam>
    /// <param name="stream">來源資料流</param>
    /// <param name="expectedKind">預期的 ODF 文件種類</param>
    /// <param name="errorMessageKey">驗證失敗時的在地化訊息鍵值</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為轉型後的文件執行個體</returns>
    internal static async Task<TDocument> LoadAsync<TDocument>(
        Stream stream,
        OdfDocumentKind expectedKind,
        string errorMessageKey,
        string? fileName = null,
        CancellationToken cancellationToken = default)
        where TDocument : OdfDocument =>
        EnsureKind<TDocument>(
            await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false),
            expectedKind,
            OdfLocalizer.GetMessage(errorMessageKey));
}
