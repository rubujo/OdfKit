using System.IO;
using BenchmarkDotNet.Attributes;
using OdfKit.Export;
using OdfKit.Text;

namespace OdfKit.Benchmarks;

/// <summary>
/// HTML／Markdown／PDF 匯出峰值配置基準（量測用，驗證 Phase 1 低緩衝改動成效，
/// 尚未納入 eng/Benchmark-Regression.ps1 回歸硬閘門）。
/// </summary>
[MemoryDiagnoser]
public class ExportMemoryBenchmarks
{
    private const int ParagraphCount = 20_000;
    private TextDocument _document = null!;

    [GlobalSetup]
    public void Setup()
    {
        byte[] odt = StandardPerformanceWorkloads.CreateComplexOdt(ParagraphCount);
        using var input = new MemoryStream(odt, writable: false);
        _document = TextDocument.Load(input, "benchmark.odt");
    }

    [Benchmark]
    public long ExportHtmlToStream()
    {
        using var destination = new MemoryStream();
        OdfExportReport report = OdfHtmlExporter.ExportToStream(_document, destination, null);
        return report.BytesWritten;
    }

    [Benchmark]
    public long ExportMarkdownToStream()
    {
        using var destination = new MemoryStream();
        OdfExportReport report = OdfMarkdownExporter.ExportToStream(_document, destination, null);
        return report.BytesWritten;
    }

    [Benchmark]
    public long ExportPdfToStream()
    {
        using var destination = new MemoryStream();
        OdfExportReport report = OdfPdfExporter.ExportToStream(_document, destination);
        return report.BytesWritten;
    }
}
