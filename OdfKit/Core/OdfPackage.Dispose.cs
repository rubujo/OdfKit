using System;
using System.IO;
using OdfKit.DOM;

namespace OdfKit.Core;

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
#if NET10_0_OR_GREATER
                _prefetchCts.Cancel();
                try
                {
                    _prefetchProcessorTask?.GetAwaiter().GetResult();
                }
                catch { }
                _prefetchCts.Dispose();
#endif

                try
                {
                    PreloadTask?.GetAwaiter().GetResult();
                }
                catch
                {
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
    /// 釋放 <see cref="OdfPackage"/> 類別所使用的資源。
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 非同步釋放 <see cref="OdfPackage"/> 類別所使用的資源。
    /// </summary>
    /// <returns>代表非同步處置作業的 ValueTask</returns>
    public async ValueTask DisposeAsync()
    {
        if (!_isDisposed)
        {
#if NET10_0_OR_GREATER
            _prefetchCts.Cancel();
            if (_prefetchProcessorTask != null)
            {
                try
                {
                    await _prefetchProcessorTask.ConfigureAwait(false);
                }
                catch { }
            }
            _prefetchCts.Dispose();
#endif

            if (PreloadTask != null)
            {
                try
                {
                    await PreloadTask.ConfigureAwait(false);
                }
                catch
                {
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
