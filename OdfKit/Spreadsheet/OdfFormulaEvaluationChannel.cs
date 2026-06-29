using System;
using System.Collections.Concurrent;
using System.Threading;
#if NET10_0_OR_GREATER
using System.Threading.Channels;
#endif
using System.Threading.Tasks;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Decouples spreadsheet cell writes from formula recalculation through an asynchronous channel.
/// 以非同步通道解耦試算表儲存格寫入與公式重算。
/// </summary>
public sealed class OdfFormulaEvaluationChannel : IDisposable, IAsyncDisposable
{
    private readonly SpreadsheetDocument _document;
#if NET10_0_OR_GREATER
    private readonly Channel<bool> _channel;
#else
    private readonly ConcurrentQueue<bool> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
#endif
    private readonly CancellationTokenSource _cts;
    private readonly Task _worker;
    private int _submittedCount;
    private int _completedCount;
    private bool _disposed;

    internal OdfFormulaEvaluationChannel(SpreadsheetDocument document, int capacity, CancellationToken cancellationToken)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

#if NET10_0_OR_GREATER
        _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
#endif
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _worker = Task.Run(ProcessAsync);
    }

    /// <summary>
    /// Gets the number of recalculation requests submitted to the channel.
    /// 取得已送入通道的重算請求數。
    /// </summary>
    public int SubmittedCount => Volatile.Read(ref _submittedCount);

    /// <summary>
    /// Gets the number of recalculation requests that have completed processing.
    /// 取得已完成處理的重算請求數。
    /// </summary>
    public int CompletedCount => Volatile.Read(ref _completedCount);

    /// <summary>
    /// Attempts to submit a formula recalculation request without blocking.
    /// 嘗試以非阻塞方式送出公式重算請求。
    /// </summary>
    /// <returns><see langword="true"/> if the request was submitted to the channel. / 若請求已送入通道則為 <see langword="true"/>。</returns>
    public bool TryEnqueue()
    {
        ThrowIfDisposed();
#if NET10_0_OR_GREATER
        if (!_channel.Writer.TryWrite(true))
            return false;
#else
        _queue.Enqueue(true);
        _signal.Release();
#endif

        Interlocked.Increment(ref _submittedCount);
        return true;
    }

    /// <summary>
    /// Asynchronously submits a formula recalculation request.
    /// 以非同步方式送出公式重算請求。
    /// </summary>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>A <see cref="ValueTask"/> that represents the submit operation. / 代表送出作業的 <see cref="ValueTask"/>。</returns>
    public async ValueTask EnqueueAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
#if NET10_0_OR_GREATER
        await _channel.Writer.WriteAsync(true, cancellationToken).ConfigureAwait(false);
#else
        cancellationToken.ThrowIfCancellationRequested();
        _queue.Enqueue(true);
        _signal.Release();
        await Task.CompletedTask.ConfigureAwait(false);
#endif
        Interlocked.Increment(ref _submittedCount);
    }

    /// <summary>
    /// Waits until currently submitted recalculation requests are completed.
    /// 等待目前已送出的重算請求完成。
    /// </summary>
    /// <param name="timeout">The maximum wait time. / 最長等待時間。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>A task that represents the wait operation. / 代表等待作業的工作。</returns>
    public async Task WaitForIdleAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (CompletedCount < SubmittedCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException();
            }

            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();
#if NET10_0_OR_GREATER
        _channel.Writer.TryComplete();
#else
        _signal.Release();
#endif
        try
        {
            _worker.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        _cts.Dispose();
#if !NET10_0_OR_GREATER
        _signal.Dispose();
#endif
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();
#if NET10_0_OR_GREATER
        _channel.Writer.TryComplete();
#else
        _signal.Release();
#endif
        try
        {
            await _worker.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        _cts.Dispose();
#if !NET10_0_OR_GREATER
        _signal.Dispose();
#endif
    }

    private async Task ProcessAsync()
    {
#if NET10_0_OR_GREATER
        while (await _channel.Reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
        {
            while (_channel.Reader.TryRead(out _))
            {
                _document.EvaluateFormulas();
                Interlocked.Increment(ref _completedCount);
            }
        }
#else
        while (true)
        {
            await _signal.WaitAsync(_cts.Token).ConfigureAwait(false);
            while (_queue.TryDequeue(out _))
            {
                _document.EvaluateFormulas();
                Interlocked.Increment(ref _completedCount);
            }
        }
#endif
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(OdfFormulaEvaluationChannel));
        }
    }
}
