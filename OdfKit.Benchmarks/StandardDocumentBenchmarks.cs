using BenchmarkDotNet.Attributes;
using OdfKit.Core;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Text;

namespace OdfKit.Benchmarks;

/// <summary>
/// Provides standard ODS read and write benchmarks.
/// 提供標準 ODS 讀寫效能基準。
/// </summary>
[MemoryDiagnoser]
public class StandardOdsBenchmarks
{
    private byte[] _streaming = null!;
    private byte[] _complex = null!;

    [GlobalSetup]
    public void Setup()
    {
        _streaming = StandardPerformanceWorkloads.CreateStreamingOds(StandardPerformanceWorkloads.StandardOdsReadRowCount);
        _complex = StandardPerformanceWorkloads.CreateComplexOds(2_000);
    }

    [Benchmark]
    public byte[] WriteStreaming() => StandardPerformanceWorkloads.CreateStreamingOds(StandardPerformanceWorkloads.StandardOdsRowCount);

    [Benchmark]
    public ulong ReadStreaming() => StandardPerformanceWorkloads.ChecksumStreamingOds(_streaming);

    [Benchmark]
    public long LoadAndSaveComplexDom()
    {
        using var input = new MemoryStream(_complex, writable: false);
        using SpreadsheetDocument document = SpreadsheetDocument.Load(input, "complex.ods");
        using var output = new MemoryStream();
        document.SaveToStream(output);
        return output.Length;
    }
}

/// <summary>
/// Provides standard ODT read and write benchmarks.
/// 提供標準 ODT 讀寫效能基準。
/// </summary>
[MemoryDiagnoser]
public class StandardOdtBenchmarks
{
    private byte[] _streaming = null!;
    private byte[] _complex = null!;

    [GlobalSetup]
    public void Setup()
    {
        _streaming = StandardPerformanceWorkloads.CreateStreamingOdt(StandardPerformanceWorkloads.StandardOdtNodeCount);
        _complex = StandardPerformanceWorkloads.CreateComplexOdt(20_000);
    }

    [Benchmark]
    public byte[] WriteStreaming() => StandardPerformanceWorkloads.CreateStreamingOdt(StandardPerformanceWorkloads.StandardOdtNodeCount);

    [Benchmark]
    public ulong ReadStreaming() => StandardPerformanceWorkloads.ChecksumStreamingOdt(_streaming);

    [Benchmark]
    public long LoadAndSaveComplexDom()
    {
        using var input = new MemoryStream(_complex, writable: false);
        using TextDocument document = TextDocument.Load(input, "complex.odt");
        using var output = new MemoryStream();
        document.SaveToStream(output);
        return output.Length;
    }
}

/// <summary>
/// Provides standard ODP DOM benchmarks.
/// 提供標準 ODP DOM 效能基準。
/// </summary>
[MemoryDiagnoser]
public class StandardOdpBenchmarks
{
    private byte[] _structure = null!;
    private byte[] _media = null!;

    [GlobalSetup]
    public void Setup()
    {
        _structure = StandardPerformanceWorkloads.CreateOdp(StandardPerformanceWorkloads.StandardOdpStructureSlideCount, includeMedia: false);
        _media = StandardPerformanceWorkloads.CreateOdp(StandardPerformanceWorkloads.StandardOdpMediaSlideCount, includeMedia: true);
    }

    [Benchmark]
    public byte[] WriteStructureDense() => StandardPerformanceWorkloads.CreateOdp(StandardPerformanceWorkloads.StandardOdpStructureSlideCount, includeMedia: false);

    [Benchmark]
    public ulong LoadAndTraverseStructureDense() => StandardPerformanceWorkloads.ChecksumOdp(_structure);

    [Benchmark]
    public long LoadAndSaveMediaDense()
    {
        using var input = new MemoryStream(_media, writable: false);
        using PresentationDocument document = PresentationDocument.Load(input, "media.odp");
        using var output = new MemoryStream();
        document.SaveToStream(output);
        return output.Length;
    }
}

/// <summary>
/// Separates ZIP package-open cost from document-model cost.
/// 將 ZIP 封裝開啟成本與文件模型成本分離。
/// </summary>
[MemoryDiagnoser]
public class StandardPackageOpenBenchmarks
{
    private byte[] _ods = null!;
    private byte[] _odt = null!;
    private byte[] _odp = null!;

    [GlobalSetup]
    public void Setup()
    {
        _ods = StandardPerformanceWorkloads.CreateStreamingOds(10_000);
        _odt = StandardPerformanceWorkloads.CreateStreamingOdt(10_000);
        _odp = StandardPerformanceWorkloads.CreateOdp(100, includeMedia: true);
    }

    [Benchmark] public int OpenOds() => Open(_ods);
    [Benchmark] public int OpenOdt() => Open(_odt);
    [Benchmark] public int OpenOdp() => Open(_odp);

    private static int Open(byte[] bytes)
    {
        using var input = new MemoryStream(bytes, writable: false);
        using OdfPackage package = OdfPackage.Open(input, leaveOpen: true);
        return package.Manifest.Count;
    }
}
