using System;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Core;

/// <summary>
/// Isolates compatibility-only synchronous API wrappers from a caller synchronization context.
/// 隔離為相容性保留的同步 API 包裝，避免捕捉呼叫端同步內容而形成死結。
/// </summary>
internal static class OdfSynchronousTask
{
    internal static void Run(Func<Task> operation)
    {
        Run(
            async () =>
            {
                await operation().ConfigureAwait(false);
                return true;
            });
    }

    internal static TResult Run<TResult>(Func<Task<TResult>> operation)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(operation, nameof(operation));
        return Task.Factory.StartNew(
                operation,
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
                TaskScheduler.Default)
            .Unwrap()
            .GetAwaiter()
            .GetResult();
    }
}
