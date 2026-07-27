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
    public void ImageDocumentAddImagesAddsBatchFrames()
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
    /// 驗證 ODI 批次裁切與旋轉 helper 會回報更新數與未命中名稱。
    /// </summary>
    [Fact]
    public void ImageDocumentBatchCropAndRotationReportMissingFrames()
    {
        using OdfImageDocument document = OdfImageDocument.Create();
        document.AddImages(
        [
            new OdfImageFrameRequest(CreatePngBytes(), 1.Cm(), 1.Cm(), 2.Cm(), 2.Cm(), "one.png", "圖一"),
            new OdfImageFrameRequest(CreatePngBytes(), 4.Cm(), 1.Cm(), 2.Cm(), 2.Cm(), "two.png", "圖二"),
        ]);

        OdfImageBatchUpdateResult rotation = document.SetImageRotations(["圖一", "missing"], 90);
        OdfImageBatchUpdateResult crop = document.SetImageCrops(["圖一", "圖二"], new OdfImageCropInfo("0.1cm", "0.2cm", "0.3cm", "0.4cm"));

        Assert.Equal(1, rotation.UpdatedCount);
        Assert.Contains("missing", rotation.MissingNames);
        Assert.Equal(2, crop.UpdatedCount);
        Assert.Empty(crop.MissingNames);
    }

    /// <summary>
    /// 驗證 Image inspection 會回報缺少替代文字與裁切旋轉風險。
    /// </summary>
    [Fact]
    public void ImageDocumentInspectImagesReportsPortableEditingRisks()
    {
        using OdfImageDocument document = OdfImageDocument.Create();
        document.AddImageFrame(CreatePngBytes(), 1.Cm(), 1.Cm(), 2.Cm(), 2.Cm(), "risk.png", "RiskFrame");
        Assert.True(document.SetImageRotation("RiskFrame", 45));

        OdfImageInspectionReport report = document.InspectImages();

        Assert.Contains(report.Issues, issue => issue.RuleId == "IMG0002");
        Assert.Contains(report.Issues, issue => issue.RuleId == "IMG0004");
        Assert.Contains(report.Issues, issue => issue.MessageKey == "Msg_ImageInspection_Transform");
    }

    /// <summary>
    /// 驗證 Drawing builder 可用 AddFlow 一次建立流程節點與連接線。
    /// </summary>
    [Fact]
    public void DrawingDocumentBuilderAddFlowCreatesConnectedSteps()
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
    /// 驗證 AddFlow 可用明確版面選項控制方向與節點大小。
    /// </summary>
    [Fact]
    public void DrawingDocumentBuilderAddFlowUsesExplicitLayoutOptions()
    {
        using DrawingDocument document = DrawingDocument.Builder()
            .AddFlow(
                "垂直流程",
                [
                    new OdfFlowStepRequest("first", "第一步"),
                    new OdfFlowStepRequest("second", "第二步"),
                ],
                OdfConnectorType.Straight,
                new OdfFlowLayoutOptions
                {
                    Horizontal = false,
                    StartXCm = 2,
                    StartYCm = 3,
                    NodeWidthCm = 4,
                    NodeHeightCm = 1.2,
                    GapCm = 0.8
                })
            .Build();

        Assert.Equal("垂直流程", document.Pages[0].Name);
        Assert.Contains(document.Pages[0].TextBoxes, textBox => textBox.Text == "第一步");
        Assert.Contains(document.Pages[0].TextBoxes, textBox => textBox.Text == "第二步");
        Assert.Contains(document.GetConnectors(), connector => connector.StartShapeId == "first" && connector.EndShapeId == "second");
    }

    /// <summary>
    /// 驗證 Drawing 文件層批次更新可修改文字與圖形位置。
    /// </summary>
    [Fact]
    public void DrawingDocumentDepthUpdatesTextAndShapes()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.Pages.Add("Canvas");
        page.AddTextBox(1.Cm(), 1.Cm(), 4.Cm(), 1.Cm(), "Hello {{Name}}");
        OdfShape shape = page.AddShape(OdfShapeType.Rectangle, 1.Cm(), 3.Cm(), 2.Cm(), 1.Cm());
        shape.Id = "box";

        int changed = document.ReplaceTextInTextBoxes("{{Name}}", "OdfKit");
        OdfBatchUpdateResult result = document.UpdateShapes(
            ["box"],
            x: 2.Cm(),
            y: null,
            width: null,
            height: null,
            layerName: "Layer1");

        Assert.Equal(1, changed);
        Assert.Contains(document.Pages[0].TextBoxes, textBox => textBox.Text == "Hello OdfKit");
        Assert.Equal(1, result.UpdatedCount);
        Assert.Contains("box", result.UpdatedNames);
        Assert.Contains(document.GetShapeLayerAssignments(), assignment => assignment.Id == "box" && assignment.LayerName == "Layer1");
    }

    /// <summary>
    /// 驗證 DrawingDocument 可用 request 批次更新圖形並調整繪圖順序。
    /// </summary>
    [Fact]
    public void DrawingDocumentUpdateShapesRequestAndMoveRelative()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.Pages.Add("Canvas");
        OdfShape first = page.AddShape(OdfShapeType.Rectangle, 1.Cm(), 1.Cm(), 2.Cm(), 1.Cm());
        first.Id = "first";
        OdfShape second = page.AddShape(OdfShapeType.Rectangle, 4.Cm(), 1.Cm(), 2.Cm(), 1.Cm());
        second.Id = "second";

        OdfBatchUpdateResult result = document.UpdateShapes(
        [
            new OdfShapeUpdateRequest
            {
                Name = "first",
                X = 2.Cm(),
                LayerName = "layout",
                FillColor = "#00AA66",
                StrokeColor = "#333333",
                ZIndex = 7
            }
        ]);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal("#00AA66", first.FillColor);
        Assert.Equal("layout", first.Node.GetAttribute("layer", OdfNamespaces.Draw));
        Assert.True(document.MoveAfter("first", "second"));
        Assert.True(document.MoveBefore("first", "second"));
        Assert.False(document.MoveAfter("first", "missing"));
    }

    /// <summary>
    /// 驗證影像相容性 helper 只回報標準化建議，不執行核心轉檔。
    /// </summary>
    [Fact]
    public void ImageCompatibilityNormalizeRequestReportsPortableFormatRisk()
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
