using System;

namespace OdfKit.Core;

#if NET6_0_OR_GREATER
using System.Diagnostics;
using System.Diagnostics.Metrics;
#endif

/// <summary>
/// 提供 OdfKit 的效能遙測與遠端測量 (ActivitySource 與 Meter) 功能。
/// </summary>
public static class OdfPerformanceTelemetry
{
#if NET6_0_OR_GREATER
    /// <summary>
    /// OdfKit 的 ActivitySource，用於追蹤關鍵操作的生命週期。
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("OdfKit.Core", OdfVersionInfo.DefaultVersionString);

    /// <summary>
    /// OdfKit 的 Meter，用於收集效能度量指標。
    /// </summary>
    public static readonly Meter Meter = new("OdfKit.Core", OdfVersionInfo.DefaultVersionString);

    private static readonly Counter<long> ZipDecompressionCounter = Meter.CreateCounter<long>(
        "odfkit.zip.decompression_count",
        description: "Zip 封裝中解壓縮 Entry 的次數");

    private static readonly Histogram<double> ZipDecompressionTime = Meter.CreateHistogram<double>(
        "odfkit.zip.decompression_time_ms",
        unit: "ms",
        description: "Zip 封裝解壓 Entry 的時間花費 (毫秒)");

    private static readonly Counter<long> XmlParseCounter = Meter.CreateCounter<long>(
        "odfkit.xml.parse_count",
        description: "XML 熱路徑解析的節點或文件次數");

    private static readonly Histogram<double> XmlParseTime = Meter.CreateHistogram<double>(
        "odfkit.xml.parse_time_ms",
        unit: "ms",
        description: "XML 解析時間花費 (毫秒)");

    private static readonly Counter<long> UnmanagedMemoryAllocated = Meter.CreateCounter<long>(
        "odfkit.memory.unmanaged_allocated_bytes",
        unit: "bytes",
        description: "非受控記憶體 (NativeMemory 與 POH) 的累積分配位元組數");

    private static readonly Counter<long> UnmanagedMemoryFreed = Meter.CreateCounter<long>(
        "odfkit.memory.unmanaged_freed_bytes",
        unit: "bytes",
        description: "非受控記憶體 (NativeMemory 與 POH) 的累積釋放位元組數");
#endif

    /// <summary>
    /// 記錄一次 Zip 解壓操作效能。
    /// </summary>
    /// <param name="milliseconds">解壓縮花費毫秒數</param>
    public static void RecordZipDecompression(double milliseconds)
    {
#if NET6_0_OR_GREATER
        ZipDecompressionCounter.Add(1);
        ZipDecompressionTime.Record(milliseconds);
#endif
    }

    /// <summary>
    /// 記錄一次 XML 解析操作效能。
    /// </summary>
    /// <param name="milliseconds">解析花費毫秒數</param>
    public static void RecordXmlParse(double milliseconds)
    {
#if NET6_0_OR_GREATER
        XmlParseCounter.Add(1);
        XmlParseTime.Record(milliseconds);
#endif
    }

    /// <summary>
    /// 記錄非受控記憶體分配。
    /// </summary>
    /// <param name="bytes">分配的位元組數</param>
    public static void RecordMemoryAllocation(long bytes)
    {
#if NET6_0_OR_GREATER
        UnmanagedMemoryAllocated.Add(bytes);
#endif
    }

    /// <summary>
    /// 記錄非受控記憶體釋放。
    /// </summary>
    /// <param name="bytes">釋放的位元組數</param>
    public static void RecordMemoryFree(long bytes)
    {
#if NET6_0_OR_GREATER
        UnmanagedMemoryFreed.Add(bytes);
#endif
    }
}
