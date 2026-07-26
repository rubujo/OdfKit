using System;
using System.IO;
using OdfKit.DOM;

namespace OdfKit.Core;
/// <summary>
/// Adds disposal and asynchronous cleanup behavior for ODF packages.
/// 提供 ODF 封裝的處置與非同步清理行為。
/// </summary>

public sealed partial class OdfPackage
{
    #region Dispose


    /// <summary>
    /// 釋放封裝持有的資源。
    /// </summary>
    /// <param name="disposing">若為 <see langword="true"/>，則釋放受控資源</param>
    private void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                ReleaseTransactionLock();
#if NET10_0_OR_GREATER
                _prefetchCts.Cancel();
                try
                {
                    _prefetchProcessorTask?.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException) when (_prefetchCts.IsCancellationRequested)
                {
                    OdfKitDiagnostics.Info("背景預讀處理器已在同步處置期間停止。");
                }
                _prefetchCts.Dispose();
#endif

                try
                {
                    PreloadTask?.GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    OdfKitDiagnostics.Warn("同步處置封裝時，背景預載工作未能完成。", ex);
                }
                _lock.Dispose();
                _archive?.Dispose();
                if (!_leaveOpen)
                {
                    _underlyingStream?.Dispose();
                }

                // 釋放已載入的專案資料流
                foreach (var entry in _entries.Values)
                {
                    entry.Dispose();
                }
                Mmf?.Dispose();
                Mmf = null;
            }
            _isDisposed = true;
        }
    }

    /// <summary>
    /// Releases unmanaged resources.
    /// 釋放 <see cref="OdfPackage"/> 類別所使用的資源。
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources for async.
    /// 非同步釋放 <see cref="OdfPackage"/> 類別所使用的資源。
    /// </summary>
    /// <returns>代表非同步處置作業的 ValueTask</returns>
    public async ValueTask DisposeAsync()
    {
        if (!_isDisposed)
        {
            ReleaseTransactionLock();
#if NET10_0_OR_GREATER
            _prefetchCts.Cancel();
            if (_prefetchProcessorTask != null)
            {
                try
                {
                    await _prefetchProcessorTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_prefetchCts.IsCancellationRequested)
                {
                    OdfKitDiagnostics.Info("背景預讀處理器已在非同步處置期間停止。");
                }
            }
            _prefetchCts.Dispose();
#endif

            if (PreloadTask != null)
            {
                try
                {
                    await PreloadTask.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    OdfKitDiagnostics.Warn("非同步處置封裝時，背景預載工作未能完成。", ex);
                }
            }
            _lock.Dispose();
            _archive?.Dispose();

            if (!_leaveOpen && _underlyingStream != null)
            {
                if (_underlyingStream is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    _underlyingStream.Dispose();
                }
            }

            foreach (var entry in _entries.Values)
            {
                entry.Dispose();
            }
            Mmf?.Dispose();
            Mmf = null;

            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }

    private static int ReadAll(Stream stream, byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = stream.Read(buffer, offset + totalRead, count - totalRead);
            if (read <= 0)
                break;
            totalRead += read;
        }
        return totalRead;
    }


    #endregion
}
