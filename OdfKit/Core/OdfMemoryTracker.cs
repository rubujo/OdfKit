using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace OdfKit.Core;

/// <summary>
/// Provides the OdfMemoryTracker API.
/// 未受控記憶體安全生命週期追蹤器 (Unmanaged Memory Leak Tracker)。
/// </summary>
public static class OdfMemoryTracker
{
    private static readonly ConcurrentDictionary<IntPtr, AllocationInfo> Allocations = new();
    private static long _trackedBytes;

    /// <summary>
    /// Gets a value indicating the DiagnosticsEnabled state.
    /// 取得或設定是否啟用記憶體與反模式診斷警示。
    /// </summary>
    public static bool DiagnosticsEnabled { get; set; } = true;

    /// <summary>
    /// Gets the LargeAllocationWarningThresholdBytes value.
    /// 取得或設定單次分配大小警示門檻；預設對齊 .NET LOH 常見門檻。
    /// </summary>
    public static long LargeAllocationWarningThresholdBytes { get; set; } = 85_000;

    /// <summary>
    /// Gets the TotalTrackedMemoryWarningThresholdBytes value.
    /// 取得或設定累計追蹤記憶體警示門檻。
    /// </summary>
    public static long TotalTrackedMemoryWarningThresholdBytes { get; set; } = 128L * 1024 * 1024;

    /// <summary>
    /// Gets the TrackedAllocationCountWarningThreshold value.
    /// 取得或設定追蹤分配數量警示門檻。
    /// </summary>
    public static int TrackedAllocationCountWarningThreshold { get; set; } = 100_000;

    /// <summary>
    /// Gets the NodeLoadWarningThreshold value.
    /// 取得或設定單次載入節點數警示門檻。
    /// </summary>
    public static long NodeLoadWarningThreshold { get; set; } = 250_000;

    /// <summary>
    /// Gets the BoxingWarningThreshold value.
    /// 取得或設定高頻 boxing 估計次數警示門檻。
    /// </summary>
    public static long BoxingWarningThreshold { get; set; } = 10_000;
    /// <summary>
    /// Short overload of Track that accepts ptr and size; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 ptr 與 size；其餘可選參數使用預設值並轉呼叫最長 Track 多載。
    /// </summary>
    public static void Track(IntPtr ptr, long size) => Track(ptr, size, null);


    /// <summary>
    /// Performs track.
    /// 追蹤非受控記憶體或 POH 鎖定分配。
    /// </summary>
    /// <param name="ptr">記憶體區塊指標</param>
    /// <param name="size">分配的大小 (位元組)</param>
    /// <param name="label">選用的標籤，用於說明分配目的</param>
    public static void Track(IntPtr ptr, long size, string? label)
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
    /// Performs untrack.
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
    /// Short overload of ReportLoadProfile that accepts nodeCount; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 nodeCount；其餘可選參數使用預設值並轉呼叫最長 ReportLoadProfile 多載。
    /// </summary>
    public static void ReportLoadProfile(long nodeCount) => ReportLoadProfile(nodeCount, null, 0, null);

    /// <summary>
    /// Short overload of ReportLoadProfile that accepts nodeCount and allocatedBytes; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 nodeCount 與 allocatedBytes；其餘可選參數使用預設值並轉呼叫最長 ReportLoadProfile 多載。
    /// </summary>
    public static void ReportLoadProfile(long nodeCount, long? allocatedBytes) => ReportLoadProfile(nodeCount, allocatedBytes, 0, null);

    /// <summary>
    /// Short overload of ReportLoadProfile that accepts nodeCount, allocatedBytes, and boxedValueCount; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 nodeCount、allocatedBytes 與 boxedValueCount；其餘可選參數使用預設值並轉呼叫最長 ReportLoadProfile 多載。
    /// </summary>
    public static void ReportLoadProfile(long nodeCount, long? allocatedBytes, long boxedValueCount) => ReportLoadProfile(nodeCount, allocatedBytes, boxedValueCount, null);


    /// <summary>
    /// Performs report load profile.
    /// 回報單次載入或批次操作的記憶體與反模式特徵，超過門檻時輸出診斷警示。
    /// </summary>
    /// <param name="nodeCount">本次載入或操作涉及的 DOM 節點數</param>
    /// <param name="allocatedBytes">本次載入或操作估計配置的位元組數；未知時可為 <see langword="null"/></param>
    /// <param name="boxedValueCount">本次操作估計發生的 boxing 次數</param>
    /// <param name="label">選用的情境標籤</param>
    public static void ReportLoadProfile(long nodeCount, long? allocatedBytes, long boxedValueCount, string? label)
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
    /// Short overload of CheckLeaks that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：CheckLeaks 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public static bool CheckLeaks() => CheckLeaks(true);


    /// <summary>
    /// Performs check leaks.
    /// 檢查是否有尚未釋放的非受控記憶體。
    /// </summary>
    /// <param name="reportLeaks">是否列印洩漏報告</param>
    /// <returns>是否有洩漏</returns>
    public static bool CheckLeaks(bool reportLeaks)
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
