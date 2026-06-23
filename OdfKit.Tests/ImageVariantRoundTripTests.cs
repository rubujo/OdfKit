using System;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Image;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 OTI 影像範本與 FODI 扁平 XML 影像的雙向轉換工作流。
/// </summary>
public class ImageVariantRoundTripTests
{
    /// <summary>
    /// 驗證 <see cref="ImageTemplateDocument.CreateFromDocument(OdfImageDocument)"/> 與
    /// <see cref="OdfImageDocument.CreateFromTemplate(ImageTemplateDocument)"/> 形成的雙向轉換，
    /// 影像框架內容完整保留。
    /// </summary>
    [Fact]
    public void OdfImageDocument_CreateTemplateFromDocument_RoundTripsBackToDocument()
    {
        using var original = OdfImageDocument.Create();
        original.SetImageLayout(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(5),
            OdfLength.FromCentimeters(5),
            "PrimaryFrame",
            "範本往返測試標題",
            "範本往返測試描述。");
        original.SetImage(CreatePngBytes(), "Primary.png");

        using var template = ImageTemplateDocument.CreateFromDocument(original);
        Assert.Equal("application/vnd.oasis.opendocument.image-template", template.Package.MimeType);
        Assert.Equal(OdfDocumentKind.ImageTemplate, template.DocumentKind);
        OdfImageFrameInfo templateFrame = Assert.Single(template.GetImageFrames());
        Assert.Equal("範本往返測試標題", templateFrame.Title);

        using var restored = OdfImageDocument.CreateFromTemplate(template);
        Assert.Equal("application/vnd.oasis.opendocument.image", restored.Package.MimeType);
        Assert.Equal(OdfDocumentKind.Image, restored.DocumentKind);
        OdfImageFrameInfo restoredFrame = Assert.Single(restored.GetImageFrames());
        Assert.Equal("範本往返測試標題", restoredFrame.Title);
        Assert.Equal("範本往返測試描述。", restoredFrame.Description);
    }

    /// <summary>
    /// 驗證 <see cref="FlatImageDocument.CreateFromDocument(OdfImageDocument)"/> 與
    /// <see cref="OdfImageDocument.CreateFromFlatDocument(FlatImageDocument)"/> 形成的雙向轉換，
    /// 影像框架內容完整保留。
    /// </summary>
    [Fact]
    public void OdfImageDocument_CreateFlatDocument_RoundTripsBackToZip()
    {
        using var original = OdfImageDocument.Create();
        original.SetImageLayout(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(5),
            OdfLength.FromCentimeters(5),
            "PrimaryFrame",
            "Flat 往返測試標題",
            null);
        original.SetImage(CreatePngBytes(), "Primary.png");

        using var flat = FlatImageDocument.CreateFromDocument(original);
        Assert.True(flat.IsFlatXml);
        Assert.Equal(OdfDocumentKind.FlatImage, flat.DocumentKind);
        OdfImageFrameInfo flatFrame = Assert.Single(flat.GetImageFrames());
        Assert.Equal("Flat 往返測試標題", flatFrame.Title);

        using var restored = OdfImageDocument.CreateFromFlatDocument(flat);
        Assert.False(restored.IsFlatXml);
        Assert.Equal(OdfDocumentKind.Image, restored.DocumentKind);
        OdfImageFrameInfo restoredFrame = Assert.Single(restored.GetImageFrames());
        Assert.Equal("Flat 往返測試標題", restoredFrame.Title);
    }

    /// <summary>
    /// 驗證四個 Image 雙向轉換工作流方法的邊界案例：
    /// 傳入 <see langword="null"/> 來源文件時皆擲出 <see cref="ArgumentNullException"/>。
    /// </summary>
    [Fact]
    public void ImageVariantConversions_NullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ImageTemplateDocument.CreateFromDocument(null!));
        Assert.Throws<ArgumentNullException>(() => OdfImageDocument.CreateFromTemplate(null!));
        Assert.Throws<ArgumentNullException>(() => FlatImageDocument.CreateFromDocument(null!));
        Assert.Throws<ArgumentNullException>(() => OdfImageDocument.CreateFromFlatDocument(null!));
    }

    private static byte[] CreatePngBytes() =>
        Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
}
