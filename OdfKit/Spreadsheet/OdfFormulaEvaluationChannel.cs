using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Core;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Decouples spreadsheet cell writes from formula recalculation through an asynchronous channel.
/// 以非同步通道解耦試算表儲存格寫入與公式重算。
/// </summary>
public sealed class OdfFormulaEvaluationChannel : IDisposable, IAsyncDisposable
{
    private readonly Action _evaluateFormulas;
    private readonly ConcurrentQueue<bool> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly SemaphoreSlim _availableSlots;
    private readonly CancellationTokenSource _cts;
    private readonly Task _worker;
    private int _submittedCount;
    private int _completedCount;
    private bool _disposed;

    internal OdfFormulaEvaluationChannel(SpreadsheetDocument document, int capacity, CancellationToken cancellationToken)
        : this(
            document,
            capacity,
            () => (document ?? throw new ArgumentNullException(nameof(document))).EvaluateFormulas(),
            cancellationToken)
    {
    }

    internal OdfFormulaEvaluationChannel(
        SpreadsheetDocument document,
        int capacity,
        Action evaluateFormulas,
        CancellationToken cancellationToken)
    {
        _ = document ?? throw new ArgumentNullException(nameof(document));
        _evaluateFormulas = evaluateFormulas ?? throw new ArgumentNullException(nameof(evaluateFormulas));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNegativeOrZero(capacity, nameof(capacity));

        _availableSlots = new SemaphoreSlim(capacity, capacity);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _worker = Task.Run(ProcessAsync, CancellationToken.None);
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
        if (!_availableSlots.Wait(0))
            return false;

        if (_cts.IsCancellationRequested)
        {
            _availableSlots.Release();
            return false;
        }

        Interlocked.Increment(ref _submittedCount);
        _queue.Enqueue(true);
        _signal.Release();
        return true;
    }
    /// <summary>
    /// Short overload of EnqueueAsync that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：EnqueueAsync 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public ValueTask EnqueueAsync() => EnqueueAsync(default);


    /// <summary>
    /// Asynchronously submits a formula recalculation request.
    /// 以非同步方式送出公式重算請求。
    /// </summary>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>A <see cref="ValueTask"/> that represents the submit operation. / 代表送出作業的 <see cref="ValueTask"/>。</returns>
    public async ValueTask EnqueueAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        await _availableSlots.WaitAsync(linkedCts.Token).ConfigureAwait(false);
        try
        {
            _cts.Token.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            Interlocked.Increment(ref _submittedCount);
            _queue.Enqueue(true);
            _signal.Release();
        }
        catch
        {
            _availableSlots.Release();
            throw;
        }
    }

    /// <summary>
    /// Short overload of WaitForIdleAsync that accepts timeout; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 timeout；其餘可選參數使用預設值並轉呼叫最長 WaitForIdleAsync 多載。
    /// </summary>
    public Task WaitForIdleAsync(TimeSpan timeout) => WaitForIdleAsync(timeout, default);


    /// <summary>
    /// Waits until currently submitted recalculation requests are completed.
    /// 等待目前已送出的重算請求完成。
    /// </summary>
    /// <param name="timeout">The maximum wait time. / 最長等待時間。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>A task that represents the wait operation. / 代表等待作業的工作。</returns>
    public async Task WaitForIdleAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        int targetCount = SubmittedCount;
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (CompletedCount < targetCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_worker.IsCompleted)
            {
                await _worker.ConfigureAwait(false);
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    OdfKit.Compliance.OdfLocalizer.GetMessage("Err_OdfFormulaEvaluationChannel_WaitForIdleTimedOut"));
            }

            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }
    }


    /// <summary>
    /// Releases unmanaged resources.
    /// 釋放非受控資源。
    /// </summary>
    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();
        _signal.Release();
        try
        {
            _worker.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            OdfKitDiagnostics.Info("公式評估背景工作已在同步處置期間停止。");
        }
        finally
        {
            _cts.Dispose();
            _signal.Dispose();
            _availableSlots.Dispose();
        }
    }

    /// <summary>
    /// Releases resources for async.
    /// 釋放 Async 資源。
    /// </summary>
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();
        _signal.Release();
        try
        {
            await _worker.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            OdfKitDiagnostics.Info("公式評估背景工作已在非同步處置期間停止。");
        }
        finally
        {
            _cts.Dispose();
            _signal.Dispose();
            _availableSlots.Dispose();
        }
    }

    private async Task ProcessAsync()
    {
        try
        {
            while (true)
            {
                await _signal.WaitAsync(_cts.Token).ConfigureAwait(false);
                while (_queue.TryDequeue(out _))
                {
                    try
                    {
                        _evaluateFormulas();
                    }
                    finally
                    {
                        _availableSlots.Release();
                    }

                    Interlocked.Increment(ref _completedCount);
                }
            }
        }
        catch
        {
            _cts.Cancel();
            throw;
        }
    }

    private void ThrowIfDisposed()
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfDisposed(_disposed, nameof(OdfFormulaEvaluationChannel));
    }
}
