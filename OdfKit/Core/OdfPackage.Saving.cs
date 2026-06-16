using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula;
using OdfKit.Styles;

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
        {
            throw new InvalidOperationException("Cannot save a read-only ODF package.");
        }

        _lock.Wait();
        OdfSaveOptions previousOptions = UseSaveOptions(options);
        try
        {
            // 若已設定，於儲存時處理公式與字型嵌入
            ProcessSaveHooks();

            bool hasEncryption = _saveOptions.Password != null || _saveOptions.CryptographyProvider != null;
            if (hasEncryption)
            {
                OdfEncryption.Encrypt(this, _saveOptions.Password ?? string.Empty, _saveOptions.EncryptionAlgorithm);
            }
            try
            {
                // 序列化前先寫入或更新 manifest
                if (!_isFlatXml)
                {
                    SaveRdfMetadataToEntries();
                    SaveManifestToEntries();
                }

                if (_underlyingStream != null && _underlyingStream.CanWrite)
                {
                    long estimatedSize = 0;
                    foreach (var entry in _entries.Values)
                    {
                        estimatedSize += entry.GetEstimatedSize();
                    }

                    bool useTempFile = estimatedSize >= 50 * 1024 * 1024;
                    Stream tempStream;

                    if (useTempFile)
                    {
                        string tempDir = _saveOptions.TemporaryDirectory ?? Path.GetTempPath();
                        if (!Directory.Exists(tempDir))
                        {
                            Directory.CreateDirectory(tempDir);
                        }
                        string tempFilePath = Path.Combine(tempDir, "odfkit_" + Path.GetRandomFileName());
                        tempStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
                    }
                    else
                    {
                        tempStream = new MemoryStream();
                    }

                    try
                    {
                        WriteToArchive(tempStream);

                        _underlyingStream.SetLength(0);
                        tempStream.Position = 0;
                        tempStream.CopyTo(_underlyingStream);
                        _underlyingStream.Flush();
                    }
                    finally
                    {
                        tempStream.Dispose();
                    }
                }
            }
            finally
            {
                if (hasEncryption)
                {
                    OdfEncryption.Decrypt(this, _saveOptions.Password ?? string.Empty);
                }
            }
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
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await SaveAsync(null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 使用指定儲存選項，將所有變更儲存回原來的檔案或資料流中（非同步）。
    /// </summary>
    /// <param name="options">單次儲存設定選項；若為 <see langword="null"/>，則使用封裝預設選項</param>
    /// <param name="cancellationToken">取消語彙</param>
    /// <returns>代表非同步作業的工作</returns>
    public async Task SaveAsync(OdfSaveOptions? options, CancellationToken cancellationToken = default)
    {
        if (_mode == OdfPackageMode.Read)
        {
            throw new InvalidOperationException("Cannot save a read-only ODF package.");
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        OdfSaveOptions previousOptions = UseSaveOptions(options);
        try
        {
            // 若已設定，於儲存時處理公式與字型嵌入
            ProcessSaveHooks();

            bool hasEncryption = _saveOptions.Password != null || _saveOptions.CryptographyProvider != null;
            if (hasEncryption)
            {
                OdfEncryption.Encrypt(this, _saveOptions.Password ?? string.Empty, _saveOptions.EncryptionAlgorithm);
            }
            try
            {
                // 序列化前先寫入或更新 manifest
                if (!_isFlatXml)
                {
                    SaveRdfMetadataToEntries();
                    SaveManifestToEntries();
                }

                if (_underlyingStream != null && _underlyingStream.CanWrite)
                {
                    long estimatedSize = 0;
                    foreach (var entry in _entries.Values)
                    {
                        estimatedSize += entry.GetEstimatedSize();
                    }

                    bool useTempFile = estimatedSize >= 50 * 1024 * 1024;
                    Stream tempStream;

                    if (useTempFile)
                    {
                        string tempDir = _saveOptions.TemporaryDirectory ?? Path.GetTempPath();
                        if (!Directory.Exists(tempDir))
                        {
                            Directory.CreateDirectory(tempDir);
                        }
                        string tempFilePath = Path.Combine(tempDir, "odfkit_" + Path.GetRandomFileName());
                        tempStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
                    }
                    else
                    {
                        tempStream = new MemoryStream();
                    }

                    try
                    {
                        await Task.Run(() => WriteToArchive(tempStream), cancellationToken).ConfigureAwait(false);

                        _underlyingStream.SetLength(0);
                        tempStream.Position = 0;
                        await tempStream.CopyToAsync(_underlyingStream, 81920, cancellationToken).ConfigureAwait(false);
                        await _underlyingStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        if (tempStream is IAsyncDisposable asyncTempStream)
                        {
                            await asyncTempStream.DisposeAsync().ConfigureAwait(false);
                        }
                        else
                        {
                            tempStream.Dispose();
                        }
                    }
                }
            }
            finally
            {
                if (hasEncryption)
                {
                    OdfEncryption.Decrypt(this, _saveOptions.Password ?? string.Empty);
                }
            }
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
        if (destinationStream == null)
            throw new ArgumentNullException(nameof(destinationStream));

        _lock.Wait();
        OdfSaveOptions previousOptions = UseSaveOptions(options);
        try
        {
            ProcessSaveHooks();

            bool hasEncryption = _saveOptions.Password != null || _saveOptions.CryptographyProvider != null;
            if (hasEncryption)
            {
                OdfEncryption.Encrypt(this, _saveOptions.Password ?? string.Empty, _saveOptions.EncryptionAlgorithm);
            }
            try
            {
                if (!_isFlatXml)
                {
                    SaveManifestToEntries();
                }
                WriteToArchive(destinationStream);
            }
            finally
            {
                if (hasEncryption)
                {
                    OdfEncryption.Decrypt(this, _saveOptions.Password ?? string.Empty);
                }
            }
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
    public async Task SaveToStreamAsync(Stream destinationStream, CancellationToken cancellationToken = default)
    {
        await SaveToStreamAsync(destinationStream, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 使用指定儲存選項，將封裝序列化儲存至指定的目的地資料流（非同步）。
    /// </summary>
    /// <param name="destinationStream">目標目的地資料流</param>
    /// <param name="options">單次儲存設定選項；若為 <see langword="null"/>，則使用封裝預設選項</param>
    /// <param name="cancellationToken">取消語彙</param>
    /// <returns>代表非同步作業的工作</returns>
    public async Task SaveToStreamAsync(Stream destinationStream, OdfSaveOptions? options, CancellationToken cancellationToken = default)
    {
        if (destinationStream == null)
            throw new ArgumentNullException(nameof(destinationStream));

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        OdfSaveOptions previousOptions = UseSaveOptions(options);
        try
        {
            ProcessSaveHooks();

            bool hasEncryption = _saveOptions.Password != null || _saveOptions.CryptographyProvider != null;
            if (hasEncryption)
            {
                OdfEncryption.Encrypt(this, _saveOptions.Password ?? string.Empty, _saveOptions.EncryptionAlgorithm);
            }
            try
            {
                if (!_isFlatXml)
                {
                    SaveManifestToEntries();
                }
                await Task.Run(() => WriteToArchive(destinationStream), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (hasEncryption)
                {
                    OdfEncryption.Decrypt(this, _saveOptions.Password ?? string.Empty);
                }
            }
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
        {
            _saveOptions = options;
        }

        return previousOptions;
    }

    #endregion
}
