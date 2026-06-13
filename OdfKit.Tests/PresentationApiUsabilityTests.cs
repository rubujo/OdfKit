using System;
using System.IO;
using System.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Presentation;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定簡報文件高階 API 的易用入口。
/// </summary>
public class PresentationApiUsabilityTests
{
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

    private static byte[] CreatePngBytes()
    {
        return Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
    }
}
