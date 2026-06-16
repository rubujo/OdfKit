using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    #region Saving and Atomic Save

    /// <summary>
    /// 將所有變更儲存回原來的檔案或資料流中。
    /// </summary>
    /// <param name="options">單次儲存設定選項；若為 <see langword="null"/>，則使用封裝預設選項</param>
    public void Save(OdfSaveOptions? options = null)
    {
        if (_mode == OdfPackageMode.Read)
            throw new InvalidOperationException("Cannot save a read-only ODF package.");

        _lock.Wait();
        OdfSaveOptions previousOptions = UseSaveOptions(options);
        try
        {
            OdfPackageSaver.SaveToUnderlyingStream(this, includeRdfMetadata: true);
        }
        finally
        {
            _saveOptions = previousOptions;
            _lock.Release();
        }
    }

    /// <summary>
    /// 將所有變更儲存回原來的檔案或資料流中（非同步）。
    /// </summary>
    /// <param name="cancellationToken">取消語彙</param>
    /// <returns>代表非同步作業的工作</returns>
    public Task SaveAsync(CancellationToken cancellationToken = default)
        => SaveAsync(null, cancellationToken);

    /// <summary>
    /// 使用指定儲存選項，將所有變更儲存回原來的檔案或資料流中（非同步）。
    /// </summary>
    /// <param name="options">單次儲存設定選項；若為 <see langword="null"/>，則使用封裝預設選項</param>
    /// <param name="cancellationToken">取消語彙</param>
    /// <returns>代表非同步作業的工作</returns>
    public async Task SaveAsync(OdfSaveOptions? options, CancellationToken cancellationToken = default)
    {
        if (_mode == OdfPackageMode.Read)
            throw new InvalidOperationException("Cannot save a read-only ODF package.");

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        OdfSaveOptions previousOptions = UseSaveOptions(options);
        try
        {
            await OdfPackageSaver.SaveToUnderlyingStreamAsync(this, includeRdfMetadata: true, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _saveOptions = previousOptions;
            _lock.Release();
        }
    }

    /// <summary>
    /// 將封裝序列化儲存至指定的目的地資料流。
    /// </summary>
    /// <param name="destinationStream">目標目的地資料流</param>
    /// <param name="options">單次儲存設定選項；若為 <see langword="null"/>，則使用封裝預設選項</param>
    public void SaveToStream(Stream destinationStream, OdfSaveOptions? options = null)
    {
        _lock.Wait();
        OdfSaveOptions previousOptions = UseSaveOptions(options);
        try
        {
            OdfPackageSaver.SaveToStream(this, destinationStream, includeRdfMetadata: false);
        }
        finally
        {
            _saveOptions = previousOptions;
            _lock.Release();
        }
    }

    /// <summary>
    /// 將封裝序列化儲存至指定的目的地資料流（非同步）。
    /// </summary>
    /// <param name="destinationStream">目標目的地資料流</param>
    /// <param name="cancellationToken">取消語彙</param>
    /// <returns>代表非同步作業的工作</returns>
    public Task SaveToStreamAsync(Stream destinationStream, CancellationToken cancellationToken = default)
        => SaveToStreamAsync(destinationStream, null, cancellationToken);

    /// <summary>
    /// 使用指定儲存選項，將封裝序列化儲存至指定的目的地資料流（非同步）。
    /// </summary>
    /// <param name="destinationStream">目標目的地資料流</param>
    /// <param name="options">單次儲存設定選項；若為 <see langword="null"/>，則使用封裝預設選項</param>
    /// <param name="cancellationToken">取消語彙</param>
    /// <returns>代表非同步作業的工作</returns>
    public async Task SaveToStreamAsync(
        Stream destinationStream,
        OdfSaveOptions? options,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        OdfSaveOptions previousOptions = UseSaveOptions(options);
        try
        {
            await OdfPackageSaver.SaveToStreamAsync(this, destinationStream, includeRdfMetadata: false, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _saveOptions = previousOptions;
            _lock.Release();
        }
    }

    private OdfSaveOptions UseSaveOptions(OdfSaveOptions? options)
    {
        OdfSaveOptions previousOptions = _saveOptions;
        if (options is not null)
            _saveOptions = options;

        return previousOptions;
    }

    #endregion
}
