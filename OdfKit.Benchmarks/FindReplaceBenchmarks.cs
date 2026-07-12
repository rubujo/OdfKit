using System.IO;
using BenchmarkDotNet.Attributes;
using OdfKit.Text;

namespace OdfKit.Benchmarks;

/// <summary>
/// FindText／ReplaceText 單次文件走訪效能基準（PERF 回歸閘門保護對象之一，見
/// eng/Benchmark-Regression.ps1）。
/// </summary>
[MemoryDiagnoser]
public class FindReplaceBenchmarks
{
    private const int ParagraphCount = 20_000;
    private byte[] _odt = [];

    [GlobalSetup]
    public void Setup()
    {
        _odt = StandardPerformanceWorkloads.CreateComplexOdt(ParagraphCount);
    }

    [Benchmark]
    public int FindText()
    {
        using var input = new MemoryStream(_odt, writable: false);
        using TextDocument document = TextDocument.Load(input, "benchmark.odt");
        return document.FindText("段落").Count;
    }

    [Benchmark]
    public int ReplaceText()
    {
        using var input = new MemoryStream(_odt, writable: false);
        using TextDocument document = TextDocument.Load(input, "benchmark.odt");
        return document.ReplaceText("段落", "節").ReplacementCount;
    }
}
