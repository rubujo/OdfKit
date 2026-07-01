using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Presentation;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定簡報文件高階 API 的易用入口。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Smoke)]
public class PresentationApiUsabilityTests
{
    /// <summary>
    /// 驗證簡報 Fluent builder 可建立中繼資料、投影片與切換效果。
    /// </summary>
    [Fact]
    public void PresentationDocumentBuilderCreatesMetadataSlidesAndTransitions()
    {
        using PresentationDocument deck = PresentationDocument.Builder()
            .WithMetadata(metadata => metadata
                .Title("產品簡報")
                .Author("OdfKit"))
            .AddSlide("開場", slide => slide
                .AddTitle("歡迎使用 OdfKit")
                .WithSpeakerNotes("介紹產品定位")
                .WithTransition(OdfTransitionType.Fade))
            .Build();

        using var stream = new MemoryStream();
        deck.SaveToStream(stream);
        stream.Position = 0;

        using PresentationDocument loaded = PresentationDocument.Load(stream);

        Assert.Equal("產品簡報", loaded.Title);
        Assert.Equal("OdfKit", loaded.Creator);
        Assert.Equal("開場", loaded.Slides[0].Name);
        Assert.Equal("歡迎使用 OdfKit", loaded.Slides[0].TextBoxes[0].Text);
        Assert.Equal("介紹產品定位", loaded.Slides[0].SpeakerNotes);
    }

    /// <summary>
    /// 驗證簡報 Fluent builder 可建立商業簡報常見的版面、圖片、圖表預留位置與動畫。
    /// </summary>
    [Fact]
    public void PresentationDocumentBuilderCreatesComplexBusinessDeck()
    {
        using PresentationDocument deck = PresentationDocument.Builder()
            .WithMetadata(metadata => metadata.Title("董事會簡報"))
            .WithTheme(OdfDesignTheme.Flowchart)
            .WithStyles(OdfStyleSet.BusinessReport)
            .WithLayoutPreset(new OdfLayoutPreset
            {
                TitleBounds = new OdfLayoutBounds(2, 1, 14, 1.5),
                ChartBounds = new OdfLayoutBounds(3, 4, 12, 6),
            })
            .WithMasterPage("BoardTheme", "#F6F8FB")
            .AddTitleSlide("封面", "年度重點", "營收成長與產品化路線")
            .AddTwoColumnSlide(
                "Roadmap",
                "下一季路線圖",
                ["Complex DSL", "JSON Collaboration subset"],
                ["Managed fidelity", "Corpus parity"],
                slide => slide
                    .AddImage(CreatePngBytes(), 16, 1, 1.5, 1.5)
                    .AddShape(OdfShapeType.Rectangle, 1, 11, 3, 1, shape => shape
                        .WithId("roadmap_highlight"))
                    .AddEntranceEffect("roadmap_highlight", OdfAnimationEffect.Fade)
                    .AddExitEffect("roadmap_highlight", OdfAnimationEffect.Zoom))
            .AddChartSlide("Metrics", "營運指標", slide => slide
                .WithSpeakerNotes("圖表頁先說趨勢，再補風險。")
                .WithTransition(OdfTransitionType.Fade))
            .Build();

        using var stream = new MemoryStream();
        deck.SaveToStream(stream);
        stream.Position = 0;

        // 先以 OdfPackage.Open(leaveOpen: true) 讀取原始 XML，並在完整釋放（含等待
        // 背景預讀工作完成）後才重用 stream 供 PresentationDocument.Load 讀取；
        // PresentationDocument.Load 本身不支援 leaveOpen，其底層 OdfPackage 會在
        // loaded 釋放時一併關閉 stream，因此必須排在最後才讀取，避免兩個仍存活的
        // 讀取端競爭同一個串流游標。
        using (OdfPackage package = OdfPackage.Open(stream, leaveOpen: true))
        {
            string stylesXml = ReadEntry(package, "styles.xml");
            Assert.Contains("style:name=\"BoardTheme\"", stylesXml, StringComparison.Ordinal);
            Assert.Contains("draw:fill-color=\"#F6F8FB\"", stylesXml, StringComparison.Ordinal);
            string contentXml = ReadEntry(package, "content.xml");
            Assert.Contains("svg:x=\"3cm\"", contentXml, StringComparison.Ordinal);
            Assert.Contains("svg:width=\"12cm\"", contentXml, StringComparison.Ordinal);
        }

        stream.Position = 0;
        using PresentationDocument loaded = PresentationDocument.Load(stream);
        Assert.Equal("董事會簡報", loaded.Title);
        Assert.Equal(3, loaded.Slides.Count);
        Assert.All(loaded.Slides, slide => Assert.Equal("BoardTheme", slide.MasterPageName));
        Assert.Equal(OdfPresentationLayout.TitleAndSubtitle, loaded.Slides[0].GetLayout());
        Assert.Equal(OdfPresentationLayout.TitleAndBody, loaded.Slides[1].GetLayout());
        Assert.Contains(loaded.Slides[1].Pictures, picture => picture.ImageHref is { } href && href.EndsWith(".png", StringComparison.Ordinal));
        Assert.Contains(loaded.Slides[1].Shapes, shape => shape.Id == "roadmap_highlight");
        Assert.Equal("#D9EAF7", loaded.Slides[1].Shapes.Single(shape => shape.Id == "roadmap_highlight").FillColor);
        Assert.Equal("#1F4E79", loaded.Slides[1].Shapes.Single(shape => shape.Id == "roadmap_highlight").StrokeColor);
        Assert.Contains(loaded.Slides[2].Placeholders, placeholder => placeholder.PlaceholderType == OdfPlaceholderType.Chart);
        Assert.Equal("圖表頁先說趨勢，再補風險。", loaded.Slides[2].SpeakerNotes);
        Assert.Contains(loaded.Slides[1].GetAnimations(), animation => animation.TargetElementId == "roadmap_highlight");
    }

    /// <summary>
    /// 驗證可用 slides collection 建立常見 ODP 內容並 round-trip。
    /// </summary>
    [Fact]
    public void CreateLoadSlidesShapesPicturesNotesHandoutAndTransitions()
    {
        using var deck = PresentationDocument.Create();
        OdfSlide slide = deck.Slides.Add("Intro");
        slide.AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(10),
            OdfLength.FromCentimeters(2),
            "標題");
        slide.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(3));
        OdfPicture picture = slide.AddPicture(
            CreatePngBytes(),
            OdfLength.FromCentimeters(6),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(2));
        slide.SpeakerNotes = "講者備忘";
        slide.SetTransition(OdfTransitionType.Zoom, OdfLength.FromPoints(72));
        deck.HandoutPage.AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(6),
            OdfLength.FromCentimeters(1),
            "講義");

        using var stream = new MemoryStream();
        deck.SaveToStream(stream);
        stream.Position = 0;

        using PresentationDocument loaded = PresentationDocument.Load(stream);

        Assert.Single(loaded.Slides);
        Assert.Equal("Intro", loaded.Slides[0].Name);
        Assert.Equal("講者備忘", loaded.Slides[0].SpeakerNotes);
        Assert.Equal("application/vnd.oasis.opendocument.presentation", loaded.Package.MimeType);
        Assert.Single(loaded.Slides[0].TextBoxes);
        Assert.Equal("標題", loaded.Slides[0].TextBoxes[0].Text);
        Assert.Single(loaded.Slides[0].Pictures);
        Assert.Equal(picture.ImageHref, loaded.Slides[0].Pictures[0].ImageHref);
        Assert.Contains(loaded.Slides[0].Shapes, shape => shape.LocalName == "rect");
    }

    /// <summary>
    /// 驗證可用 LoadAsync 非同步載入 ODP 並 round-trip。
    /// </summary>
    [Fact]
    public async Task LoadAsync_RoundTripsPresentationContent()
    {
        using var deck = PresentationDocument.Create();
        deck.Slides.Add("AsyncIntro").AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(8),
            OdfLength.FromCentimeters(2),
            "非同步標題");

        await using var stream = new MemoryStream();
        await deck.SaveAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        stream.Position = 0;

        await using var loaded = await PresentationDocument.LoadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(loaded.Slides);
        Assert.Equal("AsyncIntro", loaded.Slides[0].Name);
        Assert.Equal("非同步標題", loaded.Slides[0].TextBoxes[0].Text);
    }

    /// <summary>
    /// 預先取消的語彙應使 PresentationDocument.LoadAsync 拋出 OperationCanceledException。
    /// </summary>
    [Fact]
    public async Task LoadAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        await using var stream = new MemoryStream();
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Presentation, leaveOpen: true))
        {
            package.Save();
        }

        stream.Position = 0;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await using var _ = await PresentationDocument.LoadAsync(stream, cancellationToken: cts.Token);
        });
    }

    /// <summary>
    /// 驗證非 ODP 文件不會被誤載為簡報。
    /// </summary>
    [Fact]
    public void LoadRejectsNonPresentationDocument()
    {
        using var stream = new MemoryStream();
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Text, leaveOpen: true))
        {
            package.Save();
        }

        stream.Position = 0;

        Assert.Throws<InvalidOperationException>(() => PresentationDocument.Load(stream, "text.odt"));
    }

    /// <summary>
    /// 驗證簡報可讀回投影片動畫效果摘要。
    /// </summary>
    [Fact]
    public void ReadAnimationsAfterRoundTrip()
    {
        using var deck = PresentationDocument.Create();
        OdfSlide slide = deck.Slides.Add("動畫");
        OdfTextBox textBox = slide.AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(6),
            OdfLength.FromCentimeters(2),
            "標題");
        string shapeId = textBox.Id;
        slide.AddEntranceEffect(shapeId, OdfAnimationEffect.Fade, OdfAnimationTrigger.OnClick);

        using var stream = new MemoryStream();
        deck.SaveToStream(stream);
        stream.Position = 0;

        using PresentationDocument loaded = PresentationDocument.Load(stream);
        OdfAnimationInfo animation = Assert.Single(loaded.Slides[0].GetAnimations());

        Assert.Equal(OdfAnimationKind.Entrance, animation.Kind);
        Assert.Equal(shapeId, animation.TargetElementId);
        Assert.Equal(OdfAnimationTrigger.OnClick, animation.Trigger);
    }

    private static byte[] CreatePngBytes()
    {
        return Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
    }

    private static string ReadEntry(OdfPackage package, string path)
    {
        using Stream stream = package.GetEntryStream(path);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
