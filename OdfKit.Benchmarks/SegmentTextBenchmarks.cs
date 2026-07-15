using System.Text;
using BenchmarkDotNet.Attributes;
using OdfKit.Styles;

namespace OdfKit.Benchmarks;

/// <summary>
/// Benchmarks supplementary-plane text segmentation.
/// 量測 <see cref="OdfFontContext.SegmentText(string, string)"/> 的增補平面混排效能。
/// </summary>
[MemoryDiagnoser]
public class SegmentTextBenchmarks
{
    private const int RepetitionCount = 25_000;
    private string _mixedText = string.Empty;

    /// <summary>
    /// Gets or sets the base font used by the workload.
    /// 取得或設定工作負載使用的基礎字型。
    /// </summary>
    [Params("Noto Sans CJK TC", "MingLiU", "TW-Kai")]
    public string BaseFont { get; set; } = string.Empty;

    /// <summary>
    /// Builds the 125,000 UTF-16-code-unit mixed-plane input.
    /// 建立含 125,000 個 UTF-16 碼元的混合平面輸入。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        string plane2 = char.ConvertFromUtf32(0x20BB7);
        string plane15 = char.ConvertFromUtf32(0xF0000);
        var builder = new StringBuilder(RepetitionCount * 5);
        for (int i = 0; i < RepetitionCount; i++)
        {
            builder.Append('甲');
            builder.Append(plane2);
            builder.Append(plane15);
        }

        _mixedText = builder.ToString();
    }

    /// <summary>
    /// Segments the mixed-plane input and returns the segment count.
    /// 分段混合平面輸入並傳回片段數量。
    /// </summary>
    /// <returns>The number of produced text segments. / 產生的文字片段數量。</returns>
    [Benchmark]
    public int SegmentSupplementaryPlaneText()
        => OdfFontContext.Default.SegmentText(_mixedText, BaseFont).Count;
}
