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

    /// <summary>
    /// Loads the editable ODS model and enumerates sheet metadata without materializing sheet rows.
    /// 載入可編輯 ODS 模型並列舉工作表中繼資料，但不具現化工作表資料列。
    /// </summary>
    [Benchmark]
    public int LoadComplexDomAndEnumerateSheets()
    {
        using var input = new MemoryStream(_complex, writable: false);
        using SpreadsheetDocument document = SpreadsheetDocument.Load(input, "complex.ods");
        return document.Worksheets.Count;
    }

    /// <summary>
    /// Loads the editable ODS model and materializes the first worksheet on first cell access.
    /// 載入可編輯 ODS 模型，並於首次儲存格存取時具現化第一張工作表。
    /// </summary>
    [Benchmark]
    public object? LoadComplexDomAndReadFirstSheet()
    {
        using var input = new MemoryStream(_complex, writable: false);
        using SpreadsheetDocument document = SpreadsheetDocument.Load(input, "complex.ods");
        return document.Worksheets[0].GetCell(0, 0).CellValue;
    }

    /// <summary>
    /// Loads the editable ODS model and materializes only the last worksheet on first cell access.
    /// 載入可編輯 ODS 模型，並於首次儲存格存取時僅具現化最後一張工作表。
    /// </summary>
    [Benchmark]
    public object? LoadComplexDomAndReadLastSheet()
    {
        using var input = new MemoryStream(_complex, writable: false);
        using SpreadsheetDocument document = SpreadsheetDocument.Load(input, "complex.ods");
        return document.Worksheets[document.Worksheets.Count - 1].GetCell(0, 0).CellValue;
    }

    [Benchmark]
    public long LoadAndSaveComplexDom()
    {
        using var input = new MemoryStream(_complex, writable: false);
        using SpreadsheetDocument document = SpreadsheetDocument.Load(input, "complex.ods");
        using var output = new MemoryStream();
        document.SaveToStream(output);
        return output.Length;
    }

    [Benchmark]
    public long LoadAndSaveUntouchedLazyDom()
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
    private byte[] _largeParagraph = null!;

    [GlobalSetup]
    public void Setup()
    {
        _streaming = StandardPerformanceWorkloads.CreateStreamingOdt(StandardPerformanceWorkloads.StandardOdtNodeCount);
        _complex = StandardPerformanceWorkloads.CreateComplexOdt(20_000);
        using TextDocument document = TextDocument.Create();
        document.AddParagraph(new string('x', 1_000_000));
        using var output = new MemoryStream();
        document.SaveToStream(output);
        _largeParagraph = output.ToArray();
    }

    [Benchmark]
    public byte[] WriteStreaming() => StandardPerformanceWorkloads.CreateStreamingOdt(StandardPerformanceWorkloads.StandardOdtNodeCount);

    [Benchmark]
    public ulong ReadStreaming() => StandardPerformanceWorkloads.ChecksumStreamingOdt(_streaming);

    /// <summary>
    /// Loads an editable ODT and enumerates paragraph metadata without materializing the large paragraph body.
    /// 載入可編輯 ODT 並列舉段落中繼資料，但不具現化大型段落內容。
    /// </summary>
    [Benchmark]
    public int LoadLargeParagraphAndEnumerateParagraphs()
    {
        using var input = new MemoryStream(_largeParagraph, writable: false);
        using TextDocument document = TextDocument.Load(input, "large-paragraph.odt");
        return document.Body.Paragraphs.Items.Count;
    }

    /// <summary>
    /// Loads an editable ODT and materializes the large paragraph on first text access.
    /// 載入可編輯 ODT，並於首次文字存取時具現化大型段落。
    /// </summary>
    [Benchmark]
    public int LoadLargeParagraphAndReadText()
    {
        using var input = new MemoryStream(_largeParagraph, writable: false);
        using TextDocument document = TextDocument.Load(input, "large-paragraph.odt");
        return document.Body.Paragraphs.Items[0].TextContent.Length;
    }

    [Benchmark]
    public long LoadAndSaveComplexDom()
    {
        using var input = new MemoryStream(_complex, writable: false);
        using TextDocument document = TextDocument.Load(input, "complex.odt");
        using var output = new MemoryStream();
        document.SaveToStream(output);
        return output.Length;
    }

    [Benchmark]
    public long LoadAndSaveUntouchedLargeParagraph()
    {
        using var input = new MemoryStream(_largeParagraph, writable: false);
        using TextDocument document = TextDocument.Load(input, "large-paragraph.odt");
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
