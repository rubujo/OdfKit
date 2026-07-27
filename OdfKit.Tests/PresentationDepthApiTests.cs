using OdfKit;
using OdfKit.Presentation;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定簡報文件層高階查詢與批次更新 API。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Scenario)]
public class PresentationDepthApiTests
{
    /// <summary>
    /// 驗證簡報文件可聚合查詢文字方塊、圖片、圖形並批次更新圖片。
    /// </summary>
    [Fact]
    public void PresentationDocumentDepthQueriesAndPictureUpdates()
    {
        using PresentationDocument document = PresentationDocument.Create();
        OdfSlide slide = document.AddSlide("Intro");
        slide.AddTextBox(1.Cm(), 1.Cm(), 6.Cm(), 1.Cm(), "Hello {{Name}}");
        OdfPicture picture = slide.AddPicture(CreatePngBytes(), 1.Cm(), 3.Cm(), 2.Cm(), 2.Cm());
        picture.Id = "hero";
        slide.AddShape(OdfShapeType.Rectangle, 4.Cm(), 3.Cm(), 2.Cm(), 1.Cm());

        document.ReplaceText("{{Name}}", "OdfKit");
        OdfBatchUpdateResult result = document.UpdatePictures(
        [
            new OdfPictureUpdateRequest
            {
                Name = "hero",
                AltText = "Hero image",
                Width = 3.Cm()
            }
        ]);

        Assert.Single(document.GetTextBoxes());
        Assert.Single(document.GetPictures());
        Assert.NotEmpty(document.GetShapes());
        Assert.Equal("Hello OdfKit", document.GetTextBoxes()[0].Text);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Contains("hero", result.UpdatedNames);
        Assert.Equal("Hero image", document.GetPictures()[0].AltText);
    }

    /// <summary>
    /// 驗證簡報圖片批次更新會回報 missing 與 unchanged。
    /// </summary>
    [Fact]
    public void PresentationDocumentPictureUpdateReportsMissingAndUnchanged()
    {
        using PresentationDocument document = PresentationDocument.Create();
        OdfPicture picture = document.AddSlide("Intro").AddPicture(CreatePngBytes(), 1.Cm(), 1.Cm(), 2.Cm(), 2.Cm());
        picture.Id = "hero";

        OdfBatchUpdateResult result = document.UpdatePictures(
        [
            new OdfPictureUpdateRequest { Name = "hero" },
            new OdfPictureUpdateRequest { Name = "missing", AltText = "Missing" }
        ]);

        Assert.Equal(0, result.UpdatedCount);
        Assert.Contains("hero", result.UnchangedNames);
        Assert.Contains("missing", result.MissingNames);
    }

    /// <summary>
    /// 驗證簡報圖形可用 request 批次更新與調整繪圖順序。
    /// </summary>
    [Fact]
    public void PresentationDocumentShapeUpdateAndZOrder()
    {
        using PresentationDocument document = PresentationDocument.Create();
        OdfSlide slide = document.AddSlide("Intro");
        OdfShape back = slide.AddShape(OdfShapeType.Rectangle, 1.Cm(), 1.Cm(), 2.Cm(), 1.Cm());
        back.Id = "back";
        OdfShape front = slide.AddShape(OdfShapeType.Rectangle, 4.Cm(), 1.Cm(), 2.Cm(), 1.Cm());
        front.Id = "front";

        OdfBatchUpdateResult result = document.UpdateShapes(
        [
            new OdfShapeUpdateRequest
            {
                Name = "back",
                X = 2.Cm(),
                FillColor = "#00AA66",
                StrokeColor = "#333333",
                ZIndex = 5
            }
        ]);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal("#00AA66", back.FillColor);
        Assert.True(document.BringToFront("back"));
        Assert.True(document.SendToBack("front"));
        Assert.True(document.MoveAfter("front", "back"));
        Assert.True(document.MoveBefore("front", "back"));
        Assert.False(document.MoveBefore("front", "missing"));
    }

    private static byte[] CreatePngBytes() =>
        System.Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
}
