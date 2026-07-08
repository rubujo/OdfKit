using OdfKit.Core;
using OdfKit.Drawing;
using OdfKit.Image;
using OdfKit.Presentation;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定影像與繪圖實務 helper 的高階入口。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Smoke)]
public class ImageDrawingDepthTests
{
    /// <summary>
    /// 驗證 ODI 可透過 AddImages 批次新增圖片框架。
    /// </summary>
    [Fact]
    public void ImageDocument_AddImages_AddsBatchFrames()
    {
        using OdfImageDocument document = OdfImageDocument.Create();

        var hrefs = document.AddImages(
        [
            new OdfImageFrameRequest(CreatePngBytes(), 1.Cm(), 1.Cm(), 2.Cm(), 2.Cm(), "one.png", "圖一"),
            new OdfImageFrameRequest(CreatePngBytes(), 4.Cm(), 1.Cm(), 2.Cm(), 2.Cm(), "two.png", "圖二"),
        ]);

        Assert.Equal(2, hrefs.Count);
        Assert.Contains("Pictures/one.png", hrefs);
        Assert.Equal(2, document.GetImageFrames().Count);
    }

    /// <summary>
    /// 驗證 Drawing builder 可用 AddFlow 一次建立流程節點與連接線。
    /// </summary>
    [Fact]
    public void DrawingDocumentBuilder_AddFlow_CreatesConnectedSteps()
    {
        using DrawingDocument document = DrawingDocument.Builder()
            .AddFlow(
                "主流程",
                [
                    new OdfFlowStepRequest("load", "載入 ODF"),
                    new OdfFlowStepRequest("validate", "驗證封裝", OdfShapeType.Ellipse),
                    new OdfFlowStepRequest("export", "輸出報告"),
                ])
            .Build();

        Assert.Equal("主流程", document.Pages[0].Name);
        Assert.Contains(document.Pages[0].TextBoxes, textBox => textBox.Text == "載入 ODF");
        Assert.Contains(document.Pages[0].TextBoxes, textBox => textBox.Text == "驗證封裝");
        Assert.Contains(document.GetConnectors(), connector => connector.StartShapeId == "load" && connector.EndShapeId == "validate");
        Assert.Contains(document.GetConnectors(), connector => connector.StartShapeId == "validate" && connector.EndShapeId == "export");
    }

    /// <summary>
    /// 驗證影像相容性 helper 只回報標準化建議，不執行核心轉檔。
    /// </summary>
    [Fact]
    public void ImageCompatibility_NormalizeRequest_ReportsPortableFormatRisk()
    {
        OdfImageNormalizationRequest request = OdfImageCompatibility.NormalizeRequest(
            "scan.bmp",
            "image/bmp");

        Assert.False(request.IsPortable);
        Assert.Equal("image/png", request.RecommendedMediaType);
    }

    private static byte[] CreatePngBytes() =>
        System.Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
}
