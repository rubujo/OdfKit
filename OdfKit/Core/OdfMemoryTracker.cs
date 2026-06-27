using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace OdfKit.Core;

/// <summary>
/// 未受控記憶體安全生命週期追蹤器 (Unmanaged Memory Leak Tracker)。
/// </summary>
public static class OdfMemoryTracker
{
    private static readonly ConcurrentDictionary<IntPtr, AllocationInfo> Allocations = new();
    private static long _trackedBytes;

    /// <summary>
    /// 取得或設定是否啟用記憶體與反模式診斷警示。
    /// </summary>
    public static bool DiagnosticsEnabled { get; set; } = true;

    /// <summary>
    /// 取得或設定單次分配大小警示門檻；預設對齊 .NET LOH 常見門檻。
    /// </summary>
    public static long LargeAllocationWarningThresholdBytes { get; set; } = 85_000;

    /// <summary>
    /// 取得或設定累計追蹤記憶體警示門檻。
    /// </summary>
    public static long TotalTrackedMemoryWarningThresholdBytes { get; set; } = 128L * 1024 * 1024;

    /// <summary>
    /// 取得或設定追蹤分配數量警示門檻。
    /// </summary>
    public static int TrackedAllocationCountWarningThreshold { get; set; } = 100_000;

    /// <summary>
    /// 取得或設定單次載入節點數警示門檻。
    /// </summary>
    public static long NodeLoadWarningThreshold { get; set; } = 250_000;

    /// <summary>
    /// 取得或設定高頻 boxing 估計次數警示門檻。
    /// </summary>
    public static long BoxingWarningThreshold { get; set; } = 10_000;

    /// <summary>
    /// 追蹤非受控記憶體或 POH 鎖定分配。
    /// </summary>
    /// <param name="ptr">記憶體區塊指標</param>
    /// <param name="size">分配的大小 (位元組)</param>
    /// <param name="label">選用的標籤，用於說明分配目的</param>
    public static void Track(IntPtr ptr, long size, string? label = null)
    {
        if (ptr == IntPtr.Zero)
            return;

        var stackTrace = new StackTrace(1, true).ToString();
        var info = new AllocationInfo(size, label ?? "Unspecified", stackTrace);

        if (Allocations.TryAdd(ptr, info))
        {
            long totalBytes = System.Threading.Interlocked.Add(ref _trackedBytes, size);
            OdfPerformanceTelemetry.RecordMemoryAllocation(size);
            ReportAllocationDiagnostics(size, totalBytes, Allocations.Count, info.Label);
        }
    }

    /// <summary>
    /// 取消追蹤並釋放非受控記憶體。
    /// </summary>
    /// <param name="ptr">記憶體區塊指標</param>
    public static void Untrack(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
            return;

        if (Allocations.TryRemove(ptr, out var info))
        {
            System.Threading.Interlocked.Add(ref _trackedBytes, -info.Size);
            OdfPerformanceTelemetry.RecordMemoryFree(info.Size);
        }
    }

    /// <summary>
    /// 回報單次載入或批次操作的記憶體與反模式特徵，超過門檻時輸出診斷警示。
    /// </summary>
    /// <param name="nodeCount">本次載入或操作涉及的 DOM 節點數</param>
    /// <param name="allocatedBytes">本次載入或操作估計配置的位元組數；未知時可為 <see langword="null"/></param>
    /// <param name="boxedValueCount">本次操作估計發生的 boxing 次數</param>
    /// <param name="label">選用的情境標籤</param>
    public static void ReportLoadProfile(
        long nodeCount,
        long? allocatedBytes = null,
        long boxedValueCount = 0,
        string? label = null)
    {
        if (!DiagnosticsEnabled)
            return;

        string context = string.IsNullOrWhiteSpace(label) ? "未命名載入情境" : label!;

        if (nodeCount >= NodeLoadWarningThreshold)
        {
            OdfKitDiagnostics.Warn(
                $"OdfKit 偵測到單次載入節點數過高：{nodeCount}，情境：{context}。建議改用串流 API、lazy loading 或 PruneAndCollect。");
        }

        if (allocatedBytes is >= 0 && allocatedBytes.Value >= LargeAllocationWarningThresholdBytes)
        {
            OdfKitDiagnostics.Warn(
                $"OdfKit 偵測到可能造成 LOH/POH 壓力的大型配置：{allocatedBytes.Value} 位元組，情境：{context}。建議分段處理或使用非受控緩衝區。");
        }

        if (boxedValueCount >= BoxingWarningThreshold)
        {
            OdfKitDiagnostics.Warn(
                $"OdfKit 偵測到高頻 boxing 風險：{boxedValueCount} 次，情境：{context}。建議改用強型別值、Span 或 OdfCellData。");
        }
    }

    /// <summary>
    /// 檢查是否有尚未釋放的非受控記憶體。
    /// </summary>
    /// <param name="reportLeaks">是否列印洩漏報告</param>
    /// <returns>是否有洩漏</returns>
    public static bool CheckLeaks(bool reportLeaks = true)
    {
        if (Allocations.IsEmpty)
            return false;

        if (reportLeaks)
        {
            var sb = new StringBuilder();
            sb.AppendLine("偵測到 OdfKit 未受控記憶體洩漏：");
            foreach (var kvp in Allocations)
            {
                sb.AppendLine($"指標: 0x{kvp.Key.ToInt64():X}, 大小: {kvp.Value.Size} 位元組, 標籤: {kvp.Value.Label}");
                sb.AppendLine("分配時的堆疊追蹤：");
                sb.AppendLine(kvp.Value.StackTrace);
                sb.AppendLine(new string('-', 40));
            }
            OdfKitDiagnostics.Warn(sb.ToString());
        }

        return true;
    }

    internal static void ResetDiagnosticsForTests()
    {
        DiagnosticsEnabled = true;
        LargeAllocationWarningThresholdBytes = 85_000;
        TotalTrackedMemoryWarningThresholdBytes = 128L * 1024 * 1024;
        TrackedAllocationCountWarningThreshold = 100_000;
        NodeLoadWarningThreshold = 250_000;
        BoxingWarningThreshold = 10_000;
    }

    private static void ReportAllocationDiagnostics(long size, long totalBytes, int allocationCount, string label)
    {
        if (!DiagnosticsEnabled)
            return;

        if (size >= LargeAllocationWarningThresholdBytes)
        {
            OdfKitDiagnostics.Warn(
                $"OdfKit 偵測到可能造成 LOH/POH 壓力的大型追蹤分配：{size} 位元組，標籤：{label}。");
        }

        if (totalBytes >= TotalTrackedMemoryWarningThresholdBytes)
        {
            OdfKitDiagnostics.Warn(
                $"OdfKit 追蹤中的非受控/固定記憶體總量已達 {totalBytes} 位元組，請確認大型表格頁或緩衝區可及時釋放。");
        }

        if (allocationCount >= TrackedAllocationCountWarningThreshold)
        {
            OdfKitDiagnostics.Warn(
                $"OdfKit 追蹤中的分配數量已達 {allocationCount}，可能代表高頻小分配反模式。");
        }
    }

    private sealed record AllocationInfo(long Size, string Label, string StackTrace);
}
