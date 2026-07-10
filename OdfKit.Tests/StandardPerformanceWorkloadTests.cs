#if NET10_0_OR_GREATER
using OdfKit.Benchmarks;
using OdfKit.Presentation;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證三格式標準效能工作負載的決定性與來回讀寫語意。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Smoke)]
public class StandardPerformanceWorkloadTests
{
    /// <summary>
    /// 驗證 ODS 固定資料集在兩次建立後具有相同語意檢查碼。
    /// </summary>
    [Fact]
    public void OdsWorkload_IsDeterministicAndReadable()
    {
        byte[] first = StandardPerformanceWorkloads.CreateStreamingOds(250);
        byte[] second = StandardPerformanceWorkloads.CreateStreamingOds(250);

        Assert.Equal(
            StandardPerformanceWorkloads.ChecksumStreamingOds(first),
            StandardPerformanceWorkloads.ChecksumStreamingOds(second));
        Assert.True(StandardPerformanceWorkloads.GetPackageSizes(first).XmlBytes > first.LongLength);
    }

    /// <summary>
    /// 驗證 ODT 巢狀清單與一般文字節點不會在串流檢查碼中產生不穩定結果。
    /// </summary>
    [Fact]
    public void OdtWorkload_IsDeterministicAndReadable()
    {
        byte[] first = StandardPerformanceWorkloads.CreateStreamingOdt(500);
        byte[] second = StandardPerformanceWorkloads.CreateStreamingOdt(500);

        Assert.Equal(
            StandardPerformanceWorkloads.ChecksumStreamingOdt(first),
            StandardPerformanceWorkloads.ChecksumStreamingOdt(second));
    }

    /// <summary>
    /// 驗證 ODP 語意檢查碼涵蓋投影片、文字、圖形、媒體與講者備忘。
    /// </summary>
    [Fact]
    public void OdpWorkload_RoundTripsAllMeasuredObjectKinds()
    {
        byte[] bytes = StandardPerformanceWorkloads.CreateOdp(5, includeMedia: true);
        ulong checksum = StandardPerformanceWorkloads.ChecksumOdp(bytes);
        using var input = new MemoryStream(bytes, writable: false);
        using PresentationDocument document = PresentationDocument.Load(input, "benchmark.odp");

        Assert.Equal(5, document.Slides.Count);
        Assert.NotEmpty(document.GetMasterPages());
        Assert.All(document.Slides, slide =>
        {
            Assert.NotEmpty(slide.TextBoxes);
            Assert.NotEmpty(slide.Shapes);
            Assert.NotEmpty(slide.Pictures);
            Assert.False(string.IsNullOrWhiteSpace(slide.SpeakerNotes));
            Assert.NotEmpty(slide.GetAnimations());
        });
        Assert.NotEqual(0UL, checksum);
    }

    /// <summary>
    /// 驗證三種複雜 DOM 工作負載皆可保存並重新載入。
    /// </summary>
    [Fact]
    public void ComplexDomWorkloads_SaveAndReload()
    {
        byte[] ods = StandardPerformanceWorkloads.CreateComplexOds(20);
        byte[] odt = StandardPerformanceWorkloads.CreateComplexOdt(100);
        byte[] odp = StandardPerformanceWorkloads.CreateOdp(3, includeMedia: true);

        Assert.NotEqual(0UL, StandardPerformanceWorkloads.ChecksumComplexOds(ods));
        Assert.NotEqual(0UL, StandardPerformanceWorkloads.ChecksumStreamingOdt(odt));
        Assert.NotEqual(0UL, StandardPerformanceWorkloads.ChecksumOdp(odp));
    }
}
#endif
