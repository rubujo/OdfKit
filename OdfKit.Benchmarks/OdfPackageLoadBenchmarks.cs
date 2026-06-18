using BenchmarkDotNet.Attributes;
using OdfKit.Core;
using OdfKit.Text;

namespace OdfKit.Benchmarks;

/// <summary>
/// ODF 封裝載入效能基準。
/// </summary>
[MemoryDiagnoser]
public class OdfPackageLoadBenchmarks
{
    private byte[] _packageBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        using var ms = new MemoryStream();
        using (var document = TextDocument.Create())
        {
            document.AddParagraph("基準測試文件");
            document.AddParagraph("第二段文字內容");
            document.SaveToStream(ms);
        }

        _packageBytes = ms.ToArray();
    }

    [Benchmark]
    public int LoadPackage()
    {
        using var stream = new MemoryStream(_packageBytes, writable: false);
        using var package = OdfPackage.Open(stream, leaveOpen: true);
        return package.Manifest.Count;
    }
}
