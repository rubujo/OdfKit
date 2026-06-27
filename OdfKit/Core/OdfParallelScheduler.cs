using System;

using System.Threading;

namespace OdfKit.Core;

/// <summary>
/// 提供 OdfKit 內部平行工作負載的核心預留調度設定。
/// </summary>
public static class OdfParallelScheduler
{
    private static readonly object SyncRoot = new();
    private static double _reservationRatio;
    private static ThreadPriority? _workerThreadPriority;

    /// <summary>
    /// 取得或設定自動平行化時預留給宿主系統的 CPU 核心比例。
    /// </summary>
    /// <remarks>
    /// 值必須大於或等於 0 且小於 1。當呼叫端明確指定平行度時，不會套用此全域預留比例。
    /// </remarks>
    public static double ReservationRatio
    {
        get
        {
            lock (SyncRoot)
            {
                return _reservationRatio;
            }
        }
        set
        {
            if (double.IsNaN(value) || value < 0d || value >= 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            lock (SyncRoot)
            {
                _reservationRatio = value;
            }
        }
    }

    /// <summary>
    /// 取得或設定內部平行工作負載執行期間要暫時套用的執行緒優先權。
    /// </summary>
    /// <remarks>
    /// 設為 <see langword="null"/> 時使用執行階段預設優先權。OdfKit 只會在單一工作委派執行期間暫時套用此值，
    /// 並於委派完成後還原原本優先權，避免污染呼叫端或 ThreadPool 後續工作。
    /// </remarks>
    public static ThreadPriority? WorkerThreadPriority
    {
        get
        {
            lock (SyncRoot)
            {
                return _workerThreadPriority;
            }
        }
        set
        {
            lock (SyncRoot)
            {
                _workerThreadPriority = value;
            }
        }
    }

    /// <summary>
    /// 依指定的平行度與目前核心預留比例，計算實際可使用的工作平行度。
    /// </summary>
    /// <param name="requestedMaxConcurrency">呼叫端要求的最大平行度；小於 1 時自動依核心數與預留比例計算</param>
    /// <returns>實際平行度，至少為 1</returns>
    public static int GetEffectiveConcurrency(int requestedMaxConcurrency = 0)
    {
        if (requestedMaxConcurrency > 0)
        {
            return requestedMaxConcurrency;
        }

        int processorCount = Math.Max(1, Environment.ProcessorCount);
        double reservationRatio = ReservationRatio;
        int reservedCores = (int)Math.Floor(processorCount * reservationRatio);
        return Math.Max(1, processorCount - reservedCores);
    }

    internal static void RunWithConfiguredThreadPriority(Action action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        ThreadPriority? configuredPriority = WorkerThreadPriority;
        if (configuredPriority is null)
        {
            action();
            return;
        }

        Thread currentThread = Thread.CurrentThread;
        ThreadPriority originalPriority = currentThread.Priority;
        bool changed = false;
        try
        {
            if (originalPriority != configuredPriority.Value)
            {
                currentThread.Priority = configuredPriority.Value;
                changed = true;
            }

            action();
        }
        finally
        {
            if (changed)
            {
                currentThread.Priority = originalPriority;
            }
        }
    }

    internal static T RunWithConfiguredThreadPriority<T>(Func<T> action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        T result = default!;
        RunWithConfiguredThreadPriority((Action)(() => result = action()));
        return result;
    }
}
