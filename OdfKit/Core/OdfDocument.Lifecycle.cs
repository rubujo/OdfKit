using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    #region Package Lifecycle & Persistence

    /// <summary>
    /// 儲存文件至 ODF 封裝容器中。
    /// </summary>
    /// <param name="options">儲存設定選項</param>
    public virtual void Save(OdfSaveOptions? options = null)
    {
        options ??= OdfSaveOptions.Default;
        OdfDocumentPersistenceEngine.PrepareDomEntriesForSave(PersistenceCollaborators, options);
        Package.Save(options);
    }

    /// <summary>
    /// 將文件保存到指定檔案路徑。
    /// </summary>
    /// <param name="path">要寫入的檔案路徑。</param>
    /// <param name="options">儲存設定選項。</param>
    public void Save(string path, OdfSaveOptions? options = null)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        options ??= OdfSaveOptions.Default;
        OdfDocumentPersistenceEngine.PrepareDomEntriesForSave(PersistenceCollaborators, options);

        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Package.SaveToStream(stream, options);
    }

    /// <summary>
    /// 非同步儲存文件至 ODF 封裝容器中。
    /// </summary>
    /// <param name="options">儲存設定選項</param>
    /// <param name="cancellationToken">取消語彙</param>
    /// <returns>代表非同步作業的 Task 執行個體</returns>
    public virtual async Task SaveAsync(OdfSaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= OdfSaveOptions.Default;
        OdfDocumentPersistenceEngine.PrepareDomEntriesForSave(PersistenceCollaborators, options);
        await Package.SaveAsync(options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 非同步將文件保存到指定檔案路徑。
    /// </summary>
    /// <param name="path">要寫入的檔案路徑。</param>
    /// <param name="options">儲存設定選項；若為 <see langword="null"/>，則使用預設選項。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步儲存作業的工作。</returns>
    public async Task SaveAsync(string path, OdfSaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        options ??= OdfSaveOptions.Default;
        OdfDocumentPersistenceEngine.PrepareDomEntriesForSave(PersistenceCollaborators, options);

        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
        await Package.SaveToStreamAsync(stream, options, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Web Streaming APIs

    /// <summary>
    /// 將文件儲存為 ODF 封裝位元組陣列。
    /// </summary>
    /// <returns>包含文件封裝內容的位元組陣列</returns>
    public byte[] SaveToBytes()
    {
        using var ms = new MemoryStream();
        SaveToStream(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// 將文件儲存至指定的資料流。
    /// </summary>
    /// <param name="destinationStream">要寫入文件封裝內容的目標資料流</param>
    /// <param name="options">儲存設定選項；若為 <see langword="null"/>，則使用預設選項</param>
    public void SaveToStream(Stream destinationStream, OdfSaveOptions? options = null)
    {
        if (destinationStream == null)
            throw new ArgumentNullException(nameof(destinationStream));

        options ??= OdfSaveOptions.Default;
        OdfDocumentPersistenceEngine.PrepareDomEntriesForSave(PersistenceCollaborators, options);
        Package.SaveToStream(destinationStream, options);

        if (destinationStream.CanSeek)
        {
            destinationStream.Position = 0;
        }
    }

    /// <summary>
    /// 非同步將文件儲存至指定的資料流。
    /// </summary>
    /// <param name="destinationStream">要寫入文件封裝內容的目標資料流。</param>
    /// <param name="options">儲存設定選項；若為 <see langword="null"/>，則使用預設選項。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步儲存作業的工作。</returns>
    public Task SaveAsync(Stream destinationStream, OdfSaveOptions? options = null, CancellationToken cancellationToken = default) =>
        SaveToStreamAsync(destinationStream, options, cancellationToken);

    /// <summary>
    /// 非同步將文件儲存至指定的資料流。
    /// </summary>
    /// <param name="destinationStream">要寫入文件封裝內容的目標資料流。</param>
    /// <param name="options">儲存設定選項；若為 <see langword="null"/>，則使用預設選項。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步儲存作業的工作。</returns>
    public async Task SaveToStreamAsync(Stream destinationStream, OdfSaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (destinationStream is null)
            throw new ArgumentNullException(nameof(destinationStream));

        options ??= OdfSaveOptions.Default;
        OdfDocumentPersistenceEngine.PrepareDomEntriesForSave(PersistenceCollaborators, options);
        await Package.SaveToStreamAsync(destinationStream, options, cancellationToken).ConfigureAwait(false);

        if (destinationStream.CanSeek)
            destinationStream.Position = 0;
    }

    #endregion
}
